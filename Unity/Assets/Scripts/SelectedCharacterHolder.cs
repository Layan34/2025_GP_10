using UnityEngine;

// Holds the selected character between scenes
public class SelectedCharacterHolder : MonoBehaviour
{
    public static SelectedCharacterHolder Instance;

    public string selectedCharacterName = "";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
