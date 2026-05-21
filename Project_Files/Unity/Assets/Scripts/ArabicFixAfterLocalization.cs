using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using ArabicSupport;

[RequireComponent(typeof(TMP_Text))]
[RequireComponent(typeof(LocalizeStringEvent))]
public class ArabicFixAfterLocalization : MonoBehaviour
{
    public enum AlignMode
    {
        AutoByLanguage,   // Alignment follows the selected language 
        AlwaysCenter      // Center alignment 
    }

    [Header("Fonts")]
    public TMP_FontAsset englishFont;
    public TMP_FontAsset arabicFont;

    [Header("Alignment")]
    public AlignMode alignmentMode = AlignMode.AutoByLanguage;

    private TMP_Text _text;
    private LocalizeStringEvent _localize;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();             
        _localize = GetComponent<LocalizeStringEvent>();  

        _localize.OnUpdateString.RemoveListener(OnLocalizedStringUpdated); // Prevent duplicate listeners
        _localize.OnUpdateString.AddListener(OnLocalizedStringUpdated);
    }

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged; 
        ApplyFontAndAlignment();                                        
        _localize.RefreshString();                                    
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged; 
    }

    void OnLocaleChanged(Locale _)
    {
        ApplyFontAndAlignment();   // Update font and alignment on language change
        _localize.RefreshString(); // Re-apply localized value
    }

    private bool IsArabic()
    {
        return LocalizationSettings.SelectedLocale != null &&
               LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ar"); // Check for Arabic locale
    }

    void ApplyFontAndAlignment()
    {
        bool isArabic = IsArabic();

        // Select font based on current language
        if (isArabic && arabicFont != null)
            _text.font = arabicFont;

        if (!isArabic && englishFont != null)
            _text.font = englishFont;

        // Apply text alignment based on mode and language direction
        if (alignmentMode == AlignMode.AlwaysCenter)
        {
            _text.alignment = TextAlignmentOptions.Center;
        }
        else
        {
            _text.alignment = isArabic
                ? TextAlignmentOptions.MidlineRight
                : TextAlignmentOptions.MidlineLeft;
        }
    }

    void OnLocalizedStringUpdated(string value)
    {
        bool isArabic = IsArabic();

        _text.text = isArabic ? ArabicFixer.Fix(value, true, true) : value;

        _text.ForceMeshUpdate(true); // Ensure text mesh is updated
    }
}
