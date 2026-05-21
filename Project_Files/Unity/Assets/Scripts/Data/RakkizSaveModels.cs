using System;
using System.Collections.Generic;

[Serializable]
public class RakkizSaveData
{
    public PlayerData player;          // Stores the current player information.
    public List<SessionData> sessions; // Stores all completed gameplay sessions.

    public bool hasPlayerName;       // True when the player already entered a valid name.
    public bool hasSeenTutorial;    // True after the tutorial has been completed once.
}

[Serializable]
public class PlayerData
{
    public string playerName; // Player name shown and saved in the app.
}

[Serializable]
public class SessionData
{
    public string sessionId; // Unique ID for one gameplay session.
    public string timestamp; // Date and time when the session was saved.
    public string gameMode;  // Game name, such as Rassd or Tayyar.

    public int score; // Final score for the session.

    public float TetaBetaRatio;  // Average theta/beta ratio saved for attention analysis.
    public float BetaAlghaRatio; // Average beta/alpha ratio saved for attention analysis.

    public BehavioralMetrics behavioralMetrics; // Gameplay behavior summary for this session.
}

[Serializable]
public class BehavioralMetrics
{
    public int correctResponse; // Correct player actions.
    public int omission;        // Missed required responses.
    public int commission;      // Wrong responses to non-targets.
    public float averageReactionTimeMs;     // Average response time in milliseconds.
    public float reactionTimeVariabilityMs; // Variation in response time.
}
