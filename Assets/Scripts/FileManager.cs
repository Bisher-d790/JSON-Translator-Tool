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
    [SerializeField] private Text DebugLogTextField;
    [SerializeField] private float delayBetweenFiles = 0.1f;
    [SerializeField] private float delayBetweenTranslations = 0.1f;
    [SerializeField] private int requestCharacterLimit = 5000;

    [Header("Language Code")]
    [SerializeField] private Text fromLanguageCode;
    [SerializeField] private Text toLanguageCode;

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

    /*
     * Need to satisfy two limitation with using Google Translate API free requests:
     * 1- Limit 5k characters for each request
     * 2- 100 requests per hour
     */
    private IEnumerator TranslateFile(string path, Action OnDone, Action<bool> HasError)
    {
        bool hasError = false;

        // Open the file
        string fileText = ReadFile(path);

        // Get the sentences to translated into a string
        string translationRequestString = "";
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

            // Fill the translation request string with all the values from the JSON file
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

                        // Add the sentance to translate to the translation request string
                        if (sentanceToTranslate.Length > 0)
                        {

                            if (!string.IsNullOrWhiteSpace(translationRequestString))
                                translationRequestString += "\"";
                            translationRequestString += sentanceToTranslate;
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

        // Process the translation requests
        // Split the full request to smaller less 5k chars parts
        List<string> translationRequests = new List<string>();
        translationRequests.Add(translationRequestString);

        while (translationRequests[translationRequests.Count - 1].Length / requestCharacterLimit >= 1.0f)
        {
            string currentRequest = translationRequests[translationRequests.Count - 1];

            // Get the index to split the string into two, and it should not be the middle of a sentence
            int cutoffIndex = requestCharacterLimit - 1;
            while (!currentRequest[cutoffIndex].Equals("\"")) cutoffIndex--;

            string newRequest = currentRequest.Substring(cutoffIndex);
            currentRequest = currentRequest.Substring(0, cutoffIndex);

            translationRequests[translationRequests.Count - 1] = currentRequest;
            translationRequests.Add(newRequest);
        }

        // process the small requests one by one, and gather them into a string
        string fullTranslatedRequest = "";
        foreach (string request in translationRequests)
        {
            string translatedString = "";

            bool hasDoneTranslation = false;

            Debug.Log("Request: " + request.Length + ": " + request);

            StartCoroutine(TranslateText(
                request,
                () => hasDoneTranslation = true,
                (translatedText) => translatedString = translatedText,
                () =>
                {
                    hasError = true;
                }));

            yield return new WaitWhile(() => !hasDoneTranslation);

            if (hasError) break;

            PrintLog("Translated request: " + translatedString.Length + ": " + translatedString);
            fullTranslatedRequest += translatedString;
        }
        PrintLog("fullTranslatedRequest: " + fullTranslatedRequest.Length + ": " + fullTranslatedRequest);

        // Set the translated values as the values for the new JSON file
        if (fullTranslatedRequest.Length <= 0)
        {
            hasError = true;
            SetStatusMessage("Translated string is empty!", true, Color.red);
        }
        else
        {
            // Translate the content
            int startSentanceIndex = 0;
            bool isTranslating = false;
            string sentanceToTranslate = "";

            // variables for sentence replacement
            string[] translatedSentences = fullTranslatedRequest.Split('\"');
            int currentSentenceIndex = 0;

            // Fill the translation request string with all the values from the JSON file
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

                        // Replace the currect sentences with the translated sentences
                        if (sentanceToTranslate.Length > 0)
                        {
                            if (currentSentenceIndex >= translatedSentences.Length)
                            {
                                hasError = true;
                                break;
                            }

                            string translatedSentance = translatedSentences[currentSentenceIndex];

                            fileText = fileText.Substring(0, startSentanceIndex) + translatedSentance + fileText.Substring(i);

                            startSentanceIndex = startSentanceIndex - sentanceToTranslate.Length - translatedSentance.Length;
                            i -= sentanceToTranslate.Length - translatedSentance.Length;

                            yield return new WaitForSeconds(delayBetweenTranslations);

                            currentSentenceIndex++;
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

        // Save the file
        if (!hasError)
        {
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