using System;
using UnityEngine;

public static class SessionLogger
{
    private const string SaveFileName = "rakkiz_save.json"; // Main JSON save file.

    public static void AppendSession(
        string gameMode,
        int score,
        int correctResponse,
        int omission,
        int commission,
        float tetaBetaRatio = 0f,
        float betaAlghaRatio = 0f,
        float averageReactionTimeMs = 0f,
        float reactionTimeVariabilityMs = 0f)
    {
        var repo = new RakkizSaveRepository(SaveFileName); // Handles reading and writing save data.

        var session = new SessionData
        {
            sessionId = Guid.NewGuid().ToString(),
            timestamp = DateTime.UtcNow.ToString("o"), // Save timestamp in UTC format.
            gameMode = gameMode,
            score = score,

            TetaBetaRatio = Mathf.Round(tetaBetaRatio * 100f) / 100f, // Store TBR rounded to 2 decimals.
            BetaAlghaRatio = Mathf.Round(betaAlghaRatio * 100f) / 100f, // Store BAR rounded to 2 decimals.

            behavioralMetrics = new BehavioralMetrics
            {
                correctResponse = correctResponse,
                omission = omission,
                commission = commission,
                averageReactionTimeMs = Mathf.Round(averageReactionTimeMs * 100f) / 100f,
                reactionTimeVariabilityMs = Mathf.Round(reactionTimeVariabilityMs * 100f) / 100f
            }
        };

        repo.AppendSessionToLastPlayer(session); // Add the session under the saved player.

        Debug.Log(
            $"Session saved: {gameMode} | score={score} | correctResponse={correctResponse} | " +
            $"omission={omission} | commission={commission} | " +
            $"TBR={session.TetaBetaRatio:F2} | BAR={session.BetaAlghaRatio:F2} | " +
            $"RT={session.behavioralMetrics.averageReactionTimeMs:F2}ms | " +
            $"RTV={session.behavioralMetrics.reactionTimeVariabilityMs:F2}ms");
    }
}
