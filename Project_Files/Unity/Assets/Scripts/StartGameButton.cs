using UnityEngine;
using UnityEngine.SceneManagement;

public class StartGameButton : MonoBehaviour
{
    public void GoToInstructions()
    {
        string selectedGame = PlayerPrefs.GetString("SelectedGame", "").Trim().ToLower(); // Read selected game.

        if (string.IsNullOrEmpty(selectedGame))
        {
            Debug.LogError("SelectedGame is EMPTY — restoring default to avoid crash");

            selectedGame = "rassd"; // Safe fallback if no game was selected.
            PlayerPrefs.SetString("SelectedGame", selectedGame);
            PlayerPrefs.Save();
        }

        switch (selectedGame)
        {
            case "rassd":
                SceneManager.LoadScene("InstructionsSceneGame2");
                break;

            case "tayyar":
                SceneManager.LoadScene("InstructionsSceneGame1");
                break;

            default:
                Debug.LogError("ERROR: No valid SelectedGame found!");
                break;
        }
    }
}
