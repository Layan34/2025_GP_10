using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelToHome : MonoBehaviour
{
    public void GoToCharacters()
    {
        SceneManager.LoadScene("Characters"); 
    }
}
