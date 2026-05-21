using ArabicSupport;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

public sealed class DashboardSessionRowUI : MonoBehaviour
{
    [Header("Layout Groups")]
    [SerializeField] private RectTransform indexCircle; // Session number circle.
    [SerializeField] private RectTransform infoGroup; // Mode and date group.
    [SerializeField] private RectTransform pillsGroup; // Hits, misses, and reaction time group.
    [SerializeField] private RectTransform scoreGroup; // Score display group.

    [Header("Texts")]
    [SerializeField] private TMP_Text indexText;
    [SerializeField] private TMP_Text modeText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text hitPillText;
    [SerializeField] private TMP_Text missPillText;

    [SerializeField] private TMP_Text reactionTimeText;
    [SerializeField] private TMP_Text reactionVariabilityText;
    [SerializeField] private TMP_Text scoreLabelText;
    [SerializeField] private TMP_Text scoreValueText;

    [Header("Fonts")]
    [SerializeField] private TMP_FontAsset englishFont;
    [SerializeField] private TMP_FontAsset arabicFont;

    private static readonly Vector2 IndexCircle_EN = new Vector2(-629f, 0f);
    private static readonly Vector2 InfoGroup_EN   = new Vector2(-53f,  0f);
    private static readonly Vector2 PillsGroup_EN  = new Vector2(109f,  -27f);
    private static readonly Vector2 ScoreGroup_EN  = new Vector2(617f,  0f);

    private static readonly Vector2 IndexCircle_AR = new Vector2(629f,  0f);
    private static readonly Vector2 InfoGroup_AR   = new Vector2(53f,   0f);
    private static readonly Vector2 PillsGroup_AR  = new Vector2(-109f, -27f);
    private static readonly Vector2 ScoreGroup_AR  = new Vector2(-617f, 0f);

    private static readonly Vector2 ModeTMP_EN  = new Vector2(-417f,  15f);
    private static readonly Vector2 DateTMP_EN  = new Vector2(-417f, -17f);
    private static readonly Vector2 ModeTMP_AR  = new Vector2( 417f,  15f);
    private static readonly Vector2 DateTMP_AR  = new Vector2( 417f, -17f);

    public void Bind(DashboardSessionViewModel viewModel)
    {
        if (viewModel == null)
            return;

        AutoBindMissingReferences(); // Find missing TMP references automatically.

        bool isArabic = IsArabicLocale(); // Check if the current UI language is Arabic.

        ApplyFonts(isArabic); // Switch fonts based on language.
        ApplyLayoutDirection(isArabic); // Mirror layout for Arabic.

        SetText(indexText,        FormatNumber(viewModel.Index, isArabic));
        SetText(modeText,         FormatMode(viewModel.Mode, isArabic));
        SetText(dateText,         FormatDate(viewModel.DateText, isArabic));

        SetText(hitPillText,       FormatMetric(viewModel.HitsText,        "إصابة",       isArabic));
        SetText(missPillText,      FormatMetric(viewModel.MissesText,      "خطأ",         isArabic));

        SetText(reactionTimeText,       FormatReactionTime(viewModel.AverageReactionTimeMs, isArabic));
        SetText(reactionVariabilityText, FormatReactionTime(viewModel.ReactionTimeVariabilityMs, isArabic));

        SetText(scoreLabelText,  isArabic ? ArabicFixer.Fix("النتيجة", false, true) : "Score");
        SetText(scoreValueText,  FormatText(viewModel.ScoreText, isArabic));

    }

    private void ApplyLayoutDirection(bool isArabic)
    {
        if (indexCircle != null)
            indexCircle.anchoredPosition = isArabic ? IndexCircle_AR : IndexCircle_EN;

        if (infoGroup != null)
            infoGroup.anchoredPosition = isArabic ? InfoGroup_AR : InfoGroup_EN;

        if (pillsGroup != null)
            pillsGroup.anchoredPosition = isArabic ? PillsGroup_AR : PillsGroup_EN;

        if (scoreGroup != null)
            scoreGroup.anchoredPosition = isArabic ? ScoreGroup_AR : ScoreGroup_EN;

        if (modeText != null)
            modeText.rectTransform.anchoredPosition = isArabic ? ModeTMP_AR : ModeTMP_EN;

        if (dateText != null)
            dateText.rectTransform.anchoredPosition = isArabic ? DateTMP_AR : DateTMP_EN;
    }

