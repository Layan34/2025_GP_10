using UnityEngine;
using UnityEngine.SceneManagement;

// Controls pause/resume behavior during gameplay
public class PauseMenu : MonoBehaviour
{
    public GameObject container;  

    public void PauseGame()
    {
        container.SetActive(true); 
        
        // Freeze the game
        Time.timeScale = 0f; 
    }

    // Hide the container and resume the game
    public void ResumeGame()
    {
        container.SetActive(false);

        // Unfreeze the game
        Time.timeScale = 1f;
    }

    public void GoHome()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("GameSelection");
    }
}
