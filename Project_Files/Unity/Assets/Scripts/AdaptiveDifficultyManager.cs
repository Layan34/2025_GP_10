using UnityEngine;

public enum AdaptiveGameMode
{
    Tayyar,
    Rassd,
}

public class AdaptiveDifficultyManager : MonoBehaviour
{
    [Header("EEG Source")]
    [SerializeField] private LiveInferenceClient liveInferenceClient; // Receives focus results from the live EEG inference client.

    [Header("Game Mode")]
    [SerializeField] private AdaptiveGameMode gameMode = AdaptiveGameMode.Tayyar; // Selects which game difficulty logic to control.

    [Header("Timing")]
    [SerializeField] private float updateInterval = 2f; // Minimum time between difficulty updates.

    [Header("Debug")]
    [SerializeField] private string currentZone      = "low"; // Current difficulty zone based on focus.
    [SerializeField] private bool   currentIsFocused = false; // Last focus state received from EEG.
    [SerializeField] private bool   rassdLogicFound  = false; // Shows whether RassdLogic was found.

    private float  lastApplyTime   = -999f; // Last time difficulty was applied.
    private string lastAppliedZone = "low"; // Prevents applying the same zone repeatedly.

    private void Awake()
    {
        if (liveInferenceClient == null)
            liveInferenceClient = FindFirstObjectByType<LiveInferenceClient>(); // Find the inference client if not assigned.
    }

    private void Start()
    {
        rassdLogicFound = RassdLogic.Instance != null; // Check if Rassd difficulty logic is available.

        Debug.Log("[AdaptiveDifficulty] Start | mode=" + gameMode +
                  " | liveInferenceClient=" + (liveInferenceClient != null) +
                  " | RassdLogic=" + rassdLogicFound);
    }

    private void OnEnable()
    {
        if (liveInferenceClient == null)
            liveInferenceClient = FindFirstObjectByType<LiveInferenceClient>(); // Recheck when the object becomes active.

        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult += HandleFocusResult; // Listen for new EEG focus predictions.
        else
            Debug.LogWarning("[AdaptiveDifficulty] LiveInferenceClient not found.");
    }

    private void OnDisable()
    {
        if (liveInferenceClient != null)
            liveInferenceClient.OnFocusResult -= HandleFocusResult; // Stop listening to avoid duplicate callbacks.
    }

    private void HandleFocusResult(FocusResult result)
    {
        if (result == null || !result.predictionReceived)
            return; // Ignore missing or invalid prediction results.

        currentIsFocused = result.isFocused; // Store the latest focus state.
        currentZone      = result.isFocused ? "high" : "low"; // Focused means harder, unfocused means easier.

        if (Time.time - lastApplyTime < updateInterval)
            return; // Avoid changing difficulty too often.

        if (currentZone == lastAppliedZone)
            return; // Do not reapply the same difficulty.

        lastApplyTime = Time.time;
        ApplyDifficulty(currentZone); // Send the new difficulty to the selected game.
        lastAppliedZone = currentZone;
    }

    private void ApplyDifficulty(string zone)
    {
        switch (gameMode)
        {
            case AdaptiveGameMode.Tayyar:
                ApplyTayyarDifficulty(zone); // Apply Tayyar-specific difficulty.
                break;

            case AdaptiveGameMode.Rassd:
                ApplyRassdDifficulty(zone); // Apply Rassd-specific difficulty.
                break;
        }

        if (gameMode == AdaptiveGameMode.Tayyar && FogObstacle.Instance != null)
            FogObstacle.Instance.SetDifficulty(zone); // Match fog difficulty with Tayyar difficulty.

        Debug.Log("[AdaptiveDifficulty] Zone=" + zone +
                  " | isFocused=" + currentIsFocused +
                  " | mode=" + gameMode);
    }

    private void ApplyTayyarDifficulty(string zone)
    {
        Spawner.Instance?.SetDifficulty(zone); // Updates Tayyar spawning speed and object behavior.
    }

    private void ApplyRassdDifficulty(string zone)
    {
        rassdLogicFound = RassdLogic.Instance != null; // Refresh debug status.

        if (RassdLogic.Instance != null)
            RassdLogic.Instance.SetDifficulty(zone); // Updates Rassd stimulus difficulty.
        else
            Debug.LogWarning("[AdaptiveDifficulty] RassdLogic.Instance is NULL.");
    }
}
