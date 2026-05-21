using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenSettings : MonoBehaviour
{
    [SerializeField] private string settingsSceneName = "Settings"; // Scene opened when pressing Settings.

    public void GoToSettings()
    {
        SceneNavigationState.PreviousSceneName = SceneManager.GetActiveScene().name; // Remember where to return.

        Time.timeScale = 1f; // Make sure the game is not paused before loading.

        PlayerPrefs.SetFloat("SceneLoadStart", Time.realtimeSinceStartup); // Start NFR load-time timer.
        PlayerPrefs.SetString("SceneLoadLabel", "TayyarGame->Settings"); // Label this transition.

        SceneManager.LoadScene(settingsSceneName);
    }
}
