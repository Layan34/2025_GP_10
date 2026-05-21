using UnityEngine;

public enum StimulusType
{
    TargetGate,
    NonTargetBomb
}

public class TargetNonTarget : MonoBehaviour
{
    public StimulusType stimulusType; // Ring is target, bomb is non-target.
    public bool processed = false; // Prevents the same object from being counted twice.

    private const float ReactionWindowSeconds = 0.8f; // Time window before the player where RT can start.
    private const float PlayerX = -93f; // Player's fixed X position.

    private float reactionStartX; // X position where reaction timing starts.
    private float reactionStartTime = -1f; // Time when the ring enters the reaction window.
    private bool clockStarted = false; // True once RT measurement starts.

    private void Awake()
    {
        float speed = ObjectMoveManager.Instance != null
            ? ObjectMoveManager.Instance.CurrentSpeed
            : 95f; // Use easy speed if manager is missing.

        reactionStartX = PlayerX + (speed * ReactionWindowSeconds); // Start RT based on current object speed.

        Debug.Log($"[RT] {stimulusType} spawned | speed={speed} | reactionStartX={reactionStartX:F1}");
    }

    private void Update()
    {
        if (clockStarted)
            return;

        if (stimulusType != StimulusType.TargetGate)
            return; // Only target rings need reaction-time tracking.

        if (transform.position.x <= reactionStartX)
        {
            clockStarted = true;
            reactionStartTime = Time.time;
            Debug.Log($"[RT] Ring crossed X={reactionStartX:F1} | RT clock started");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (processed)
            return;

        if (!other.CompareTag("Player"))
            return;

        processed = true;

        if (TayyarLogic.Instance == null)
            return;

        if (stimulusType == StimulusType.TargetGate)
        {
            if (!clockStarted)
            {
                Debug.Log("[RT] Target HIT before reaction window — anticipatory, excluded from RT avg");
                TayyarLogic.Instance.RegisterTargetHit(-1f); // Exclude anticipatory hits from RT average.
            }
            else
            {
                float rtMs = (Time.time - reactionStartTime) * 1000f; // Calculate reaction time in ms.
                Debug.Log($"[RT] Target HIT | RT={rtMs:F1}ms");
                TayyarLogic.Instance.RegisterTargetHit(rtMs);
            }
        }
        else
        {
            TayyarLogic.Instance.RegisterFalseAlarm(-1f); // Player collided with a bomb.
        }

        Destroy(gameObject);
    }
}
