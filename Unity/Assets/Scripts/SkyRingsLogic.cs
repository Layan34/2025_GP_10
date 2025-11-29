using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManagerOnline : MonoBehaviour
{
    public static GameManagerOnline Instance;

    [Header("Score")]
    public TextMeshProUGUI scoreText;
    private int score = 0;

    [Header("Hearts")]
    public GameObject heart1;
    public GameObject heart2;
    public GameObject heart3;
    private int hearts = 3;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI hitCountText;
    public TextMeshProUGUI missCountText;

    public int HitCount { get; private set; }
    public int MissCount { get; private set; }
    public int FalseAlarmCount { get; private set; } // Wrong triggers

    private void Awake()
    {
        // Singleton reference for global access
        Instance = this; 
    }

    private void Start()
    {
        // Initialize score and hearts
        UpdateScoreUI();
        UpdateHeartsUI();
        gameOverPanel.SetActive(false); // Hide panel at start
    }

    public void RegisterTargetHit()
    {
        HitCount++;
        score += 10;
        UpdateScoreUI();
    }

    public void RegisterMiss()
    {
        MissCount++;
    }

    public void RegisterFalseAlarm()
    {
        FalseAlarmCount++;
        hearts--;
        UpdateHeartsUI();

        if (hearts <= 0)
            // End game
            GameOver();
    }

    // Update score UI -----
    private void UpdateScoreUI()
    {
        scoreText.text = "SCORE: " + score.ToString();
    }

    // Update hearts UI -----
    private void UpdateHeartsUI()
    {
        heart1.SetActive(hearts >= 1);
        heart2.SetActive(hearts >= 2);
        heart3.SetActive(hearts >= 3);
    }

    // Game Over logic
    private void GameOver()
    {
        // Pause game
        Time.timeScale = 0f;

        // Load name
        string name = LoadPlayerNameFromJSON();

        playerNameText.text = "PLAYER: " + name;
        finalScoreText.text = "SCORE: " + score.ToString();
        hitCountText.text = "HIT: " + HitCount.ToString();
        missCountText.text = "MISSES: " + MissCount.ToString() ;

        gameOverPanel.SetActive(true);
    }

    public void GoHome()
    {
        Time.timeScale = 1f; // Resume game
        SceneManager.LoadScene("GameSelection");
    }

    
    string LoadPlayerNameFromJSON()
    {
    string path = System.IO.Path.Combine(Application.persistentDataPath, "playerData.json");

    if (!System.IO.File.Exists(path))
        return "PLAYER";

    string json = System.IO.File.ReadAllText(path);
    EnterNameScript.PlayerData data = JsonUtility.FromJson<EnterNameScript.PlayerData>(json);

    return data.playerName;
    }

    

}
