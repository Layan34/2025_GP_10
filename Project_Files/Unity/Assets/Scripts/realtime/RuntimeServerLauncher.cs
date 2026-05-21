using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public class RuntimeServerLauncher : MonoBehaviour
{
    [Header("Folder Names")]
    [SerializeField] private string eegRepoFolderName        = "EEG_Data"; // Folder containing the Python environment.
    [SerializeField] private string inferenceScriptRelPath   = @"player_scripts\8_runtime_inference_server.py"; // Runtime server script.

    [Header("Server Settings")]
    [SerializeField] private string host = "127.0.0.1"; // Localhost for Unity-to-Python communication.
    [SerializeField] private int    port = 65432;      // Must match LiveInferenceClient port.

    [Header("Auto-launch on Start")]
    [SerializeField] private bool launchOnStart = true; // Automatically start Python when this object starts.

    public bool IsRunning => _process != null && !_process.HasExited; // True while Python server is alive.

    private Process _process;


    private void Start()
    {
        if (launchOnStart) // Start server automatically when enabled in Inspector.
            LaunchServer();
    }

    private void OnDestroy()
    {
        StopServer(); // Make sure the Python process does not stay open.
    }

    private void OnApplicationQuit()
    {
        StopServer(); // Make sure the Python process does not stay open.
    }


    private string GetUnityProjectRoot()
    {
        return Directory.GetParent(Application.dataPath).FullName; // Gets the Unity project folder.
    }

    private string GetParentFolder()
    {
        return Directory.GetParent(GetUnityProjectRoot()).FullName;
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


    public void LaunchServer(Action onStarted = null, Action<string> onFail = null)
    {
        if (IsRunning) // Prevent launching another copy of the same server.
        {
            UnityEngine.Debug.LogWarning("[InferenceLauncher] Server is already running.");
            return;
        }

        string eegPath    = GetEegRepoPath(); // Root EEG_Data folder.
        string pythonPath = GetPythonExePath();
        string scriptPath = Path.Combine(eegPath, inferenceScriptRelPath);

        UnityEngine.Debug.Log("[InferenceLauncher] EEG repo   : " + eegPath);
        UnityEngine.Debug.Log("[InferenceLauncher] Python     : " + pythonPath);
        UnityEngine.Debug.Log("[InferenceLauncher] Script     : " + scriptPath);

        if (!Directory.Exists(eegPath)) // Stop if EEG_Data cannot be found.
        {
            string err = $"EEG repo not found: {eegPath}";
            UnityEngine.Debug.LogError("[InferenceLauncher] " + err);
            onFail?.Invoke(err);
            return;
        }

        if (!File.Exists(pythonPath)) // Stop if the virtual environment is missing.
        {
            string err = $"Python venv not found: {pythonPath}";
            UnityEngine.Debug.LogError("[InferenceLauncher] " + err);
            onFail?.Invoke(err);
            return;
        }

        if (!File.Exists(scriptPath)) // Stop if the server script is missing.
        {
            string err = $"Inference script not found: {scriptPath}";
            UnityEngine.Debug.LogError("[InferenceLauncher] " + err);
            onFail?.Invoke(err);
            return;
        }

        try
        {
            var info = new ProcessStartInfo
            {
                FileName               = pythonPath,
                Arguments              = $"\"{scriptPath}\" --host {host} --port {port}",
                WorkingDirectory       = eegPath,
                UseShellExecute        = false, // Required to capture Python output.
                RedirectStandardOutput = true,  // Show normal Python logs in Unity.
                RedirectStandardError  = true,  // Show Python errors in Unity.
                CreateNoWindow         = true,
            };

            _process = new Process { StartInfo = info };

            _process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    UnityEngine.Debug.Log("[InferenceServer] " + e.Data);
            };

            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    UnityEngine.Debug.LogWarning("[InferenceServer STDERR] " + e.Data);
            };

            _process.Start(); // Launch the Python server process.
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            UnityEngine.Debug.Log($"[InferenceLauncher] Server started (PID {_process.Id}).");
            onStarted?.Invoke();
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("[InferenceLauncher] Failed to start: " + ex.Message);
            onFail?.Invoke(ex.Message);
        }
    }

    public void StopServer()
    {
        if (_process == null) return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(); // Force close the server if it is still running.
                UnityEngine.Debug.Log("[InferenceLauncher] Server stopped.");
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[InferenceLauncher] Stop error: " + e.Message);
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }
}
