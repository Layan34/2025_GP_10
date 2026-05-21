using UnityEngine;

public class ObjectOutOfBounds : MonoBehaviour
{
    public float destroyAtX = -300f; // X position where the object is removed.

    private bool processed = false; // Prevents the same object from being counted twice.
    private TargetNonTarget targetNonTarget; // Stores whether the object is target or non-target.

    private void Start()
    {
        targetNonTarget = GetComponent<TargetNonTarget>(); // Read stimulus type from the object.
    }

    private void Update()
    {
        if (processed)
            return; // Already handled.

        if (transform.position.x >= destroyAtX)
            return; // Object is still inside the valid area.

        processed = true;

        if (targetNonTarget != null)
        {
            if (targetNonTarget.processed)
            {
                Destroy(gameObject); // Another script already counted it.
                return;
            }

            targetNonTarget.processed = true; // Mark it as handled everywhere.
        }

        Debug.Log("[OOB] Destroyed: " + gameObject.name);

        if (TayyarLogic.Instance != null && !TayyarLogic.Instance.IsSessionEnded)
        {
            if (targetNonTarget != null && targetNonTarget.stimulusType == StimulusType.TargetGate)
                TayyarLogic.Instance.RegisterMiss(); // Target left the screen without being collected.
            else
                TayyarLogic.Instance.RegisterCorrectRejection(); // Non-target passed without collision.
        }

        Destroy(gameObject);
    }
}
