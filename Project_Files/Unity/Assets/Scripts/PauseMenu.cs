using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject container; // Pause menu panel.

    public void PauseGame()
    {
        bool ended = (TayyarLogic.Instance != null && TayyarLogic.Instance.IsSessionEnded)
                  || (RassdLogic.Instance  != null && RassdLogic.Instance.IsSessionEnded);

        if (ended)
            return; // Do not open pause menu after game over.

        container.SetActive(true);

        if (Time.timeScale > 0f)
            Time.timeScale = 0f; // Pause gameplay.
    }

    public void ResumeGame()
    {
        container.SetActive(false);

        bool ended = (TayyarLogic.Instance != null && TayyarLogic.Instance.IsSessionEnded)
                  || (RassdLogic.Instance  != null && RassdLogic.Instance.IsSessionEnded);

        if (!ended)
            Time.timeScale = 1f; // Resume only if the session is not over.
    }

    public void GoHome()
    {
        Time.timeScale = 1f; // Reset time before changing scene.
        SceneManager.LoadScene("GameSelection");
    }

    public void GoDashboardScene()
    {
        Time.timeScale = 1f; // Reset time before changing scene.
        SceneManager.LoadScene("DashboardScene");
    }
}
