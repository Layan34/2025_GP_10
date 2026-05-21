using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

// Automatically adjusts font size for Arabic text to match English visual size.
// Attach this script to any TMP_Text GameObject that needs size adjustment.
[RequireComponent(typeof(TMP_Text))]
public class ArabicFontSizeAdjuster : MonoBehaviour
{
    [Header("Font Sizes")]
    [Tooltip("Font size when language is English")]
    public float englishFontSize = 36f;

    [Tooltip("Font size when language is Arabic (usually larger)")]
    public float arabicFontSize = 48f;

    private TMP_Text _text;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        ApplyFontSize();
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    // Called automatically when the language changes
    void OnLocaleChanged(Locale _)
    {
        ApplyFontSize();
    }

    void ApplyFontSize()
    {
        if (_text == null) return;

        bool isArabic = LocalizationSettings.SelectedLocale != null &&
                        LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ar");

        _text.fontSize = isArabic ? arabicFontSize : englishFontSize;
    }
}