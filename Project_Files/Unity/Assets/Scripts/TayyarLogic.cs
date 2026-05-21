using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using ArabicSupport;
using System;
using System.Collections.Generic;
using System.Linq;

public class TayyarLogic : MonoBehaviour
{
    public static TayyarLogic Instance; // Allows other scripts to report Tayyar events.

    [Header("Score (Localized)")]
    [SerializeField] private LocalizeStringEvent scoreTextLocalized;
    private int score = 0; // Current Tayyar score.

    [Header("Game Over UI (Localized)")]
    public GameObject gameOverPanel; // Panel shown after the session ends.

    [SerializeField] private LocalizeStringEvent playerNameLocalized;
    [SerializeField] private LocalizeStringEvent finalScoreLocalized;
    [SerializeField] private LocalizeStringEvent hitCountLocalized;
    [SerializeField] private LocalizeStringEvent missCountLocalized;
    [SerializeField] private LocalizeStringEvent attentionAvgLocalized;

    [SerializeField] private AttentionSessionTracker attentionTracker; // Tracks average focus during the session.

    private string _currentDifficultyZone = "easy"; // Current Tayyar difficulty level.

    public int HitCount { get; private set; }
    public int MissCount { get; private set; }
    public int FalseAlarmCount { get; private set; }
    public int CorrectRejectionCount { get; private set; }

    private bool sessionSaved = false; // Prevents saving game over data twice.
    private string sessionId; // Unique ID for this Tayyar session.

    private int eventNumber = 0; // Counts recorded gameplay events.

    private int eegWindowIndexOffset = int.MinValue; // Aligns EEG window numbers to this session.

    private readonly List<TayyarEventRecord> eventRecords = new List<TayyarEventRecord>();
    private readonly List<float> validTargetReactionTimesMs = new List<float>();

