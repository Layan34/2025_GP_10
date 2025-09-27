using UnityEngine;
using UnityEngine.SceneManagement;

public class gameTohome : MonoBehaviour
{
     public void GoToMainScreen()
    {
        SceneManager.LoadScene("Characters");
    }

    public void GoToLevels()
    {
        SceneManager.LoadScene("Level"); 
    }
}
