using UnityEngine;
using UnityEngine.SceneManagement;

public class SkipButtonScript : MonoBehaviour
{
    [Header("UI Buttons")]
    public GameObject skipButton; // Button used to skip instructions and start the selected game.
    public GameObject homeButton; // Button used to return to game selection.

    private bool gameStarted = false; // Prevents loading the game more than once.

    void Start()
    {
        float startTime = PlayerPrefs.GetFloat("SceneLoadStart", -1f); // Read NFR transition start time.
        string label = PlayerPrefs.GetString("SceneLoadLabel", ""); // Read which transition is being measured.

        if (startTime >= 0f && (label == "GameSelection->InstructionsGame1" || label == "GameSelection->InstructionsGame2"))
        {
            float loadTime = Time.realtimeSinceStartup - startTime; // Calculate scene loading time.
            NFRLoadTimeLogger.LogTransition(label, loadTime);
            PlayerPrefs.SetFloat("SceneLoadStart", -1f); // Reset timer after logging.
        }

        skipButton.SetActive(true); // Keep skip button visible on the instructions screen.
    }

    public void GoToGame()
    {
        if (gameStarted)
            return; // Prevent double clicks from loading twice.

        gameStarted = true;
        LoadCorrectGameScene();
    }

    public void GoHome()
    {
        SceneManager.LoadScene("GameSelection"); // Return to game selection screen.
    }

    private void LoadCorrectGameScene()
    {
        string selectedGame = PlayerPrefs.GetString("SelectedGame", "").Trim().ToLower(); // Read chosen game.

        switch (selectedGame)
        {
            case "rassd":
                PlayerPrefs.SetFloat("SceneLoadStart", Time.realtimeSinceStartup); // Start NFR timer for Rassd load.
                PlayerPrefs.SetString("SceneLoadLabel", "Instructions2->RassdGame");
                SceneManager.LoadScene("RassdGame");
                break;

            case "tayyar":
                SceneManager.LoadScene("TayyarGame");
                break;

            default:
                Debug.LogError("ERROR: No valid SelectedGame found for Skip button!");
                break;
        }
    }
}
