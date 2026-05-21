using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;
using System.Collections;
using UnityEngine.Localization.Components;
using ArabicSupport; // ArabicFixer

public class EnterNameScript : MonoBehaviour
{
    [System.Serializable]
    public class PlayerData : global::PlayerData { }

    [Header("UI References")]
    public TMP_InputField nameInput;
    public TextMeshProUGUI warningText;
    public Button startButton;

    [Header("Scene")]
    public string nextSceneName = "TutorialScene";

    [Header("EEG Setup")]
    [Tooltip("Assign the LabRecorderRcsController GameObject here. It will DontDestroyOnLoad automatically.")]
    public LabRecorderRcsController recorder;

    private RakkizSaveRepository saveRepo;

    [Header("Localization")]
    [SerializeField] private LocalizeStringEvent warningLSE;

    private void Start()
    {
        saveRepo = new RakkizSaveRepository("rakkiz_save.json");

        // Start Emotiv-LSL + LabRecorder early so they are ready before the experiment.
        if (recorder != null)
            recorder.Prewarm();
        else
            Debug.LogWarning("[EnterName] recorder not assigned — Prewarm will not run.");

        // The button remains visible; only interactability is changed.
        if (startButton != null)
            startButton.gameObject.SetActive(true);

        if (warningText != null)
            warningText.gameObject.SetActive(false);

        if (nameInput != null)
        {
            // Validates input characters before they appear in the input field.
            nameInput.onValidateInput = ValidateNameCharacter;

            // Validates the full name after input updates.
            nameInput.onValueChanged.AddListener(_ => OnNameChanged());
        }

        // Updates the warning text whenever the localized value changes.
        if (warningLSE != null)
            warningLSE.OnUpdateString.AddListener(OnWarningLocalizedUpdated);

        InitializeFromSavedData();
    }

    private void InitializeFromSavedData()
    {
        if (nameInput == null) return;

        nameInput.text = saveRepo.HasPlayerName()
            ? saveRepo.GetPlayerNameOrDefault(string.Empty)
            : string.Empty;

        UpdateStartButtonState(isValid: IsValidName(nameInput.text));
        HideWarning();
    }

    // Validates the current input after it has been applied.
    private void OnNameChanged()
    {
        string currentName = nameInput.text.Trim();

        if (string.IsNullOrWhiteSpace(currentName))
        {
            ShowWarningKey("warn_enter_name");
            UpdateStartButtonState(isValid: false);
            return;
        }

        if (!IsEnglishOnly(currentName))
        {
            ShowWarningKey("warn_english_only");
            UpdateStartButtonState(isValid: false);
            return;
        }

        HideWarning();
        UpdateStartButtonState(isValid: true);
    }

    public void StartGame()
    {
        string playerName = nameInput.text.Trim();

        if (!IsValidName(playerName))
        {
            OnNameChanged();
            return;
        }

        // Persists the validated player name and proceeds to the next scene.
        saveRepo.SetPlayerName(playerName);

        // NFR3 - Efficiency: record transition start time
        PlayerPrefs.SetFloat("SceneLoadStart", Time.realtimeSinceStartup);
        PlayerPrefs.SetString("SceneLoadLabel", "EnterName->TutorialScene");

        SceneManager.LoadScene(nextSceneName);
    }

    private bool IsValidName(string candidate)
    {
        string name = candidate == null ? string.Empty : candidate.Trim();
        return !string.IsNullOrWhiteSpace(name) && IsEnglishOnly(name);
    }

    private void UpdateStartButtonState(bool isValid)
    {
        if (startButton == null) return;

        // The button stays visible; only interaction is enabled/disabled.
        startButton.interactable = isValid;
    }

    private bool IsEnglishOnly(string name)
    {
        // Accepts English letters and spaces only.
        return Regex.IsMatch(name, @"^[a-zA-Z ]+$");
    }

    // Validates each character before it is rendered in the input field.
    private char ValidateNameCharacter(string currentText, int charIndex, char addedChar)
    {
        // Allows control characters.
        if (char.IsControl(addedChar))
            return addedChar;

        // Allows spaces.
        if (addedChar == ' ')
            return addedChar;

        // Allows English letters only.
        bool isEnglishLetter = (addedChar >= 'a' && addedChar <= 'z') || (addedChar >= 'A' && addedChar <= 'Z');
        if (isEnglishLetter)
            return addedChar;

        // Rejects unsupported characters and shows the corresponding warning.
        ShowWarningKey("warn_english_only");
        UpdateStartButtonState(isValid: false);
        return '\0';
    }

    private void ShowWarningKey(string key)
    {
        if (warningText == null) return;

        warningText.gameObject.SetActive(true);

        if (warningLSE != null)
        {
            warningLSE.StringReference.TableReference = "lang";
            warningLSE.StringReference.TableEntryReference = key;
            warningLSE.RefreshString();
            return;
        }

        StartCoroutine(SetLocalizedWarning(key));
    }

    private IEnumerator SetLocalizedWarning(string key)
    {
        // Waits for the localization system to complete initialization.
        yield return LocalizationSettings.InitializationOperation;

        var table = LocalizationSettings.StringDatabase.GetTable("lang");
        if (table == null)
        {
            SetWarningTextSafe("Missing String Table: lang");
            yield break;
        }

        var entry = table.GetEntry(key);
        string raw = entry != null ? entry.GetLocalizedString() : $"Missing key: {key}";
        SetWarningTextSafe(raw);
    }

    private void HideWarning()
    {
        if (warningText == null) return;
        warningText.gameObject.SetActive(false);
    }

    private void OnWarningLocalizedUpdated(string localizedValue)
    {
        SetWarningTextSafe(localizedValue);
    }

    private void SetWarningTextSafe(string raw)
    {
        if (warningText == null) return;

        // Applies Arabic shaping when the message contains Arabic characters.
        warningText.text = FixArabicIfNeeded(raw);
    }

    private static string FixArabicIfNeeded(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (!ContainsArabicLetters(s)) return s;

        return ArabicFixer.Fix(s, showTashkeel: true, useHinduNumbers: false);
    }

    private static bool ContainsArabicLetters(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if ((c >= '\u0600' && c <= '\u06FF') ||
                (c >= '\u0750' && c <= '\u077F') ||
                (c >= '\u08A0' && c <= '\u08FF') ||
                (c >= '\uFB50' && c <= '\uFDFF') ||
                (c >= '\uFE70' && c <= '\uFEFF'))
                return true;
        }
        return false;
    }
}