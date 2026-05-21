using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using ArabicSupport;

public class RassdLogic : MonoBehaviour
{
    public static RassdLogic Instance; // Allows other scripts to access Rassd logic.

    [Header("Stimulus")]
    public GameObject coffeePrefab; // Target/non-target coffee object.
    public GameObject scorpionPrefab; // Distractor object used in hard mode.

    public Transform topPos;
    public Transform bottomPos;
    public Transform crossSign;

    private float targetProbability = 0.5f; // Chance of spawning a target trial.
    private bool useScorpionDistractor = false; // Enables scorpion distractors in hard mode.

    [Header("UI (Localized)")]
    [SerializeField] private LocalizeStringEvent scoreTextLocalized;

    [Header("Game Over UI (Localized)")]
    public GameObject gameOverPanel;
    [SerializeField] private LocalizeStringEvent playerNameLocalized;
    [SerializeField] private LocalizeStringEvent finalScoreLocalized;
    [SerializeField] private LocalizeStringEvent hitsLocalized;
    [SerializeField] private LocalizeStringEvent missLocalized;

    [Header("Stimulus Count")]
    [Tooltip("The game ends after this number of stimuli/trials.")]
    public int totalStimuli = 300; // Total trials before game over.

    public TextMeshProUGUI timerText;

    [Header("Fixed Timing")]
    [Tooltip("0.4 seconds = 400 ms trial duration.")]
    public float trialDuration = 0.5f; // Full trial duration.

    [Tooltip("0.1 seconds = 100 ms stimulus duration.")]
    public float stimDuration = 0.1f; // Time the stimulus stays visible.

    [Header("Countdown (Code Only)")]
    public GameObject countdownPanel;
    public TextMeshProUGUI countdownText;

    private int score = 0; // Current game score.
    private int hitCount = 0;
    private int missCount = 0;
    private int falseAlarmCount = 0;
    private int correctRejectionCount = 0;

    private bool sessionSaved = false; // Prevents saving the same session twice.
    private GameObject currentStimulus;

    private bool isTarget = false;
    private bool responded = false;

    private bool currentIsCoffee = false;
    private bool currentIsTop = false;

    private bool inputLocked = true; // Blocks input during countdown.
    private bool countdownStarted = false;

    private string _currentDifficultyZone = "easy"; // Current Rassd difficulty level.

    private float stimulusShownTime;
    private int trialNumber = 0;
    private int eegWindowIndexOffset = int.MinValue;

    private string sessionId;

    private const float MinValidReactionTimeMs = 50f; // Too-fast responses are marked invalid.

    private readonly List<float> validTargetReactionTimesMs = new List<float>();
    private readonly List<RassdStimulusRecord> stimulusRecords = new List<RassdStimulusRecord>();
    private RassdStimulusRecord currentStimulusRecord;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsSessionEnded => sessionSaved;

    private void Start()
    {
        float startTime = PlayerPrefs.GetFloat("SceneLoadStart", -1f);
        string label    = PlayerPrefs.GetString("SceneLoadLabel", "");
        if (startTime >= 0f && label == "Instructions2->RassdGame")
        {
            float loadTime = Time.realtimeSinceStartup - startTime;
            NFRLoadTimeLogger.LogTransition(label, loadTime);
            PlayerPrefs.SetFloat("SceneLoadStart", -1f);
        }

        Time.timeScale = 1f;
        LiveInferenceClient.Instance?.NotifyNewSession(); // Tell inference that a new game session started.

        EegBandPowerTracker.Instance?.ResetSession(); // Clear previous EEG bar averages.
        EegSessionRecorder.Instance?.RebindToScene(); // Attach recorder to the current scene.
        EegSessionRecorder.Instance?.ResetRecords(); // Start a clean EEG record list.

        sessionId = Guid.NewGuid().ToString();
        eegWindowIndexOffset = int.MinValue;

        trialDuration = 0.5f;
        stimDuration = 0.1f;

        UpdateScore();
        UpdateTimerUI();

        HitBasedDifficultyManager.Instance?.ResetDifficulty(); // Start difficulty from easy.

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (!countdownStarted)
            StartCoroutine(StartCountdown()); // Begin countdown before trials.
    }

