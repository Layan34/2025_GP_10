using UnityEngine;

public class MissZoneTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        var obj = other.GetComponent<TargetNonTarget>();
        if (obj == null) return;
        if (obj.processed) return;
        obj.processed = true;

        if (TayyarLogic.Instance == null || TayyarLogic.Instance.IsSessionEnded)
        {
            Destroy(other.gameObject);
            return;
        }

        if (obj.stimulusType == StimulusType.TargetGate)
        {
            Debug.Log("[MissZone] Hit by: " + other.gameObject.name);
            TayyarLogic.Instance.RegisterMiss();
        }
        else if (obj.stimulusType == StimulusType.NonTargetBomb)
        {
            Debug.Log("[MissZone] Bomb passed - Correct Rejection");
            TayyarLogic.Instance.RegisterCorrectRejection();
        }
    }
}
