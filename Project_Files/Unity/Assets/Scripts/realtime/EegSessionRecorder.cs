using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EegWindowRecord
{
    public int windowIndex; // Order of this EEG window in the session.
    public float gameTimeSeconds; // Unity time when this window was recorded.
    public string utcTimestamp; // Real UTC timestamp for later analysis.

    public string zone; // Focus zone returned by the inference server.
    public float score; // Focus score saved for this window.
    public float generalScore; // General model score saved for reporting.
    public float confidence; // Confidence returned by the model.
    public bool isFocused; // Final focused/unfocused state.

    public float[] theta = new float[14]; // Theta band values for 14 EEG channels.
    public float[] alpha = new float[14]; // Alpha band values for 14 EEG channels.
    public float[] beta = new float[14]; // Beta band values for 14 EEG channels.
    public float[] tbr = new float[14]; // Theta/Beta ratio per channel.
    public float[] bar = new float[14]; // Beta/Alpha ratio per channel.

    public string difficultyZone; // Difficulty level at this moment.
    public int scoreAtWindow; // Game score at this moment.
    public int heartsAtWindow; // Remaining hearts at this moment.
    public int hitsAtWindow; // Hits count at this moment.
    public int missesAtWindow; // Misses count at this moment.
    public int falseAlarmsAtWindow; // False alarms count at this moment.
}

public class EegSessionRecorder : MonoBehaviour
{
    public static EegSessionRecorder Instance { get; private set; } // Active recorder in the current scene.

    [Header("EEG Source")]
    [SerializeField] private LiveInferenceClient liveInferenceClient; // Sends EEG inference results.

    public IReadOnlyList<EegWindowRecord> Records => _records; // Read-only access for export.
    private readonly List<EegWindowRecord> _records = new(); // Stores all recorded EEG windows.

    private int _ctxScore; // Latest game score.
    private int _ctxHearts; // Latest heart count.
    private int _ctxHits; // Latest hit count.
    private int _ctxMisses; // Latest miss count.
    private int _ctxFalseAlarms; // Latest false alarm count.
    private string _ctxZone = "medium"; // Latest difficulty zone.
    private bool _recording = true; // Controls whether new windows are saved.

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this); // Keep the scene object, remove only the duplicate recorder.
            return;
        }

        Instance = this; // Use one recorder per game scene.
    }

    private void OnDestroy()
    {
        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult -= OnFocusResult; // Avoid duplicated event calls.

        if (Instance == this)
            Instance = null; // Clear global reference when this recorder is destroyed.
    }

    private void OnEnable()
    {
        if (liveInferenceClient == null)
            liveInferenceClient = LiveInferenceClient.Instance != null
                ? LiveInferenceClient.Instance
                : FindFirstObjectByType<LiveInferenceClient>(); // Find the persistent inference client.

        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult += OnFocusResult; // Start recording incoming EEG windows.
        else
            Debug.LogWarning("[EegSessionRecorder] LiveInferenceClient not found — EEG windows will not be recorded.");
    }

    private void OnDisable()
    {
        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult -= OnFocusResult; // Stop recording when disabled.
    }

    public void RebindToScene()
    {
        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult -= OnFocusResult; // Remove old binding first.

        liveInferenceClient = LiveInferenceClient.Instance != null
            ? LiveInferenceClient.Instance
            : FindFirstObjectByType<LiveInferenceClient>(); // Get the current active client.

        if (liveInferenceClient != null)
        {
            liveInferenceClient.OnFocusResult += OnFocusResult; // Listen again after scene changes.
            Debug.Log("[EegSessionRecorder] Rebound to LiveInferenceClient in new scene.");
        }
        else
        {
            Debug.LogWarning("[EegSessionRecorder] RebindToScene: LiveInferenceClient not found.");
        }
    }

    public void SetGameContext(int score, int hearts, int hits, int misses, int falseAlarms, string difficultyZone)
    {
        _ctxScore = score; // Save current score for the next EEG row.
        _ctxHearts = hearts; // Save current hearts for the next EEG row.
        _ctxHits = hits; // Save current hits for the next EEG row.
        _ctxMisses = misses; // Save current misses for the next EEG row.
        _ctxFalseAlarms = falseAlarms; // Save current false alarms for the next EEG row.
        _ctxZone = difficultyZone ?? "medium"; // Use medium if the zone is missing.
    }

    public void StopRecording() => _recording = false; // Stop adding new rows.

    public void ResetRecords()
    {
        _records.Clear(); // Clear old session rows.
        _recording = true; // Allow recording for a new session.
    }

    private void OnFocusResult(FocusResult result)
    {
        if (!_recording) return; // Do not save after game-over.
        if (result == null) return; // Ignore empty results.

        bool hasBandData =
            result.theta != null && result.theta.Length == 14 &&
            result.alpha != null && result.alpha.Length == 14 &&
            result.beta != null && result.beta.Length == 14 &&
            result.tbr != null && result.tbr.Length == 14 &&
            result.bar != null && result.bar.Length == 14; // Require all 14-channel EEG arrays.

        if (!hasBandData)
        {
            Debug.LogWarning("[EegSessionRecorder] Skipped EEG window because band-power arrays were missing.");
            return;
        }

        bool validPrediction = result.IsValid; // True only when Python accepted the prediction.

        var rec = new EegWindowRecord
        {
            windowIndex = _records.Count, // Next row index.
            gameTimeSeconds = Time.time, // Save current Unity time.
            utcTimestamp = DateTime.UtcNow.ToString("o"), // Save exact UTC time.

            zone = validPrediction ? result.zone : "rejected", // Mark rejected predictions clearly.
            score = validPrediction ? result.score : 0f, // Do not use rejected scores.
            generalScore = validPrediction ? result.generalScore : 0f, // Do not use rejected general scores.
            confidence = validPrediction ? result.confidence : 0f, // Confidence is zero when rejected.
            isFocused = validPrediction && result.isFocused, // Focus is true only for valid focused results.

            difficultyZone = _ctxZone, // Attach current difficulty context.
            scoreAtWindow = _ctxScore, // Attach current score context.
            heartsAtWindow = _ctxHearts, // Attach current hearts context.
            hitsAtWindow = _ctxHits, // Attach current hits context.
            missesAtWindow = _ctxMisses, // Attach current misses context.
            falseAlarmsAtWindow = _ctxFalseAlarms // Attach current false alarms context.
        };

        Array.Copy(result.theta, rec.theta, 14); // Copy theta values into the record.
        Array.Copy(result.alpha, rec.alpha, 14); // Copy alpha values into the record.
        Array.Copy(result.beta, rec.beta, 14); // Copy beta values into the record.
        Array.Copy(result.tbr, rec.tbr, 14); // Copy TBR values into the record.
        Array.Copy(result.bar, rec.bar, 14); // Copy BAR values into the record.

        _records.Add(rec); // Save the completed EEG row.

        if (validPrediction)
            Debug.Log($"[EegSessionRecorder] Window {rec.windowIndex} recorded | zone={rec.zone} score={rec.score:F3}");
        else
            Debug.LogWarning($"[EegSessionRecorder] Window {rec.windowIndex} recorded with EEG values only; prediction rejected: {result.error}");
    }
}
