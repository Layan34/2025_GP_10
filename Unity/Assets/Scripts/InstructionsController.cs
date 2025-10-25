using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class InstructionsController : MonoBehaviour
{
    // Reference to the "Next" button assigned from the Unity Inspector
    public Button nextButton;

    // Timer to track how long the player stays on the instruction screen
    private float timer = 0f;

    void Start()
    {
        // At the start, hide the "Next" button until a few seconds pass
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);

            // Add listener to the button that will load the Game scene when clicked
            nextButton.onClick.AddListener(GoToGame);
        }
        else
        {
            // Display an error in the Console if the button is not linked in the Inspector
            Debug.LogError("Next button is not assigned in the Inspector!");
        }
    }

    void Update()
    {
        // Increment the timer based on real-time seconds
        timer += Time.deltaTime;

        // After 5 seconds, make the "Next" button visible to the player
        if (timer >= 5f && nextButton != null && !nextButton.gameObject.activeSelf)
        {
            nextButton.gameObject.SetActive(true);
            Debug.Log("Next button is now visible");
        }

        // Automatically transition to the Game scene after 30 seconds
        if (timer >= 30f)
        {
            GoToGame();
        }
    }

    // Loads the main Game scene
    private void GoToGame()
    {
        Debug.Log("Loading Game scene...");
        SceneManager.LoadScene("Game"); // The scene name must match exactly in the Build Settings
    }
}
