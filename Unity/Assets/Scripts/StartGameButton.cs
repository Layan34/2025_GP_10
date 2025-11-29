using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGameButton : MonoBehaviour
{
   public void GoToInstructions()
{
    string selectedGame = PlayerPrefs.GetString("SelectedGame", "").Trim().ToLower();

    if (string.IsNullOrEmpty(selectedGame))
    {
        Debug.LogError("SelectedGame is EMPTY — restoring default to avoid crash");

        // Temporary fallback
        selectedGame = "skywatcher";
        PlayerPrefs.SetString("SelectedGame", selectedGame);
        PlayerPrefs.Save();
    }

    switch (selectedGame)
    {
        case "skywatcher":
            SceneManager.LoadScene("InstructionsSceneGame2");
            break;

        case "skyrings":
            SceneManager.LoadScene("InstructionsSceneGame1");
            break;

        default:
            Debug.LogError("ERROR: No valid SelectedGame found!");
            break;
    }
}

}
