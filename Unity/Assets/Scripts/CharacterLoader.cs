using UnityEngine;

public class CharacterLoader : MonoBehaviour
{
    public GameObject boyPrefab;    // Prefab for the boy character
    public GameObject manPrefab;    // Prefab for the man character
    public GameObject girlPrefab;   // Prefab for the girl character
    public Transform spawnPoint;    // Point in the scene where the character should appear

    private GameObject currentCharacter; // Holds the currently spawned character instance

    void Start()
    {
        string selectedCharacter = PlayerPrefs.GetString("SelectedCharacter", "boyB"); // Retrieve saved character or use "boyB" as default
        Debug.Log("Loaded character: " + selectedCharacter);                           // Log which character is loaded

        GameObject prefabToSpawn = null;                                               // Prepare variable for chosen prefab

        // Choose which prefab to load based on saved name
        switch (selectedCharacter)
        {
            case "manB":                                                               // If the player chose the man character
                prefabToSpawn = manPrefab;
                break;
            case "girlB":                                                              // If the player chose the girl character
                prefabToSpawn = girlPrefab;
                break;
            default:                                                                   // If nothing saved, use boy by default
                prefabToSpawn = boyPrefab;
                break;
        }

        // Check that prefab and spawn point exist before spawning
        if (prefabToSpawn != null && spawnPoint != null)
        {
            currentCharacter = Instantiate(prefabToSpawn, spawnPoint.position, Quaternion.identity); // Spawn character in scene

            currentCharacter.transform.SetParent(spawnPoint);                          // Make spawned object a child of the spawn point
            currentCharacter.transform.localPosition = Vector3.zero;                   // Center the character exactly on the spawn point
            currentCharacter.transform.localScale = new Vector3(1f, 1f, 1f);           // Ensure correct scale for character

            Vector3 position = currentCharacter.transform.localPosition;               // Copy current position
            position.z = -6f;                                                          // Move slightly forward on Z-axis to appear above background
            currentCharacter.transform.localPosition = position;                       // Apply adjusted position
        }
        else
        {
            Debug.LogWarning("CharacterLoader: Missing prefab or spawn point!");       // Warn if something is not assigned
        }
    }
}
