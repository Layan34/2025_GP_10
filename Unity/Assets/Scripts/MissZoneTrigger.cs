using UnityEngine;

public class MissZoneTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Register a miss when the target reaches the miss zone without being hit
        if (other.CompareTag("Target"))
        {
            GameManagerOnline.Instance.RegisterMiss();
        }

    }
}
