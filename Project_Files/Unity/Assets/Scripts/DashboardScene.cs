using UnityEngine;
using UnityEngine.SceneManagement;

public class DashboardScene : MonoBehaviour
{
    // Navigate back to main game selection menu
    public void GoToDashboardScenee()
    {
        SceneManager.LoadScene("DashboardScene");
    }
}
