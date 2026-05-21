using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class EegBandPowerTracker : MonoBehaviour
{
    public static EegBandPowerTracker Instance { get; private set; } // Active tracker for the current scene.

    [Header("UI")]
    [SerializeField] private Image tbrFill; // Fill image for the TBR bar.
    [SerializeField] private Image barFill; // Fill image for the BAR bar.
    [SerializeField] private TextMeshProUGUI tbrLevelText; // Text label for TBR status.
    [SerializeField] private TextMeshProUGUI barLevelText; // Text label for BAR status.

    [Header("Runtime Inference Source")]
    [Tooltip("Uses the TBR/BAR values returned by the Python inference server.")]
    [SerializeField] private LiveInferenceClient liveInferenceClient; // Receives band-power results from Python.

    [Header("Movement")]
    [Range(0.01f, 1f)]
    [SerializeField] private float valueSmoothing = 0.25f; // Smooths the raw TBR/BAR values.

    [Range(0.01f, 1f)]
    [SerializeField] private float fillSmoothing = 0.55f; // Smooths the UI bar movement.

    [Header("Debug")]
    [SerializeField] private bool logValues = true; // Enables console logs for checking values.

    [System.Serializable]
    public class RatioCalibration
    {
        public float focused_threshold; // Player's focused threshold.
        public float unfocused_threshold; // Player's unfocused threshold.
        public string direction; // Ratio direction from calibration.
        public float center; // Calibration center value.
        public float scale; // Calibration spread value.
        public RatioSummary summary; // Extra statistics from calibration.
    }

    [System.Serializable]
    public class RatioSummary
    {
        public float min; // Minimum calibration value.
        public float max; // Maximum calibration value.
        public float mean; // Average calibration value.
        public float std; // Standard deviation.
        public float q25; // First quartile.
        public float q50; // Median.
        public float q75; // Third quartile.
    }

    [System.Serializable]
    public class PlayerCalibration
    {
        public string participant_id; // Player ID from calibration.
        public int n_windows; // Number of calibration windows.
        public RatioCalibration tbr; // TBR calibration data.
        public RatioCalibration bar; // BAR calibration data.
        public string notes; // Notes saved by Python.
    }

    [Header("Calibration File")]
    [SerializeField] private string calibrationRelativePath =
        "player_data/calibration/current_player_calibration.json"; // Path inside EEG_Data.

    [SerializeField] private bool useCalibrationForBars = true; // Use player thresholds when available.
    [SerializeField] private bool logCalibrationComparison = true; // Print calibration vs runtime values.

    private PlayerCalibration calibration; // Loaded calibration object.
    private bool calibrationLoaded = false; // True when calibration file is valid.

    private static readonly Color GOOD = new Color(0.20f, 0.78f, 0.35f); // Green status color.
    private static readonly Color MID = new Color(0.98f, 0.76f, 0.15f); // Yellow status color.
    private static readonly Color BAD = new Color(0.90f, 0.25f, 0.25f); // Red status color.

    public float CurrentTBR { get; private set; } // Latest smoothed TBR value.
    public float CurrentBAR { get; private set; } // Latest smoothed BAR value.

    public float AverageTBR => sampleCount > 0 ? tbrSum / sampleCount : 0f; // Session average TBR.
    public float AverageBAR => sampleCount > 0 ? barSum / sampleCount : 0f; // Session average BAR.
    public int SampleCount => sampleCount; // Number of saved samples.
    public bool HasData => sampleCount > 0; // True after receiving data.

    private float tbrSum; // Sum of TBR samples.
    private float barSum; // Sum of BAR samples.
    private int sampleCount; // Number of valid samples.
    private bool recording = true; // Stops updates after game-over.
    private bool visualsFrozenForGameOver = false; // Keeps final UI values still.

    private float displayedTbrFill = 0f; // Current TBR UI fill.
    private float displayedBarFill = 0f; // Current BAR UI fill.

    private void Awake()
    {
        Instance = this; // Always let the newest scene tracker become active.
    }

    private void OnDestroy()
    {
        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult -= HandleFocusResult; // Avoid duplicate event listeners.

        if (Instance == this)
            Instance = null; // Clear this tracker when destroyed.
    }

    private void OnEnable()
    {
        if (liveInferenceClient == null)
            liveInferenceClient = LiveInferenceClient.Instance != null
                ? LiveInferenceClient.Instance
                : FindFirstObjectByType<LiveInferenceClient>(); // Find the persistent inference client.

        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult += HandleFocusResult; // Listen for new TBR/BAR values.
        else
            Debug.LogWarning("[BandPowerTracker] LiveInferenceClient not assigned/found.");
    }

    private void OnDisable()
    {
        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult -= HandleFocusResult; // Stop listening when disabled.
    }

    private void RebindToCurrentInferenceClient()
    {
        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult -= HandleFocusResult; // Remove the old connection.

        liveInferenceClient = LiveInferenceClient.Instance != null
            ? LiveInferenceClient.Instance
            : FindFirstObjectByType<LiveInferenceClient>(); // Get the current active client.

        if (liveInferenceClient != null)
        {
            liveInferenceClient.OnFocusResult += HandleFocusResult; // Connect again after scene changes.
            Debug.Log("[BandPowerTracker] Rebound to LiveInferenceClient.");
        }
        else
        {
            Debug.LogWarning("[BandPowerTracker] Rebind failed: LiveInferenceClient not found.");
        }
    }

    private void Start()
    {
        LoadCalibrationFile(); // Load player-specific thresholds.
        Debug.Log("[BandPowerTracker] Using TBR/BAR from Python inference server band_powers. No separate Unity PSD is used.");
    }

    private void HandleFocusResult(FocusResult result)
    {
        if (visualsFrozenForGameOver) return; // Keep final game-over values unchanged.
        if (!recording || result == null) return; // Ignore updates when recording is stopped.

        if (!MeanValid(result.tbr, out float tbrMean) || !MeanValid(result.bar, out float barMean))
        {
            Debug.LogWarning("[BandPowerTracker] Inference result did not contain valid TBR/BAR arrays.");
            return;
        }

        ApplyBandValues(tbrMean, barMean); // Update session values and UI bars.

        if (!string.IsNullOrEmpty(result.error) && logValues)
        {
            Debug.LogWarning(
                "[BandPowerTracker] Bars updated from server band_powers, but prediction was rejected: " +
                result.error
            ); // Bars can update even if gameplay rejects the prediction.
        }
    }

    private static bool MeanValid(float[] values, out float mean)
    {
        mean = 0f;
        if (values == null || values.Length == 0) return false; // No array means no valid ratio.

        float sum = 0f;
        int count = 0;

        for (int i = 0; i < values.Length; i++)
        {
            float v = values[i];
            if (!IsGood(v)) continue; // Skip invalid or non-positive values.
            sum += v;
            count++;
        }

        if (count == 0) return false; // All values were invalid.
        mean = sum / count; // Average valid channel values.
        return true;
    }

    private void ApplyBandValues(float tbr, float bar)
    {
        if (sampleCount == 0)
        {
            CurrentTBR = tbr; // First sample starts without smoothing.
            CurrentBAR = bar;
        }
        else
        {
            CurrentTBR = Mathf.Lerp(CurrentTBR, tbr, valueSmoothing); // Smooth sudden TBR changes.
            CurrentBAR = Mathf.Lerp(CurrentBAR, bar, valueSmoothing); // Smooth sudden BAR changes.
        }

        tbrSum += CurrentTBR; // Add to session average.
        barSum += CurrentBAR;
        sampleCount++;

        UpdateBars(); // Refresh UI fill and text.

        if (logValues)
        {
            Debug.Log(
                "[BandPowerTracker SERVER] #" + sampleCount +
                " TBR=" + CurrentTBR.ToString("F3") +
                " BAR=" + CurrentBAR.ToString("F3")
            );
        }
    }

    private void LoadCalibrationFile()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName; // Unity project folder.
        string calibrationPath = Path.Combine(projectRoot, "..", "EEG_Data", calibrationRelativePath); // Full calibration path.
        calibrationPath = Path.GetFullPath(calibrationPath); // Normalize the path.

        if (!File.Exists(calibrationPath))
        {
            Debug.LogWarning("[BandPowerTracker CALIB] Calibration file not found: " + calibrationPath);
            calibrationLoaded = false;
            return;
        }

        string json = File.ReadAllText(calibrationPath); // Read calibration JSON.
        calibration = JsonUtility.FromJson<PlayerCalibration>(json); // Convert JSON to C# object.
        calibrationLoaded = calibration != null && calibration.tbr != null && calibration.bar != null; // Validate needed ratios.

        if (calibrationLoaded)
        {
            Debug.Log(
                "[BandPowerTracker CALIB] Loaded calibration." +
                " TBR focused=" + calibration.tbr.focused_threshold.ToString("F3") +
                " unfocused=" + calibration.tbr.unfocused_threshold.ToString("F3") +
                " center=" + calibration.tbr.center.ToString("F3") +
                " scale=" + calibration.tbr.scale.ToString("F3") +
                " | BAR focused=" + calibration.bar.focused_threshold.ToString("F3") +
                " unfocused=" + calibration.bar.unfocused_threshold.ToString("F3") +
                " center=" + calibration.bar.center.ToString("F3") +
                " scale=" + calibration.bar.scale.ToString("F3")
            );
        }
    }

    private void UpdateBars()
    {
        float tbrTarget; // Target fill for TBR.
        float barTarget; // Target fill for BAR.

        if (logCalibrationComparison && calibrationLoaded)
            LogCalibrationComparison(); // Print values for debugging calibration.

        float tbrMin, tbrMax, barMin, barMax; // Display ranges for both bars.

        if (useCalibrationForBars && calibrationLoaded)
        {
            float tbrFocused = calibration.tbr.focused_threshold; // Player's focused TBR baseline.
            float tbrScale = Mathf.Max(calibration.tbr.scale, 0.05f); // Avoid too-small scale.
            tbrMin = tbrFocused - tbrScale * 0.5f; // Low TBR keeps the bar mostly empty.
            tbrMax = tbrFocused + 8.0f; // High TBR fills the distracted bar.

            float barFocused = calibration.bar.focused_threshold; // Player's focused BAR baseline.
            barMin = Mathf.Max(barFocused - 1.5f, 0.3f); // Lower bound for BAR display.
            barMax = barFocused + 1.5f; // Upper bound for BAR display.
        }
        else
        {
            tbrMin = 0.5f; // Default TBR lower bound.
            tbrMax = 8.5f; // Default TBR upper bound.
            barMin = 0.8f; // Default BAR lower bound.
            barMax = 1.8f; // Default BAR upper bound.
        }

        tbrTarget = Mathf.Clamp01(Mathf.InverseLerp(tbrMin, tbrMax, CurrentTBR)); // Higher TBR means more distracted.
        barTarget = Mathf.Clamp01(Mathf.InverseLerp(barMin, barMax, CurrentBAR)); // Higher BAR means more focused.

        displayedTbrFill = Mathf.Lerp(displayedTbrFill, tbrTarget, fillSmoothing); // Smooth TBR fill.
        displayedBarFill = Mathf.Lerp(displayedBarFill, barTarget, fillSmoothing); // Smooth BAR fill.

        if (tbrFill != null)
        {
            tbrFill.fillAmount = displayedTbrFill; // Update TBR bar size.
            tbrFill.color = LerpThree(GOOD, MID, BAD, displayedTbrFill); // TBR high becomes red.
        }

        if (barFill != null)
        {
            barFill.fillAmount = displayedBarFill; // Update BAR bar size.
            barFill.color = LerpThree(BAD, MID, GOOD, displayedBarFill); // BAR high becomes green.
        }

        if (tbrLevelText != null)
            tbrLevelText.text = LevelText(displayedTbrFill, isTbrBar: true); // Update TBR label.

        if (barLevelText != null)
            barLevelText.text = LevelText(displayedBarFill, isTbrBar: false); // Update BAR label.
    }

    private static float RatioScoreFromThresholds(float current, float lowThreshold, float highThreshold)
    {
        if (!IsGood(current) || !IsGood(lowThreshold) || !IsGood(highThreshold) ||
            Mathf.Abs(highThreshold - lowThreshold) < 0.000001f)
            return 0f; // Avoid invalid ratio ranges.

        return Mathf.Clamp01(Mathf.InverseLerp(lowThreshold, highThreshold, current)); // Convert value to 0-1.
    }

    private void LogCalibrationComparison()
    {
        string tbrRange = calibration.tbr.summary != null
            ? " range min=" + calibration.tbr.summary.min.ToString("F3") + " max=" + calibration.tbr.summary.max.ToString("F3")
            : ""; // Add TBR range if available.

        string barRange = calibration.bar.summary != null
            ? " range min=" + calibration.bar.summary.min.ToString("F3") + " max=" + calibration.bar.summary.max.ToString("F3")
            : ""; // Add BAR range if available.

        Debug.Log(
            "[BandPowerTracker CHECK] " +
            "CurrentTBR=" + CurrentTBR.ToString("F3") +
            " | TBR" + tbrRange +
            " focused=" + calibration.tbr.focused_threshold.ToString("F3") +
            " unfocused=" + calibration.tbr.unfocused_threshold.ToString("F3") +
            " || CurrentBAR=" + CurrentBAR.ToString("F3") +
            " | BAR" + barRange +
            " focused=" + calibration.bar.focused_threshold.ToString("F3") +
            " unfocused=" + calibration.bar.unfocused_threshold.ToString("F3")
        );
    }

    private static bool IsGood(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f; // Accept only usable positive values.
    }

    private static Color LerpThree(Color low, Color mid, Color high, float t)
    {
        return t < 0.5f
            ? Color.Lerp(low, mid, t * 2f)
            : Color.Lerp(mid, high, (t - 0.5f) * 2f); // Blend between three status colors.
    }

    private static string LevelText(float t, bool isTbrBar)
    {
        if (isTbrBar)
        {
            if (t > 0.66f) return "<color=#E63E3E>Distracted</color>"; // High TBR = distracted.
            if (t > 0.33f) return "<color=#FAC315>Moderate</color>"; // Middle TBR = moderate.
            return "<color=#28C75B>Focused</color>"; // Low TBR = focused.
        }
        else
        {
            if (t > 0.66f) return "<color=#28C75B>Focused</color>"; // High BAR = focused.
            if (t > 0.33f) return "<color=#FAC315>Moderate</color>"; // Middle BAR = moderate.
            return "<color=#E63E3E>Distracted</color>"; // Low BAR = distracted.
        }
    }

    public void ResetSession()
    {
        tbrSum = 0f; // Clear TBR total.
        barSum = 0f; // Clear BAR total.
        sampleCount = 0; // Restart sample count.
        CurrentTBR = 0f; // Reset current TBR.
        CurrentBAR = 0f; // Reset current BAR.
        displayedTbrFill = 0f; // Reset TBR UI.
        displayedBarFill = 0f; // Reset BAR UI.
        visualsFrozenForGameOver = false; // Allow UI to update again.
        recording = true; // Start recording again.
        RebindToCurrentInferenceClient(); // Reconnect after scene/session reset.
    }

    public void FreezeVisualsForGameOver()
    {
        visualsFrozenForGameOver = true; // Keep final values on screen.
        recording = false; // Stop collecting more samples.
    }

    public void StopRecording()
    {
        recording = false; // Stop collecting session averages.
    }
}
