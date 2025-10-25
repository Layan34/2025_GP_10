using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelToHome : MonoBehaviour
{
    // This method is used to navigate back to the Characters scene
    public void GoToCharacters()
    {
        // Load the scene named Characters
        SceneManager.LoadScene("Characters");
    }
}
