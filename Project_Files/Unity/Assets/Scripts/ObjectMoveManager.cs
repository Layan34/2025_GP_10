using UnityEngine;

public class ObjectMoveManager : MonoBehaviour
{
    public static ObjectMoveManager Instance;
    public float CurrentSpeed { get; private set; } = 55f; // Current speed applied to moving objects.

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(Instance.gameObject); // Keep only one manager.

        Instance = this;
        CurrentSpeed = 55f; // Reset speed when the scene loads.
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null; // Clear singleton reference.
    }

    public void SetDifficultyByLevel(string level)
    {
        switch (level)
        {
            case "easy":
                CurrentSpeed = 95f;
                break;

            case "medium":
                CurrentSpeed = 110f;
                break;

            case "hard":
                CurrentSpeed = 125f;
                break;

            default:
                CurrentSpeed = 95f;
                break;
        }

        ApplyToAllObjects(); // Update objects that already exist in the scene.
        Debug.Log($"[ObjectMoveManager] Hit Level={level} | Speed={CurrentSpeed}");
    }

    private void ApplyToAllObjects()
    {
        foreach (ObjectMove obj in FindObjectsOfType<ObjectMove>())
            obj.SetSpeed(CurrentSpeed); // Apply current speed to every active moving object.
    }

    public void ApplySpeedToObject(GameObject obj)
    {
        if (obj == null)
            return;

        ObjectMove move = obj.GetComponent<ObjectMove>();

        if (move != null)
            move.SetSpeed(CurrentSpeed); // Apply speed to newly spawned objects.
    }
}
