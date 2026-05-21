using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public class FocusResult
{
    public int prediction;       // Final decision: 0 = unfocused, 1 = focused.
    public int modelPrediction;  // Raw model output before rule/calibration changes.
    public bool isFocused;       // True when the final prediction is focused.
    public bool predictionReceived; // True when Python returned a usable prediction.

    public string zone;
    public string decisionSource;
    public float score;
    public float gameplayScore;
    public float generalScore;
    public float confidence;
    public float calibrationScore;
    public int runtimeOutliersP01P99;
    public string error;

    public float[] theta; // Theta power values for EEG channels.
    public float[] alpha; // Alpha power values for EEG channels.
    public float[] beta;  // Beta power values for EEG channels.
    public float[] tbr;   // Theta/Beta ratio values.
    public float[] bar;   // Beta/Alpha ratio values.

    public bool IsValid => predictionReceived && string.IsNullOrEmpty(error);
}

public class LiveInferenceClient : MonoBehaviour
{
    public static LiveInferenceClient Instance { get; private set; }
    private bool isQuitting = false; // Prevent reconnect logic during app shutdown.
    [Header("Python Server")]
    public string host = "127.0.0.1"; // Local Python server address.
    public int port = 65432;          // Port used by Unity and Python.

    [Header("EEG Source")]
    [SerializeField] private EmotivLive emotivLive;

    [Header("Reconnect")]
    public float reconnectDelaySec = 3f; // Time between connection attempts.

    [Header("Startup Delay")]
    public float startupDelaySec = 3f;   // Delay before sending EEG windows.

    public event Action<FocusResult> OnFocusResult;
    public event Action<bool> OnConnectionChanged;

    public bool IsConnected { get; private set; }
    public FocusResult LastResult { get; private set; }

    private TcpClient tcp;
    private NetworkStream stream;
    private Thread recvThread;
    private bool running;
    private float startTime;
    private int lastSentWindowVersion = -1;

    private readonly Queue<FocusResult> resultQueue = new Queue<FocusResult>();
    private readonly object resultLock = new object();

    private readonly Queue<string> sendQueue = new Queue<string>();
    private readonly object sendLock = new object();

    private void Awake()
    {
        if (Instance != null && Instance != this) // Keep only one inference client across scenes.
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // Keep the TCP connection alive between scenes.

        if (emotivLive == null)
            emotivLive = EmotivLive.Instance != null ? EmotivLive.Instance : FindFirstObjectByType<EmotivLive>();
    }

    private void Start()
    {
        running = true; // Start connection and sending loops.
        startTime = Time.time;
        StartCoroutine(ConnectLoop()); // Keep trying until Python server connects.
    }

    private void Update()
    {
        lock (resultLock)
        {
            while (resultQueue.Count > 0) // Move received predictions to Unity main thread.
            {
                FocusResult r = resultQueue.Dequeue();
                LastResult = r;
                OnFocusResult?.Invoke(r);
            }
        }

        if (!IsConnected) return; // Do not send EEG data before the socket connects.
        if (Time.time - startTime < startupDelaySec) return; // Wait for systems to initialize.

        if (emotivLive == null)
        {
            emotivLive = EmotivLive.Instance != null ? EmotivLive.Instance : FindFirstObjectByType<EmotivLive>();

            if (emotivLive == null)
            {
                Debug.LogWarning("[InferenceClient] EmotivLive not found.");
                return;
            }
        }

        if (emotivLive.LatestWindow == null) return; // No EEG window is ready yet.
        if (emotivLive.WindowVersion == lastSentWindowVersion) return; // Avoid sending the same window twice.

        string json = BuildEegJson(emotivLive.LatestWindow); // Convert EEG samples into JSON for Python.
        lastSentWindowVersion = emotivLive.WindowVersion;

        lock (sendLock)
        {
            sendQueue.Enqueue(json + "\n");
        }

        Debug.Log($"[InferenceClient] Sent EEG window v{lastSentWindowVersion}");
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        running = false;

        Disconnect();

        Instance = null;
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
        running = false;
        Disconnect();

        if (Instance == this)
            Instance = null;
    }

    private string BuildEegJson(Dictionary<string, float[]> window)
    {
        StringBuilder sb = new StringBuilder(1024 * 60);
        sb.Append("{\"eeg\":{");

        bool firstChannel = true;

        foreach (KeyValuePair<string, float[]> kvp in window)
        {
            if (!firstChannel) sb.Append(',');
            firstChannel = false;

            sb.Append('"').Append(kvp.Key).Append("\":[");

            float[] samples = kvp.Value;

            for (int i = 0; i < samples.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(samples[i].ToString("G7", System.Globalization.CultureInfo.InvariantCulture));
            }

            sb.Append(']');
        }

        sb.Append("}}");
        return sb.ToString();
    }

