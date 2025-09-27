using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class InstructionsController : MonoBehaviour
{
    public Button nextButton;   // Assign this in the Inspector
    private float timer = 0f;

    void Start()
    {
        // Hide the Next button at the start
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
            nextButton.onClick.AddListener(GoToGame);
        }
        else
        {
            Debug.LogError("Next button is not assigned in the Inspector!");
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        // After 5 seconds, show the Next button
        if (timer >= 5f && nextButton != null && !nextButton.gameObject.activeSelf)
        {
            nextButton.gameObject.SetActive(true);
            Debug.Log("Next button is now visible");
        }

        // After 30 seconds, automatically go to the game
        if (timer >= 30f)
        {
            GoToGame();
        }
    }

    private void GoToGame()
    {
        Debug.Log("Loading Game scene...");
        SceneManager.LoadScene("Game"); // Scene name must match exactly
    }
}
