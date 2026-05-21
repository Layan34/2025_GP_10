using UnityEngine;
using UnityEngine.SceneManagement;

public class SettingsBackButton : MonoBehaviour
{
    [SerializeField] private string fallbackSceneName = "GameSelection";

    public void GoBack()
    {
        var target = SceneNavigationState.PreviousSceneName;

        if (string.IsNullOrWhiteSpace(target))
        {
            target = fallbackSceneName;
        }

        SceneManager.LoadScene(target);
    }
}
