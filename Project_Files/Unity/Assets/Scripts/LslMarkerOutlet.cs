using System.Globalization;
using UnityEngine;
using LSL;

public sealed class LslMarkerOutlet : MonoBehaviour
{
    public static LslMarkerOutlet Instance { get; private set; }

    [Header("Stream Identity")]
    [SerializeField] private string streamName = "Unity_Markers";
    [SerializeField] private string streamType = "Markers";
    [SerializeField] private string sourceId   = "rakkiz_unity_markers_v1";

    private StreamInfo   _info;
    private StreamOutlet _outlet;
    private readonly string[] _buffer = new string[1]; // reused every push

    private void Awake()
    {
        // Enforce singleton
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _info = new StreamInfo(
            streamName, streamType,
            1,
            LSL.LSL.IRREGULAR_RATE,
            channel_format_t.cf_string,
            sourceId);

        _outlet = new StreamOutlet(_info);
        Debug.Log($"[LSL] Outlet ready → name='{streamName}'  sourceId='{sourceId}'");
    }

    private void OnDestroy()
    {
        _outlet?.Dispose();
        _info?.Dispose();
    }

    public void Push(string marker)
    {
        PushAt(marker, LSL.LSL.local_clock());
    }

    public void PushAt(string marker, double lslTimestamp)
    {
        if (_outlet == null) { Debug.LogWarning("[LSL] Outlet not ready."); return; }

        _buffer[0] = marker;
        _outlet.push_sample(_buffer, lslTimestamp);
        Debug.Log($"[LSL] ▶ {marker}  @t={lslTimestamp:F6}");
    }

    public void PushTrial(int participant, int block, int trial, bool isTarget)
    {
        double t = LSL.LSL.local_clock();

        string marker = string.Format(CultureInfo.InvariantCulture,
            "{{\"event\":\"trial_onset\",\"participant\":{0},\"block\":{1}," +
            "\"trial\":{2},\"stimulus\":\"{3}\",\"lsl_t\":{4:F6}}}",
            participant, block, trial,
            isTarget ? "target" : "nonTarget",
            t);

        PushAt(marker, t);
    }

    public bool HasConsumers() => _outlet?.have_consumers() ?? false;

    public void PushJsonMarker(string json, double lslTimestamp) => PushAt(json, lslTimestamp);

    public void PushTrialMarker(int participant, int block, int trial, bool isTarget, double lslTimestamp)
    {
        string marker = string.Format(CultureInfo.InvariantCulture,
            "{{\"event\":\"trial_onset\",\"participant\":{0},\"block\":{1}," +
            "\"trial\":{2},\"stimulus\":\"{3}\",\"lsl_t\":{4}}}",
            participant, block, trial,
            isTarget ? "target" : "nonTarget",
            lslTimestamp.ToString("F6", CultureInfo.InvariantCulture));
        PushAt(marker, lslTimestamp);
    }

}