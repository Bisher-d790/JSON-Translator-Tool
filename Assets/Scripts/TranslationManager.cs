using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SimpleJSON;
using System;
using UnityEngine.Networking;

public class TranslationManager : MonoBehaviour
{
    public const string REQUESTS_LOG_KEY = "REQUESTS_LOG";

    [Header("Only for testing")]
    [SerializeField] private Text testToTranslateTextField;
    [SerializeField] private Text testTranslatedTextField;
    [SerializeField] private Text testStatusTextField;
    [SerializeField] private Text fromLanguageCode;
    [SerializeField] private Text toLanguageCode;
    [SerializeField] private bool saveTranslationValuesLocally = true;


    #region For Testing
    public void TestTranslate()
    {
        StartCoroutine(Process(fromLanguageCode.text, toLanguageCode.text, testToTranslateTextField.text, saveTranslationValuesLocally,
            (translatedText) => testTranslatedTextField.text = translatedText,
            (errorString) => testStatusTextField.text = errorString));
    }
    #endregion

    // We have use googles own api built into google Translator.
    public static IEnumerator Process(string sourceLang, string targetLang, string sourceText, bool saveValuesLocally, Action<string> OnTranslated, Action<string> OnError)
    {
        // We use Auto by default to determine if google can figure it out.. sometimes it can't.
        if (string.IsNullOrEmpty(sourceLang))
            sourceLang = "auto";

        // Check if the sourceText was already translated before to the targetLanguage
        if (saveValuesLocally && PlayerPrefs.HasKey(sourceText + "_" + targetLang))
        {
            string translatedText = PlayerPrefs.GetString(sourceText + "_" + targetLang);
            OnTranslated(translatedText);
        }
        else
        {
            // Construct the url using our variables and googles api.
            string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl="
                + sourceLang + "&tl=" + targetLang + "&dt=t&q=" + UnityWebRequest.EscapeURL(sourceText);

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {
                // Request and wait for the desired page.
                yield return webRequest.SendWebRequest();

                // Check to see if we don't have any errors.
                if (string.IsNullOrEmpty(webRequest.error))
                {
                    // Parse the response using JSON.
                    var N = JSONNode.Parse(webRequest.downloadHandler.text);

                    // Dig through and take apart the text to get to the good stuff.
                    string translatedText = N[0][0][0];

                    // Save the translated value to player prefs
                    if (saveValuesLocally)
                    {
                        PlayerPrefs.SetString(sourceText + "_" + targetLang, translatedText);
                        PlayerPrefs.Save();
                    }

                    // Save the request date
                    PlayerPrefs.SetString(REQUESTS_LOG_KEY, PlayerPrefs.GetString(REQUESTS_LOG_KEY) + "\n" + DateTime.UtcNow.ToString());

                    OnTranslated(translatedText);
                }
                else
                {
                    OnError(webRequest.error);
                }
            }
        }
    }
}
