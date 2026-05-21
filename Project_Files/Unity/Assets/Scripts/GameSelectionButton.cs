using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSelectionButton : MonoBehaviour
{
    [Header("Game Identifier")]
    public string GameName;

    private void Start()
    {
        // NFR3 - Efficiency: record end time for EnterName->GameSelection transition
        float startTime = PlayerPrefs.GetFloat("SceneLoadStart", -1f);
        string label    = PlayerPrefs.GetString("SceneLoadLabel", "");

        if (startTime >= 0f && label == "EnterName->GameSelection")
        {
            float loadTime = Time.realtimeSinceStartup - startTime;
            NFRLoadTimeLogger.LogTransition(label, loadTime);
            PlayerPrefs.SetFloat("SceneLoadStart", -1f);
        }
    }

    public void OnGameSelected()
    {
        string selectedGame = GameName.Trim().ToLower();

        PlayerPrefs.SetString("SelectedGame", selectedGame);
        PlayerPrefs.Save();

        switch (selectedGame)
        {
            case "tayyar":
                // NFR3 - Efficiency: record transition start time
                PlayerPrefs.SetFloat("SceneLoadStart", Time.realtimeSinceStartup);
                PlayerPrefs.SetString("SceneLoadLabel", "GameSelection->InstructionsGame1");
                SceneManager.LoadScene("InstructionsSceneGame1");
                break;

            case "rassd":
                // NFR3 - Efficiency: record transition start time
                PlayerPrefs.SetFloat("SceneLoadStart", Time.realtimeSinceStartup);
                PlayerPrefs.SetString("SceneLoadLabel", "GameSelection->InstructionsGame2");
                SceneManager.LoadScene("InstructionsSceneGame2");
                break;

            default:
                Debug.LogError("Invalid GameName: " + selectedGame);
                break;
        }
    }
}