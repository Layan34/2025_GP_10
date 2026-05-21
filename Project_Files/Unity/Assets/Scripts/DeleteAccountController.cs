using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DeleteAccountController : MonoBehaviour
{
    [Header("Scene After Deletion")]
    [SerializeField] private string startSceneName = "StartScene"; // Scene loaded after account deletion

    public void ConfirmDeleteAccount()
    {
        DeleteSaveFile();     // Remove local save data
        ClearPlayerPrefs();   // Clear stored preferences
        LoadStartScene();     // Redirect to start scene
    }

    private void DeleteSaveFile()
    {
        string path = Path.Combine(Application.persistentDataPath, "rakkiz_save.json");

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    
    private void ClearPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }

    // Loads the initial scene after deletion
    private void LoadStartScene()
    {
        SceneManager.LoadScene(startSceneName);
    }
}
