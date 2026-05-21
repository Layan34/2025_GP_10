using UnityEngine;

public class DashboardSceneNFR : MonoBehaviour
{
    private void Start()
    {
        float startTime = PlayerPrefs.GetFloat("SceneLoadStart", -1f);
        string label    = PlayerPrefs.GetString("SceneLoadLabel", "");

        if (startTime >= 0f && label == "Settings->Dashboard")
        {
            float loadTime = Time.realtimeSinceStartup - startTime;
            NFRLoadTimeLogger.LogTransition(label, loadTime);
            PlayerPrefs.SetFloat("SceneLoadStart", -1f);
        }
    }
}