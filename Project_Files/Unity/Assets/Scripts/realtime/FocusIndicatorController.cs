using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class FocusIndicatorController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Image outerCircle; // Outer visual ring of the focus indicator.
    [SerializeField] private Image innerCircle; // Inner circle that follows the focus color.
    [SerializeField] private Image brainIcon;   // Brain icon shown inside the indicator.

    [Header("EEG Source")]
    [SerializeField] private LiveInferenceClient liveInferenceClient; // Sends live focus predictions from Python.

    [Header("Colors")]
    [SerializeField] private Color focusedColor   = new Color(0.25f, 0.75f, 0.25f);
    [SerializeField] private Color unfocusedColor = new Color(0.85f, 0.20f, 0.20f);
    [SerializeField] private Color waitingColor   = new Color(0.50f, 0.50f, 0.50f);

    [Header("Inner Circle Tint")]
    [Range(0f, 1f)]
    [SerializeField] private float innerTintAmount = 0.15f;

    [Header("Pulse")]
    [SerializeField] private float focusedPulseSpeed   = 1.4f;
    [SerializeField] private float unfocusedPulseSpeed = 2.2f;
    [Range(0f, 1f)]
    [SerializeField] private float pulseAlphaMin = 0.35f;

    [Header("Signal Timeout")]
    [SerializeField] private float signalTimeoutSec = 30f; // Gray out if no prediction arrives for this time.

    [Header("Signal Quality")]
    [SerializeField] private int grayOutAfterRejectedWindows = 5; // Gray out after repeated invalid predictions.

    private Coroutine pulseCoroutine;
    private Coroutine delayedRebindCoroutine;
    private bool      subscribed;
    private bool      isFocused;
    private bool      hasFirstResult;
    private bool      isConnected;
    private float     lastResultTime;
    private int       consecutiveRejectedWindows = 0;
    private bool      isGrayedOut = false;
    private bool      visualsFrozenForGameOver = false;

    public bool IsFocused       => isFocused;
    public bool HasFirstResult  => hasFirstResult;

        private void Awake()
    {
        if (liveInferenceClient == null) // Use the persistent inference client if no client was assigned.
            liveInferenceClient = LiveInferenceClient.Instance != null
                ? LiveInferenceClient.Instance
                : FindFirstObjectByType<LiveInferenceClient>();
    }

    private void OnEnable()
    {
        ResetIndicatorState(); // Start each scene with a waiting visual state.
        RebindToCurrentInferenceClient(); // Connect this UI to the active inference client.

        delayedRebindCoroutine = StartCoroutine(RebindNextFrame()); // Rebind again after scene objects finish loading.
    }

    private void OnDisable()
    {
        if (delayedRebindCoroutine != null)
        {
            StopCoroutine(delayedRebindCoroutine);
            delayedRebindCoroutine = null;
        }

        UnsubscribeFromInferenceClient(); // Avoid duplicate event calls after leaving the scene.

        HitBasedDifficultyManager.Instance?.OnEegSignalLost(); // Use behavior-only difficulty when EEG is unavailable.

        StopPulse();
    }

    private void Update()
    {
        if (visualsFrozenForGameOver) return; // Keep final game-over visuals unchanged.
        if (!isConnected || !hasFirstResult) return;

        if (Time.time - lastResultTime > signalTimeoutSec) // Detect lost EEG/server updates.
        {
            hasFirstResult = false;
            SetState(waitingColor, false); // Show neutral state until a valid prediction arrives.
            HitBasedDifficultyManager.Instance?.OnEegSignalLost(); // Use behavior-only difficulty when EEG is unavailable.
            Debug.Log($"[FocusIndicator] Timeout: no prediction for {signalTimeoutSec}s.");
        }
    }

        private IEnumerator RebindNextFrame()
    {
        yield return null; // Wait one frame before rebinding.
        RebindToCurrentInferenceClient(); // Connect this UI to the active inference client.
        delayedRebindCoroutine = null;
    }

    private void RebindToCurrentInferenceClient()
    {
        LiveInferenceClient currentClient = LiveInferenceClient.Instance != null
            ? LiveInferenceClient.Instance
            : FindFirstObjectByType<LiveInferenceClient>();

        if (currentClient == null) // Cannot update EEG visuals without the inference client.
        {
            Debug.LogWarning("[FocusIndicator] LiveInferenceClient not assigned/found.");
            return;
        }

        if (liveInferenceClient == currentClient && subscribed)
        {
            isConnected = liveInferenceClient.IsConnected;
            return;
        }

        UnsubscribeFromInferenceClient(); // Avoid duplicate event calls after leaving the scene.

        liveInferenceClient = currentClient;
        liveInferenceClient.OnFocusResult       += HandleFocusResult;       // Listen for focus predictions.
        liveInferenceClient.OnConnectionChanged += HandleConnectionChanged; // Listen for connection status.
        subscribed = true;

        isConnected = liveInferenceClient.IsConnected;

        if (liveInferenceClient.LastResult != null && liveInferenceClient.LastResult.IsValid)
            ApplyFocusResultToVisuals(liveInferenceClient.LastResult);

        Debug.Log("[FocusIndicator] Bound to current LiveInferenceClient.");
    }

    private void UnsubscribeFromInferenceClient()
    {
        if (liveInferenceClient != null && subscribed)
        {
            liveInferenceClient.OnFocusResult       -= HandleFocusResult;
            liveInferenceClient.OnConnectionChanged -= HandleConnectionChanged;
        }

        subscribed = false;
    }

    private void ResetIndicatorState()
    {
        visualsFrozenForGameOver = false;
        consecutiveRejectedWindows = 0;
        isGrayedOut    = false;
        hasFirstResult = false;
        isFocused      = false;
        lastResultTime = Time.time;
        SetState(waitingColor, false); // Show neutral state until a valid prediction arrives.
    }

        private void HandleConnectionChanged(bool connected)
    {
        isConnected = connected;

        if (!connected)
        {
            hasFirstResult = false;
            SetState(waitingColor, false); // Show neutral state until a valid prediction arrives.
            HitBasedDifficultyManager.Instance?.OnEegSignalLost(); // Use behavior-only difficulty when EEG is unavailable.
        }

        Debug.Log($"[FocusIndicator] Connected={connected}");
    }

    private void HandleFocusResult(FocusResult result)
    {
        if (visualsFrozenForGameOver) return; // Keep final game-over visuals unchanged.
        if (result == null) return;

        EegLatencyTracker.LogLatency(Time.time - EegLatencyTracker.SignalTime); // Record EEG-to-game reaction time.

        if (result.predictionReceived && string.IsNullOrEmpty(result.error))
        {
            ApplyFocusResultToVisuals(result); // Update color and pulse based on focus result.

            HitBasedDifficultyManager.Instance?.OnEegPrediction(result.prediction); // Send EEG focus result to difficulty logic.

            Debug.Log($"[FocusIndicator] prediction={result.prediction} " +
                      $"model={result.modelPrediction} source={result.decisionSource} " +
                      $"confidence={result.confidence:F2} " +
                      $"outliers={result.runtimeOutliersP01P99}/70 " +
                      $"-> {(isFocused ? "FOCUSED" : "UNFOCUSED")} " +
                      $"| Routed to DifficultyManager");
            return;
        }

        consecutiveRejectedWindows++; // Count invalid windows before graying out.

        if (consecutiveRejectedWindows >= grayOutAfterRejectedWindows && !isGrayedOut)
        {
            isGrayedOut    = true;
            hasFirstResult = false;
            SetState(waitingColor, false); // Show neutral state until a valid prediction arrives.
            HitBasedDifficultyManager.Instance?.OnEegSignalLost(); // Use behavior-only difficulty when EEG is unavailable.

            Debug.LogWarning($"[FocusIndicator] {consecutiveRejectedWindows} consecutive " +
                             $"rejected windows — indicator gray. Error={result.error}");
        }
    }

    private void ApplyFocusResultToVisuals(FocusResult result)
    {
        consecutiveRejectedWindows = 0;
        isGrayedOut    = false;
        lastResultTime = Time.time;
        hasFirstResult = true;
        isFocused      = result.isFocused;

        Color color      = isFocused ? focusedColor : unfocusedColor;
        float pulseSpeed = isFocused ? focusedPulseSpeed : unfocusedPulseSpeed;
        SetState(color, true, pulseSpeed);
    }

    public void FreezeVisualsForGameOver()
    {
        visualsFrozenForGameOver = true;
        StopPulse();
    }

        private void SetState(Color color, bool pulse, float pulseSpeed = 1.4f)
    {
        if (outerCircle != null) outerCircle.color = color;
        if (innerCircle != null) innerCircle.color = Color.Lerp(Color.white, color, innerTintAmount);
        if (brainIcon   != null) brainIcon.color   = color;

        StopPulse();
        if (pulse)
            pulseCoroutine = StartCoroutine(PulseRing(color, pulseSpeed));
    }

    private void StopPulse()
    {
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        if (outerCircle != null)
        {
            Color c = outerCircle.color;
            c.a = 1f;
            outerCircle.color = c;
        }
    }

    private IEnumerator PulseRing(Color baseColor, float cycleDuration)
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(
                pulseAlphaMin,
                1f,
                (Mathf.Sin((t / cycleDuration) * Mathf.PI * 2f) + 1f) * 0.5f
            );

            Color c = baseColor;
            c.a = alpha;
            if (outerCircle != null) outerCircle.color = c;
            yield return null; // Wait one frame before rebinding.
        }
    }
}