    private void AutoBindMissingReferences()
    {
        if (modeText == null && infoGroup != null)
            modeText = FindTextByName(infoGroup, "ModeTMP");

        if (dateText == null && infoGroup != null)
            dateText = FindTextByName(infoGroup, "DateTMP");

        if (reactionTimeText == null && pillsGroup != null)
            reactionTimeText = FindTextByName(pillsGroup, "RTPill");

        if (reactionVariabilityText == null && pillsGroup != null)
            reactionVariabilityText = FindTextByName(pillsGroup, "RTVPill");

        if (scoreLabelText == null && scoreGroup != null)
            scoreLabelText = FindTextByName(scoreGroup, "Text (TMP)");

        if (scoreValueText == null && scoreGroup != null)
            scoreValueText = FindTextByName(scoreGroup, "ScoreTMP");
    }

    private static TMP_Text FindTextByName(RectTransform parent, string childName)
    {
        TMP_Text[] texts = parent.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text.gameObject.name == childName)
                return text;
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private void ApplyFonts(bool isArabic)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null)
                continue;

            if (isArabic && arabicFont != null)
                text.font = arabicFont;
            else if (!isArabic && englishFont != null)
                text.font = englishFont;

            text.isRightToLeftText = false;
            text.alignment = TextAlignmentOptions.Center;
        }
    }

    private static string FormatMode(string mode, bool isArabic)
    {
        if (!isArabic)
            return string.IsNullOrWhiteSpace(mode) ? "—" : mode;

        string normalized = string.IsNullOrWhiteSpace(mode)
            ? ""
            : mode.Trim().ToLowerInvariant();

        string arabicMode = normalized switch
        {
            "rassd"  => "رصد",
            "tayyar" => "طيّار",
            _        => string.IsNullOrWhiteSpace(mode) ? "—" : mode
        };

        return ArabicFixer.Fix(arabicMode, false, true);
    }

    private static string FormatDate(string value, bool isArabic)
    {
        if (string.IsNullOrWhiteSpace(value))
            value = "—";

        if (!isArabic)
            return value;

        value = ConvertMonthToArabic(value);
        return ArabicFixer.Fix(ConvertToArabicDigits(value), false, true);
    }

    private static string FormatMetric(string value, string arabicWord, bool isArabic)
    {
        if (!isArabic)
            return string.IsNullOrWhiteSpace(value) ? "0" : value;

        int number = ExtractNumber(value);
        string text = $"{FormatNumber(number, true)} {arabicWord}";
        return ArabicFixer.Fix(text, false, true);
    }

    private static string FormatReactionTime(float ms, bool isArabic)
    {
        if (ms <= 0f)
            return isArabic ? ArabicFixer.Fix("غير متاح", false, true) : "N/A";

        string value = $"{Mathf.RoundToInt(ms)} ms";
        return isArabic ? ArabicFixer.Fix(ConvertToArabicDigits(value), false, true) : value;
    }

    private static string FormatText(string value, bool isArabic)
    {
        if (string.IsNullOrWhiteSpace(value))
            value = "—";

        return isArabic
            ? ArabicFixer.Fix(ConvertToArabicDigits(value), false, true)
            : value;
    }

    private static string FormatNumber(int value, bool isArabic)
    {
        string text = value.ToString();
        return isArabic ? ConvertToArabicDigits(text) : text;
    }

    private static string ConvertMonthToArabic(string text)
    {
        return text
            .Replace("Jan", "يناير")
            .Replace("Feb", "فبراير")
            .Replace("Mar", "مارس")
            .Replace("Apr", "أبريل")
            .Replace("May", "مايو")
            .Replace("Jun", "يونيو")
            .Replace("Jul", "يوليو")
            .Replace("Aug", "أغسطس")
            .Replace("Sep", "سبتمبر")
            .Replace("Oct", "أكتوبر")
            .Replace("Nov", "نوفمبر")
            .Replace("Dec", "ديسمبر");
    }

    private static int ExtractNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        string digits = "";

        foreach (char c in text)
        {
            if (char.IsDigit(c))
                digits += c;
        }

        return int.TryParse(digits, out int value) ? value : 0;
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

    private static bool IsArabicLocale()
    {
        var locale = LocalizationSettings.SelectedLocale;
        return locale != null && locale.Identifier.Code.StartsWith("ar");
    }

    private static void SetText(TMP_Text textComponent, string value)
    {
        if (textComponent != null)
            textComponent.text = value;
    }

    private static void SetTextColor(TMP_Text textComponent, string htmlColor)
    {
        if (textComponent == null)
            return;

        if (ColorUtility.TryParseHtmlString(htmlColor, out Color color))
            textComponent.color = color;
    }

}