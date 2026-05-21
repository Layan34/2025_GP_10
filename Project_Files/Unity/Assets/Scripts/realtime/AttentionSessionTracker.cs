using UnityEngine;

public class AttentionSessionTracker : MonoBehaviour
{
    [SerializeField] private LiveInferenceClient liveInferenceClient; // Source of live focus results.

    private float attentionSum = 0f; // Sum of all focus values.
    private int attentionCount = 0; // Number of valid focus samples.

    public static AttentionSessionTracker Instance { get; private set; } // Easy access from other scripts.

    public float AverageAttention => attentionCount > 0 ? attentionSum / attentionCount : 0f; // Current average focus.
    public int SampleCount => attentionCount; // Number of samples used in the average.
    public bool HasData => attentionCount > 0; // True after at least one valid result.

    public static float CurrentAverage =>
        Instance != null && Instance.attentionCount > 0
            ? Instance.attentionSum / Instance.attentionCount
            : 0f; // Safe global average value.

    public static bool CurrentHasData =>
        Instance != null && Instance.attentionCount > 0; // Safe global data check.

    private void Awake()
    {
        Instance = this; // Register this tracker for global access.
    }

    private void OnEnable()
    {
        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult += HandleFocusResult; // Listen for new focus predictions.
    }

    private void OnDisable()
    {
        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult -= HandleFocusResult; // Stop listening when disabled.
    }

    private void HandleFocusResult(FocusResult result)
    {
        if (result == null || !result.IsValid)
            return; // Ignore missing or rejected predictions.

        float value = result.isFocused ? 1f : 0f; // Convert focused/unfocused to 1 or 0.

        attentionSum += value; // Add this sample to the total.
        attentionCount++; // Count this valid sample.

        Debug.Log($"[AttentionTracker] obj={gameObject.name} id={GetInstanceID()} value={value} count={attentionCount} avg={AverageAttention}");
    }
}
