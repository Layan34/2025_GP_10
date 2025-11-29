using UnityEngine;

public class CharacterCardDisplay : MonoBehaviour
{
    [Header("Card Parent")]
    public Transform cardParent;

    [Header("Character Prefabs")]
    public GameObject boyPrefab;
    public GameObject girlPrefab;
    public GameObject manPrefab;

    private void Start()
    {
        // Load the selected character
        string selected = PlayerPrefs.GetString("SelectedCharacter", "girlB");

        GameObject prefabToSpawn = null;

        // Pick the correct prefab
        if (selected == "boyB")
            prefabToSpawn = boyPrefab;
        else if (selected == "manB")
            prefabToSpawn = manPrefab;
        else
            // Default
            prefabToSpawn = girlPrefab;

        // Spawn the selected character under the parent
        if (prefabToSpawn != null)
        {
            Instantiate(prefabToSpawn, cardParent);
        }
    }
}
