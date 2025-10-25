using UnityEngine;
using UnityEngine.SceneManagement;

// Manages game navigation and pause functionality
// Handles scene transitions and pause/resume actions
public class GameButtons : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject pausePanel; // Assign the pause menu panel here

    private bool isPaused = false;


    // Loads the main Characters screen and resumes gameplay time
    public void GoToMainScreen()
    {
        // Resume time before switching scenes
        Time.timeScale = 1f;
        SceneManager.LoadScene("Characters");
    }


    // Loads the Level selection scene
    public void GoToLevels()
    {
        SceneManager.LoadScene("Level");
    }

    // Pauses the entire game
    public void PauseGame()
    {
        if (isPaused) return; // Prevent multiple pauses

        isPaused = true;
        FindFirstObjectByType<GameManager>().isPaused = true;

        Time.timeScale = 0f; // Stops all movement and physics
        if (pausePanel != null)
            pausePanel.SetActive(true); // Show the pause menu
        Debug.Log("Game Paused");
    }

    // Resumes the game from where it was paused
    public void ResumeGame()
    {
        // Ensures the pause action only triggers once
        if (!isPaused) return;

        isPaused = false;
        FindFirstObjectByType<GameManager>().isPaused = false;

        Time.timeScale = 1f; // Resume all movement and physics
        if (pausePanel != null)
            pausePanel.SetActive(false); // Hide the pause menu
        Debug.Log("Game Resumed");
    }
}

    
