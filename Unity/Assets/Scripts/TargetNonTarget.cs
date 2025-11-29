using UnityEngine;

public enum StimulusType
{
    TargetGate,
    NonTargetBomb
}

public class TargetNonTarget : MonoBehaviour
{
    public StimulusType stimulusType;

    private bool processed = false;

// Handles collisions for target and non-target objects in Sky Rings game
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (processed) return;
        if (!other.CompareTag("Player")) return;

        processed = true;

        if (GameManagerOnline.Instance == null)
            return;

        if (stimulusType == StimulusType.TargetGate)
        {
            GameManagerOnline.Instance.RegisterTargetHit();
        }
        else
        {
            GameManagerOnline.Instance.RegisterFalseAlarm();
        }

        Destroy(gameObject);
    }

    
}