    public void SetDifficulty(string level)
    {
        _currentDifficultyZone = level;

        trialDuration = 0.5f;
        stimDuration = 0.1f;

        switch (level)
        {
            case "hard":
                useScorpionDistractor = true;
                break;

            case "easy":
            default:
                useScorpionDistractor = false;
                _currentDifficultyZone = "easy";
                break;
        }

        DifficultyIndicatorUI.Instance?.OnDifficultyChanged(_currentDifficultyZone); // Show difficulty feedback.

        Debug.Log("[RassdLogic] Difficulty applied: " + _currentDifficultyZone +
              " | fixed trialDuration = " + trialDuration +
              " | fixed stimDuration = " + stimDuration +
              " | scorpion = " + useScorpionDistractor);
    }

    private string GetCurrentDifficultyZone()
    {
        return _currentDifficultyZone;
    }

    private IEnumerator StartCountdown()
    {
        if (countdownStarted)
            yield break;

        countdownStarted = true;

        if (countdownPanel != null)
            countdownPanel.SetActive(true);

        if (countdownText != null)
            countdownText.text = "";

        yield return null;

        if (countdownText != null)
            countdownText.text = GetCountdownNumberText(3);

        yield return new WaitForSeconds(1f);

        if (countdownText != null)
            countdownText.text = GetCountdownNumberText(2);

        yield return new WaitForSeconds(1f);

        if (countdownText != null)
            countdownText.text = GetCountdownNumberText(1);

        yield return new WaitForSeconds(1f);

        string goText = IsArabicLocale() ? "ابدأ!" : "GO!";

        if (countdownText != null)
        {
            countdownText.text = IsArabicLocale()
                ? ArabicFixer.Fix(goText, false, false)
                : goText;
        }

        yield return new WaitForSeconds(0.5f);

        if (countdownPanel != null)
            countdownPanel.SetActive(false);

        inputLocked = false;

        StartCoroutine(RunTrials());
    }

    private string GetCountdownNumberText(int n)
    {
        if (!IsArabicLocale())
            return n.ToString();

        return ToArabicDigits(n.ToString());
    }

    private bool IsArabicLocale()
    {
        var locale = LocalizationSettings.SelectedLocale;
        return locale != null && locale.Identifier.Code.StartsWith("ar");
    }

    private IEnumerator RunTrials()
    {
        while (trialNumber < totalStimuli && !sessionSaved)
        {
            responded = false;
            SpawnStimulus(); // Create the next trial stimulus.

            float elapsed = 0f;

            while (elapsed < trialDuration && !sessionSaved)
            {
                if (elapsed >= stimDuration && currentStimulus != null && currentStimulus.activeSelf)
                    currentStimulus.SetActive(false);

                float delta = Time.deltaTime;
                elapsed += delta;

                UpdateTimerUI();

                yield return null;
            }

            if (currentStimulus != null)
                Destroy(currentStimulus);

            FinalizeTrialIfNoResponse(); // Count miss or correct rejection if no key was pressed.
        }

        TriggerGameOver(); // End and save the session.
    }

