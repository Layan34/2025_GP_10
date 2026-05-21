using UnityEngine;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

public sealed class LabRecorderRcsController : MonoBehaviour
{
    [Header("LabRecorder")]
    [SerializeField] private string labRecorderExePath = ""; // Optional direct path to LabRecorder.
    [SerializeField] private string labRecorderConfigPath = ""; // Optional LabRecorder config file path.

    [Header("Emotiv-LSL (Optional)")]
    [SerializeField] private string pythonExePath = ""; // Optional Python path for emotiv-lsl.
    [SerializeField] private string emotivLslRepoPath = ""; // Optional path to emotiv-lsl repo.

    [Header("RCS Connection")]
    [SerializeField] private string rcsHost = "127.0.0.1"; // Local LabRecorder RCS host.
    [SerializeField] private int rcsPort = 22345; // LabRecorder RCS control port.

    [Header("Recording Metadata")]
    [SerializeField] private int participantID = 1; // Participant identifier saved with recordings.
    [SerializeField] private string session = "S001"; // Session label.
    [SerializeField] private string task = "Default"; // Task name for recording metadata.
    [SerializeField] private int run = 1; // Run number for repeated recordings.
    public static LabRecorderRcsController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private Process _emotivProc;
    private Process _lrProc;
    private TcpClient _tcp;

    private bool _prewarmStarted;
    private bool _prewarmDone;
    private bool _recording;
    private bool _recordingConfirmed;

    // Track stream info
    private bool _markerStreamFound;
    private bool _emotivStreamFound;
    private string _emotivStreamName = "";
    public bool IsConnected => _tcp is { Connected: true }; // True when RCS TCP is connected.
    public bool IsPrewarmed => _prewarmDone; // True after tools are launched and checked.
    public bool IsRecordingConfirmed => _recordingConfirmed; // True after recording starts.
    public bool IsEmotivConnected => _emotivStreamFound; // True when an Emotiv LSL stream is detected.
    public void Prewarm()
    {
        if (_prewarmStarted) return; // Do not launch tools twice.
        _prewarmStarted = true;
        StartCoroutine(PrewarmRoutine());
    }

    public void SetParticipantID(int id)
    {
        participantID = id;
        UnityEngine.Debug.Log($"[RCS] ParticipantID = {id}");
    }

    public void BeginRecording()
    {
        if (_recording) return; // Avoid starting recording twice.
        StartCoroutine(BeginRecordingRoutine());
    }

    public void StopRecording()
    {
        if (!_recording) return; // Nothing to stop.
        SafeSend("stop"); // Stop LabRecorder recording.
        _recording = _recordingConfirmed = false;
        CloseTcp();
        UnityEngine.Debug.Log("[RCS] Recording stopped");
    }

    private void OnApplicationQuit()
    {
        if (_recording) SafeSend("stop"); // Stop LabRecorder recording.
        CloseTcp();

        // Clean up processes
        try { _emotivProc?.Kill(); } catch { }
        try { _lrProc?.Kill(); } catch { }
    }
    private IEnumerator PrewarmRoutine()
    {
        UnityEngine.Debug.Log("[Prewarm] Starting - launching tools silently");

        KillExistingLabRecorder(); // Close old LabRecorder instances first.

        LaunchEmotivLsl(); // Start Emotiv-to-LSL bridge if available.

        LaunchLabRecorder(); // Start LabRecorder.

        if (!IsProcessAlive(_lrProc))
        {
            UnityEngine.Debug.LogWarning("[Prewarm] LabRecorder failed to launch - XDF will not be saved");
            _prewarmDone = true;
            yield break;
        }

        UnityEngine.Debug.Log("[Prewarm] Waiting for LabRecorder RCS server");
        yield return new WaitForSecondsRealtime(3f);

        yield return ConnectRcsWithRetry(); // Try to connect to LabRecorder control server.

        if (!IsConnected)
        {
            UnityEngine.Debug.LogError("[RCS] Failed to connect to LabRecorder");
            _prewarmDone = true;
            yield break;
        }

        UnityEngine.Debug.Log("[RCS] Connected to LabRecorder");

        SafeSend("update");
        yield return new WaitForSecondsRealtime(1f);

        yield return WaitForMarkerStream(5f); // Wait until Unity marker stream appears.

        ScanForEmotivStreams(); // Search the LSL network for EEG streams.

        SafeSend("update");
        yield return new WaitForSecondsRealtime(0.5f);
        SafeSend("select all"); // Select all visible streams in LabRecorder.
        yield return new WaitForSecondsRealtime(0.5f);

        if (_emotivStreamFound && !string.IsNullOrEmpty(_emotivStreamName))
        {
            SelectEmotivStream();
        }

        VerifySelectedStreams(); // Check if marker/EEG streams are selected.

        string reply = ReadAvailable();
        if (!string.IsNullOrWhiteSpace(reply))
            UnityEngine.Debug.Log("[RCS] Reply: " + reply.Trim());

        _prewarmDone = true;
        UnityEngine.Debug.Log("[Prewarm] Complete - ready for recording");
    }

