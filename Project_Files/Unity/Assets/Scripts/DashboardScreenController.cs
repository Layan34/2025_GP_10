using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using ArabicSupport;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public sealed class DashboardScreenController : MonoBehaviour
{
    [Header("JSON")]
    [SerializeField] private string saveFileName = "rakkiz_save.json"; // Save file used to read dashboard data.

    [Header("Text (TMP)")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text lastSessionDateText;
    [SerializeField] private TMP_Text totalSessionsText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text averageScoreText;
    [SerializeField] private TMP_Text insightText;
    [SerializeField] private TMP_Text targetsHitText;
    [SerializeField] private TMP_Text missedTargetsText;
    [SerializeField] private TMP_Text reactionVariabilityText;
    [SerializeField] private TMP_Text avgReactionTimeText;

    [Header("Empty State")]
    [Tooltip("نص يظهر في المنتصف لما ما في جلسات — اربطه بـ TMP في المنتصف")]
    [SerializeField] private TMP_Text emptyStateText;

    [Tooltip("كل الكروت/العناصر اللي تختفي لما ما في جلسات")]
    [SerializeField] private GameObject[] cardsToHideWhenEmpty;

    [Header("Presenters")]
    [SerializeField] private DashboardSessionsPresenter sessionsPresenter;

    private DashboardMetrics lastMetrics; // Stores the latest calculated dashboard values.
    private bool hasMetrics; // True when at least one session exists.
    private Coroutine reloadRoutine; // Prevents overlapping reloads after language changes.
    private int localeVersion; // Tracks the latest language change request.

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation;
        LoadAndRenderDashboard();
    }

    private void OnLocaleChanged(Locale _)
    {
        localeVersion++;

        if (reloadRoutine != null)
            StopCoroutine(reloadRoutine);

        reloadRoutine = StartCoroutine(ReloadAfterLocaleChange(localeVersion));
    }

    private IEnumerator ReloadAfterLocaleChange(int version)
    {
        yield return LocalizationSettings.InitializationOperation;

        if (version != localeVersion)
            yield break;

        LoadAndRenderDashboard();
    }

    private void LoadAndRenderDashboard()
    {
        LoadMetrics();
        RenderDashboard();
    }

    private void LoadMetrics()
    {
        var repository = new DashboardDataRepository(saveFileName);

        if (!repository.TryLoad(out RakkizSaveData data))
        {
            hasMetrics = false;
            lastMetrics = null;
            return;
        }

        lastMetrics = DashboardMetrics.FromSave(data);
        hasMetrics = lastMetrics != null && lastMetrics.TotalSessions > 0;
    }

    private void RenderDashboard()
    {
        if (!hasMetrics || lastMetrics == null || lastMetrics.TotalSessions == 0)
        {
            RenderEmptyState();
            return;
        }

        RenderText(lastMetrics);
        RenderPresenters(lastMetrics);
        SetCardsVisible(true);
        SetEmptyStateVisible(false);
    }

    private void RenderPresenters(DashboardMetrics metrics)
    {
        sessionsPresenter?.Render(metrics);
    }

    private void RenderText(DashboardMetrics metrics)
    {
        bool isArabic = IsArabicLocale(); // Check if the current UI language is Arabic.

        SetSafeText(nameText,                FormatName(metrics.PlayerName, isArabic));
        SetSafeText(lastSessionDateText,     FormatLastSessionDate(metrics.LastSessionDateText, isArabic));
        SetSafeText(totalSessionsText,       FormatNumber(metrics.TotalSessions, isArabic));
        SetSafeText(bestScoreText,           FormatNumber(metrics.BestScore, isArabic));
        SetSafeText(averageScoreText,        FormatNumber(metrics.AverageScore, isArabic));
        SetSafeText(targetsHitText,          FormatNumber(metrics.LastSessionTargetsHit, isArabic));
        SetSafeText(missedTargetsText,       FormatNumber(metrics.LastSessionMissedTargets, isArabic));
        SetSafeText(reactionVariabilityText, FormatNumber(metrics.LastSessionReactionVariabilityMs, isArabic));
        SetSafeText(avgReactionTimeText,     FormatNumber(metrics.LastSessionAvgReactionTimeMs, isArabic));
        SetSafeText(insightText,             GetInsightMessage(metrics, isArabic));
    }

    private void RenderEmptyState()
    {
        bool isArabic = IsArabicLocale(); // Check if the current UI language is Arabic.

        string playerName = "Player";
        var repo = new DashboardDataRepository(saveFileName);
        if (repo.TryLoad(out RakkizSaveData data) && data?.player != null
            && !string.IsNullOrWhiteSpace(data.player.playerName))
        {
            playerName = data.player.playerName.Trim();
        }

        SetSafeText(nameText,            FormatName(playerName, isArabic));
        SetSafeText(lastSessionDateText, isArabic ? FixArabic("آخر جلسة · —") : "Last session · —");

        SetCardsVisible(false); // Hide dashboard cards when there are no sessions.

        SetEmptyStateVisible(true); // Show the empty message instead.

        string msg = isArabic
            ? FixArabic("العب جلسة أولاً لتظهر نتائجك هنا")
            : "Play a session first to see your results here";

        SetSafeText(emptyStateText, msg);
    }


    private void SetCardsVisible(bool visible)
    {
        if (cardsToHideWhenEmpty == null) return;

        foreach (GameObject card in cardsToHideWhenEmpty)
        {
            if (card != null)
                card.SetActive(visible);
        }
    }

    private void SetEmptyStateVisible(bool visible)
    {
        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(visible);
    }


    private static string GetInsightMessage(DashboardMetrics metrics, bool isArabic)
    {
        if (metrics == null || metrics.Sessions == null || metrics.Sessions.Count == 0)
        {
            return isArabic
                ? FixArabic("لا توجد جلسات كافية لعرض ملخص الأداء.")
                : "No sessions yet. Play more sessions to view performance insights.";
        }

        if (metrics.Sessions.Count == 1)
        {
            return isArabic
                ? FixArabic("تم تسجيل أول جلسة. تابع اللعب لقياس تغيّر النتيجة.")
                : "First session recorded. Keep playing to track score changes.";
        }

        DashboardSessionViewModel previous = metrics.Sessions[metrics.Sessions.Count - 2];
        DashboardSessionViewModel latest   = metrics.Sessions[metrics.Sessions.Count - 1];

        int scoreDifference = ParseIntSafe(latest.ScoreText) - ParseIntSafe(previous.ScoreText);

        return isArabic
            ? GetArabicInsight(scoreDifference)
            : GetEnglishInsight(scoreDifference);
    }

    private static string GetEnglishInsight(int scoreDifference)
    {
        if (scoreDifference > 0)
            return $"Score improved by {scoreDifference} points compared with the previous session.";

        if (scoreDifference < 0)
            return $"Score decreased by {Mathf.Abs(scoreDifference)} points compared with the previous session.";

        return "Score stayed the same compared with the previous session.";
    }

    private static string GetArabicInsight(int scoreDifference)
    {
        string message;

        if (scoreDifference > 0)
            message = $"تحسنت النتيجة بمقدار {scoreDifference} نقاط مقارنة بالجلسة السابقة.";
        else if (scoreDifference < 0)
            message = $"انخفضت النتيجة بمقدار {Mathf.Abs(scoreDifference)} نقاط مقارنة بالجلسة السابقة.";
        else
            message = "بقيت النتيجة كما هي مقارنة بالجلسة السابقة.";

        return FixArabic(ConvertToArabicDigits(message));
    }


    private static int ParseIntSafe(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        string digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int value) ? value : 0;
    }

    private static string FormatName(string value, bool isArabic)
    {
        if (string.IsNullOrWhiteSpace(value)) value = "Player";
        return isArabic ? FixArabic(value) : value.ToUpperInvariant();
    }

    private static string FormatLastSessionDate(string value, bool isArabic)
    {
        if (string.IsNullOrWhiteSpace(value))
            return isArabic ? FixArabic("آخر جلسة · —") : "Last session · —";

        if (!isArabic) return value;

        string cleanDate = value
            .Replace("LAST SESSION", "")
            .Replace("Last session", "")
            .Replace("last session", "")
            .Replace("·", "")
            .Trim();

        string[] formats = { "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy", "d MMMM yyyy" };

        if (DateTime.TryParseExact(cleanDate, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime parsedDate))
        {
            string arabicDate = $"{parsedDate.Day} {GetArabicMonth(parsedDate.Month)} {parsedDate.Year}";
            return FixArabic($"آخر جلسة · {ConvertToArabicDigits(arabicDate)}");
        }

        return FixArabic($"آخر جلسة · {ConvertToArabicDigits(cleanDate)}");
    }

    private static string GetArabicMonth(int month)
    {
        switch (month)
        {
            case 1:  return "يناير";
            case 2:  return "فبراير";
            case 3:  return "مارس";
            case 4:  return "أبريل";
            case 5:  return "مايو";
            case 6:  return "يونيو";
            case 7:  return "يوليو";
            case 8:  return "أغسطس";
            case 9:  return "سبتمبر";
            case 10: return "أكتوبر";
            case 11: return "نوفمبر";
            case 12: return "ديسمبر";
            default: return "";
        }
    }

    private static string FormatNumber(float number, bool isArabic)
    {
        return FormatNumber(Mathf.RoundToInt(number), isArabic);
    }

    private static string FormatNumber(int number, bool isArabic)
    {
        string text = number.ToString();
        return isArabic ? ConvertToArabicDigits(text) : text;
    }

    private static string ConvertToArabicDigits(string text)
    {
        return text
            .Replace('0', '٠').Replace('1', '١').Replace('2', '٢')
            .Replace('3', '٣').Replace('4', '٤').Replace('5', '٥')
            .Replace('6', '٦').Replace('7', '٧').Replace('8', '٨')
            .Replace('9', '٩');
    }

    private static string FixArabic(string text)
    {
        return ArabicFixer.Fix(text, false, true);
    }

    private static void SetSafeText(TMP_Text textComponent, string value)
    {
        if (textComponent != null)
            textComponent.text = value;
    }

    private static bool IsArabicLocale()
    {
        Locale locale = LocalizationSettings.SelectedLocale;
        return locale != null && locale.Identifier.Code.StartsWith("ar");
    }
}