    private void SpawnStimulus()
    {
        currentIsCoffee = false;
        currentIsTop = false;
        isTarget = false;

        GameObject prefabToSpawn;
        Vector3 pos;

        string stimulusType;
        string stimulusPosition;

        bool spawnTarget = UnityEngine.Random.value < targetProbability;

        if (spawnTarget)
        {
            prefabToSpawn = coffeePrefab;
            pos = topPos.position;

            currentIsCoffee = true;
            currentIsTop = true;
            isTarget = true;

            stimulusType = "Coffee";
            stimulusPosition = "Top";
        }
        else
        {
            bool spawnScorpion = useScorpionDistractor &&
                                 scorpionPrefab != null &&
                                 UnityEngine.Random.value < 0.5f;

            if (spawnScorpion)
            {
                prefabToSpawn = scorpionPrefab;

                bool scorpionAtTop = UnityEngine.Random.value < 0.5f;
                pos = scorpionAtTop ? topPos.position : bottomPos.position;

                currentIsCoffee = false;
                currentIsTop    = scorpionAtTop;
                isTarget        = false;

                stimulusType     = "Scorpion";
                stimulusPosition = scorpionAtTop ? "Top" : "Bottom";
            }
            else
            {
                prefabToSpawn = coffeePrefab;
                pos = bottomPos.position;

                currentIsCoffee = true;
                currentIsTop = false;
                isTarget = false;

                stimulusType = "Coffee";
                stimulusPosition = "Bottom";
            }
        }

        currentStimulus = Instantiate(prefabToSpawn, pos, Quaternion.identity);
        stimulusShownTime = Time.time;
        trialNumber++;

        currentStimulusRecord = new RassdStimulusRecord
        {
            trialNumber = trialNumber,
            sessionId = sessionId,
            playerName = LoadPlayerNameFromJSON(),
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            difficultyZone = GetCurrentDifficultyZone(),

            stimulusType = stimulusType,
            position = stimulusPosition,
            isTarget = isTarget,

            pressed = false,
            correct = false,
            responseType = "Pending",

            targetReactionTimeMs = -1f,

            trialDurationSec = trialDuration,
            stimDurationSec = stimDuration,

            scoreAfterTrial = score,
            hitsAfterTrial = hitCount,
            missesAfterTrial = missCount,
            falseAlarmsAfterTrial = falseAlarmCount,
            correctRejectionsAfterTrial = correctRejectionCount
        };
    }

    private void Update()
    {
        if (sessionSaved || responded || inputLocked)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
            RegisterResponse(); // Handle Space key response.
    }

    private void RegisterResponse()
    {
        if (responded)
            return;

        responded = true;

        bool correctTargetResponse = currentIsCoffee && currentIsTop;

        if (correctTargetResponse)
        {
            float rtMs = GetCurrentReactionTimeMs();

            if (rtMs < MinValidReactionTimeMs)
            {
                UpdateCurrentStimulusRecord(
                    pressed: true,
                    correct: false,
                    responseType: "InvalidRT",
                    targetReactionTimeMs: rtMs
                );
            }
            else
            {
                validTargetReactionTimesMs.Add(rtMs);

                hitCount++;
                HitBasedDifficultyManager.Instance?.OnCorrectHit(hitCount);
                score += 10;
                UpdateScore();

                UpdateCurrentStimulusRecord(
                    pressed: true,
                    correct: true,
                    responseType: "Hit",
                    targetReactionTimeMs: rtMs
                );
            }
        }
        else
        {
            float rtMs = GetCurrentReactionTimeMs();

            falseAlarmCount++;
            HitBasedDifficultyManager.Instance?.OnFalseAlarm();

            UpdateCurrentStimulusRecord(
                pressed: true,
                correct: false,
                responseType: "FalseAlarm",
                targetReactionTimeMs: -1f,
                falseAlarmReactionTimeMs: rtMs
            );
        }

        SyncEegGameContext(); // Send current gameplay stats to EEG recorder.
    }

    private void FinalizeTrialIfNoResponse()
    {
        if (currentStimulusRecord == null)
            return;

        if (!responded && isTarget)
        {
            missCount++;

            UpdateCurrentStimulusRecord(
                pressed: false,
                correct: false,
                responseType: "Miss",
                targetReactionTimeMs: -1f
            );
        }
        else if (!responded && !isTarget)
        {
            correctRejectionCount++;

            UpdateCurrentStimulusRecord(
                pressed: false,
                correct: true,
                responseType: "CorrectRejection",
                targetReactionTimeMs: -1f
            );
        }

        SyncEegGameContext(); // Send current gameplay stats to EEG recorder.

        SnapshotEegIntoRecord(currentStimulusRecord); // Attach latest EEG values to this trial.

        stimulusRecords.Add(currentStimulusRecord);
        currentStimulusRecord = null;
    }

    private int GetSessionEegWindowIndex(EegWindowRecord w)
    {
        if (w == null) return -1;

        if (eegWindowIndexOffset == int.MinValue)
        {
            // If EEG already produced windows before the first gameplay record,
            // make that first record a pre-warm row (-1). Otherwise keep normal 0-based indexing.
            eegWindowIndexOffset = w.windowIndex > 0 ? w.windowIndex + 1 : 0;
            Debug.Log($"[RassdLogic] EEG session window offset = {eegWindowIndexOffset}");
        }

        return w.windowIndex - eegWindowIndexOffset;
    }

