using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using ArabicSupport;

public class LanguageDropdownController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown dropdown; // Language selection dropdown

    private bool isInitializing; // Prevents callbacks during initial setup

    private async void Start()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        isInitializing = true;

        // Ensure localization system is fully initialized
        await LocalizationSettings.InitializationOperation.Task;

        dropdown.ClearOptions();
        dropdown.options.Add(new TMP_Dropdown.OptionData(""));
        dropdown.options.Add(new TMP_Dropdown.OptionData(""));

        dropdown.onValueChanged.AddListener(OnDropdownChanged);

        await RefreshOptionLabels();      // Update option labels based on current locale
        SyncValueWithSelectedLocale();    // Sync dropdown value with selected language

        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;

        isInitializing = false;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged; // Unsubscribe on destroy
    }

    private async void OnSelectedLocaleChanged(Locale _)
    {
        await RefreshOptionLabels();      // Update labels when language changes
        SyncValueWithSelectedLocale();    // Keep dropdown value consistent
    }

    private void SyncValueWithSelectedLocale()
    {
        var code = LocalizationSettings.SelectedLocale.Identifier.Code;
        dropdown.SetValueWithoutNotify(code == "ar" ? 1 : 0); // Arabic = 1, English = 0
        dropdown.RefreshShownValue();
    }

    private Task RefreshOptionLabels()
    {
        var currentCode = LocalizationSettings.SelectedLocale.Identifier.Code;

        string option0; // English language option
        string option1; // Arabic language option

        if (currentCode == "ar")
        {
            option0 = "الإنجليزية";
            option1 = "العربية";

            option0 = ArabicFixer.Fix(option0, true, true);
            option1 = ArabicFixer.Fix(option1, true, true);
        }
        else
        {
            option0 = "English";
            option1 = "Arabic";
        }

        dropdown.options[0].text = option0;
        dropdown.options[1].text = option1;

        dropdown.RefreshShownValue();
        return Task.CompletedTask;
    }

    private void OnDropdownChanged(int index)
    {
        if (isInitializing) return; // Ignore changes during initialization

        var targetCode = (index == 1) ? "ar" : "en";
        var locales = LocalizationSettings.AvailableLocales.Locales;
        var target = locales.Find(l => l.Identifier.Code == targetCode);

        if (target != null)
            LocalizationSettings.SelectedLocale = target; // Apply selected language
    }
}
