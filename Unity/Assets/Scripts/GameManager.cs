using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText;       // Displays the player's score on screen
    public GameObject[] bullets;            // Icons representing remaining tries
    public GameObject[] pigeons;            // Icons representing completed rounds
    public GameObject congratsPanel;        // Panel shown when player wins
    public GameObject gameOverPanel;        // Panel shown when player loses
    public RectTransform crosshair;         // Crosshair UI for aiming

    public GameObject pigeonPrefab;         // Prefab used to spawn pigeons
    public Transform pigeonSpawn;           // Spawn location for pigeons
    public float hitRadiusPixels = 130f;    // Hit radius in pixels for detecting a shot
    public int triesPerRound = 3;           // Number of shots per round
    public int roundsPerLevel = 5;          // Number of rounds per level

    public float pigeonSpeed = 2f;          // Base speed of pigeon movement
    public float changeTargetInterval = 2f; // Interval between direction changes

    private int tries;                      // Remaining tries for the current round
    private int round;                      // Current round number
    private int score = 0;                  // Player’s current score
    private GameObject activePigeon;        // Reference to the active pigeon
    private bool roundActive = false;       // True when round is ongoing
    private Vector3 pigeonTarget;           // Current target position for pigeon wandering
    private float nextTargetTime;           // Time to decide new target
    public bool isPaused = false;           // Prevents shooting when paused

    public GameObject featherImage;         // Feather image shown when pigeon is hit

    // Shooting feedback
    public float crosshairPulseScale = 1f;    // Crosshair grows slightly when shooting
    public float pulseDuration = 0.1f;          // Duration of crosshair pulse
    public float cameraShakeIntensity = 0.06f;  // Intensity of camera shake
    public float cameraShakeDuration = 0.15f;   // Duration of camera shake


    void Start()
    {
        // Ensure that the player character is loaded correctly in the scene
        if (FindFirstObjectByType<CharacterLoader>() == null)
            Debug.LogWarning("No CharacterLoader found in scene!");

        // Begin the first level setup
        StartLevel();
    }

    void Update()
    {
        // Allow shooting only when round is active and game not paused
        if (roundActive && !isPaused && Input.GetKeyDown(KeyCode.Space))
            Shoot(); // Handles shooting and hit detection

        // Move pigeon continuously while active
        if (roundActive && activePigeon != null)
            MovePigeon();

        // Respawn a pigeon automatically if it was destroyed before round ends
        if (roundActive && activePigeon == null && tries > 0)
            SpawnNewPigeon();
    }

    void StartLevel()
    {
        crosshair.gameObject.SetActive(true);   // Show crosshair when level starts
        score = 0;                              // Reset player score
        scoreText.text = "Score:\n0";           // Display initial score
        round = 0;                              // Reset round counter
        NextRound();                            // Start the first round
    }

    void Shoot()
    {
        if (!roundActive || tries <= 0) return; // Prevent shooting if round inactive or no tries

        tries--;                                // Reduce bullet count after each shot
        UpdateBulletsUI();                      // Update bullet icons in UI

        // Trigger visual shooting feedback
        StartCoroutine(CrosshairPulseEffect());
        StartCoroutine(CameraShakeEffect());

        if (activePigeon == null)               // If pigeon already gone, skip to next round
        {
            if (tries <= 0) EndCurrentRound();  // End round if out of tries
            return;
        }

        // Convert crosshair UI position to world coordinates for hit detection
        Vector3 worldPoint = Camera.main.ScreenToWorldPoint(crosshair.position);
        worldPoint.z = 0;

        // Calculate distance between crosshair and pigeon
        float distance = Vector2.Distance(activePigeon.transform.position, worldPoint);
        Debug.Log("Distance: " + distance);     // Log for debugging

        // Check if within hit radius
        if (distance <= hitRadiusPixels / 100f)
        {
            score += 500;
            scoreText.text = "Score:\n" + score;

            featherImage.SetActive(true);       // Show feather effect
            Destroy(activePigeon);              // Remove pigeon
            roundActive = false;

            StartCoroutine(HideFeatherAndNextRound());
        }
        else if (tries <= 0)                    // If missed and no tries left
            EndCurrentRound();                  // End current round
    }

    IEnumerator HideFeatherAndNextRound()
    {
        yield return new WaitForSeconds(0.4f);   // Wait for 0.4 seconds before hiding the feather
        featherImage.SetActive(false);           // Hide feather image
        NextRound();                             // Start the next round
    }

    IEnumerator CrosshairPulseEffect()
    {
        Vector3 originalScale = crosshair.localScale;           // Save original crosshair size
        Vector3 targetScale = originalScale * crosshairPulseScale; // Calculate enlarged size
        crosshair.localScale = targetScale;                     // Apply pulse
        yield return new WaitForSeconds(pulseDuration);         // Wait for pulse duration
        crosshair.localScale = originalScale;                   // Restore original size
    }

    IEnumerator CameraShakeEffect()
    {
        Vector3 originalPos = Camera.main.transform.position;   // Store camera's original position
        float elapsed = 0f;

        while (elapsed < cameraShakeDuration)
        {
            // Generate random offset in X and Y for shaking effect
            float offsetX = Random.Range(-1f, 1f) * cameraShakeIntensity;
            float offsetY = Random.Range(-1f, 1f) * cameraShakeIntensity;
            Camera.main.transform.position = originalPos + new Vector3(offsetX, offsetY, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Camera.main.transform.position = originalPos;           // Reset camera position
    }


    void EndCurrentRound()
    {
        roundActive = false;                    // Deactivate round
        if (activePigeon != null)               // Remove any remaining pigeon
            Destroy(activePigeon);

        Invoke(nameof(NextRound), 0.7f);        // Start next round after short delay
    }

    void NextRound()
    {
        if (round >= roundsPerLevel)            // Check if level completed
        {
            EndLevel();                         // Trigger level-end logic
            return;
        }

        round++;                                // Increment round number
        tries = triesPerRound;                  // Reset bullet count
        UpdateBulletsUI();                      // Refresh bullet UI
        UpdateProgressUI();                     // Update pigeon progress icons
        SpawnNewPigeon();                       // Spawn new pigeon for next round
        roundActive = true;                     // Mark round as active
    }

    void SpawnNewPigeon()
    {
        if (activePigeon != null)               // Clear any existing pigeon
            Destroy(activePigeon);

        // Spawn pigeon slightly outside the right edge of the screen
        Vector3 spawnPos = pigeonSpawn.position;
        spawnPos.x = Camera.main.ViewportToWorldPoint(new Vector3(1.2f, 0.5f, 10f)).x;
        spawnPos.y = Random.Range(-2f, 3f);

        // Create new pigeon at defined position
        activePigeon = Instantiate(pigeonPrefab, spawnPos, Quaternion.identity);

        // Flip sprite so it moves right-to-left
        SpriteRenderer sr = activePigeon.GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipX = true;

        // Convert crosshair position to world coordinates
        Vector3 crosshairWorldPos = Camera.main.ScreenToWorldPoint(crosshair.position);
        crosshairWorldPos.z = spawnPos.z;

        // Start the pigeon flight coroutine
        StartCoroutine(MoveThroughCrosshairThenWander(crosshairWorldPos));
    }

    IEnumerator MoveThroughCrosshairThenWander(Vector3 target)
    {
        roundActive = true;                         // Mark round as active

        float approachSpeed = pigeonSpeed * 1.6f;   // Speed before reaching crosshair
        float pauseDuration = 2f;                   // Wait time at crosshair
        float exitSpeed = pigeonSpeed * 2.5f;       // Speed after passing crosshair

        // Move pigeon toward the crosshair
        while (activePigeon != null && Vector3.Distance(activePigeon.transform.position, target) > 0.2f)
        {
            activePigeon.transform.position = Vector3.MoveTowards(
                activePigeon.transform.position, target, approachSpeed * Time.deltaTime);
            yield return null;
        }

        // Pause briefly at the crosshair
        if (activePigeon != null)
            yield return new WaitForSeconds(pauseDuration);

        // Continue flying left and respawn after leaving screen
        if (activePigeon != null)
        {
            Vector3 exitLeft = target + new Vector3(-12f, Random.Range(-1f, 1f), 0);
            while (activePigeon != null && Vector3.Distance(activePigeon.transform.position, exitLeft) > 0.2f)
            {
                activePigeon.transform.position = Vector3.MoveTowards(
                    activePigeon.transform.position, exitLeft, exitSpeed * Time.deltaTime);
                yield return null;
            }

            if (activePigeon != null)
                SpawnNewPigeon(); // Spawn a new pigeon after exiting the screen
        }
    }

       void MovePigeon()
    {
        if (activePigeon == null) return;  // Skip if pigeon destroyed

        // Use a smoothed (slower) movement speed
        float smoothSpeed = pigeonSpeed * 0.6f;   // 60% of original speed

        // Move pigeon smoothly toward its target position
        activePigeon.transform.position = Vector3.MoveTowards(
            activePigeon.transform.position, pigeonTarget, smoothSpeed * Time.deltaTime);

        // If pigeon reaches target or time to change direction, pick a new target
        if (Time.time >= nextTargetTime || Vector3.Distance(activePigeon.transform.position, pigeonTarget) < 0.3f)
        {
            pigeonTarget = GetRandomTarget();   // Assign a new random target
            nextTargetTime = Time.time + changeTargetInterval;
        }

        // If pigeon flies too far left (off-screen), respawn a new one
        float leftBound = Camera.main.ViewportToWorldPoint(new Vector3(-0.2f, 0, 10f)).x;
        if (activePigeon.transform.position.x < leftBound)
            SpawnNewPigeon();
    }

    Vector3 GetRandomTarget()
    {
        // Get current position to base movement direction
        Vector3 currentPos = activePigeon.transform.position;

        // Define horizontal limits (only move left)
        float screenRight = Camera.main.ViewportToWorldPoint(new Vector3(1.3f, 0, 10f)).x;
        float screenLeft = Camera.main.ViewportToWorldPoint(new Vector3(-0.3f, 0, 10f)).x;

        // Move slightly left each time (never right)
        float x = currentPos.x - Random.Range(1.5f, 3.5f);  // Always move left
        x = Mathf.Clamp(x, screenLeft, screenRight);

        // Random vertical motion within range to create a "floating" movement
        float y = currentPos.y + Random.Range(-1.8f, 1.8f);
        y = Mathf.Clamp(y, -2.5f, 3.5f);  // Keep within visible area

        return new Vector3(x, y, pigeonSpawn.position.z); // Return smooth randomized position
    }

    void EndLevel()
    {
        // Hide crosshair after finishing level
        crosshair.gameObject.SetActive(false);

        // Save player data
        PlayerPrefs.SetString("PlayerName", PlayerPrefs.GetString("PlayerName", "Unknown"));
        PlayerPrefs.SetInt("PlayerScore", score);  
        PlayerPrefs.SetInt("PlayerLevel", 1);      // Replace with actual level if needed
        PlayerPrefs.Save();

        // Show end screen based on score
        if (score >= 1500)
            congratsPanel.SetActive(true);
        else
            gameOverPanel.SetActive(true);
    }

    void UpdateBulletsUI()
    {
        for (int i = 0; i < bullets.Length; i++)
            bullets[i].SetActive(i < tries);        // Activate bullet icons according to remaining tries
    }

    void UpdateProgressUI()
    {
        for (int i = 0; i < pigeons.Length; i++)
            pigeons[i].SetActive(i < round);        // Show pigeon icons for completed rounds
    }
}
