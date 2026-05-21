using UnityEngine;
using TMPro;

public class Spawner : MonoBehaviour
{
    public static Spawner Instance;

    public GameObject ringPrefab; // Target object.
    public GameObject bombPrefab; // Non-target object.

    public int totalObjects = 300; // Session ends after this number of spawned objects.
    private int spawnedCount = 0; // Number of objects spawned so far.

    public float spawnX = 270f; // X position where objects appear.
    public float interval = 0.8f; // Time between spawns.
    public float minY = -50f; // Lowest random spawn Y.
    public float maxY = 50f; // Highest random spawn Y.
    public float bombProbability = 0.4f; // Chance of spawning a bomb.

    private bool isSpawning = false; // Prevents starting multiple spawn loops.

    public TextMeshProUGUI trialCounterText; // Shows current object count.

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(Instance.gameObject); // Keep only one spawner.

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null; // Clear singleton reference.
    }

    private void Start()
    {
        spawnedCount = 0; // Reset count for each scene load.
        isSpawning = false;
        StartSpawning();
    }

    private void StartSpawning()
    {
        if (isSpawning)
            return; // Already spawning.

        isSpawning = true;
        CancelInvoke(nameof(SpawnObject));
        SpawnObject(); // Spawn first object immediately.
        InvokeRepeating(nameof(SpawnObject), interval, interval); // Continue spawning repeatedly.
    }

    public void SetDifficulty(string zone)
    {
        switch (zone)
        {
            case "low":
                interval = 2.5f;
                bombProbability = 0.25f;
                break;

            case "high":
                interval = 1.2f;
                bombProbability = 0.60f;
                break;

            default:
                interval = 2.5f;
                bombProbability = 0.25f;
                break;
        }

        RestartSpawnLoop(); // Apply the new interval.
        Debug.Log($"[Spawner] EEG Zone={zone} | Interval={interval} | Bomb%={bombProbability}");
    }

    public void SetDifficultyByLevel(string level)
    {
        switch (level)
        {
            case "easy":
                bombProbability = 0.35f;
                break;

            case "medium":
                bombProbability = 0.50f;
                break;

            case "hard":
                bombProbability = 0.65f;
                break;

            default:
                bombProbability = 0.35f;
                break;
        }

        RestartSpawnLoop(); // Keep the spawn loop updated after difficulty changes.
        Debug.Log($"[Spawner] Hit Level={level} | Interval={interval} | Bomb%={bombProbability}");
    }

    private void RestartSpawnLoop()
    {
        if (isSpawning)
        {
            CancelInvoke(nameof(SpawnObject)); // Stop old timing.
            InvokeRepeating(nameof(SpawnObject), interval, interval); // Restart with the current interval.
        }
    }

    private void SpawnObject()
    {
        if (TayyarLogic.Instance != null && TayyarLogic.Instance.IsSessionEnded)
        {
            StopSpawning(); // Stop spawning after game over.
            return;
        }

        if (spawnedCount >= totalObjects)
        {
            CancelInvoke(nameof(SpawnObject));
            TayyarLogic.Instance?.GameOver(); // End session when all objects are spawned.
            return;
        }

        spawnedCount++;

        if (trialCounterText != null)
            trialCounterText.text = $"{spawnedCount}/{totalObjects}";

        Vector3 pos = new Vector3(spawnX, Random.Range(minY, maxY), 0); // Random vertical spawn position.

        GameObject spawnedObj = Random.value < bombProbability
            ? Instantiate(bombPrefab, pos, Quaternion.identity)
            : Instantiate(ringPrefab, pos, Quaternion.identity);

        ObjectMoveManager.Instance?.ApplySpeedToObject(spawnedObj); // Match object speed with current difficulty.
    }

    public void StopSpawning()
    {
        isSpawning = false;
        CancelInvoke(nameof(SpawnObject)); // Stop all scheduled spawns.
    }
}