    private IEnumerator ConnectLoop()
    {
        while (running)
        {
            if (!IsConnected)
                yield return StartCoroutine(TryConnect());

            yield return new WaitForSeconds(reconnectDelaySec);
        }
    }

    private IEnumerator TryConnect()
    {
        Debug.Log($"[InferenceClient] Connecting to {host}:{port}...");
        yield return null;

        try
        {
            tcp = new TcpClient();
            tcp.Connect(host, port);

            stream = tcp.GetStream();
            IsConnected = true;
            OnConnectionChanged?.Invoke(true);

            recvThread = new Thread(ReceiveLoop) { IsBackground = true }; // Read server responses without freezing Unity.
            recvThread.Start();

            StartCoroutine(SendLoop()); // Send queued EEG windows to Python.

            Debug.Log("[InferenceClient] Connected.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[InferenceClient] Connect failed: {e.Message}");
            Disconnect();
        }
    }

    private IEnumerator SendLoop()
    {
        while (IsConnected && running)
        {
            string msg = null;

            lock (sendLock)
            {
                if (sendQueue.Count > 0)
                    msg = sendQueue.Dequeue();
            }

            if (msg != null)
            {
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(msg); // TCP sends bytes, not strings.
                    stream.Write(bytes, 0, bytes.Length);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[InferenceClient] Send error: {e.Message}");
                    Disconnect();
                    yield break;
                }
            }

            yield return null;
        }
    }

    private void ReceiveLoop()
    {
        byte[] buffer = new byte[262144];
        StringBuilder partial = new StringBuilder();

        try
        {
            while (IsConnected && running)
            {
                int n = stream.Read(buffer, 0, buffer.Length); // Read raw response bytes from Python.
                if (n == 0) break;

                partial.Append(Encoding.UTF8.GetString(buffer, 0, n));
                string data = partial.ToString();

                int newlineIndex;

                while ((newlineIndex = data.IndexOf('\n')) >= 0) // Each prediction ends with a newline.
                {
                    string line = data.Substring(0, newlineIndex).Trim();
                    data = data.Substring(newlineIndex + 1);

                    if (!string.IsNullOrEmpty(line))
                    {
                        FocusResult result = ParseResult(line); // Convert server JSON into a Unity object.

                        lock (resultLock)
                        {
                            resultQueue.Enqueue(result);
                        }
                    }
                }

                partial.Clear();
                partial.Append(data);
            }
        }
        catch (Exception e)
        {
            if (running)
                Debug.LogWarning($"[InferenceClient] Receive error: {e.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    private static FocusResult ParseResult(string json)
    {
        FocusResult r = new FocusResult();

        try
        {
            r.error = ExtractString(json, "error"); // Python may return an error instead of prediction.

            r.prediction = ExtractIntIfPresent(json, "prediction", -1); // Final binary prediction from Python.
            r.modelPrediction = ExtractIntIfPresent(json, "model_prediction", r.prediction);

            r.decisionSource = ExtractString(json, "decision_source") ?? "model";
            r.confidence = ExtractFloatIfPresent(json, "confidence", 0f);
            r.calibrationScore = ExtractFloatIfPresent(json, "calibration_score", -1f);
            r.runtimeOutliersP01P99 = ExtractIntIfPresent(json, "runtime_outliers_p01_p99", 0);

            int bpIdx = json.IndexOf("\"band_powers\":", StringComparison.Ordinal);

            if (bpIdx >= 0)
            {
                r.theta = ExtractFloatArrayFromObject(json, "theta", 14, bpIdx);
                r.alpha = ExtractFloatArrayFromObject(json, "alpha", 14, bpIdx);
                r.beta = ExtractFloatArrayFromObject(json, "beta", 14, bpIdx);
                r.tbr = ExtractFloatArrayFromObject(json, "tbr", 14, bpIdx);
                r.bar = ExtractFloatArrayFromObject(json, "bar", 14, bpIdx);
            }

            if (!string.IsNullOrEmpty(r.error)) // Do not use gameplay prediction when server reports an error.
            {
                r.predictionReceived = false;
                return r;
            }

            if (r.prediction == -1)
            {
                bool? focused = ExtractBoolIfPresent(json, "is_focused");

                if (focused.HasValue)
                    r.prediction = focused.Value ? 1 : 0;
            }

            if (r.prediction == 0 || r.prediction == 1)
            {
                r.predictionReceived = true;
                r.isFocused = r.prediction == 1;
            }
            else
            {
                r.predictionReceived = false;
                r.error = "No binary prediction found in server response.";
                return r;
            }

            r.score = r.isFocused ? 1f : 0f;
            r.gameplayScore = r.score;
            r.generalScore = r.score;
            r.zone = ExtractString(json, "zone") ?? (r.isFocused ? "high" : "low");
        }
        catch (Exception e)
        {
            r.error = $"Parse error: {e.Message}";
            r.predictionReceived = false;
        }

        return r;
    }

    private static string ExtractString(string json, string key)
    {
        int i = json.IndexOf($"\"{key}\":", StringComparison.Ordinal);
        if (i < 0) return null;

        string rest = json.Substring(i + key.Length + 3).TrimStart();

        if (rest.StartsWith("null", StringComparison.OrdinalIgnoreCase)) return null;
        if (!rest.StartsWith("\"")) return null;

        int q2 = rest.IndexOf('"', 1);
        if (q2 < 0) return null;

        return rest.Substring(1, q2 - 1);
    }

    private static bool? ExtractBoolIfPresent(string json, string key)
    {
        int i = json.IndexOf($"\"{key}\":", StringComparison.Ordinal);
        if (i < 0) return null;

        string rest = json.Substring(i + key.Length + 3).TrimStart().ToLowerInvariant();

        if (rest.StartsWith("true")) return true;
        if (rest.StartsWith("false")) return false;

        return null;
    }

    private static int ExtractIntIfPresent(string json, string key, int fallback)
    {
        string token = ExtractNumberToken(json, key);

        if (string.IsNullOrEmpty(token)) return fallback;

        return int.TryParse(
            token,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out int v
        ) ? v : fallback;
    }

    private static float ExtractFloatIfPresent(string json, string key, float fallback)
    {
        string token = ExtractNumberToken(json, key);

        if (string.IsNullOrEmpty(token)) return fallback;

        return float.TryParse(
            token,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float v
        ) ? v : fallback;
    }

    private static string ExtractNumberToken(string json, string key)
    {
        int i = json.IndexOf($"\"{key}\":", StringComparison.Ordinal);
        if (i < 0) return null;

        int start = i + key.Length + 3;
        StringBuilder sb = new StringBuilder();

        for (int j = start; j < json.Length; j++)
        {
            char c = json[j];

            if (c == ' ') continue;

            if ((c >= '0' && c <= '9') ||
                c == '-' ||
                c == '+' ||
                c == '.' ||
                c == 'e' ||
                c == 'E')
            {
                sb.Append(c);
            }
            else
            {
                break;
            }
        }

        return sb.ToString();
    }

    private static float[] ExtractFloatArrayFromObject(string json, string key, int expectedLen, int searchStart)
    {
        float[] result = new float[expectedLen];

        int keyIdx = json.IndexOf($"\"{key}\":", searchStart, StringComparison.Ordinal);
        if (keyIdx < 0) return result;

        int arrStart = json.IndexOf('[', keyIdx);
        int arrEnd = arrStart >= 0 ? json.IndexOf(']', arrStart) : -1;

        if (arrStart < 0 || arrEnd < 0) return result;

        string inner = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
        string[] parts = inner.Split(',');

        for (int i = 0; i < expectedLen && i < parts.Length; i++)
        {
            if (float.TryParse(
                parts[i].Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float v))
            {
                result[i] = v;
            }
        }

        return result;
    }

    private void Disconnect()
    {
        if (IsConnected)
        {
            IsConnected = false;
            OnConnectionChanged?.Invoke(false);
            Debug.Log("[InferenceClient] Disconnected.");
        }

        try { stream?.Close(); } catch { }
        try { tcp?.Close(); } catch { }
    }

    public void ResetSession()
    {
        lastSentWindowVersion = -1;
    }

    public void NotifyNewSession()
    {
        if (emotivLive == null)
            emotivLive = EmotivLive.Instance != null ? EmotivLive.Instance : FindFirstObjectByType<EmotivLive>();

        lastSentWindowVersion = -1;
        Debug.Log("[InferenceClient] NotifyNewSession — will re-send current EEG window on next Update.");
    }
}
