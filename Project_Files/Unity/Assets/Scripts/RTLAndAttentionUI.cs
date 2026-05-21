using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public sealed class RTLAndAttentionUI : MonoBehaviour
{
    [Header("Fonts")]
    [SerializeField] private TMP_FontAsset englishFont; // Font used for English UI.
    [SerializeField] private TMP_FontAsset arabicFont; // Font used for Arabic UI.

    [Header("Image Position")]
    [SerializeField] private RectTransform iconRect; // Icon moved based on language direction.
    [SerializeField] private Vector2 englishPosition; // Icon position for English layout.
    [SerializeField] private Vector2 arabicPosition; // Icon position for Arabic layout.

    private TMP_Text[] texts; // All text elements under this object.

    private void Awake()
    {
        texts = GetComponentsInChildren<TMP_Text>(true); // Cache texts for faster updates.
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged; // Update UI when language changes.
        ApplyUI();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged; // Avoid duplicate event calls.
    }

    private void OnLocaleChanged(Locale _)
    {
        ApplyUI(); // Reapply fonts and icon position.
    }

    private void ApplyUI()
    {
        bool isArabic = IsArabicLocale(); // Check current language.

        foreach (TMP_Text tmp in texts)
        {
            if (tmp == null)
                continue;

            if (isArabic && arabicFont != null)
                tmp.font = arabicFont;
            else if (!isArabic && englishFont != null)
                tmp.font = englishFont;
        }

        if (iconRect != null)
            iconRect.anchoredPosition = isArabic ? arabicPosition : englishPosition; // Move icon for RTL/LTR.
    }

    private static bool IsArabicLocale()
    {
        Locale locale = LocalizationSettings.SelectedLocale;
        return locale != null && locale.Identifier.Code.StartsWith("ar");
    }
}
