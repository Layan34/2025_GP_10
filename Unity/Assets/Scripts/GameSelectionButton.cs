using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSelectionButton : MonoBehaviour
{
    public string GameName;

    public void OnGameSelected()
    {
        // Store selected game to load its specific setup
        PlayerPrefs.SetString("SelectedGame", GameName);
        PlayerPrefs.Save();

        SceneManager.LoadScene("CharacterScene");
    }
}