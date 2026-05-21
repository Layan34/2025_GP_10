using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Localization.Settings;
using LSL;
using Debug = UnityEngine.Debug;

public class OfflineExperiment : MonoBehaviour
{
    [Header("UI Screens")]
    public GameObject WelcomePlane; // First screen shown to the participant.
    public GameObject InstructionPlane; // Screen that explains the task.
    public GameObject ReadyState; // Countdown screen before trials start.
    public GameObject Stimulus; // Main stimulus screen.
    public GameObject SAM; // Self-assessment screen after each block.
    public GameObject FinishPlane; // Final screen after experiment ends.

    [Header("Ready Screen")]
    public TextMeshProUGUI readyTimerText; // Shows countdown before trials.

    [Header("Stimulus Elements")]
    public GameObject Square; // Visual stimulus shown during each trial.
    public GameObject CrossSign; // Fixation cross shown before stimulus.
    public Transform topSpawnPoint; // Target stimulus position.
    public Transform bottomSpawnPoint; // Non-target stimulus position.

    [Header("Experiment Settings")]
    public int numberOfBlocks = 2; // Total number of experiment blocks.
    public float stimDurationMs = 100f; // How long the stimulus is visible.
    public float isiMs = 2000f; // Time between trial starts.
    public KeyCode responseKey = KeyCode.Space; // Key used for participant response.

    [Header("Single Offline Participant")]
    [SerializeField] private int fixedParticipantID = 1; // Single participant ID used offline.
    [SerializeField] private bool overwriteCsvEachRun = false; // Controls whether old CSV is replaced.

    [Header("Auto Pipeline")]
    [SerializeField] private bool runPipelineAfterExperiment = true; // Runs EEG pipeline after tutorial data collection.
    [SerializeField] private bool waitForPipelineToFinish = false;
    [SerializeField] private float pipelineStartDelaySeconds = 3f;
    [SerializeField] private string pythonExePath = "";
    [SerializeField] private string eegDataProjectPath = "";
    [SerializeField] private bool skipClassification = false;
    [SerializeField] private TutorialPipelineRunner tutorialPipelineRunner; // Starts the Python tutorial pipeline.

    [Header("LSL & Recording")]
    public LslMarkerOutlet markerOutlet; // Sends trial markers to LSL.
    public LabRecorderRcsController recorder; // Controls LabRecorder recording.

// keep the comment
// for real tutorial 126 targets and 36 non targets
    private const int TARGETS_PER_BLOCK = 126; // Number of target trials per block.
    private const int NONTARGETS_PER_BLOCK = 36; // Number of non-target trials per block.
    private const int TRIALS_PER_BLOCK = TARGETS_PER_BLOCK + NONTARGETS_PER_BLOCK;
    private const float FAST_RT_MS = 150f; // Responses faster than this are excluded.

    private int currentBlock = 1;
    private int trialIndex = 0;
    private bool isTargetTrial;
    private bool canRespond;
    private float stimulusStartTime;
    private double stimulusLslTimeSec;

    private readonly List<float> hitRtList = new List<float>();
    private readonly List<bool> trialList = new List<bool>();

    private int hits;
    private int falseAlarms;
    private int misses;
    private int excludedFastCount;

    private string csvPath;
    private int participantID;
    private bool recordingStarted = false;
    private bool expStartMarkerSent = false;
    private bool pipelineLaunchStarted = false;

    private void Start()
    {
        markerOutlet = LslMarkerOutlet.Instance; // Get the shared LSL marker outlet.
        recorder = LabRecorderRcsController.Instance; // Get the shared LabRecorder controller.

        if (markerOutlet == null)
            Debug.LogWarning("[Experiment] LslMarkerOutlet not found");
        if (recorder == null)
            Debug.LogWarning("[Experiment] LabRecorderRcsController not found");

        participantID = Mathf.Max(1, fixedParticipantID); // Ensure participant ID is valid.

        recorder?.SetParticipantID(participantID);

        string root = Application.persistentDataPath;
        csvPath = Path.Combine(root, "Behavior.csv");
        CreateFileIfNeeded();

        currentBlock = 1;
        trialIndex = 0;
        recordingStarted = false;
        expStartMarkerSent = false;
        pipelineLaunchStarted = false;
        ResetCountersPerBlock();

        Debug.Log($"[Experiment] Offline single participant mode | Participant {participantID} | CSV: {csvPath}");
        Debug.Log($"[Experiment] GP1 folder: {Application.persistentDataPath}");
        ShowOnly(WelcomePlane);
    }

    private void Update()
    {
        if (canRespond && Input.GetKeyDown(responseKey))
            HandleResponse(); // Process key response.
    }

