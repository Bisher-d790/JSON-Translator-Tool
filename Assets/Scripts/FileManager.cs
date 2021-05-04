using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class FileManager : MonoBehaviour
{
    private List<string> inputFileDirectories = new List<string>();
    private string outputFolderDirectory;
    [SerializeField] private Text fileNamePrefix;
    [SerializeField] private Text statusMessageTextField;
    [SerializeField] private Text selectedFilesTextField;
    [SerializeField] private float delayBetweenFiles = 0.1f;

    [Header("Language Code")]
    [SerializeField] private Text fromLanguageCode;
    [SerializeField] private Text toLanguageCode;

    public void SelectInputFiles()
    {
        string path = EditorUtility.OpenFilePanel("Select JSON file", "", "json");

        inputFileDirectories.Add(path);

        selectedFilesTextField.text = path;
    }

    public void SelectInputFolder()
    {
        string path = EditorUtility.OpenFolderPanel("Select JSON folder", "", "");

        DirectoryInfo directory = new DirectoryInfo(path);
        FileInfo[] files = directory.GetFiles();
        foreach (FileInfo file in files) inputFileDirectories.Add(file.FullName);

        selectedFilesTextField.text = path;
    }

    public void StartTranslatingFiles()
    {
        StartCoroutine(TranslateSelectedFiles());
    }

    private IEnumerator TranslateSelectedFiles()
    {
        SetStatusMessage("", false);
        bool hasError = false;

        if (inputFileDirectories.Count <= 0)
        {
            hasError = true;

            SetStatusMessage("No JSON files selected!", true, Color.red);

            yield return null;
        }

        outputFolderDirectory = EditorUtility.OpenFolderPanel("Select output folder", "", "");

        foreach (string file in inputFileDirectories)
        {
            bool isTranslating = false;

            StartCoroutine(TranslateFile(file, () => isTranslating = false, (HasError) => hasError = HasError));

            yield return new WaitWhile(() => isTranslating);

            if (hasError) break;
        }

        if (!hasError) SetStatusMessage("Finished Translation!", false, Color.green);
        else SetStatusMessage("Finished with errors", true, Color.red);
    }

    private IEnumerator TranslateFile(string path, Action OnDone, Action<bool> HasError)
    {
        bool hasError = false;

        // Open the file
        string fileText = ReadFile(path);

        if (fileText.Length <= 0)
        {
            HasError(true);
            OnDone();
            hasError = true;
            SetStatusMessage("File is empty!", true, Color.red);

            yield return null;
        };

        // Translate the content
        int startSentanceIndex = 0;
        bool isTranslating = false;
        string sentanceToTranslate = "";
        for (int i = 0; i < fileText.Length; i++)
        {
            if (fileText[i] == '\"')
            {
                if (!isTranslating)
                {
                    for (int j = (i - 1); j > startSentanceIndex; j--)
                    {
                        if (fileText[j] == ' ') continue;
                        else if (fileText[j] == ':')
                        {
                            isTranslating = true;
                            startSentanceIndex = i + 1;
                            sentanceToTranslate = "";
                            break;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    isTranslating = false;

                    // Translate the sentance
                    if (sentanceToTranslate.Length > 0)
                    {
                        string translatedSentance = "";

                        bool hasDoneTranslation = false;

                        StartCoroutine(TranslateText(sentanceToTranslate,
                            () => hasDoneTranslation = true,
                            (translatedText) => translatedSentance = translatedText,
                            () =>
                            {
                                hasError = true;
                                HasError(true);
                                OnDone();
                            }));

                        yield return new WaitWhile(() => !hasDoneTranslation);

                        if (hasError) break;

                        fileText = fileText.Substring(0, startSentanceIndex) + translatedSentance + fileText.Substring(i);

                        startSentanceIndex = startSentanceIndex - sentanceToTranslate.Length - translatedSentance.Length;
                        i -= sentanceToTranslate.Length - translatedSentance.Length;
                    }
                }
            }
            else
            {
                if (isTranslating)
                {
                    sentanceToTranslate += fileText[i] + "";
                }
            }

            yield return new WaitForSeconds(delayBetweenFiles);
        }

        if (hasError) yield return null;

        // Save the file
        string pathToSave = outputFolderDirectory + "/" + path.Split('/')[path.Split('/').Length - 1].Split('.')[0] + fileNamePrefix.text + ".json";

        if (!WriteFile(pathToSave, fileText, false))
        {
            hasError = true;
            HasError(true);
            OnDone();

            yield return null;
        }

        OnDone();
    }

    private IEnumerator TranslateText(string sentance, Action IsDone, Action<string> TranslatedText, Action OnError)
    {
        yield return TranslationManager.Process(fromLanguageCode.text, toLanguageCode.text, sentance,
            (translatedText) =>
            {
                TranslatedText(translatedText);
                IsDone();
            },
            (errors) =>
            {
                SetStatusMessage(errors, true, Color.red);
                OnError();
                IsDone();
            });
    }

    private string ReadFile(string path)
    {
        try
        {
            //Read the text from directly from the path
            StreamReader reader = new StreamReader(path);
            string fileText = reader.ReadToEnd();

            reader.Close();

            Debug.Log("Read Text: " + fileText);

            return fileText;
        }
        catch (Exception e)
        {
            SetStatusMessage(e.Message, true, Color.red);
            return "";
        }

    }

    private bool WriteFile(string path, string content, bool append)
    {
        try
        {
            //Write some text to the path of the file
            StreamWriter writer = new StreamWriter(path, append);
            writer.WriteLine(content);
            writer.Close();

            Debug.Log("written Text: " + content);
        }
        catch (Exception e)
        {
            SetStatusMessage(e.Message, true, Color.red);
            return false;
        }

        return true;
    }

    private void SetStatusMessage(string message, bool append, Color color = new Color())
    {
        if (append)
            statusMessageTextField.text += message;
        else
            statusMessageTextField.text = message;

        statusMessageTextField.color = color;
    }
}