        private void KillExistingLabRecorder()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("LabRecorder"))
            {
                if (proc.Id != (_lrProc?.Id ?? -1))
                {
                    UnityEngine.Debug.Log("[Prewarm] Killing existing LabRecorder process");
                    proc.Kill();
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Prewarm] Error killing LabRecorder: {e.Message}");
        }
    }

        private IEnumerator ConnectRcsWithRetry()
    {
        int maxAttempts = 10;
        int attempt = 0;

        while (attempt < maxAttempts && !IsConnected)
        {
            attempt++;
            UnityEngine.Debug.Log($"[RCS] Connection attempt {attempt}/{maxAttempts}");

            TryConnectOnce();

            if (!IsConnected)
                yield return new WaitForSecondsRealtime(0.5f);
        }
    }

        private IEnumerator WaitForMarkerStream(float timeoutSeconds)
    {
        if (LslMarkerOutlet.Instance == null)
        {
            UnityEngine.Debug.Log("[Prewarm] LslMarkerOutlet not found - cannot wait for marker stream");
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        bool markerFound = false;

        while (Time.realtimeSinceStartup < deadline && !markerFound)
        {
            SafeSend("update");
            yield return new WaitForSecondsRealtime(0.2f);

            markerFound = FindMarkerStream();

            if (!markerFound)
                yield return new WaitForSecondsRealtime(0.3f);
        }

        _markerStreamFound = markerFound;

        if (markerFound)
        {
            UnityEngine.Debug.Log("[Prewarm] Marker stream found successfully");
            SafeSend("update");
            yield return new WaitForSecondsRealtime(0.2f);
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[Prewarm] Marker stream not found after {timeoutSeconds}s");
        }
    }

        private void ScanForEmotivStreams()
    {
        try
        {
            UnityEngine.Debug.Log("=== SCANNING LSL NETWORK FOR EMOTIV ===");
            var streams = LSL.LSL.resolve_streams(3.0);
            UnityEngine.Debug.Log($"Found {streams.Length} LSL streams total");

            _emotivStreamFound = false;
            _emotivStreamName = "";

            foreach (var s in streams)
            {
                string name = "";
                string type = "";
                string sourceId = "";

                try { name = s.name(); } catch { }
                try { type = s.type(); } catch { }
                try { sourceId = s.source_id(); } catch { }

                UnityEngine.Debug.Log($"[LSL] Stream: Name='{name}' | Type='{type}' | Source='{sourceId}'");

                if (type.ToLower().Contains("eeg") ||
                    name.ToLower().Contains("emotiv") ||
                    name.ToLower().Contains("epoc"))
                {
                    UnityEngine.Debug.Log("EMOTIV FOUND ON NETWORK!");
                    _emotivStreamFound = true;
                    _emotivStreamName = name; // Store exact stream name for selection.
                }
            }

            if (_emotivStreamFound)
            {
                UnityEngine.Debug.Log($"[RCS] Emotiv stream name: '{_emotivStreamName}'");
            }
            else
            {
                UnityEngine.Debug.Log("[RCS] No Emotiv stream found - continuing without EEG");
            }

            UnityEngine.Debug.Log("=== END EMOTIV SCAN ===");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Error scanning LSL: {e.Message}");
        }
    }

        private void SelectEmotivStream()
    {
        if (!IsConnected || string.IsNullOrEmpty(_emotivStreamName)) return;

        UnityEngine.Debug.Log($"[RCS] Attempting to select Emotiv stream: '{_emotivStreamName}'");

        SafeSend($"select \"{_emotivStreamName}\"");
        System.Threading.Thread.Sleep(300);

        SafeSend("list");
        System.Threading.Thread.Sleep(500);

        string reply = ReadAvailable();
        if (reply.Contains(_emotivStreamName) || reply.Contains("EEG"))
        {
            UnityEngine.Debug.Log("✅ Emotiv stream successfully selected!");
        }
        else
        {
            UnityEngine.Debug.Log("[RCS] Retrying without quotes...");
            SafeSend($"select {_emotivStreamName}");
        }
    }

        private void VerifySelectedStreams()
    {
        if (!IsConnected) return;

        SafeSend("list");
        System.Threading.Thread.Sleep(500);

        string reply = ReadAvailable();

        if (string.IsNullOrEmpty(reply))
        {
            UnityEngine.Debug.LogWarning("[RCS] No reply to list command");
            return;
        }

        UnityEngine.Debug.Log("[RCS] Currently selected streams:");
        UnityEngine.Debug.Log(reply);

        if (_emotivStreamFound)
        {
            if (reply.Contains(_emotivStreamName) || reply.Contains("EEG") || reply.Contains("eeg"))
            {
                UnityEngine.Debug.Log("EMOTIV IS SELECTED IN LABRECORDER!");
            }
            else
            {
                SelectEmotivStream();
            }
        }
    }

    private string ResolvePythonExe(string repo)
    {
        if (!string.IsNullOrWhiteSpace(pythonExePath) && File.Exists(pythonExePath))
            return pythonExePath;

        string repoVenvPy = Path.Combine(repo, "venv", "Scripts", "python.exe");
        if (File.Exists(repoVenvPy))
            return repoVenvPy;

        string repoDotVenvPy = Path.Combine(repo, ".venv", "Scripts", "python.exe");
        if (File.Exists(repoDotVenvPy))
            return repoDotVenvPy;

        string projectDotVenvPy = Path.Combine(Directory.GetCurrentDirectory(), ".venv", "Scripts", "python.exe");
        if (File.Exists(projectDotVenvPy))
            return projectDotVenvPy;

        string windowsPyLauncher = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "py.exe"
        );
        if (File.Exists(windowsPyLauncher))
            return windowsPyLauncher;

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string[] commonCandidates =
        {
            Path.Combine(localAppData, "Programs", "Python", "Python311", "python.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python312", "python.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python310", "python.exe"),
            Path.Combine(localAppData, "Programs", "Python", "Python313", "python.exe"),
            Path.Combine(userProfile, "AppData", "Local", "Programs", "Python", "Python311", "python.exe"),
            Path.Combine(userProfile, "AppData", "Local", "Programs", "Python", "Python312", "python.exe"),
            Path.Combine(userProfile, "AppData", "Local", "Programs", "Python", "Python310", "python.exe"),
            Path.Combine(userProfile, "AppData", "Local", "Programs", "Python", "Python313", "python.exe")
        };

        foreach (string candidate in commonCandidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return "python";
    }
    private IEnumerator BeginRecordingRoutine()
    {
        float deadline = Time.realtimeSinceStartup + 3f;
        while (!_prewarmDone && Time.realtimeSinceStartup < deadline)
        {
            yield return new WaitForSecondsRealtime(0.1f);
        }

        if (!_prewarmDone)
            UnityEngine.Debug.LogWarning("[RCS] Prewarm not complete - attempting to record anyway");

        if (!IsConnected)
        {
            UnityEngine.Debug.LogWarning("[RCS] Not connected - running without XDF recording");
            _recording = _recordingConfirmed = true;
            yield break;
        }

        Directory.CreateDirectory(GetRecordingRoot());
        SendFilenameCommand();
        yield return new WaitForSecondsRealtime(0.2f);

        SafeSend("start"); // Start LabRecorder recording.
        _recording = _recordingConfirmed = true;
        UnityEngine.Debug.Log("[RCS] Recording started");

        if (_emotivStreamFound)
            UnityEngine.Debug.Log("Emotiv EEG will be recorded in XDF");
        else
            UnityEngine.Debug.Log("[RCS] No Emotiv detected - running with markers only");
    }
    private void LaunchEmotivLsl()
    {
        if (IsProcessAlive(_emotivProc)) return;

        string repo = ResolveDir(emotivLslRepoPath, "emotiv-lsl");
        if (string.IsNullOrEmpty(repo))
        {
            UnityEngine.Debug.Log("[Emotiv] emotiv-lsl not found - skipping");
            return;
        }

        string mainPy = Path.Combine(repo, "main.py");
        if (!File.Exists(mainPy))
        {
            UnityEngine.Debug.Log("[Emotiv] main.py not found - skipping");
            return;
        }

        string py = ResolvePythonExe(repo);

        UnityEngine.Debug.Log($"[Emotiv] Python resolved to: {py}");
        UnityEngine.Debug.Log($"[Emotiv] Repo resolved to: {repo}");
        UnityEngine.Debug.Log($"[Emotiv] main.py resolved to: {mainPy}");

        _emotivProc = LaunchHidden(py, $"-u \"{mainPy}\"", repo);

        if (_emotivProc != null)
            UnityEngine.Debug.Log("[Emotiv] emotiv-lsl launched successfully");
        else
            UnityEngine.Debug.LogError("[Emotiv] Failed to launch emotiv-lsl");
    }
    private void LaunchLabRecorder()
    {
        if (IsProcessAlive(_lrProc)) return;

        string exe = "";

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        exe = ResolveFile(labRecorderExePath,
            Path.Combine("LabRecorder", "Windows", "LabRecorder.exe"),
            Path.Combine("Tools", "LabRecorder", "Windows", "LabRecorder.exe"),
            Path.Combine("Windows", "LabRecorder.exe"),
            @"C:\LSL\LabRecorder\LabRecorder.exe",
            @"C:\LabRecorder\LabRecorder.exe");
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        exe = ResolveFile(labRecorderExePath,
            Path.Combine("LabRecorder", "MacOS", "LabRecorder.app", "Contents", "MacOS", "LabRecorder"),
            Path.Combine("Tools", "LabRecorder", "MacOS", "LabRecorder.app", "Contents", "MacOS", "LabRecorder"),
            Path.Combine("MacOS", "LabRecorder.app", "Contents", "MacOS", "LabRecorder"));
#else
        exe = ResolveFile(labRecorderExePath,
            Path.Combine("LabRecorder", "LabRecorder.exe"),
            Path.Combine("Tools", "LabRecorder", "LabRecorder.exe"));
#endif

        if (string.IsNullOrEmpty(exe))
        {
            UnityEngine.Debug.LogError("[LabRecorder] LabRecorder executable not found");
            return;
        }

        string cfg = ResolveFile(labRecorderConfigPath,
            Path.Combine("LabRecorder", "LabRecorder.cfg"),
            Path.Combine("Tools", "LabRecorder", "LabRecorder.cfg"),
            Path.Combine("LabRecorder", "Windows", "LabRecorder.cfg"),
            Path.Combine("Tools", "LabRecorder", "Windows", "LabRecorder.cfg"));

        string args = string.IsNullOrEmpty(cfg) ? "" : $"-c \"{cfg}\"";

        _lrProc = LaunchHidden(exe, args, Path.GetDirectoryName(exe));

        if (_lrProc != null)
        {
            UnityEngine.Debug.Log($"[LabRecorder] Launched: {exe}");

        #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            StartCoroutine(HideLabRecorderWindowMac());
        #endif
        }
        else
        {
            UnityEngine.Debug.LogError("[LabRecorder] Launch failed");
        }
    }
    private static Process LaunchHidden(string exe, string args, string workDir)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.dwFlags = STARTF_USESHOWWINDOW;
            si.wShowWindow = 0;

            bool ok = CreateProcess(
                null,
                $"\"{exe}\" {args}".Trim(),
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CREATE_NO_WINDOW | CREATE_NEW_PROCESS_GROUP,
                IntPtr.Zero,
                string.IsNullOrEmpty(workDir) ? null : workDir,
                ref si,
                out var pi);

            if (!ok)
            {
                UnityEngine.Debug.LogWarning($"[Process] CreateProcess failed (error {Marshal.GetLastWin32Error()})");
                return null;
            }

            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return Process.GetProcessById((int)pi.dwProcessId);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Process] Exception: {e.Message}");
            return null;
        }
