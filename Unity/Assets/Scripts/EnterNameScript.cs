using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine.UI;

public class EnterNameScript : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField nameInput;      // Input field for player's name
    public TextMeshProUGUI warningText;   // Warning text for invalid name
    public Button startButton;            // Start button
    public string nextSceneName = "GameSelection"; // Scene to load

    private string filePath; // Path for saving JSON file

    [System.Serializable]
    public class PlayerData
    {
        public string playerName;   // Saved player's name
    }

    void Start()
    {
        // Build the file path for saving player data
        filePath = Path.Combine(Application.persistentDataPath, "playerData.json");

        // Hide warning at the beginning
        if (warningText != null)
            warningText.gameObject.SetActive(false);

        // Validate text as user types
        nameInput.onValueChanged.AddListener(delegate { OnNameChanged(); });

        // Load saved name if exists
        LoadSavedName();
    }

    // Triggered whenever the player types in the name field
    void OnNameChanged()
    {
        string currentName = nameInput.text.Trim();

        if (string.IsNullOrWhiteSpace(currentName))
        {
            ShowWarning("Please enter your name.");
            return;
        }

        if (!IsEnglishOnly(currentName))
        {
            ShowWarning("Name must contain English letters only.");
            return;
        }

        warningText.gameObject.SetActive(false);
    }

    // Called when Start button is pressed
    public void StartGame()
    {
        string playerName = nameInput.text.Trim();

        if (string.IsNullOrWhiteSpace(playerName))
        {
            ShowWarning("Please enter your name.");
            return;
        }

        if (!IsEnglishOnly(playerName))
        {
            ShowWarning("Name must contain English letters only.");
            return;
        }

        // Save player name and load next scene
        SaveNameToJSON(playerName);
        SceneManager.LoadScene(nextSceneName);
    }

     // Store name in external JSON file
    void SaveNameToJSON(string name)
    {
        PlayerData data = new PlayerData();
        data.playerName = name;

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(filePath, json);
    }

    // Load name if user opened the game before
    void LoadSavedName()
    {
        if (!File.Exists(filePath))
            return;

        string json = File.ReadAllText(filePath);
        PlayerData loadedData = JsonUtility.FromJson<PlayerData>(json);

        if (!string.IsNullOrEmpty(loadedData.playerName))
        {
            // Fill the name field with saved value
            nameInput.text = loadedData.playerName;

            warningText.gameObject.SetActive(false);
        }
    }

    // Only accept alphabetical English input
    bool IsEnglishOnly(string name)
    {
        return Regex.IsMatch(name, @"^[a-zA-Z ]+$");
    }

    // Show warning message
    void ShowWarning(string msg)
    {
        warningText.gameObject.SetActive(true);
        warningText.text = msg;
    }
    
}
