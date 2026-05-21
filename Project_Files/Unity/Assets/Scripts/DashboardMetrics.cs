using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public sealed class DashboardMetrics
{
    public string PlayerName { get; }
    public int TotalSessions { get; }
    public float OverallAttentionAvg { get; }
    public int BestScore { get; }
    public float AverageScore { get; }
    public string LastSessionDateText { get; }

    public int LastSessionTargetsHit { get; }
    public int LastSessionMissedTargets { get; }
    public float LastSessionReactionVariabilityMs { get; }
    public float LastSessionAvgReactionTimeMs { get; }

    public IReadOnlyList<float> DailyAverageScores { get; }
    public IReadOnlyList<string> DailyAverageScoreLabels { get; }
    public IReadOnlyList<DashboardSessionViewModel> Sessions { get; }

    private DashboardMetrics(
        string playerName,
        int totalSessions,
        float overallAttentionAvg,
        int bestScore,
        float averageScore,
        string lastSessionDateText,
        int lastSessionTargetsHit,
        int lastSessionMissedTargets,
        float lastSessionReactionVariabilityMs,
        float lastSessionAvgReactionTimeMs,
        List<float> dailyAverageScores,
        List<string> dailyAverageScoreLabels,
        List<DashboardSessionViewModel> sessions)
    {
        PlayerName = playerName;
        TotalSessions = totalSessions;
        OverallAttentionAvg = overallAttentionAvg;
        BestScore = bestScore;
        AverageScore = averageScore;
        LastSessionDateText = lastSessionDateText;
        LastSessionTargetsHit = lastSessionTargetsHit;
        LastSessionMissedTargets = lastSessionMissedTargets;
        LastSessionReactionVariabilityMs = lastSessionReactionVariabilityMs;
        LastSessionAvgReactionTimeMs = lastSessionAvgReactionTimeMs;
        DailyAverageScores = dailyAverageScores;
        DailyAverageScoreLabels = dailyAverageScoreLabels;
        Sessions = sessions;
    }

    public static DashboardMetrics FromSave(RakkizSaveData data)
    {
        if (data == null)
            return Empty("Player"); // Return safe empty values if the save file is missing.

        string playerName = GetPlayerName(data);
        List<SessionData> sessions = data.sessions ?? new List<SessionData>(); // Avoid null session lists.

        int totalSessions = sessions.Count;
        int bestScore = totalSessions == 0 ? 0 : sessions.Max(s => s.score); // Highest saved score.
        float averageScore = totalSessions == 0 ? 0f : (float)sessions.Average(s => s.score); // Average score across sessions.

        float overallAttentionAvg = totalSessions == 0
            ? 0f
            : (float)sessions.Average(s => s.BetaAlghaRatio); // Uses BAR as the dashboard attention indicator.

        BuildDailyAverageScores(sessions, out List<float> dailyScores, out List<string> dailyLabels);

        List<DashboardSessionViewModel> sessionViewModels = BuildSessionViewModels(sessions);

        DashboardSessionViewModel lastVm = sessionViewModels.Count > 0
            ? sessionViewModels[sessionViewModels.Count - 1] // Most recent session after sorting.
            : null;

        int lastHits = lastVm != null ? ParseIntSafe(lastVm.HitsText) : 0;
        int lastMisses = lastVm != null ? ParseIntSafe(lastVm.MissesText) : 0;
        float lastVariability = lastVm?.ReactionTimeVariabilityMs ?? 0f;
        float lastAvgReactionTime = lastVm?.AverageReactionTimeMs ?? 0f;

        return new DashboardMetrics(
            playerName,
            totalSessions,
            overallAttentionAvg,
            bestScore,
            averageScore,
            GetLastSessionText(sessions),
            lastHits,
            lastMisses,
            lastVariability,
            lastAvgReactionTime,
            dailyScores,
            dailyLabels,
            sessionViewModels);
    }

    private static string GetPlayerName(RakkizSaveData data)
    {
        if (data.player == null || string.IsNullOrWhiteSpace(data.player.playerName))
            return "Player"; // Default name if none was saved.

        return data.player.playerName;
    }

    private static void BuildDailyAverageScores(
        List<SessionData> sessions,
        out List<float> scores,
        out List<string> labels)
    {
        scores = new List<float>();
        labels = new List<string>();

        if (sessions == null || sessions.Count == 0)
            return; // No chart points to build.

        var grouped = sessions
            .Select(session => new
            {
                Session = session,
                Date = TryParseUtcDate(session.timestamp, out DateTime parsed)
                    ? parsed.Date
                    : (DateTime?)null // Ignore sessions with invalid dates.
            })
            .Where(item => item.Date.HasValue)
            .GroupBy(item => item.Date.Value) // Group sessions by day.
            .OrderBy(group => group.Key);

        foreach (var group in grouped)
        {
            scores.Add((float)group.Average(item => item.Session.score)); // Average score for that day.
            labels.Add(group.Key.ToString("dd MMM", CultureInfo.InvariantCulture)); // Short label for the chart.
        }
    }

    private static List<DashboardSessionViewModel> BuildSessionViewModels(List<SessionData> sessions)
    {
        var result = new List<DashboardSessionViewModel>();

        if (sessions == null || sessions.Count == 0)
            return result;

        var orderedSessions = sessions
            .OrderBy(session => ParseSortableDate(session.timestamp)) // Oldest to newest.
            .ToList();

        for (int i = 0; i < orderedSessions.Count; i++)
        {
            SessionData session = orderedSessions[i];

            int correctResponse = session.behavioralMetrics != null
                ? session.behavioralMetrics.correctResponse
                : 0;

            int omission = session.behavioralMetrics != null
                ? session.behavioralMetrics.omission
                : 0;

            int commission = session.behavioralMetrics != null
                ? session.behavioralMetrics.commission
                : 0;

            float averageReactionTimeMs = session.behavioralMetrics != null
                ? session.behavioralMetrics.averageReactionTimeMs
                : 0f;

            float reactionTimeVariabilityMs = session.behavioralMetrics != null
                ? session.behavioralMetrics.reactionTimeVariabilityMs
                : 0f;

            int denominator = correctResponse + omission + commission;
            int accuracy = denominator <= 0
                ? 0
                : RoundToInt((correctResponse / (float)denominator) * 100f); // Correct responses percentage.

            result.Add(new DashboardSessionViewModel(
                index: i + 1,
                mode: ToDisplayMode(session.gameMode),
                dateText: FormatSessionDate(session.timestamp),
                hitsText: correctResponse.ToString(CultureInfo.InvariantCulture),
                missesText: omission.ToString(CultureInfo.InvariantCulture),
                falseAlarmsText: commission.ToString(CultureInfo.InvariantCulture),
                scoreText: session.score.ToString(CultureInfo.InvariantCulture),
                accuracyText: $"{accuracy}% acc",
                averageReactionTimeMs: averageReactionTimeMs,
                reactionTimeVariabilityMs: reactionTimeVariabilityMs,
                attentionAvg: session.BetaAlghaRatio));
        }

        return result;
    }

    private static string GetLastSessionText(List<SessionData> sessions)
    {
        if (sessions == null || sessions.Count == 0)
            return "Last session · —";

        SessionData latest = sessions
            .OrderByDescending(session => ParseSortableDate(session.timestamp)) // Find newest session.
            .First();

        return $"Last session · {FormatDateOnly(latest.timestamp)}";
    }

    private static string ToDisplayMode(string rawMode)
    {
        if (string.IsNullOrWhiteSpace(rawMode))
            return "Unknown";

        rawMode = rawMode.Trim().ToLowerInvariant();

        return rawMode switch
        {
            "rassd" => "Rassd",
            "tayyar" => "Tayyar",
            "skywatcher" => "Rassd",
            "skyrings" => "Tayyar",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(rawMode) // Clean display for unknown modes.
        };
    }

    private static string FormatSessionDate(string isoTimestamp)
    {
        if (!TryParseUtcDate(isoTimestamp, out DateTime date))
            return "—";

        return date.ToLocalTime().ToString("dd MMM · HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatDateOnly(string isoTimestamp)
    {
        if (!TryParseUtcDate(isoTimestamp, out DateTime date))
            return "—";

        return date.ToLocalTime().ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    }

    private static DateTime ParseSortableDate(string isoTimestamp)
    {
        return TryParseUtcDate(isoTimestamp, out DateTime parsed)
            ? parsed
            : DateTime.MinValue; // Invalid dates go first.
    }

    private static bool TryParseUtcDate(string isoTimestamp, out DateTime utcDateTime)
    {
        utcDateTime = default;

        if (string.IsNullOrWhiteSpace(isoTimestamp))
            return false;

        return DateTime.TryParse(
            isoTimestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out utcDateTime);
    }

    private static int RoundToInt(float value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero); // Normal rounding for dashboard values.
    }

    private static int ParseIntSafe(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var digits = new string(text.Where(char.IsDigit).ToArray()); // Keep only numbers from labels.
        return int.TryParse(digits, out int v) ? v : 0;
    }

    private static DashboardMetrics Empty(string playerName)
    {
        return new DashboardMetrics(
            playerName,
            0,
            0f,
            0,
            0f,
            "Last session · —",
            0,
            0,
            0f,
            0f,
            new List<float>(),
            new List<string>(),
            new List<DashboardSessionViewModel>());
    }
}

public sealed class DashboardSessionViewModel
{
    public int Index { get; }
    public string Mode { get; }
    public string DateText { get; }
    public string HitsText { get; }
    public string MissesText { get; }
    public string FalseAlarmsText { get; }
    public string ScoreText { get; }
    public string AccuracyText { get; }
    public float AverageReactionTimeMs { get; }
    public float ReactionTimeVariabilityMs { get; }
    public float AttentionAvg { get; }

    public DashboardSessionViewModel(
        int index,
        string mode,
        string dateText,
        string hitsText,
        string missesText,
        string falseAlarmsText,
        string scoreText,
        string accuracyText,
        float averageReactionTimeMs,
        float reactionTimeVariabilityMs,
        float attentionAvg)
    {
        Index = index;
        Mode = mode;
        DateText = dateText;
        HitsText = hitsText;
        MissesText = missesText;
        FalseAlarmsText = falseAlarmsText;
        ScoreText = scoreText;
        AccuracyText = accuracyText;
        AverageReactionTimeMs = averageReactionTimeMs;
        ReactionTimeVariabilityMs = reactionTimeVariabilityMs;
        AttentionAvg = attentionAvg;
    }
}
