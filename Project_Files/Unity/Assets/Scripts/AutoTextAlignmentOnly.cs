using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

[RequireComponent(typeof(TMP_Text))]
public class AutoTextAlignmentOnly : MonoBehaviour
{
    private TMP_Text text;

    void Awake()
    {
        text = GetComponent<TMP_Text>(); 
    }

    void OnEnable()
    {
        ApplyAlignment(); // Apply alignment based on current locale
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged; 
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged; // Listen for locale changes
    }

    private void OnLocaleChanged(Locale _)
    {
        ApplyAlignment(); // Re-apply alignment when language changes
    }

    private void ApplyAlignment()
    {
        bool isArabic = LocalizationSettings.SelectedLocale != null &&
                        LocalizationSettings.SelectedLocale.Identifier.Code == "ar"; 

        text.alignment = isArabic
            ? TextAlignmentOptions.MidlineRight
            : TextAlignmentOptions.MidlineLeft; // Apply RTL or LTR alignment

        text.ForceMeshUpdate(true); // Ensure text layout is updated
    }
}
