using UnityEngine;

public class HitBasedDifficultyManager : MonoBehaviour
{
    public static HitBasedDifficultyManager Instance;

    public enum DifficultyMode { TwoLevels, ThreeLevels }

    [Header("Mode")]
    [Tooltip("TwoLevels for Rassd. ThreeLevels for Tayyar.")]
    public DifficultyMode difficultyMode = DifficultyMode.ThreeLevels; // Controls how many difficulty levels are used.

    [Header("Thresholds")]
    [Tooltip("ThreeLevels only: score needed to reach medium.")]
    public float mediumThreshold = 10f; // Minimum combined score for medium.

    [Tooltip("Score needed to reach hard.")]
    public float hardThreshold = 20f; // Minimum combined score for hard.

    [Header("Weights")]
    [Range(0f, 1f)]
    public float hitWeight = 0.75f; // Main weight for gameplay performance.

    [Range(0f, 1f)]
    public float eegWeight = 0.25f; // Smaller weight for EEG focus state.

    [Header("EEG Score Settings")]
    public float eegScoreStart = 10f; // Neutral EEG score at the beginning.
    public float maxEegScore = 20f; // Highest EEG score after repeated focus.
    public float eegStep = 1f; // Amount added or removed per EEG prediction.

    [Header("Runtime")]
    public string currentLevel = "easy"; // Current applied difficulty level.
    public int netScore = 0; // Correct hits minus false alarms.
    public float eegScore = 10f; // EEG score that moves up/down over time.
    public int eegPrediction = -1; // -1 means no EEG data yet.
    public float difficultyScore = 0f; // Final weighted score used for level selection.

    private bool hasEegData = false; // Tracks whether EEG has started sending predictions.

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(Instance.gameObject); // Keep one manager active.

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null; // Clear singleton reference.
    }

    private void Start()
    {
        ResetDifficulty(); // Start every session from easy.
    }

    public void ResetDifficulty()
    {
        netScore = 0;
        eegScore = eegScoreStart;
        eegPrediction = -1;
        difficultyScore = 0f;
        hasEegData = false;
        currentLevel = "easy";

        ApplyDifficulty(currentLevel); // Apply the initial easy level.

        Debug.Log("[DifficultyManager] Reset => Level=easy");
    }

    public void OnCorrectHit(int totalHits)
    {
        netScore++; // Reward correct gameplay response.
        EvaluateLevel();
    }

    public void OnFalseAlarm()
    {
        netScore--; // Penalize wrong response.

        if (netScore < 0)
            netScore = 0; // Keep score from going negative.

        EvaluateLevel();
    }

    public void OnEegPrediction(int prediction)
    {
        eegPrediction = prediction;
        hasEegData = true;

        if (prediction == 1)
            eegScore = Mathf.Min(eegScore + eegStep, maxEegScore); // Focus increases EEG score.
        else if (prediction == 0)
            eegScore = Mathf.Max(eegScore - eegStep, 0f); // Unfocus decreases EEG score.

        EvaluateLevel();
    }

    public void OnEegSignalLost()
    {
        eegPrediction = -1;
        hasEegData = false; // Keep the last EEG score but mark signal unavailable.
        EvaluateLevel();
    }

    private void EvaluateLevel()
    {
        difficultyScore = (netScore * hitWeight) + (eegScore * eegWeight); // Combine behavior and EEG.

        string newLevel;

        if (difficultyMode == DifficultyMode.TwoLevels)
        {
            newLevel = difficultyScore >= hardThreshold ? "hard" : "easy";
        }
        else
        {
            if      (difficultyScore >= hardThreshold)   newLevel = "hard";
            else if (difficultyScore >= mediumThreshold) newLevel = "medium";
            else                                         newLevel = "easy";
        }

        if (newLevel == currentLevel)
            return; // Avoid applying the same level again.

        currentLevel = newLevel;
        ApplyDifficulty(currentLevel);

        Debug.Log($"[DifficultyManager] netScore={netScore} eegScore={eegScore:F1} " +
                  $"difficultyScore={difficultyScore:F2} => Level={currentLevel}");
    }

    private void ApplyDifficulty(string level)
    {
        RassdLogic.Instance?.SetDifficulty(level); // Update Rassd if it exists.
        TayyarLogic.Instance?.SetDifficulty(level); // Update Tayyar if it exists.
        Spawner.Instance?.SetDifficultyByLevel(level); // Update spawn behavior.
        ObjectMoveManager.Instance?.SetDifficultyByLevel(level); // Update object movement.
        FogObstacle.Instance?.SetDifficultyByLevel(level); // Update fog intensity.
        DifficultyIndicatorUI.Instance?.OnDifficultyChanged(level); // Show level change feedback.
    }
}
