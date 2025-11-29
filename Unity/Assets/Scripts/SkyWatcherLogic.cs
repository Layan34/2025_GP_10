using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class SkyWatcherLogic : MonoBehaviour
{
    [Header("Stimulus")]
    public GameObject falconPrefab; 
    public Transform topPos;
    public Transform bottomPos;
    public Transform crossSign;

    [Header("UI")]
    public TextMeshProUGUI scoreText;
    public GameObject heart1;
    public GameObject heart2;
    public GameObject heart3;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI hitsText;
    public TextMeshProUGUI missText;

    [Header("Timing")]
    public float trialDuration = 2f;
    public float stimDuration = 0.1f;

    [Header("Countdown")]
    public GameObject countdownPanel;
    public TextMeshProUGUI countdownText;


    // Internal state variables
    int score = 0;
    int hearts = 3;
    int hitCount = 0;
    int missCount = 0;
    // Non-target clicked (wrong response)
    int falseAlarmCount = 0;

    GameObject currentStimulus;
    bool isTarget = false;
    bool responded = false;
    bool inputLocked = true;


    void Start()
    {
        // Ensure game runs normally
        Time.timeScale = 1f;

        UpdateScore();
        UpdateHearts();
        gameOverPanel.SetActive(false);

        // Begin countdown
        StartCoroutine(StartCountdown());
    }

    IEnumerator StartCountdown()
    {
        countdownPanel.SetActive(true);

        countdownText.text = "3";
        yield return new WaitForSeconds(1f);

        countdownText.text = "2";
        yield return new WaitForSeconds(1f);

        countdownText.text = "1";
        yield return new WaitForSeconds(1f);

        countdownText.text = "GO!";
        yield return new WaitForSeconds(0.5f);

        countdownPanel.SetActive(false);
        inputLocked = false;   // Now player can press Space
        

        //Begin trial sequence
        StartCoroutine(RunTrials());
    }

    IEnumerator RunTrials()
    {
        // Loop until player loses all hearts
        while (hearts > 0)
        {
            responded = false;
            SpawnStimulus();

            float elapsed = 0f;

            while (elapsed < trialDuration && hearts > 0)
            {
                // After stimulus duration, hide the falcon
                if (elapsed >= stimDuration && currentStimulus != null && currentStimulus.activeSelf)
                    currentStimulus.SetActive(false);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Remove the falcon after trial ends
            if (currentStimulus != null)
                Destroy(currentStimulus);

            // If trial was target and player did not click count as miss
            if (!responded && isTarget)
                missCount++;
        }

        // End game when hearts reach 0
        TriggerGameOver();
    }

    void SpawnStimulus()
    {
        // Choose target or non-target
        isTarget = Random.Range(0, 2) == 0;

        Vector3 pos = isTarget ? topPos.position : bottomPos.position;

        // Spawn falcon
        currentStimulus = Instantiate(falconPrefab, pos, Quaternion.identity);
    }

    void Update()
    {
    if (hearts <= 0 || responded || inputLocked) return;
        // Keyboard response
    if (Input.GetKeyDown(KeyCode.Space))
    {
        RegisterResponse();
        return;
    }
    }


    void RegisterResponse()
    {
        // Prevent double responses
        if (responded) return;

        responded = true;

        if (isTarget)
        {
            hitCount++;
            score += 10;
            UpdateScore();
        }
        else
        {
            falseAlarmCount++;
            hearts--;
            UpdateHearts();

            // End game if no hearts left
            if (hearts <= 0)
            {
                TriggerGameOver();
                return;
            }
        }
    }

    void UpdateScore()
    {
        scoreText.text = "SCORE: " + score;
    }

    void UpdateHearts()
    {
        heart1.SetActive(hearts >= 1);
        heart2.SetActive(hearts >= 2);
        heart3.SetActive(hearts >= 3);
    }

    void TriggerGameOver()
    {
        // Pause game
        Time.timeScale = 0f;

        string n = LoadPlayerNameFromJSON();

        playerNameText.text = "PLAYER: " + n;
        finalScoreText.text = "SCORE: " + score;
        hitsText.text = "HIT: " + hitCount;
        missText.text = "MISS: " + missCount;

        gameOverPanel.SetActive(true);

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

