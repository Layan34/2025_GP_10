using UnityEngine;
using UnityEngine.SceneManagement;

// Handles Skip and Home button behavior on the Instructions screen
public class SkipButtonScript : MonoBehaviour
{
    [Header("UI Buttons")]
    public GameObject skipButton;
    public GameObject homeButton;

    private bool gameStarted = false;

    void Start()
    {
        // Skip button must ALWAYS be visible based on the user story
        skipButton.SetActive(true);
    }

    // Triggered when Skip is pressed
    public void GoToGame()
    {
        if (gameStarted) return;   // Prevent double-triggering
        gameStarted = true;
        LoadCorrectGameScene();
    }

    // Triggered when Home is pressed
    public void GoHome()
    {
        SceneManager.LoadScene("GameSelection");  // Return to game selection screen
    }

    // Loads the correct game scene based on the user's selection
    private void LoadCorrectGameScene()
    {
        string selectedGame = PlayerPrefs.GetString("SelectedGame", "").Trim().ToLower();

        switch (selectedGame)
        {
            case "skywatcher":
                SceneManager.LoadScene("SkyWatcherGame");
                break;

            case "skyrings":
                SceneManager.LoadScene("SkyRingsGame");
                break;

            default:
                Debug.LogError("ERROR: No valid SelectedGame found for Skip button!");
                break;
        }
    }
}
