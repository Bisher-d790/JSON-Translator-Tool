using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SFB;
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
    [SerializeField] private Text availableRequestsTextField;
    [SerializeField] private Text DebugLogTextField;
    [SerializeField] private float delayBetweenFiles = 0.1f;
    [SerializeField] private float delayBetweenTranslations = 0.1f;
    [SerializeField] private bool saveTranslationValuesLocally = true;

    [Header("Language Code")]
    [SerializeField] private Text fromLanguageCode;
    [SerializeField] private Text toLanguageCode;

    private void Start()
    {
        CheckAvailableRequests();
    }

    public void SelectInputFiles()
    {
        //string path = EditorUtility.OpenFilePanel("Select JSON file", "", "json");
        string[] files = StandaloneFileBrowser.OpenFilePanel("Select JSON file", "", "json", true);

        inputFileDirectories.AddRange(files);

        if (files.Length > 0)
            selectedFilesTextField.text = files.Length > 1 ? files.Length.ToString() + " files selected." : files[0];
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

        if (!hasError)
        {
            //outputFolderDirectory = EditorUtility.OpenFolderPanel("Select output folder", "", "");
            string[] Directories = StandaloneFileBrowser.OpenFolderPanel("Select output folder", "", false);
            if (Directories.Length > 0)
                outputFolderDirectory = Directories[0];
            else
            {
                hasError = true;

                SetStatusMessage("No output directory selected!", true, Color.red);

                yield return null;
            }
        }

        if (!hasError)
        {
            foreach (string file in inputFileDirectories)
            {
                bool isTranslating = true;

                StartCoroutine(TranslateFile(file, () => isTranslating = false, (HasError) => hasError = HasError));

                yield return new WaitWhile(() => isTranslating);

                if (hasError) break;

                yield return new WaitForSeconds(delayBetweenFiles);
            }
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
            hasError = true;
            SetStatusMessage("File is empty!", true, Color.red);
        }
        else
        {
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
                                }));

                            yield return new WaitWhile(() => !hasDoneTranslation);

                            if (hasError) break;

                            PrintLog("Translated Sentance: " + translatedSentance);

                            fileText = fileText.Substring(0, startSentanceIndex) + translatedSentance + fileText.Substring(i);

                            startSentanceIndex = startSentanceIndex - sentanceToTranslate.Length - translatedSentance.Length;
                            i -= sentanceToTranslate.Length - translatedSentance.Length;

                            yield return new WaitForSeconds(delayBetweenTranslations);
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
            }
        }

        CheckAvailableRequests();

        if (!hasError)
        {
            // Save the file
            string pathToSave = outputFolderDirectory + "/" + path.Split('/')[path.Split('/').Length - 1].Split('.')[0] + fileNamePrefix.text + ".json";

            if (!WriteFile(pathToSave, fileText, false))
            {
                hasError = true;
            }
        }

        if (!hasError)
        {
            HasError(false);
            OnDone();
        }
        else
        {
            HasError(true);
            OnDone();
        }

    }

    private IEnumerator TranslateText(string sentance, Action IsDone, Action<string> TranslatedText, Action OnError)
    {
        yield return TranslationManager.Process(fromLanguageCode.text, toLanguageCode.text, sentance, saveTranslationValuesLocally,
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

            PrintLog("Read Text: " + fileText);

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

            PrintLog("written Text: " + content);
        }
        catch (Exception e)
        {
            SetStatusMessage(e.Message, true, Color.red);
            return false;
        }

        return true;
    }

    public void CheckAvailableRequests()
    {
        string currentLog = PlayerPrefs.GetString(TranslationManager.REQUESTS_LOG_KEY);
        string newLog = "";
        List<DateTime> dates = new List<DateTime>();

        foreach (string dateString in currentLog.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(dateString))
                continue;

            DateTime date = DateTime.Parse(dateString);

            // Check if the request has been made with in the last hour
            if (date.CompareTo(DateTime.UtcNow.AddHours(-1)) > 0)
            {
                dates.Add(date);
                newLog += "\n" + dateString;
            }
        }

        PlayerPrefs.SetString(TranslationManager.REQUESTS_LOG_KEY, newLog);

        availableRequestsTextField.text = (100 - dates.Count) < 0 ? "0" : (100 - dates.Count).ToString();
    }

    private void SetStatusMessage(string message, bool append, Color color = new Color())
    {
        if (append)
            statusMessageTextField.text += message;
        else
            statusMessageTextField.text = message;

        statusMessageTextField.color = color;
    }

    public void PrintLog(string message)
    {
        Debug.Log(message);
        DebugLogTextField.text = message;
    }
}