using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

public class OfflineExperiment : MonoBehaviour
{
    [Header("Screens")]
    public GameObject WelcomePlane;
    public GameObject InstructionPlane;
    public GameObject ReadyState; // 3-second countdown screen
    public GameObject Stimulus;
    public GameObject SAM; // Self-Assessment Manikin rating screen
    public GameObject FinishPlane;


    [Header("Ready UI")]
    public TextMeshProUGUI readyTimerText; // Countdown text

    [Header("Stimulus Elements")]
    public GameObject Square; // The white target/non-target square
    public GameObject CrossSign;
    public Transform topSpawnPoint;
    public Transform bottomSpawnPoint;

    [Header("Settings")]
    public int trialsPerBlock = 162;   // 126 target + 36 non-target per block
    public int numberOfBlocks = 2;
    public float stimDurationMs = 100f;
    public float isiMs = 2000f;
    public KeyCode responseKey = KeyCode.Space; // Player response key

    private int currentBlock = 1;
    private int trialIndex = 0;
    private bool isTargetTrial;
    private bool canRespond; // True while response is allowed
    private float stimulusStartTime;

    private List<bool> trialList = new List<bool>(); // Shuffled list of trials
    private List<float> rtList = new List<float>();  // RT list for valid hits

    private int hits;
    private int falseAlarms;
    private int misses;
    private int correctRejections;

    private string trialsPath;
    private string summaryPath;
    private string samPath;

    // Added: dynamic participant ID
    private int participantID;

    void Start()
    {
        string root = Application.persistentDataPath;

        // unified single CSV file
        trialsPath = Path.Combine(root, "offline_all.csv");
        summaryPath = trialsPath;
        samPath = trialsPath;

        // FIXED: Incremental unique participant ID (1,2,3…)
        participantID = PlayerPrefs.GetInt("participantCounter", 0);
        participantID++;
        PlayerPrefs.SetInt("participantCounter", participantID);
        PlayerPrefs.Save();

        CreateFiles();
        ResetCounters();

        PrintCSVPaths();
        ShowOnly(WelcomePlane);
    }

    void Update()
    {
        // If response allowed and player pressed
        if (canRespond && Input.GetKeyDown(responseKey))
            HandleResponse();
    }

    // === NAVIGATION ====

    public void StartInstructions()
    {
        ShowOnly(InstructionPlane);
    }

    public void StartReady()
    {
        // Show countdown page
        ShowOnly(ReadyState);
        StartCoroutine(ReadyCountdown());
    }

    IEnumerator ReadyCountdown()
    {
        int count = 3;

        while (count > 0)
        {
            if (readyTimerText != null)
                readyTimerText.text = count + "s";

            yield return new WaitForSeconds(1f);
            count--;
        }

        if (readyTimerText != null)
            readyTimerText.text = "";

        // Start trials
        BeginBlock();
    }

    // ==== Block Cycle ====

    void BeginBlock()
    {
        GenerateTrials();
        trialIndex = 0;

        // Enter stimulus screen
        ShowOnly(Stimulus);
        NextTrial();
    }

    void EndBlock()
    {
        // Show SAM rating page
        ShowOnly(SAM);
    }

    void EndExperiment()
    {
        // Show final finish screen after completing all blocks
        ShowOnly(FinishPlane);
    }

    // ==== Trial Logic ====

    void NextTrial()
    {
        if (trialIndex >= trialsPerBlock || trialIndex >= trialList.Count)
        {
            EndBlock();
            return;
        }

        StartCoroutine(RunTrial());
    }

    IEnumerator RunTrial()
    {
        // Allow responses
        canRespond = true;

        // Determine trial type
        isTargetTrial = trialList[trialIndex];

        Transform pos = isTargetTrial ? topSpawnPoint : bottomSpawnPoint;
        Square.transform.position = pos.position;

        CrossSign.SetActive(true);
        Square.SetActive(false);

        // small delay before square (fixation first)
        yield return new WaitForSeconds(0.10f);

        Square.SetActive(true);

        stimulusStartTime = Time.time;

        float stimSec = stimDurationMs / 1000f;
        float isiSec = isiMs / 1000f;

        yield return new WaitForSeconds(stimSec);

        Square.SetActive(false);

        float remainingIsi = Mathf.Max(0f, isiSec - stimSec);
        yield return new WaitForSeconds(remainingIsi);

        // No response made during valid window
        if (canRespond)
        {
            if (isTargetTrial)
            {
                // Missed target
                misses++;
                WriteTrial("none", "miss", "");
            }
            else
            {
                // Correctly ignored non-target
                correctRejections++;
                WriteTrial("none", "cr", "");
            }

            canRespond = false;
        }

        // Move to next trial
        trialIndex++;
        NextTrial();
    }

