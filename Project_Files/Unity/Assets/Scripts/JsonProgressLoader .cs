using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class JsonProgressLoader : MonoBehaviour
{
    [Header("JSON Settings")]
    [SerializeField] private string jsonFileName = "rakkiz_save.json"; // Save file used for chart data.

    [Header("Session Filter")]
    [SerializeField] private bool includeAllGameModes = true; // If false, only Rassd sessions are shown.

    [Header("Chart")]
    [SerializeField] private SimpleUILineChart lineChart; // UI chart that draws progress lines.

    [Header("Overall Progress Texts")]
    [SerializeField] private TMP_Text insightTitleText; // Title for the progress summary.
    [SerializeField] private TMP_Text insightDetailsText; // Detailed progress values.
    [SerializeField] private TMP_Text insightSummaryText; // Short feedback message.

    [Header("Overall Progress Style")]
    [SerializeField] private int titleFontSize = 22;
    [SerializeField] private int detailsFontSize = 14;
    [SerializeField] private int summaryFontSize = 14;

    [SerializeField] private Color titleColor = new Color32(39, 103, 65, 255);
    [SerializeField] private Color detailsColor = new Color32(55, 55, 55, 255);
    [SerializeField] private Color summaryColor = new Color32(39, 103, 65, 255);

    [SerializeField] private float detailsLineSpacing = 5f;
    [SerializeField] private float summaryLineSpacing = 4f;

    [Header("Fonts")]
    [SerializeField] private TMP_FontAsset numberFontAsset;

    private bool IsArabic
    {
        get
        {
            try
            {
                if (LocalizationSettings.HasSettings)
                {
                    var locale = LocalizationSettings.SelectedLocale;
                    if (locale != null)
                        return locale.Identifier.Code == "ar";
                }
            }
            catch
            {
                // Ignore until localization becomes ready.
            }

            return Application.systemLanguage == SystemLanguage.Arabic;
        }
    }

    private void Start()
    {
        LoadJsonSessionsAndDrawChart(); // Load saved sessions when the screen starts.
    }

    [ContextMenu("Reload JSON Chart")]
    public void LoadJsonSessionsAndDrawChart()
    {
        bool ar = IsArabic;

        if (lineChart != null)
        {
            lineChart.SetArabicMode(ar); // Match chart direction/language.

            if (numberFontAsset != null)
                lineChart.SetNumberFont(numberFontAsset); // Use the assigned number font.
        }

        RakkizSaveRepository repository = new RakkizSaveRepository(jsonFileName);
        RakkizSaveData data = repository.LoadOrCreate(); // Read existing save data or create a new file.

        if (data == null || data.sessions == null || data.sessions.Count == 0)
        {
            DrawEmptyChart(); // Clear the chart when there is no data.
            ApplyEmptyOverallProgress(ar); // Show empty-state insight text.
            return;
        }

        List<SessionData> sessions = data.sessions
            .Where(s => s != null)
            .Where(s => includeAllGameModes ||
                        string.Equals(s.gameMode, "Rassd", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => ParseDate(s.timestamp))
            .ToList();

        if (sessions.Count == 0)
        {
            DrawEmptyChart(); // Clear the chart when there is no data.
            ApplyEmptyOverallProgress(ar); // Show empty-state insight text.
            return;
        }

        List<string> labels = sessions
            .Select((_, index) => (index + 1).ToString(CultureInfo.InvariantCulture))
            .ToList();
        List<float> correctRaw = sessions
            .Select(s => s.behavioralMetrics != null
                ? Mathf.Max(0f, s.behavioralMetrics.correctResponse)
                : 0f)
            .ToList();

        List<float> totalRaw = sessions
            .Select(s => s.behavioralMetrics != null
                ? Mathf.Max(0f, s.behavioralMetrics.correctResponse) +
                  Mathf.Max(0f, s.behavioralMetrics.omission)
                : 0f)
            .ToList();

        List<float> thetaBetaRaw = sessions
            .Select(s => s.TetaBetaRatio)
            .ToList();

        List<float> betaAlphaRaw = sessions
            .Select(s => s.BetaAlghaRatio)
            .ToList();
        List<float> correctAnswersScore = sessions
            .Select(CalculateCorrectAnswerScore)
            .ToList();

        List<float> focusScore = NormalizeRatioToHundred(thetaBetaRaw, higherIsBetter: false); // Lower TBR means better focus.
        List<float> alertnessScore = NormalizeRatioToHundred(betaAlphaRaw, higherIsBetter: true); // Higher BAR means stronger alertness.

        List<float> overallProgressScore = BuildOverallProgressScore(
            correctAnswersScore,
            focusScore,
            alertnessScore
        );

        ApplyOverallProgressPanel(
            correctAnswersScore,
            focusScore,
            alertnessScore,
            overallProgressScore,
            ar
        );

        if (lineChart == null)
        {
            Debug.LogError("[JsonProgressLoader] Line Chart reference is missing.");
            return;
        }

        ChartSeries correctSeries = new ChartSeries(
            "Correct Answers",
            "الإجابات الصحيحة",
            correctAnswersScore,
            "Shows the percentage of targets answered correctly. Higher = better.",
            "يوضح نسبة الإجابات الصحيحة في كل جلسة. كلما زادت النتيجة كان الأداء أفضل."
        );
        correctSeries.RawValues = correctRaw;
        correctSeries.RawValues2 = totalRaw;
        correctSeries.RawUnit = "correct answers";
        correctSeries.RawUnitArabic = "إجابة صحيحة";

        ChartSeries unfocusedSeries = new ChartSeries(
            "Unfocused Indicator",
            "مؤشر عدم التركيز",
            focusScore,
            "Shows the player's lack of focus during the session. Higher = more unfocused.",
            "يوضح مستوى عدم التركيز أثناء الجلسة. كلما زادت النتيجة كان عدم التركيز أعلى."
        );
        unfocusedSeries.RawValues = thetaBetaRaw;
        unfocusedSeries.RawUnit = "Theta/Beta ratio";
        unfocusedSeries.RawUnitArabic = "نسبة ثيتا/بيتا";

        ChartSeries focusSeries = new ChartSeries(
            "Focus Indicator",
            "مؤشر التركيز",
            alertnessScore,
            "Shows the player's focus level during the session. Higher = stronger focus.",
            "يوضح مستوى التركيز أثناء الجلسة. كلما زادت النتيجة كان التركيز أفضل."
        );
        focusSeries.RawValues = betaAlphaRaw;
        focusSeries.RawUnit = "Beta/Alpha ratio";
        focusSeries.RawUnitArabic = "نسبة بيتا/ألفا";

        ChartSeries overallSeries = new ChartSeries(
            "Overall Progress",
            "التقدم الكلي",
            overallProgressScore,
            "Average of Correct Answers, Focus, and Alertness on a unified 0-100 scale.",
            "يمثل متوسط الإجابات الصحيحة والتركيز واليقظة على مقياس موحد من ٠ إلى ١٠٠.",
            "",
            true
        );

        lineChart.DrawChart(labels, new List<ChartSeries>
        {
            correctSeries,
            unfocusedSeries,
            focusSeries,
            overallSeries
        });

        Debug.Log("[JsonProgressLoader] Loaded " + sessions.Count + " sessions from JSON.");
    }

    private static float CalculateCorrectAnswerScore(SessionData session)
    {
        if (session == null || session.behavioralMetrics == null)
            return 0f;

        float correct = Mathf.Max(0f, session.behavioralMetrics.correctResponse);
        float omission = Mathf.Max(0f, session.behavioralMetrics.omission);
        float total = correct + omission;

        if (total <= 0.0001f)
            return 0f;

        return Mathf.Clamp((correct / total) * 100f, 0f, 100f);
    }

    private static List<float> NormalizeRatioToHundred(List<float> values, bool higherIsBetter)
    {
        if (values == null || values.Count == 0)
            return new List<float>();

        List<float> valid = values
            .Where(v => v > 0.0001f)
            .ToList();

        if (valid.Count <= 1)
            return values.Select(_ => 50f).ToList();

        float min = valid.Min();
        float max = valid.Max();
        float range = max - min;

        if (range <= 0.0001f)
            return values.Select(_ => 50f).ToList();

        return values.Select(value =>
        {
            if (value <= 0.0001f)
                return 50f;

            float score = ((value - min) / range) * 100f;

            if (!higherIsBetter)
                score = 100f - score;

            return Mathf.Clamp(score, 0f, 100f);
        }).ToList();
    }

    private static List<float> BuildOverallProgressScore(
        List<float> correct,
        List<float> focus,
        List<float> alertness)
    {
        int count = Mathf.Min(correct.Count, Mathf.Min(focus.Count, alertness.Count));
        List<float> result = new List<float>(count);

        for (int i = 0; i < count; i++)
        {
            float overall = (correct[i] + focus[i] + alertness[i]) / 3f;
            result.Add(Mathf.Clamp(overall, 0f, 100f));
        }

        return result;
    }

    private void DrawEmptyChart()
    {
        if (lineChart != null)
            lineChart.DrawChart(new List<string>(), new List<ChartSeries>());
    }

    private void ApplyEmptyOverallProgress(bool ar)
    {
        SetInsightTitle(ar ? "التقدم الكلي" : "Overall Progress");

        SetInsightDetails(ar
            ? "الدرجة الحالية: —\nالإجابات الصحيحة: —\nالتركيز: —\nاليقظة: —"
            : "Current score: —\nCorrect answers: —\nFocus: —\nAlertness: —");

        SetInsightSummary(ar
            ? "لا توجد جلسات بعد.\nالعب أكثر حتى يبدأ النظام في تتبع تقدمك."
            : "No sessions yet.\nPlay more sessions to start tracking your progress.");
    }

    private void ApplyOverallProgressPanel(
        List<float> correctScore,
        List<float> focusScore,
        List<float> alertnessScore,
        List<float> overallScore,
        bool ar)
    {
        TrendResult correctTrend = AnalyzeTrend(correctScore, true, 5f);
        TrendResult focusTrend = AnalyzeTrend(focusScore, true, 5f);
        TrendResult alertTrend = AnalyzeTrend(alertnessScore, true, 5f);
        TrendResult overallTrend = AnalyzeTrend(overallScore, true, 5f);

        float currentOverall = overallScore != null && overallScore.Count > 0
            ? overallScore.Last()
            : 0f;

        float previousOverall = overallScore != null && overallScore.Count > 1
            ? overallScore[overallScore.Count - 2]
            : currentOverall;

        float change = currentOverall - previousOverall;

        string summary = BuildSummary(
            overallTrend,
            correctTrend,
            focusTrend,
            alertTrend,
            overallScore,
            ar
        );

        SetInsightTitle(ar ? "التقدم الكلي" : "Overall Progress");

        if (ar)
        {
            SetInsightDetails(
                "درجتك الحالية: " + ToArabicIndicDigits(Mathf.RoundToInt(currentOverall)) + "/١٠٠\n" +
                "التغير عن الجلسة السابقة: " + FormatSignedArabic(change) + " نقطة\n" +
                "الإجابات الصحيحة: " + TrendLabel(correctTrend.Label, true) + "\n" +
                "التركيز: " + TrendLabel(focusTrend.Label, true) + "\n" +
                "اليقظة: " + TrendLabel(alertTrend.Label, true)
            );
        }
        else
        {
            SetInsightDetails(
                "Current score: " + Mathf.RoundToInt(currentOverall) + "/100\n" +
                "Change from previous session: " + FormatSignedEnglish(change) + " points\n" +
                "Correct answers: " + TrendLabel(correctTrend.Label, false) + "\n" +
                "Focus: " + TrendLabel(focusTrend.Label, false) + "\n" +
                "Alertness: " + TrendLabel(alertTrend.Label, false)
            );
        }

        SetInsightSummary(summary);
    }

    private void SetInsightTitle(string text)
    {
        if (insightTitleText == null)
            return;

        insightTitleText.text = text;
        insightTitleText.fontSize = titleFontSize;
        insightTitleText.color = titleColor;
        insightTitleText.alignment = TextAlignmentOptions.Center;
        insightTitleText.lineSpacing = 0f;
        insightTitleText.enableWordWrapping = true;
        insightTitleText.overflowMode = TextOverflowModes.Overflow;
        insightTitleText.richText = false;
        insightTitleText.fontStyle = FontStyles.Bold;
        insightTitleText.fontWeight = FontWeight.Bold;

        FixArabicText(insightTitleText);
    }

    private void SetInsightDetails(string text)
    {
        if (insightDetailsText == null)
            return;

        insightDetailsText.text = text;
        insightDetailsText.fontSize = detailsFontSize;
        insightDetailsText.color = detailsColor;
        insightDetailsText.alignment = IsArabic ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        insightDetailsText.lineSpacing = detailsLineSpacing;
        insightDetailsText.enableWordWrapping = true;
        insightDetailsText.overflowMode = TextOverflowModes.Overflow;
        insightDetailsText.richText = false;
        insightDetailsText.fontStyle = FontStyles.Bold;
        insightDetailsText.fontWeight = FontWeight.Bold;

        FixArabicText(insightDetailsText);
    }

    private void SetInsightSummary(string text)
    {
        if (insightSummaryText == null)
            return;

        insightSummaryText.text = text;
        insightSummaryText.fontSize = summaryFontSize;
        insightSummaryText.color = summaryColor;
        insightSummaryText.alignment = TextAlignmentOptions.Center;
        insightSummaryText.lineSpacing = summaryLineSpacing;
        insightSummaryText.enableWordWrapping = true;
        insightSummaryText.overflowMode = TextOverflowModes.Overflow;
        insightSummaryText.richText = false;
        insightSummaryText.fontStyle = FontStyles.Bold;
        insightSummaryText.fontWeight = FontWeight.Bold;

        FixArabicText(insightSummaryText);
    }

    private void FixArabicText(TMP_Text text)
    {
        if (!IsArabic || text == null)
            return;

        text.gameObject.SendMessage(
            "UpdateString",
            text.text,
            SendMessageOptions.DontRequireReceiver
        );
    }

    private static string BuildSummary(
        TrendResult overallTrend,
        TrendResult correctTrend,
        TrendResult focusTrend,
        TrendResult alertTrend,
        List<float> overallScore,
        bool ar)
    {
        if (overallScore == null || overallScore.Count == 0)
        {
            return ar
                ? "لا توجد بيانات تقدم بعد.\nالعب المزيد من الجلسات حتى تظهر نتيجتك."
                : "No progress data yet.\nPlay more sessions to start tracking your progress.";
        }

        if (overallScore.Count == 1)
        {
            return ar
                ? "هذه أول جلسة لك.\nبعد الجلسة القادمة سيقارن النظام نتائجك.\nسيظهر تقدمك بشكل أوضح."
                : "This is your first session.\nAfter the next session, the system will compare your results.\nYour progress will be clearer.";
        }

        float previous = overallScore[overallScore.Count - 2];
        float current = overallScore[overallScore.Count - 1];
        float change = current - previous;
        float absChange = Mathf.Abs(change);

        if (absChange < 5f)
        {
            return ar
                ? "نتيجتك قريبة من الجلسة السابقة.\nلا يوجد تغير كبير حالياً.\nحاول المحافظة على نفس المستوى."
                : "Your result is close to the previous session.\nThere is no major change yet.\nTry to maintain your level.";
        }

        if (change > 0f)
        {
            return ar
                ? "أداؤك تحسن بمقدار " + ToArabicIndicDigits(Mathf.RoundToInt(absChange)) + " نقطة.\nهذا يدل على تقدم واضح.\nحافظ على التركيز والإجابات الصحيحة."
                : "Your performance improved by " + Mathf.RoundToInt(absChange) + " points.\nThis shows clear progress.\nKeep your focus and correct answers.";
        }

        if (overallTrend.Label == "Improving")
        {
            return ar
                ? "نتيجتك انخفضت بمقدار " + ToArabicIndicDigits(Mathf.RoundToInt(absChange)) + " نقطة.\nلكن اتجاهك العام ما زال جيداً.\nحاول استعادة تقدمك في الجلسة القادمة."
                : "Your result dropped by " + Mathf.RoundToInt(absChange) + " points.\nBut your overall trend is still good.\nTry to recover your progress next session.";
        }

        return ar
            ? "نتيجتك انخفضت بمقدار " + ToArabicIndicDigits(Mathf.RoundToInt(absChange)) + " نقطة.\nحاول التمهل أكثر.\nركز على الإجابات الصحيحة في الجلسة القادمة."
            : "Your result dropped by " + Mathf.RoundToInt(absChange) + " points.\nTry to slow down.\nFocus on correct answers next session.";
    }

    private static string TrendLabel(string label, bool ar)
    {
        if (!ar)
        {
            return label switch
            {
                "Improving" => "Improving",
                "Decreasing" => "Lower than before",
                "Stable" => "Almost the same",
                "Mixed" => "Mixed",
                _ => "Not enough data"
            };
        }

        return label switch
        {
            "Improving" => "تحسن",
            "Decreasing" => "انخفاض",
            "Stable" => "ثابت تقريباً",
            "Mixed" => "متغير",
            _ => "بيانات غير كافية"
        };
    }

    private static TrendResult AnalyzeTrend(
        List<float> values,
        bool higherIsBetter,
        float minChange)
    {
        if (values == null || values.Count < 2)
        {
            return new TrendResult
            {
                Label = "Not enough data",
                Slope = 0f,
                Delta = 0f
            };
        }

        float first = values.First();
        float last = values.Last();
        float rawDelta = last - first;
        float slope = LinearSlope(values);

        float adjustedDelta = higherIsBetter ? rawDelta : -rawDelta;
        float adjustedSlope = higherIsBetter ? slope : -slope;

        bool bigDelta = Mathf.Abs(adjustedDelta) >= minChange;
        bool bigSlope = Mathf.Abs(adjustedSlope) >= minChange * 0.15f;

        string label;

        if (!bigDelta && !bigSlope)
            label = "Stable";
        else if (adjustedDelta > 0f && adjustedSlope > 0f)
            label = "Improving";
        else if (adjustedDelta < 0f && adjustedSlope < 0f)
            label = "Decreasing";
        else
            label = "Mixed";

        return new TrendResult
        {
            Label = label,
            Slope = slope,
            Delta = rawDelta
        };
    }

    private static float LinearSlope(List<float> values)
    {
        int count = values.Count;

        float sumX = 0f;
        float sumY = 0f;
        float sumXY = 0f;
        float sumXX = 0f;

        for (int i = 0; i < count; i++)
        {
            sumX += i;
            sumY += values[i];
            sumXY += i * values[i];
            sumXX += i * i;
        }

        float denominator = count * sumXX - sumX * sumX;

        if (Mathf.Abs(denominator) < 0.0001f)
            return 0f;

        return (count * sumXY - sumX * sumY) / denominator;
    }

    private static DateTime ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DateTime.MinValue;

        bool parsed = DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTime date
        );

        return parsed ? date : DateTime.MinValue;
    }

    private static string FormatSignedEnglish(float value)
    {
        int rounded = Mathf.RoundToInt(value);

        if (rounded > 0)
            return "+" + rounded;

        return rounded.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatSignedArabic(float value)
    {
        int rounded = Mathf.RoundToInt(value);

        if (rounded > 0)
            return "+" + ToArabicIndicDigits(rounded);

        return ToArabicIndicDigits(rounded);
    }

    private static string ToArabicIndicDigits(int number)
    {
        return ToArabicIndicDigits(number.ToString(CultureInfo.InvariantCulture));
    }

    private static string ToArabicIndicDigits(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        char[] result = value.ToCharArray();

        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] >= '0' && result[i] <= '9')
                result[i] = (char)('٠' + (result[i] - '0'));
        }

        return new string(result);
    }
}

[Serializable]
public class TrendResult
{
    public string Label;
    public float Slope;
    public float Delta;
}