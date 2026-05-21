using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public class TutorialPipelineRunner : MonoBehaviour
{
    [Header("Folder Names")]
    [SerializeField] private string eegRepoFolderName = "EEG_Data"; // Folder that contains Python scripts and player data.
    [SerializeField] private string pipelineScriptRelativePath = @"player_scripts\5_runtime_pipeline.py"; // Calibration pipeline script path.

    [Header("Optional")]
    [SerializeField] private float waitBeforeRunSeconds = 1.0f; // Small delay before starting Python after tutorial ends.

    public bool IsRunning { get; private set; }
    public bool LastRunSucceeded { get; private set; }
    public string LastError { get; private set; }

    private string GetUnityProjectRoot()
    {
        return Directory.GetParent(Application.dataPath).FullName; // Gets the Unity project folder.
    }

    private string GetParentFolder()
    {
        return Directory.GetParent(GetUnityProjectRoot()).FullName; // Gets the folder that contains Unity and EEG_Data.
    }

    private string GetEegRepoPath()
    {
        return Path.Combine(GetParentFolder(), eegRepoFolderName);
    }

    private string GetPythonExePath()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return Path.Combine(GetEegRepoPath(), ".venv", "Scripts", "python.exe");
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        return Path.Combine(GetEegRepoPath(), ".venv", "bin", "python");
#else
        return "";
#endif
    }

    public void RunPipelineAfterTutorial(Action onSuccess = null, Action<string> onFail = null)
    {
        if (IsRunning) // Prevent starting two pipeline processes at the same time.
        {
            UnityEngine.Debug.LogWarning("Pipeline is already running.");
            return;
        }

        StartCoroutine(RunPipelineCoroutine(onSuccess, onFail));
    }

    private IEnumerator RunPipelineCoroutine(Action onSuccess, Action<string> onFail)
    {
        IsRunning = true; // Mark the pipeline as active.
        LastRunSucceeded = false; // Reset previous result before this run.
        LastError = "";

        yield return new WaitForSecondsRealtime(waitBeforeRunSeconds); // Give Unity time to finish tutorial shutdown.

        string eegRepoPath = GetEegRepoPath(); // Root folder for EEG pipeline files.
        string pythonExePath = GetPythonExePath();
        string scriptPath = Path.Combine(eegRepoPath, pipelineScriptRelativePath);
        string calibrationPath = Path.Combine(
            eegRepoPath,
            "player_data",
            "calibration",
            "current_player_calibration.json"
        );

        UnityEngine.Debug.Log("[Pipeline] Calibration path: " + calibrationPath);

        UnityEngine.Debug.Log("[Pipeline] Unity root: " + GetUnityProjectRoot());
        UnityEngine.Debug.Log("[Pipeline] Parent folder: " + GetParentFolder());
        UnityEngine.Debug.Log("[Pipeline] EEG repo path: " + eegRepoPath);
        UnityEngine.Debug.Log("[Pipeline] Python path: " + pythonExePath);

        if (!Directory.Exists(eegRepoPath)) // Stop if the EEG_Data folder is missing.
        {
            LastError = $"EEG repo not found: {eegRepoPath}";
            IsRunning = false;
            onFail?.Invoke(LastError);
            yield break;
        }

        if (!File.Exists(pythonExePath)) // Stop if the Python virtual environment is missing.
        {
            LastError = $"Python not found: {pythonExePath}";
            IsRunning = false;
            onFail?.Invoke(LastError);
            yield break;
        }

        if (!File.Exists(scriptPath)) // Stop if the pipeline script is missing.
        {
            LastError = $"Pipeline script not found: {scriptPath}";
            IsRunning = false;
            onFail?.Invoke(LastError);
            yield break;
        }

        Process process = null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExePath,
                Arguments = $"\"{scriptPath}\"",
                WorkingDirectory = eegRepoPath,
                UseShellExecute = false,        // Required for reading Python output inside Unity.
                RedirectStandardOutput = true, // Capture normal Python logs.
                RedirectStandardError = true,  // Capture Python errors.
                CreateNoWindow = true
            };

            process = new Process();
            process.StartInfo = startInfo;

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    UnityEngine.Debug.Log("[Pipeline] " + e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    UnityEngine.Debug.LogWarning("[Pipeline STDERR] " + e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            IsRunning = false;
            onFail?.Invoke(LastError);
            yield break;
        }

        while (!process.HasExited) // Wait until Python finishes.
            yield return null;

        if (process.ExitCode != 0) // Non-zero means the pipeline failed.
        {
            LastError = $"Pipeline failed with exit code {process.ExitCode}";
            IsRunning = false;
            onFail?.Invoke(LastError);
            yield break;
        }

        if (!File.Exists(calibrationPath))
        {
            LastError = $"Pipeline finished but calibration file was not found: {calibrationPath}";
            IsRunning = false;
            onFail?.Invoke(LastError);
            yield break;
        }

        LastRunSucceeded = true;
        IsRunning = false;
        onSuccess?.Invoke();
    }
}