    // ==== Responses ====

    void HandleResponse()
    {
        if (!canRespond)
            return;

        canRespond = false;

        float rtMs = (Time.time - stimulusStartTime) * 1000f;

        // if the response is too fast consider it invalid
        if (rtMs < 150f)
        {
            WriteTrial("key", "excludedFast", rtMs.ToString("F2"));
            return;
        }

        if (isTargetTrial)
        {
            hits++;
            rtList.Add(rtMs);
            WriteTrial("key", "hit", rtMs.ToString("F2"));
        }
        else
        {
            falseAlarms++;
            WriteTrial("key", "fa", rtMs.ToString("F2"));
        }
    }

    // ==== SAM Rating ====

    public void Sam_High() => SaveSam("High");
    public void Sam_MedHigh() => SaveSam("Medium-High");
    public void Sam_Medium() => SaveSam("Medium");
    public void Sam_MedLow() => SaveSam("Medium-Low");
    public void Sam_Low() => SaveSam("Low");

    void SaveSam(string rating)
    {
        string line =
            participantID + ",sam,," +
            currentBlock + "," +
            ",,,," +
            hits + "," +
            falseAlarms + "," +
            misses + "," +
            correctRejections + "," +
            252 + "," +
            72 + "," +
            rating + "\n";

        File.AppendAllText(samPath, line);

        // >>> FIX: write summary AFTER SAM per participant block
        SaveSummaryCsv();

        // Move to next block
        currentBlock++;

        if (currentBlock <= numberOfBlocks)
            StartReady();
        else
            EndExperiment();
    }

    // ==== CSV ====

    void CreateFiles()
    {
        if (!File.Exists(trialsPath))
            File.WriteAllText(trialsPath,
                "participant,section,trialIndex,block,stimulus,response,outcome,rt_ms,hits,falseAlarms,misses,correctRejections,totalTargets,totalNonTargets,samRating\n");
    }

    void WriteTrial(string response, string outcome, string rtString)
    {
        string stim = isTargetTrial ? "target" : "nonTarget";

        string line =
            participantID + ",trial," +  // participant dynamic ID, section = trial
            trialIndex + "," +
            currentBlock + "," +
            stim + "," +
            response + "," +
            outcome + "," +
            rtString + "," +
            hits + "," +
            falseAlarms + "," +
            misses + "," +
            correctRejections + "," +
            252 + "," +
            72 + "," +
            "" + "\n"; // samRating empty for trials

        File.AppendAllText(trialsPath, line);
    }

    void SaveSummaryCsv()
    {
        int totalTargets = 252;
        int totalNonTargets = 72;

        string line =
            participantID + ",summary,,," +
            ",,,," +
            hits + "," +
            falseAlarms + "," +
            misses + "," +
            correctRejections + "," +
            totalTargets + "," +
            totalNonTargets + ",\n";

        File.AppendAllText(summaryPath, line);
    }

    // ==== Utilities ====

    void GenerateTrials()
    {
        trialList.Clear();

        for (int i = 0; i < 126; i++) trialList.Add(true);  // Target trials
        for (int i = 0; i < 36; i++) trialList.Add(false);  // Non-target trials

        // Fisher-Yates Shuffle
        for (int i = 0; i < trialList.Count; i++)
        {
            int j = Random.Range(i, trialList.Count);
            (trialList[i], trialList[j]) = (trialList[j], trialList[i]);
        }
    }

    void ResetCounters()
    {
        hits = falseAlarms = misses = correctRejections = 0;
        rtList.Clear();
    }

    void ShowOnly(GameObject screen)
    {
        WelcomePlane.SetActive(false);
        InstructionPlane.SetActive(false);
        ReadyState.SetActive(false);
        Stimulus.SetActive(false);
        SAM.SetActive(false);

        if (screen != null)
            screen.SetActive(true);
    }

    void PrintCSVPaths()
    {
        Debug.Log("Unified CSV Path: " + trialsPath);
    }
}
