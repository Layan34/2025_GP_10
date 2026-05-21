using System.Collections.Generic;
using UnityEngine;
using EmotivUnityPlugin;

public class EmotivLive : MonoBehaviour
{
    public static EmotivLive Instance { get; private set; } // Persistent EEG stream instance.
    private bool isQuitting = false; // True only when the app is closing.

    [Header("Cortex App Credentials")]
    public string clientId = "secret"; // Emotiv app client ID.
    public string clientSecret = "secret"; // Emotiv app client secret.
    public string appName = "RakkizEEG_Unity"; // App name shown to Cortex.

    [Header("Streams")]
    public List<string> streams = new List<string> { "eeg" }; // Request raw EEG stream only.

    private string headsetId = ""; // Selected headset ID.
    private bool startedStream = false; // True after EEG stream starts.
    private float startTime; // Used to delay reading until stream stabilizes.

    public static readonly string[] ChannelNames =
    {
        "AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2",
        "P8", "T8", "FC6", "F4", "F8", "AF4"
    }; // Channel order must match the trained model.

    private static readonly Channel_t[] EegChannels =
    {
        Channel_t.CHAN_AF3, Channel_t.CHAN_F7, Channel_t.CHAN_F3,
        Channel_t.CHAN_FC5, Channel_t.CHAN_T7, Channel_t.CHAN_P7,
        Channel_t.CHAN_O1, Channel_t.CHAN_O2, Channel_t.CHAN_P8,
        Channel_t.CHAN_T8, Channel_t.CHAN_FC6, Channel_t.CHAN_F4,
        Channel_t.CHAN_F8, Channel_t.CHAN_AF4
    }; // Emotiv SDK channel constants.

    private const int WindowSamples = 128; // 1 second at 128 Hz.
    private const float SFreq = 128f; // Sampling frequency.

    private readonly float[][] _buffer = new float[14][]; // Ring buffer for all channels.
    private int _writeHead = 0; // Current write position in the ring buffer.
    private int _buffered = 0; // Number of samples currently available.
    private int _samplesSinceLastPublish = 0; // Counts samples since last published window.
    private const int PublishEveryNSamples = WindowSamples; // Publish one window per second.

    public Dictionary<string, float[]> LatestWindow { get; private set; } // Latest full EEG window.
    public int WindowVersion { get; private set; } = 0; // Increments when a new window is ready.
    public bool HasFreshWindow { get; private set; } = false; // True only on the frame a window is published.

