using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using ArabicSupport;

[RequireComponent(typeof(TMP_Text))]
[RequireComponent(typeof(LocalizeStringEvent))]
public class LocalizedArabicTMPFixer : MonoBehaviour
{
    [Header("Optional fonts")]
    [SerializeField] private TMP_FontAsset englishFont; 
    [SerializeField] private TMP_FontAsset arabicFont;  

    private TMP_Text tmp;                
    private LocalizeStringEvent loc;      // Localization string event source

    void Awake()
    {
        tmp = GetComponent<TMP_Text>();
        loc = GetComponent<LocalizeStringEvent>();

        loc.OnUpdateString.AddListener(Apply); // Update text when localization changes
    }

    void OnDestroy()
    {
        if (loc != null)
            loc.OnUpdateString.RemoveListener(Apply); // Clean up listener
    }

    void Apply(string s)
    {
        bool isArabic =
            LocalizationSettings.SelectedLocale != null &&
            LocalizationSettings.SelectedLocale.Identifier.Code == "ar"; // Detect Arabic locale

        tmp.isRightToLeftText = isArabic; // Enable RTL rendering for Arabic
        tmp.alignment = isArabic
            ? TextAlignmentOptions.MidlineRight
            : TextAlignmentOptions.MidlineLeft; // Align text based on direction

        if (isArabic && arabicFont != null)
            tmp.font = arabicFont;

        if (!isArabic && englishFont != null)
            tmp.font = englishFont;

        tmp.text = isArabic
            ? ArabicFixer.Fix(s, true, true) // Apply Arabic shaping when needed
            : s;

        tmp.ForceMeshUpdate(true); // Ensure text mesh is refreshed
    }
}
