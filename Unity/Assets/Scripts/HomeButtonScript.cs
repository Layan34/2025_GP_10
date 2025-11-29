using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeButtonScript : MonoBehaviour
{
    // Navigate back to main game selection menu
    public void GoToGameSelectionScene()
    {
        SceneManager.LoadScene("GameSelection");
    }
}