    public bool IsSessionEnded => sessionSaved;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }
        Instance = this;
        Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // NFR3 - Efficiency: record end time for GameSelection->InstructionsGame1 transition
        float startTime = PlayerPrefs.GetFloat("SceneLoadStart", -1f);
        string label    = PlayerPrefs.GetString("SceneLoadLabel", "");
        if (startTime >= 0f && label == "GameSelection->InstructionsGame1")
        {
            float loadTime = Time.realtimeSinceStartup - startTime;
            NFRLoadTimeLogger.LogTransition(label, loadTime);
            PlayerPrefs.SetFloat("SceneLoadStart", -1f);
        }

        Time.timeScale = 1f;

        InitializeEegForNewTayyarSession(resetRecords: true); // Start fresh EEG tracking for Tayyar.
        StartCoroutine(RebindEegAfterSceneReady()); // Rebind again after one frame for scene safety.

        sessionId = Guid.NewGuid().ToString(); // Create unique session ID.

        score = 0;
        HitCount = 0;
        MissCount = 0;
        FalseAlarmCount = 0;
        CorrectRejectionCount = 0;
        eventNumber = 0;
        eegWindowIndexOffset = int.MinValue;
        eventRecords.Clear();
        validTargetReactionTimesMs.Clear();

        UpdateScoreUI();

        HitBasedDifficultyManager.Instance?.ResetDifficulty(); // Start difficulty from easy.

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        SyncEegGameContext(); // Send current gameplay values to EEG recorder.
    }

    private void InitializeEegForNewTayyarSession(bool resetRecords)
    {
        var liveClient = FindFirstObjectByType<LiveInferenceClient>();

        if (liveClient != null)
        {
            liveClient.NotifyNewSession(); // Make inference client send new windows for this session.
            Debug.Log("[TayyarLogic] LiveInferenceClient notified for new Tayyar session.");
        }
        else
        {
            Debug.LogWarning("[TayyarLogic] LiveInferenceClient not found during Tayyar Start.");
        }

        EegBandPowerTracker.Instance?.ResetSession(); // Clear EEG bar averages.
        EegSessionRecorder.Instance?.RebindToScene(); // Attach recorder to the active scene.

        if (resetRecords)
            EegSessionRecorder.Instance?.ResetRecords(); // Clear records after exporting.

        SyncEegGameContext(); // Send current gameplay values to EEG recorder.
    }

    private System.Collections.IEnumerator RebindEegAfterSceneReady()
    {
        yield return null;
        InitializeEegForNewTayyarSession(resetRecords: false);
    }

    public void SetDifficulty(string level)
    {
        if (level == "hard")
            _currentDifficultyZone = "hard";
        else if (level == "medium")
            _currentDifficultyZone = "medium";
        else
            _currentDifficultyZone = "easy";

        SyncEegGameContext(); // Send current gameplay values to EEG recorder.

        Debug.Log("[TayyarLogic] EEG context updated only: " + _currentDifficultyZone);
    }

    public void RegisterTargetHit(float targetReactionTimeMs = -1f)
    {
        if (sessionSaved) return;

        HitCount++;
        HitBasedDifficultyManager.Instance?.OnCorrectHit(HitCount); // Correct hit can increase difficulty.
        score += 10;

        if (targetReactionTimeMs >= 0f)
            validTargetReactionTimesMs.Add(targetReactionTimeMs);

        UpdateScoreUI();
        SyncEegGameContext(); // Send current gameplay values to EEG recorder.

        AddEventRecord(
            objectType: "Ring",
            isTarget: true,
            playerAction: true,
            correct: true,
            responseType: "Hit",
            targetReactionTimeMs: targetReactionTimeMs,
            falseAlarmReactionTimeMs: -1f
        );
    }

    public void RegisterMiss()
    {
        if (sessionSaved) return;

        MissCount++;
        SyncEegGameContext(); // Send current gameplay values to EEG recorder.

        AddEventRecord(
            objectType: "Ring",
            isTarget: true,
            playerAction: false,
            correct: false,
            responseType: "Miss",
            targetReactionTimeMs: -1f,
            falseAlarmReactionTimeMs: -1f
        );
    }

    public void RegisterFalseAlarm(float falseAlarmReactionTimeMs = -1f)
    {
        if (sessionSaved) return;

        FalseAlarmCount++;
        HitBasedDifficultyManager.Instance?.OnFalseAlarm(); // False alarm can lower difficulty. 
        SyncEegGameContext(); // Send current gameplay values to EEG recorder.

        AddEventRecord(
            objectType: "Bomb",
            isTarget: false,
            playerAction: true,
            correct: false,
            responseType: "FalseAlarm",
            targetReactionTimeMs: -1f,
            falseAlarmReactionTimeMs: falseAlarmReactionTimeMs
        );
    }

    public void RegisterCorrectRejection()
    {
        if (sessionSaved) return;

        CorrectRejectionCount++;
        SyncEegGameContext(); // Send current gameplay values to EEG recorder.

        AddEventRecord(
            objectType: "Bomb",
            isTarget: false,
            playerAction: false,
            correct: true,
            responseType: "CorrectRejection",
            targetReactionTimeMs: -1f,
            falseAlarmReactionTimeMs: -1f
        );
    }

    private void AddEventRecord(
        string objectType,
        bool isTarget,
        bool playerAction,
        bool correct,
        string responseType,
        float targetReactionTimeMs,
        float falseAlarmReactionTimeMs)
    {
        var record = new TayyarEventRecord
        {
            eventNumber = ++eventNumber,
            sessionId = sessionId,
            playerName = LoadPlayerNameFromJSON(),
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            difficultyZone = _currentDifficultyZone,

            objectType = objectType,
            isTarget = isTarget,
            playerAction = playerAction,
            correct = correct,
            responseType = responseType,
            targetReactionTimeMs = targetReactionTimeMs,
            falseAlarmReactionTimeMs = falseAlarmReactionTimeMs,
            gameTimeSeconds = Time.time,

            scoreAfterEvent = score,
            hitsAfterEvent = HitCount,
            missesAfterEvent = MissCount,
            falseAlarmsAfterEvent = FalseAlarmCount,
            correctRejectionsAfterEvent = CorrectRejectionCount
        };

        SnapshotEegBeforeEvent(record); // Store EEG state before event.
        SnapshotEegIntoRecord(record); // Store current EEG values.
        SnapshotEegAfterEvent(record); // Store EEG state after event.

        eventRecords.Add(record); // Add event to final CSV list.
    }

    private int GetSessionEegWindowIndex(EegWindowRecord w)
    {
        if (w == null) return -1;

        if (eegWindowIndexOffset == int.MinValue)
        {
            eegWindowIndexOffset = w.windowIndex > 0 ? w.windowIndex + 1 : 0;
            Debug.Log($"[TayyarLogic] EEG session window offset = {eegWindowIndexOffset}");
        }

        return w.windowIndex - eegWindowIndexOffset;
    }

    private void SnapshotEegIntoRecord(TayyarEventRecord record)
    {
        if (record == null) return;

        var eegRecords = EegSessionRecorder.Instance?.Records;
        if (eegRecords == null || eegRecords.Count == 0) return;

        var w = eegRecords[eegRecords.Count - 1];
        int sessionWindowIndex = GetSessionEegWindowIndex(w);

        record.eegWindowIndex = sessionWindowIndex; // Link event with EEG window.

        if (sessionWindowIndex < 0) return;

        record.eegZone = w.zone;
        record.eegScore = w.score;
        record.eegGeneralScore = w.generalScore;
        record.eegConfidence = w.confidence;
        record.eegIsFocused = w.isFocused;

        record.thetaAvg = Average(w.theta);
        record.alphaAvg = Average(w.alpha);
        record.betaAvg = Average(w.beta);
        record.tbrMean = Average(w.tbr);
        record.barMean = Average(w.bar);
        record.tbrMedian = Median(w.tbr);
        record.tbrStd = StdDev(w.tbr);

        record.tbrAF3 = SafeGet(w.tbr, 0);
        record.tbrF7  = SafeGet(w.tbr, 1);
        record.tbrF3  = SafeGet(w.tbr, 2);
        record.tbrFC5 = SafeGet(w.tbr, 3);
        record.tbrT7  = SafeGet(w.tbr, 4);
        record.tbrP7  = SafeGet(w.tbr, 5);
        record.tbrO1  = SafeGet(w.tbr, 6);
        record.tbrO2  = SafeGet(w.tbr, 7);
        record.tbrP8  = SafeGet(w.tbr, 8);
        record.tbrT8  = SafeGet(w.tbr, 9);
        record.tbrFC6 = SafeGet(w.tbr, 10);
        record.tbrF4  = SafeGet(w.tbr, 11);
        record.tbrF8  = SafeGet(w.tbr, 12);
        record.tbrAF4 = SafeGet(w.tbr, 13);
    }

    private void SnapshotEegBeforeEvent(TayyarEventRecord record)
    {
        if (record == null) return;

        var eegRecords = EegSessionRecorder.Instance?.Records;
        if (eegRecords == null || eegRecords.Count == 0) return;

        var w = eegRecords[eegRecords.Count - 1];
        int sessionWindowIndex = GetSessionEegWindowIndex(w);

        record.eegBeforeWindowIndex = sessionWindowIndex;

        if (sessionWindowIndex < 0) return;

        record.eegBeforeZone = w.zone;
        record.eegBeforeScore = w.score;
        record.eegBeforeConfidence = w.confidence;
        record.eegBeforeIsFocused = w.isFocused;

        record.thetaBeforeAvg = Average(w.theta);
        record.alphaBeforeAvg = Average(w.alpha);
        record.betaBeforeAvg = Average(w.beta);
        record.tbrBeforeMean = Average(w.tbr);
        record.barBeforeMean = Average(w.bar);
    }

    private void SnapshotEegAfterEvent(TayyarEventRecord record)
    {
        if (record == null) return;

        var eegRecords = EegSessionRecorder.Instance?.Records;
        if (eegRecords == null || eegRecords.Count == 0) return;

        var w = eegRecords[eegRecords.Count - 1];
        int sessionWindowIndex = GetSessionEegWindowIndex(w);

        record.eegAfterWindowIndex = sessionWindowIndex;

        if (sessionWindowIndex < 0) return;

        record.eegAfterZone = w.zone;
        record.eegAfterScore = w.score;
        record.eegAfterConfidence = w.confidence;
        record.eegAfterIsFocused = w.isFocused;

        record.thetaAfterAvg = Average(w.theta);
        record.alphaAfterAvg = Average(w.alpha);
        record.betaAfterAvg = Average(w.beta);
        record.tbrAfterMean = Average(w.tbr);
        record.barAfterMean = Average(w.bar);
    }

    private static float Average(float[] arr)
    {
        if (arr == null || arr.Length == 0) return 0f;
        float sum = 0f;
        for (int i = 0; i < arr.Length; i++) sum += arr[i];
        return sum / arr.Length;
    }

    private static float Median(float[] arr)
    {
        if (arr == null || arr.Length == 0) return 0f;
        float[] sorted = (float[])arr.Clone();
        Array.Sort(sorted);
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2f
            : sorted[mid];
    }

    private static float StdDev(float[] arr)
    {
        if (arr == null || arr.Length < 2) return 0f;
        float mean = Average(arr);
        float sumSq = 0f;
        for (int i = 0; i < arr.Length; i++)
        {
            float diff = arr[i] - mean;
            sumSq += diff * diff;
        }
        return Mathf.Sqrt(sumSq / arr.Length);
    }

    private static float SafeGet(float[] arr, int idx)
    {
        if (arr == null || idx < 0 || idx >= arr.Length) return 0f;
        return arr[idx];
    }

    private float GetAverageTargetReactionTime()
    {
        if (validTargetReactionTimesMs.Count == 0)
            return 0f;

        return Mathf.Round((float)validTargetReactionTimesMs.Average() * 100f) / 100f;
    }

    private float GetTargetReactionTimeVariability()
    {
        int n = validTargetReactionTimesMs.Count;
        if (n <= 1) return 0f;

        float mean = (float)validTargetReactionTimesMs.Average();
        float sumSquaredDiff = 0f;

        for (int i = 0; i < n; i++)
            sumSquaredDiff += Mathf.Pow(validTargetReactionTimesMs[i] - mean, 2f);

        float variance = sumSquaredDiff / (n - 1);
        float std = Mathf.Sqrt(variance);

        return Mathf.Round(std * 100f) / 100f;
    }

    private void SyncEegGameContext()
    {
        EegSessionRecorder.Instance?.SetGameContext(
            score,
            0,
            HitCount,
            MissCount,
            FalseAlarmCount,
            difficultyZone: _currentDifficultyZone
        );
    }

    private void UpdateScoreUI()
    {
        if (scoreTextLocalized != null)
        {
            scoreTextLocalized.StringReference.Arguments = new object[] { score };
            scoreTextLocalized.RefreshString();
        }
    }

    public void GameOver()
    {
        if (sessionSaved) return;
        sessionSaved = true;

        Spawner.Instance?.StopSpawning(); // Stop new objects after game over.

        FreezeGameWorldBehindGameOver(); // Freeze movement behind the game-over panel.

        string name = LoadPlayerNameFromJSON();

        float avgAttention = AttentionSessionTracker.CurrentAverage;
        bool hasAttentionData = AttentionSessionTracker.CurrentHasData;
        float valueToSave = hasAttentionData ? avgAttention : 0f;

        if (playerNameLocalized != null)
        {
            playerNameLocalized.StringReference.Arguments = new object[] { name };
            playerNameLocalized.RefreshString();
        }

        if (finalScoreLocalized != null)
        {
            finalScoreLocalized.StringReference.Arguments = new object[] { score };
            finalScoreLocalized.RefreshString();
        }

        if (hitCountLocalized != null)
        {
            hitCountLocalized.StringReference.Arguments = new object[] { HitCount };
            hitCountLocalized.RefreshString();
        }

        if (missCountLocalized != null)
        {
            missCountLocalized.StringReference.Arguments = new object[] { MissCount };
            missCountLocalized.RefreshString();
        }

        float avgRt = GetAverageTargetReactionTime();
        float rtv = GetTargetReactionTimeVariability();

        SessionLogger.AppendSession(
            gameMode: "Tayyar",
            score: score,
            correctResponse: HitCount,
            omission: MissCount,
            commission: FalseAlarmCount,
            tetaBetaRatio: valueToSave,
            betaAlghaRatio: 0f,
            averageReactionTimeMs: avgRt,
            reactionTimeVariabilityMs: rtv
        );

        string performancePath = TayyarPerformanceExcelExporter.Export(
            name,
            sessionId,
            score,
            HitCount,
            MissCount,
            FalseAlarmCount,
            CorrectRejectionCount,
            valueToSave,
            avgRt,
            rtv,
            eventRecords
        );

        if (performancePath != null)
            Debug.Log("[Tayyar] Performance CSV: " + performancePath);

        EegSessionRecorder.Instance?.StopRecording(); // Stop collecting EEG records for this session.

        EegSessionRecorder.Instance?.ResetRecords(); // Clear records after exporting.

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        StartCoroutine(SetAttentionTextNextFrame(avgAttention, hasAttentionData));
    }

    private void FreezeGameWorldBehindGameOver()
    {
        Time.timeScale = 0f; // Freeze gameplay.

        foreach (var indicator in FindObjectsByType<FocusIndicatorController>(FindObjectsSortMode.None))
            indicator.FreezeVisualsForGameOver();

        foreach (var bandTracker in FindObjectsByType<EegBandPowerTracker>(FindObjectsSortMode.None))
            bandTracker.FreezeVisualsForGameOver();
    }

    private System.Collections.IEnumerator SetAttentionTextNextFrame(float avgAttention, bool hasAttentionData)
    {
        yield return new WaitForSecondsRealtime(0.05f);

        if (attentionAvgLocalized == null)
            yield break;

        string shownValue = hasAttentionData
            ? Mathf.RoundToInt(avgAttention * 100f) + "%"
            : "N/A";

        attentionAvgLocalized.enabled = false;

        var tmp = attentionAvgLocalized.GetComponent<TMP_Text>();
        if (tmp != null)
            tmp.text = "ATTENTION LEVEL: " + shownValue;
    }

    public void GoHome()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameSelection"); // Return to game selection.
    }

    private string LoadPlayerNameFromJSON()
    {
        var repo = new RakkizSaveRepository("rakkiz_save.json");

        if (!repo.HasPlayerName())
            return "PLAYER";

        return repo.GetPlayerNameOrDefault("PLAYER").Trim();
    }
}