    public void StartInstructions()
    {
        ShowOnly(InstructionPlane);
        recorder?.Prewarm(); // Start LabRecorder setup before trials.
    }

    public void StartReady()
    {
        Time.timeScale = 1f;
        ShowOnly(ReadyState);

        if (!recordingStarted && recorder != null)
        {
            recordingStarted = true;
            StartCoroutine(BeginRecordingWithTimeout());
        }

        StartCoroutine(ReadyCountdown());
    }

    private IEnumerator BeginRecordingWithTimeout()
    {
        yield return new WaitForSecondsRealtime(0.2f);

        recorder.BeginRecording(); // Start XDF recording.

        float timeout = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < timeout)
        {
            if (recorder.IsRecordingConfirmed)
            {
                Debug.Log("[Experiment] Recording confirmed");
                yield break;
            }
            yield return null;
        }

        Debug.LogWarning("[Experiment] Recording not confirmed - continuing anyway");
    }

    private IEnumerator ReadyCountdown()
    {
        for (int count = 3; count > 0; count--)
        {
            if (readyTimerText != null)
            {
                // Show Arabic numerals and label if Arabic locale is selected
                bool isArabic = LocalizationSettings.SelectedLocale != null &&
                                LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ar");

                readyTimerText.text = isArabic
                    ? ToArabicNumerals(count)
                    : count + "s";
            }

            yield return new WaitForSecondsRealtime(1f);
        }

        if (readyTimerText != null)
            readyTimerText.text = "";

        if (recorder != null && !recorder.IsRecordingConfirmed)
            Debug.LogWarning("[Experiment] Recording not confirmed by countdown end");

        if (!expStartMarkerSent && markerOutlet != null)
        {
            expStartMarkerSent = true;
            double t = LSL.LSL.local_clock();
            markerOutlet.PushJsonMarker(
                "{\"event\":\"EXP_START\"," +
                $"\"participant\":{participantID}," +
                $"\"block\":{currentBlock}," +
                $"\"lsl_time_s\":{t.ToString("F6", CultureInfo.InvariantCulture)}}}",
                t);
        }

        BeginBlock(); // Start the first block after countdown.
    }

    private void BeginBlock()
    {
        ResetCountersPerBlock();
        GenerateTrials(); // Create target/non-target order.
        trialIndex = 0;

        if (markerOutlet != null)
        {
            double t = LSL.LSL.local_clock();
            markerOutlet.PushJsonMarker(
                "{\"event\":\"BLOCK_START\"," +
                $"\"participant\":{participantID}," +
                $"\"block\":{currentBlock}," +
                $"\"lsl_time_s\":{t.ToString("F6", CultureInfo.InvariantCulture)}}}",
                t);
        }

        ShowOnly(Stimulus);
        NextTrial();
    }

    private void EndBlock()
    {
        canRespond = false;

        if (markerOutlet != null)
        {
            double t = LSL.LSL.local_clock();
            markerOutlet.PushJsonMarker(
                "{\"event\":\"BLOCK_END\"," +
                $"\"participant\":{participantID}," +
                $"\"block\":{currentBlock}," +
                $"\"lsl_time_s\":{t.ToString("F6", CultureInfo.InvariantCulture)}}}",
                t);
        }

        ShowOnly(SAM);
    }

    private void EndExperiment()
    {
        if (markerOutlet != null)
        {
            double t = LSL.LSL.local_clock();
            markerOutlet.PushJsonMarker(
                "{\"event\":\"EXP_END\"," +
                $"\"participant\":{participantID}," +
                $"\"lsl_time_s\":{t.ToString("F6", CultureInfo.InvariantCulture)}}}",
                t);
        }

        recorder?.StopRecording();
        ShowOnly(FinishPlane);

        if (runPipelineAfterExperiment && !pipelineLaunchStarted)
        {
            pipelineLaunchStarted = true;
            StartCoroutine(RunPipelineAfterRecording());
        }
        else
        {
            StartCoroutine(GoToGameSelection());
        }
    }

    private IEnumerator RunPipelineAfterRecording()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, pipelineStartDelaySeconds));

        if (tutorialPipelineRunner == null)
        {
            Debug.LogError("[Pipeline] TutorialPipelineRunner is not assigned.");
            StartCoroutine(GoToGameSelection());
            yield break;
        }

        bool finished = false;
        bool success = false;
        string errorMessage = "";

        tutorialPipelineRunner.RunPipelineAfterTutorial(
            onSuccess: () =>
            {
                success = true;
                finished = true;
            },
            onFail: (error) =>
            {
                success = false;
                errorMessage = error;
                finished = true;
            }
        );

        while (!finished)
            yield return null;

        if (!success)
            Debug.LogError("[Pipeline] Failed: " + errorMessage);
        else
            Debug.Log("[Pipeline] Completed successfully.");

        StartCoroutine(GoToGameSelection());
    }

    private string ResolvePythonExe()
    {
        if (!string.IsNullOrWhiteSpace(pythonExePath))
            return pythonExePath;

        return "python";
    }

    private IEnumerator GoToGameSelection()
    {
        yield return new WaitForSecondsRealtime(3f);
        SceneManager.LoadScene("GameSelection");
    }

    private void NextTrial()
    {
        if (trialIndex >= TRIALS_PER_BLOCK || trialIndex >= trialList.Count)
        {
            EndBlock();
            return;
        }

        StartCoroutine(RunTrial()); // Run one stimulus trial.
    }

    private IEnumerator RunTrial()
    {
        canRespond = false;
        isTargetTrial = trialList[trialIndex];

        if (Square != null && topSpawnPoint != null && bottomSpawnPoint != null)
            Square.transform.position = (isTargetTrial ? topSpawnPoint : bottomSpawnPoint).position;

        if (CrossSign != null) CrossSign.SetActive(true);
        if (Square != null) Square.SetActive(false);

        yield return new WaitForSecondsRealtime(0.10f);

        if (Square != null) Square.SetActive(true);
        stimulusLslTimeSec = LSL.LSL.local_clock();
        stimulusStartTime = Time.realtimeSinceStartup;

        if (markerOutlet != null)
        {
            if (trialIndex == 0 && currentBlock == 1)
            {
                markerOutlet.PushJsonMarker(
                    "{\"event\":\"first_trial_start\"," +
                    $"\"participant\":{participantID}," +
                    $"\"block\":{currentBlock}," +
                    $"\"trialIndex\":{trialIndex}," +
                    $"\"lsl_time_s\":{stimulusLslTimeSec.ToString("F6", CultureInfo.InvariantCulture)}}}",
                    stimulusLslTimeSec);
            }

            markerOutlet.PushTrialMarker(
                participantID, currentBlock, trialIndex, isTargetTrial, stimulusLslTimeSec);
        }

        canRespond = true; // Allow response while trial is active.

        float stimSec = stimDurationMs / 1000f;
        float isiSec = isiMs / 1000f;

        yield return new WaitForSecondsRealtime(stimSec);
        if (Square != null) Square.SetActive(false);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, isiSec - stimSec));

        if (canRespond)
        {
            if (isTargetTrial)
            {
                misses++;
                AppendTrialRow(trialIndex, "target", "none", "miss", "", 0, stimulusLslTimeSec);
            }
            else
            {
                AppendTrialRow(trialIndex, "nonTarget", "none", "cr", "", 0, stimulusLslTimeSec);
            }

            canRespond = false;
        }

        trialIndex++;
        NextTrial();
    }

    private void HandleResponse()
    {
        if (!canRespond) return;
        canRespond = false;

        float rtMs = (Time.realtimeSinceStartup - stimulusStartTime) * 1000f;

        if (rtMs < FAST_RT_MS)
        {
            excludedFastCount++;
            AppendTrialRow(
                trialIndex,
                isTargetTrial ? "target" : "nonTarget",
                "key",
                "excludedFast",
                rtMs.ToString("F2", CultureInfo.InvariantCulture),
                1,
                stimulusLslTimeSec);
            return;
        }

        if (isTargetTrial)
        {
            hits++;
            hitRtList.Add(rtMs);
            AppendTrialRow(
                trialIndex,
                "target",
                "key",
                "hit",
                rtMs.ToString("F2", CultureInfo.InvariantCulture),
                0,
                stimulusLslTimeSec);
        }
        else
        {
            falseAlarms++;
            AppendTrialRow(
                trialIndex,
                "nonTarget",
                "key",
                "fa",
                rtMs.ToString("F2", CultureInfo.InvariantCulture),
                0,
                stimulusLslTimeSec);
        }
    }

    public void Sam_High() => SaveSamAndAdvance("High");
    public void Sam_MedHigh() => SaveSamAndAdvance("Medium-High");
    public void Sam_Medium() => SaveSamAndAdvance("Medium");
    public void Sam_MedLow() => SaveSamAndAdvance("Medium-Low");
    public void Sam_Low() => SaveSamAndAdvance("Low");

    private void SaveSamAndAdvance(string rating)
    {
        AppendSamRow(rating);
        AppendSummaryRow(); // Save block summary to CSV.

        currentBlock++;

        if (currentBlock <= numberOfBlocks)
        {
            expStartMarkerSent = false;
            StartReady();
        }
        else
        {
            EndExperiment();
        }
    }

    private void CreateFileIfNeeded()
    {
        bool shouldRewrite = overwriteCsvEachRun || !File.Exists(csvPath);

        if (shouldRewrite)
        {
            File.WriteAllText(csvPath,
                "participant,block,row_type,trialIndex,stimulus,response,outcome," +
                "rt_ms,excluded_fast,lsl_time_s," +
                "hits,falseAlarms,misses,totalTargets,totalNonTargets," +
                "rt_mean_ms,rtv_ms,hit_rate,false_alarm_rate,sam_rating\n");
        }
    }

    private void AppendTrialRow(int tIdx, string stimulus, string response, string outcome,
        string rtMs, int excludedFast, double lslTimeSec)
    {
        string lslStr = lslTimeSec <= 0 ? "" : lslTimeSec.ToString("F6", CultureInfo.InvariantCulture);

        File.AppendAllText(csvPath,
            $"{participantID},{currentBlock},trial,{tIdx}," +
            $"{stimulus},{response},{outcome},{rtMs},{excludedFast},{lslStr}," +
            $",,,{TARGETS_PER_BLOCK},{NONTARGETS_PER_BLOCK},,,,\n");
    }

    private void AppendSamRow(string rating)
    {
        File.AppendAllText(csvPath,
            $"{participantID},{currentBlock},sam,,,,,,,," +
            $",,,{TARGETS_PER_BLOCK},{NONTARGETS_PER_BLOCK},,,,,{rating}\n");
    }

    private void AppendSummaryRow()
    {
        float rtMean = hitRtList.Count > 0 ? Mean(hitRtList) : float.NaN;
        float rtv = hitRtList.Count > 1 ? SampleStd(hitRtList) : float.NaN;
        double hr = (double)hits / TARGETS_PER_BLOCK;
        double far = (double)falseAlarms / NONTARGETS_PER_BLOCK;

        File.AppendAllText(csvPath,
            $"{participantID},{currentBlock},summary,,,,," +
            $",{excludedFastCount},," +
            $"{hits},{falseAlarms},{misses},{TARGETS_PER_BLOCK},{NONTARGETS_PER_BLOCK}," +
            $"{(float.IsNaN(rtMean) ? "" : rtMean.ToString("F2", CultureInfo.InvariantCulture))}," +
            $"{(float.IsNaN(rtv) ? "" : rtv.ToString("F2", CultureInfo.InvariantCulture))}," +
            $"{hr.ToString("F6", CultureInfo.InvariantCulture)}," +
            $"{far.ToString("F6", CultureInfo.InvariantCulture)},\n");
    }

    private void GenerateTrials()
    {
        trialList.Clear();

        for (int i = 0; i < TARGETS_PER_BLOCK; i++)
            trialList.Add(true);

        for (int i = 0; i < NONTARGETS_PER_BLOCK; i++)
            trialList.Add(false);

        for (int i = 0; i < trialList.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, trialList.Count);
            (trialList[i], trialList[j]) = (trialList[j], trialList[i]);
        }
    }

    private void ResetCountersPerBlock()
    {
        hits = 0;
        falseAlarms = 0;
        misses = 0;
        excludedFastCount = 0;
        hitRtList.Clear();
        stimulusLslTimeSec = 0;
    }

    private static float Mean(List<float> xs)
    {
        double sum = 0;
        for (int i = 0; i < xs.Count; i++)
            sum += xs[i];

        return (float)(sum / xs.Count);
    }

    private static float SampleStd(List<float> xs)
    {
        if (xs.Count <= 1)
            return float.NaN;

        float mean = Mean(xs);
        double ss = 0;

        for (int i = 0; i < xs.Count; i++)
        {
            double d = xs[i] - mean;
            ss += d * d;
        }

        return (float)Math.Sqrt(ss / (xs.Count - 1));
    }

    // Converts Western numerals to Arabic-Indic numerals (٣ ٢ ١)
    private static string ToArabicNumerals(int number)
    {
        string result = number.ToString();
        result = result.Replace("0", "٠").Replace("1", "١").Replace("2", "٢")
                       .Replace("3", "٣").Replace("4", "٤").Replace("5", "٥")
                       .Replace("6", "٦").Replace("7", "٧").Replace("8", "٨")
                       .Replace("9", "٩");
        return result;
    }

    private void ShowOnly(GameObject activeScreen)
    {
        if (WelcomePlane != null) WelcomePlane.SetActive(false);
        if (InstructionPlane != null) InstructionPlane.SetActive(false);
        if (ReadyState != null) ReadyState.SetActive(false);
        if (Stimulus != null) Stimulus.SetActive(false);
        if (SAM != null) SAM.SetActive(false);
        if (FinishPlane != null) FinishPlane.SetActive(false);

        if (activeScreen != null)
            activeScreen.SetActive(true);
    }
}