    private void SnapshotEegIntoRecord(RassdStimulusRecord record)
    {
        if (record == null) return;

        var eegRecords = EegSessionRecorder.Instance?.Records;
        if (eegRecords == null || eegRecords.Count == 0) return;

        var w = eegRecords[eegRecords.Count - 1];
        int sessionWindowIndex = GetSessionEegWindowIndex(w);

        record.eegWindowIndex = sessionWindowIndex;
        if (sessionWindowIndex < 0) return;

        record.eegZone         = w.zone;
        record.eegScore        = w.score;
        record.eegGeneralScore = w.generalScore;
        record.eegConfidence   = w.confidence;
        record.eegIsFocused    = w.isFocused;

        record.thetaAvg = Average(w.theta);
        record.alphaAvg = Average(w.alpha);
        record.betaAvg  = Average(w.beta);
        record.tbrMean  = Average(w.tbr);
        record.barMean  = Average(w.bar);

        record.tbrMedian = Median(w.tbr);
        record.tbrStd    = StdDev(w.tbr);

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

    private static float Average(float[] arr)
    {
        if (arr == null || arr.Length == 0) return 0f;
        float s = 0f;
        for (int i = 0; i < arr.Length; i++) s += arr[i];
        return s / arr.Length;
    }

    private static float Median(float[] arr)
    {
        if (arr == null || arr.Length == 0) return 0f;
        float[] sorted = (float[])arr.Clone();
        System.Array.Sort(sorted);
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
        for (int i = 0; i < arr.Length; i++) { float d = arr[i] - mean; sumSq += d * d; }
        return Mathf.Sqrt(sumSq / arr.Length);
    }

    private static float SafeGet(float[] arr, int idx)
    {
        if (arr == null || idx < 0 || idx >= arr.Length) return 0f;
        return arr[idx];
    }

    private void UpdateCurrentStimulusRecord(
        bool pressed,
        bool correct,
        string responseType,
        float targetReactionTimeMs,
        float falseAlarmReactionTimeMs = -1f)
    {
        if (currentStimulusRecord == null)
            return;

        currentStimulusRecord.pressed = pressed;
        currentStimulusRecord.correct = correct;
        currentStimulusRecord.responseType = responseType;
        currentStimulusRecord.targetReactionTimeMs = targetReactionTimeMs;
        currentStimulusRecord.falseAlarmReactionTimeMs = falseAlarmReactionTimeMs;

        currentStimulusRecord.scoreAfterTrial = score;
        currentStimulusRecord.hitsAfterTrial = hitCount;
        currentStimulusRecord.missesAfterTrial = missCount;
        currentStimulusRecord.falseAlarmsAfterTrial = falseAlarmCount;
        currentStimulusRecord.correctRejectionsAfterTrial = correctRejectionCount;
    }

    private float GetCurrentReactionTimeMs()
    {
        float reactionTimeMs = (Time.time - stimulusShownTime) * 1000f;
        return Mathf.Round(reactionTimeMs * 100f) / 100f;
    }

    private float GetAverageHitReactionTime()
    {
        if (validTargetReactionTimesMs.Count == 0)
            return 0f;

        float mean = (float)validTargetReactionTimesMs.Average();
        return Mathf.Round(mean * 100f) / 100f;
    }

    private float GetTargetReactionTimeVariability()
    {
        int n = validTargetReactionTimesMs.Count;

        if (n <= 1)
            return 0f;

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
            hitCount,
            missCount,
            falseAlarmCount,
            difficultyZone: GetCurrentDifficultyZone()
        );
    }

    private void UpdateScore()
    {
        if (scoreTextLocalized != null)
        {
            scoreTextLocalized.StringReference.Arguments = new object[] { score };
            scoreTextLocalized.RefreshString();
        }
    }

