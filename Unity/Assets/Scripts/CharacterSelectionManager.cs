using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSelectionManager : MonoBehaviour
{
    public static string selectedCharacter; // store which character was chosen

    // Called when a character button is clicked
    public void SelectCharacter(string characterName)
    {
        selectedCharacter = characterName;
        Debug.Log("Selected character: " + selectedCharacter);
    }

    // Called by Play button
    public void PlayGame()
    {
        if (!string.IsNullOrEmpty(selectedCharacter))
        {
            Debug.Log("Loading StoryTelling scene with: " + selectedCharacter);
            SceneManager.LoadScene("StoryTelling"); // make sure you have a scene named Game
        }
        else
        {
            Debug.Log("No character selected!"); 
            // optional: show a warning UI instead of just Debug.Log
        }
    }

    // Called by Levels button
    public void GoToLevels()
    {
        Debug.Log("Loading Levels scene...");
        SceneManager.LoadScene("Level"); // make sure you have a scene named Levels
    }
}
