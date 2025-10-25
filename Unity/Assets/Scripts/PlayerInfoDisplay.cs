using UnityEngine;
using TMPro;

// Displays stored player information (name, score, and level) on the screen
public class PlayerInfoDisplay : MonoBehaviour
{
    [Header("Text Fields")]
    public TMP_Text playerInfoText;  // UI text element used to display player data

    void Start()
    {
        // Retrieve previously saved player information (default values if none exist)
        string playerName = PlayerPrefs.GetString("PlayerName", "Unknown");
        int playerScore = PlayerPrefs.GetInt("PlayerScore", 0);
        int playerLevel = PlayerPrefs.GetInt("PlayerLevel", 1);

        // Format and display player information in the text field
        playerInfoText.text =
            $"Player: {playerName}\n" +
            $"Score: {playerScore}\n" +
            $"Level: {playerLevel}";
    }
}
