using UnityEngine;
using UnityEngine.SceneManagement;

public class SettingsSceneNFR : MonoBehaviour
{
    [SerializeField] private string dashboardSceneName = "DashboardScene"; // Dashboard scene loaded from Settings.

    private void Start()
    {
        float startTime = PlayerPrefs.GetFloat("SceneLoadStart", -1f); // Read transition start time.
        string label    = PlayerPrefs.GetString("SceneLoadLabel", ""); // Read transition name.

        if (startTime >= 0f && label == "TayyarGame->Settings")
        {
            float loadTime = Time.realtimeSinceStartup - startTime; // Calculate scene load duration.
            NFRLoadTimeLogger.LogTransition(label, loadTime);
            PlayerPrefs.SetFloat("SceneLoadStart", -1f); // Reset timer after logging.
        }
    }

    public void GoToDashboard()
    {
        PlayerPrefs.SetFloat("SceneLoadStart", Time.realtimeSinceStartup); // Start timing Settings to Dashboard.
        PlayerPrefs.SetString("SceneLoadLabel", "Settings->Dashboard");

        SceneManager.LoadScene(dashboardSceneName);
    }
}
