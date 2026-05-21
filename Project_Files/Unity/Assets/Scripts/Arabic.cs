using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

[RequireComponent(typeof(TMP_Text))]
public class Arabic : MonoBehaviour
{
    public TMP_FontAsset englishFont;
    public TMP_FontAsset arabicFont;

    private TMP_Text textComponent;

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        ApplyArabicNumberStyle();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private void OnLocaleChanged(Locale _)
    {
        ApplyArabicNumberStyle();
    }

    public void ApplyArabicNumberStyle()
    {
        if (textComponent == null)
            return;

        if (IsArabic())
        {
            if (arabicFont != null)
                textComponent.font = arabicFont;

            textComponent.text = ConvertToArabicDigits(textComponent.text);
        }
        else
        {
            if (englishFont != null)
                textComponent.font = englishFont;
        }

        textComponent.ForceMeshUpdate(true);
    }

    private static bool IsArabic()
    {
        return LocalizationSettings.SelectedLocale != null &&
               LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ar");
    }

    private static string ConvertToArabicDigits(string text)
    {
        return text
            .Replace('0', '٠')
            .Replace('1', '١')
            .Replace('2', '٢')
            .Replace('3', '٣')
            .Replace('4', '٤')
            .Replace('5', '٥')
            .Replace('6', '٦')
            .Replace('7', '٧')
            .Replace('8', '٨')
            .Replace('9', '٩');
    }
}