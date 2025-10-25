using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Manages character selection, saving preferences, and navigating between scenes
public class CharacterSelectionManager : MonoBehaviour
{
    [Header("Character Panels")]
    public Button panelMan;    // Button for male character
    public Button panelGirl;   // Button for female character
    public Button panelBoy;    // Button for boy character (default)

    [Header("Main Buttons")]
    public Button playButton;    // Button to start the game
    public Button levelsButton;  // Button to go to levels menu

    [Header("Settings")]
    public string defaultCharacter = "boyB";        // Default character loaded on start
    public string nextSceneName = "StoryTelling";   // Scene to load after selecting a character

    [Header("Panel Colors")]
    [SerializeField] private Color normalColor = new Color(0.92f, 0.9f, 0.85f, 1f);   // Default panel color
    [SerializeField] private Color selectedColor = new Color(0.85f, 0.65f, 0.4f, 1f); // Highlighted panel color

    private string selectedCharacter = "";  // Holds the currently selected character

    void Start()
    {
        // Initialize default character at the start
        selectedCharacter = defaultCharacter;
        PlayerPrefs.SetString("SelectedCharacter", defaultCharacter);
        PlayerPrefs.Save();

        // Add listeners for character panels
        panelMan.onClick.AddListener(() => SelectCharacter(panelMan, "manB"));
        panelGirl.onClick.AddListener(() => SelectCharacter(panelGirl, "girlB"));
        panelBoy.onClick.AddListener(() => SelectCharacter(panelBoy, "boyB"));

        // Add listeners for main control buttons
        playButton.onClick.AddListener(PlayGame);
        levelsButton.onClick.AddListener(GoToLevels);

        // Visually highlight the default character panel
        HighlightPanel(panelBoy);
    }

    // Handles character selection and updates PlayerPrefs
    void SelectCharacter(Button clickedPanel, string characterName)
    {
        selectedCharacter = characterName;                           // Update selected character
        PlayerPrefs.SetString("SelectedCharacter", selectedCharacter); // Save selection
        PlayerPrefs.Save();

        HighlightPanel(clickedPanel);                                // Highlight the selected panel
        Debug.Log("Selected character: " + selectedCharacter);
    }

    // Updates panel colors to visually indicate which character is selected
    void HighlightPanel(Button selectedPanel)
    {
        // Reset all panels to normal color
        panelMan.image.color = normalColor;
        panelGirl.image.color = normalColor;
        panelBoy.image.color = normalColor;

        // Highlight the selected panel
        selectedPanel.image.color = selectedColor;
    }

    // Starts the game and loads the next scene with the selected character
    void PlayGame()
    {
        PlayerPrefs.SetString("SelectedCharacter", selectedCharacter); // Save selection for next scene
        PlayerPrefs.Save();
        Debug.Log("Character saved for next scene: " + selectedCharacter);
        SceneManager.LoadScene(nextSceneName);                         // Load story scene
    }

    // Navigates to the level selection screen
    void GoToLevels()
    {
        SceneManager.LoadScene("Level");
    }
}
