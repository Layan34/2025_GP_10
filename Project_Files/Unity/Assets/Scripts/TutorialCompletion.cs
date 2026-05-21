using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TutorialCompletion : MonoBehaviour
{
    [SerializeField] private string saveFileName = "rakkiz_save.json"; // Save file where tutorial status is stored.
    [SerializeField] private bool markSeenOnSceneStart = true; // Marks tutorial as seen when this scene starts.

    private void Start()
    {
        float startTime = PlayerPrefs.GetFloat("SceneLoadStart", -1f); // Read NFR transition start time.
        string label = PlayerPrefs.GetString("SceneLoadLabel", ""); // Read transition label.

        if (startTime >= 0f && label == "EnterName->TutorialScene")
        {
            float loadTime = Time.realtimeSinceStartup - startTime; // Calculate load duration.
            NFRLoadTimeLogger.LogTransition(label, loadTime);
            PlayerPrefs.SetFloat("SceneLoadStart", -1f); // Reset timer after logging.
        }

        if (markSeenOnSceneStart)
            MarkSeenOnly(); // Save that tutorial has been reached/seen.
    }

    public void OnTutorialFinished()
    {
        MarkSeenOnly(); // Save tutorial completion when finished.
    }

    private void MarkSeenOnly()
    {
        var repo = new RakkizSaveRepository(saveFileName);
        repo.MarkTutorialSeen(); // Update save file tutorial flag.
    }
}