        private void SetGameOverText(
        LocalizeStringEvent localizeEvent,
        object value,
        string labelEn,
        string labelAr)
    {
        if (localizeEvent == null)
            return;

        localizeEvent.StringReference.Arguments = new object[] { value };
        localizeEvent.RefreshString();

        TextMeshProUGUI tmp = localizeEvent.GetComponent<TextMeshProUGUI>();

        if (tmp == null)
            return;

        string valueText = value == null ? "" : value.ToString();
        bool isAr = IsArabicLocale();

        if (isAr)
            valueText = ToArabicDigits(valueText);

        string label = isAr ? labelAr : labelEn;
        string fullText = label + ": " + valueText;

        tmp.text = isAr
            ? ArabicFixer.Fix(fullText, false, false)
            : fullText;
    }

    private void UpdateTimerUI()
    {
        if (timerText == null)
            return;

        string progressText = $"{trialNumber}/{totalStimuli}";

        timerText.text = IsArabicLocale()
            ? ArabicFixer.Fix(ToArabicDigits(progressText), false, false)
            : progressText;
    }

    private string ToArabicDigits(string text)
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

    private void TriggerGameOver()
    {
        if (sessionSaved)
            return;

        sessionSaved = true;
        inputLocked = true;
        FreezeGameWorldBehindGameOver();

        if (currentStimulus != null)
            Destroy(currentStimulus);

        if (currentStimulusRecord != null)
            FinalizeTrialIfNoResponse(); // Count miss or correct rejection if no key was pressed.

        string playerName = LoadPlayerNameFromJSON();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // Game Over Panel — أربع قيم فقط
        SetGameOverText(playerNameLocalized, playerName, "PLAYER",            "اللاعب");
        SetGameOverText(finalScoreLocalized, score,      "SCORE",             "النقاط");
        SetGameOverText(hitsLocalized,       hitCount,   "CORRECT RESPONSES", "الاستجابات الصحيحة");
        SetGameOverText(missLocalized,       missCount,  "OMISSIONS",         "الإغفال");
        float avgTargetRt = GetAverageHitReactionTime();
        float targetRtv = GetTargetReactionTimeVariability();

        SessionLogger.AppendSession(
            gameMode: "Rassd",
            score: score,
            correctResponse: hitCount,
            omission: missCount,
            commission: falseAlarmCount,
            tetaBetaRatio: EegBandPowerTracker.Instance != null ? EegBandPowerTracker.Instance.AverageTBR : 0f,
            betaAlghaRatio: EegBandPowerTracker.Instance != null ? EegBandPowerTracker.Instance.AverageBAR : 0f,
            averageReactionTimeMs: avgTargetRt,
            reactionTimeVariabilityMs: targetRtv
        );

        string performancePath = RassdStimulusExcelExporter.Export(
            playerName,
            sessionId,
            score,
            hitCount,
            missCount,
            falseAlarmCount,
            correctRejectionCount,
            avgTargetRt,
            targetRtv,
            stimulusRecords
        );

        if (performancePath != null)
            Debug.Log("[Rassd] Stimulus performance CSV: " + performancePath);

        Debug.Log("HAS DATA: " + AttentionSessionTracker.CurrentHasData);
        Debug.Log("Target Avg RT: " + avgTargetRt + " ms | Target RTV: " + targetRtv + " ms");

        EegSessionRecorder.Instance?.StopRecording();

        // SessionExports CSV logging was removed intentionally.
        // The game now keeps only the main performance CSV and JSON dashboard data.

        EegSessionRecorder.Instance?.ResetRecords(); // Start a clean EEG record list.

        Time.timeScale = 0f;
    }

    private void FreezeGameWorldBehindGameOver()
    {
        Time.timeScale = 0f;

        foreach (var indicator in FindObjectsByType<FocusIndicatorController>(FindObjectsSortMode.None))
            indicator.FreezeVisualsForGameOver();

        foreach (var bandTracker in FindObjectsByType<EegBandPowerTracker>(FindObjectsSortMode.None))
            bandTracker.FreezeVisualsForGameOver();
    }

    private string LoadPlayerNameFromJSON()
    {
        var repo = new RakkizSaveRepository("rakkiz_save.json");

        string playerName = repo.GetPlayerNameOrDefault("PLAYER");

        if (string.IsNullOrWhiteSpace(playerName))
            return "PLAYER";

        return playerName.Trim();
    }
}