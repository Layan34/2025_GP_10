using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.IO;


// Controls the start screen: manages player name input
// saves both name and level in JSON, and activates Start button logic
public class StartMenu : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField nameInputField;      // Input field for player name
    public Button startButton;                 // Start button (always visible)
    public TextMeshProUGUI warningText;        // Warning or status text
    public string nextSceneName = "Characters"; // Next scene to load

    private string filePath; // Path for saving JSON data

    [System.Serializable]
    public class PlayerData
    {
        public string playerName;
        public int playerLevel;
    }

    void Start()
    {
        filePath = Path.Combine(Application.persistentDataPath, "playerData.json");
        InitializeUI();
    }

    
    // Initializes UI state and loads saved data if available
    void InitializeUI()
    {
        PlayerData savedData = LoadPlayerData();

        // Fill input field if name exists
        nameInputField.text = savedData.playerName;

        // Keep Start button visible but only clickable if name exists
        startButton.interactable = !string.IsNullOrWhiteSpace(savedData.playerName);

        // Hide warning at start
        warningText.text = "";

        // Listeners
        nameInputField.onValueChanged.AddListener(delegate { OnNameChanged(); });
        startButton.onClick.AddListener(OnStartClicked);
    }

  
    // Updates the Start button's state dynamically based on player input
    void OnNameChanged()
    {
        string currentName = nameInputField.text.Trim();
        bool hasName = !string.IsNullOrWhiteSpace(currentName);

        startButton.interactable = hasName;
        warningText.text = hasName ? "" : "Please enter your name";
    }


    // Triggered when Start button is pressed
    void OnStartClicked()
    {
        string playerName = nameInputField.text.Trim();

        if (string.IsNullOrWhiteSpace(playerName))
        {
            warningText.text = "Please enter your name first!";
            return;
        }

        // If player is new, start at Level 1; otherwise keep current level
        PlayerData existingData = LoadPlayerData();
        int playerLevel = existingData.playerLevel > 0 ? existingData.playerLevel : 1;

        SavePlayerData(playerName, playerLevel);
        SceneManager.LoadScene(nextSceneName);
    }


    /// Saves player name and level into JSON
    void SavePlayerData(string name, int level)
    {
        PlayerData data = new PlayerData { playerName = name, playerLevel = level };
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(filePath, json);

        Debug.Log($" Player data saved to: {filePath}\n{json}");
    }


    // Loads player data (name + level) if file exists; otherwise returns default.
    PlayerData LoadPlayerData()
    {
        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);
            PlayerData data = JsonUtility.FromJson<PlayerData>(json);
            return data;
        }

        // Default for first-time players
        return new PlayerData { playerName = "", playerLevel = 1 };
    }
}