    public float LastAF3RawMean { get; private set; } = 0f; // Simple signal check for AF3.

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this); // Keep the existing EEG stream and remove only this duplicate.
            return;
        }

        Instance = this; // Store global EEG stream reference.
        DontDestroyOnLoad(gameObject); // Keep the stream alive between scenes.

        for (int i = 0; i < 14; i++)
            _buffer[i] = new float[WindowSamples]; // Create one buffer per EEG channel.
    }

    private void Start()
    {
        Debug.Log("[EmotivLive] Start — RAW EEG stream mode (µV → Python PSD)");
        startTime = Time.time; // Mark when streaming setup started.

        try
        {
            EmotivUnityPlugin.Config.AppUrl = "wss://localhost:6868"; // Local Cortex WebSocket URL.
            EmotivUnityItf.Instance.Init(
                clientId,
                clientSecret,
                appName,
                allowSaveLogToFile: true,
                isDataBufferUsing: true,
                appUrl: "wss://localhost:6868"
            ); // Initialize Emotiv plugin.

            EmotivUnityItf.Instance.Start(); // Start Cortex connection.
            Debug.Log("[EmotivLive] Plugin started");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[EmotivLive] Init Error: " + e.Message);
            return;
        }

        InvokeRepeating(nameof(Tick), 1f, 2f); // Keep checking until authorization/headset is ready.
    }

    private void Tick()
    {
        if (!EmotivUnityItf.Instance.IsAuthorizedOK)
        {
            Debug.Log("[EmotivLive] Not authorized yet");
            return;
        }

        EmotivUnityItf.Instance.QueryHeadsets(); // Refresh detected headset list.
        var headsets = EmotivUnityItf.Instance.GetDetectedHeadsets();
        if (headsets == null || headsets.Count == 0) return; // Wait until a headset appears.

        if (!startedStream)
        {
            headsetId = headsets[0].HeadsetID; // Use the first detected headset.
            Debug.Log("[EmotivLive] Starting EEG stream: " + headsetId);
            EmotivUnityItf.Instance.StartDataStream(streams, headsetId); // Start raw EEG streaming.
            startedStream = true;
            CancelInvoke(nameof(Tick)); // Stop checking after stream starts.
        }
    }

    private void Update()
    {
        HasFreshWindow = false; // Reset freshness each frame.
        if (!EmotivUnityItf.Instance.IsAuthorizedOK || !startedStream) return; // Read only after stream starts.
        if (Time.time - startTime < 5f) return; // Give the stream a short warm-up time.

        int newSamples = EmotivUnityItf.Instance.GetNumberEEGSamples(); // Number of new EEG samples.
        if (newSamples <= 0) return;

        double[][] frameData = new double[EegChannels.Length][]; // Holds raw data for all channels.
        for (int c = 0; c < EegChannels.Length; c++)
        {
            double[] raw = EmotivUnityItf.Instance.GetEEGData(EegChannels[c]); // Read full buffered channel data.
            frameData[c] = (raw != null) ? raw : System.Array.Empty<double>(); // Avoid null arrays.
        }

        int sampleCount = newSamples; // Start from plugin-reported sample count.
        for (int c = 0; c < EegChannels.Length; c++)
            if (frameData[c].Length < sampleCount)
                sampleCount = frameData[c].Length; // Use the shortest channel to keep alignment.

        if (sampleCount <= 0) return;

        for (int s = 0; s < sampleCount; s++)
        {
            for (int c = 0; c < EegChannels.Length; c++)
            {
                int offset = frameData[c].Length - sampleCount; // Use only the newest samples.
                _buffer[c][_writeHead] = (float)frameData[c][offset + s]; // Save sample in ring buffer.
            }

            _writeHead = (_writeHead + 1) % WindowSamples; // Move write position in a circular way.

            if (_buffered < WindowSamples)
                _buffered++; // Count until the buffer becomes full.

            _samplesSinceLastPublish++; // Track when to publish next window.

            if (_buffered >= WindowSamples && _samplesSinceLastPublish >= PublishEveryNSamples)
            {
                PublishWindow(); // Send one complete 128-sample window.
                _samplesSinceLastPublish = 0;
            }
        }
    }

    private void PublishWindow()
    {
        var window = new Dictionary<string, float[]>(14); // Window sent to LiveInferenceClient.
        float af3Sum = 0f; // Used to calculate AF3 mean.

        for (int c = 0; c < EegChannels.Length; c++)
        {
            string name = ChannelNames[c]; // Human-readable channel name.
            float[] samples = new float[WindowSamples]; // Copy of this channel window.

            for (int i = 0; i < WindowSamples; i++)
                samples[i] = _buffer[c][(_writeHead + i) % WindowSamples]; // Read samples in correct order.

            window[name] = samples; // Add channel to the published window.

            if (c == 0)
            {
                for (int i = 0; i < WindowSamples; i++)
                    af3Sum += samples[i]; // Sum AF3 samples for quick signal check.

                LastAF3RawMean = af3Sum / WindowSamples; // Store AF3 average.
            }
        }

        LatestWindow = window; // Make the window available to the inference client.
        WindowVersion++; // Notify that a new window exists.
        HasFreshWindow = true; // Mark this frame as fresh.

        EegLatencyTracker.SignalTime = Time.time; // Start latency timing from EEG publish moment.

        Debug.Log($"[EmotivLive] EEG window v{WindowVersion} | AF3 mean={LastAF3RawMean:F2} µV");
    }

    public bool TryGetLatestRawSamples(int requestedSamples, out Dictionary<string, float[]> window)
    {
        window = null;

        if (_buffered < requestedSamples)
            return false; // Not enough samples yet.

        requestedSamples = Mathf.Clamp(requestedSamples, 1, WindowSamples); // Keep request inside buffer size.

        window = new Dictionary<string, float[]>(14); // Output raw sample window.

        int startIndex = (_writeHead - requestedSamples + WindowSamples) % WindowSamples; // Oldest requested sample.

        for (int c = 0; c < EegChannels.Length; c++)
        {
            string name = ChannelNames[c];
            float[] samples = new float[requestedSamples];

            for (int i = 0; i < requestedSamples; i++)
            {
                int idx = (startIndex + i) % WindowSamples; // Wrap around the ring buffer.
                samples[i] = _buffer[c][idx];
            }

            window[name] = samples; // Add channel samples to output.
        }

        return true;
    }

    public void ResetWindowVersion()
    {
        WindowVersion = 0; // Reset published window counter.
        _writeHead = 0; // Restart buffer write position.
        _buffered = 0; // Clear buffer count.
        _samplesSinceLastPublish = 0; // Reset publish counter.
        HasFreshWindow = false; // No fresh window after reset.
        LatestWindow = null; // Remove old window reference.
    }

    private void OnDisable()
    {
        Debug.Log("[EmotivLive] Disabled");

        if (!isQuitting)
            return; // Do not stop EEG during normal scene changes.

        HasFreshWindow = false;

        if (startedStream)
        {
            EmotivUnityItf.Instance.Stop(); // Stop plugin only when quitting.
            startedStream = false;
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true; // Allow shutdown cleanup.

        if (startedStream)
        {
            EmotivUnityItf.Instance.Stop(); // Stop EEG stream before closing.
            startedStream = false;
        }

        if (Instance == this)
            Instance = null; // Clear global reference.
    }
}