#else
        try
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = workDir ?? "",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[Process] Failed: {e.Message}");
            return null;
        }
#endif
    }

    private IEnumerator HideLabRecorderWindowMac()
    {
    #if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        yield return new WaitForSecondsRealtime(1.0f);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                Arguments = "-e 'tell application \"System Events\" to set visible of process \"LabRecorder\" to false'",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            UnityEngine.Debug.Log("[LabRecorder] Hide command sent on macOS");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[LabRecorder] Failed to hide macOS window: {e.Message}");
        }
    #else
        yield break;
    #endif
    }

    private static bool IsProcessAlive(Process p)
    {
        try { return p != null && !p.HasExited; }
        catch { return false; }
    }
    private static bool FindMarkerStream()
    {
        try
        {
            foreach (var s in LSL.LSL.resolve_streams(1.0))
            {
                string name = TryGet(s.name);
                string type = TryGet(s.type);

                if (type.Equals("Markers", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Unity_Markers", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    private static string TryGet(Func<string> f)
    {
        try { return f() ?? ""; }
        catch { return ""; }
    }
    private void TryConnectOnce()
    {
        CloseTcp();
        try
        {
            _tcp = new TcpClient();
            var ar = _tcp.BeginConnect(rcsHost, rcsPort, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(400)) || !_tcp.Connected)
                CloseTcp();
            else
                _tcp.EndConnect(ar);
        }
        catch { CloseTcp(); }
    }

    private bool SafeSend(string cmd)
    {
        if (!IsConnected) return false;

        try
        {
            byte[] b = Encoding.UTF8.GetBytes(cmd + "\n");
            var ns = _tcp.GetStream();
            ns.Write(b, 0, b.Length);
            ns.Flush();
            UnityEngine.Debug.Log($"[RCS] → {cmd}");
            return true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[RCS] Send failed: {e.Message}");
            CloseTcp();
            return false;
        }
    }

    private string ReadAvailable()
    {
        try
        {
            if (!IsConnected) return "";

            var ns = _tcp.GetStream();
            if (!ns.DataAvailable) return "";

            var sb = new StringBuilder();
            var buf = new byte[4096];

            while (ns.DataAvailable)
            {
                int n = ns.Read(buf, 0, buf.Length);
                if (n > 0)
                    sb.Append(Encoding.UTF8.GetString(buf, 0, n));
            }

            return sb.ToString();
        }
        catch { return ""; }
    }

    private void CloseTcp()
    {
        try { _tcp?.Close(); }
        catch { }
        _tcp = null;
    }
    private string GetRecordingRoot() =>
        Application.persistentDataPath;

    private void SendFilenameCommand()
    {
        string root = GetRecordingRoot();
        Directory.CreateDirectory(root);

        string file = "recording.xdf";
        SafeSend($"filename {{root:{Clean(root)}}} {{template:{Clean(file)}}}");
        UnityEngine.Debug.Log($"[RCS] Saving to: {Path.Combine(root, file)}");
    }

    private static string Clean(string s) =>
        s.Replace("\n", "").Replace("\r", "").Replace("{", "").Replace("}", "");

    private static string ResolveDir(string @override, string relative)
    {
        if (!string.IsNullOrWhiteSpace(@override) && Directory.Exists(@override))
            return @override;

        foreach (var candidate in GetPathCandidates(relative))
            if (Directory.Exists(candidate))
                return candidate;

        return "";
    }

    private static string ResolveFile(string @override, params string[] relatives)
    {
        if (!string.IsNullOrWhiteSpace(@override) && File.Exists(@override))
            return @override;

        foreach (var relative in relatives)
            foreach (var candidate in GetPathCandidates(relative))
                if (File.Exists(candidate))
                    return candidate;

        return "";
    }

    private static string[] GetPathCandidates(string relative)
    {
        if (Path.IsPathRooted(relative))
            return new[] { relative };

        string streamingAssets = Application.streamingAssetsPath;
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string oneDriveDesktop = Path.Combine(userProfile, "OneDrive", "Desktop");

        return new[]
        {
            Path.Combine(streamingAssets, "Tools", relative),
            Path.Combine(streamingAssets, relative),
            Path.Combine(desktop, relative),
            Path.Combine(oneDriveDesktop, relative),
            Path.Combine(userProfile, relative),
        };
    }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize,
                  dwXCountChars, dwYCountChars,
                  dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public uint dwProcessId, dwThreadId;
    }

    private const int STARTF_USESHOWWINDOW = 0x0001;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint CREATE_NEW_PROCESS_GROUP = 0x00000200;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);
#endif
}