using UnityEngine;

public class TayyarObjectReporter : MonoBehaviour
{
    public enum TayyarObjectType
    {
        Ring,
        Bomb
    }

    [Header("Object Type")]
    public TayyarObjectType objectType = TayyarObjectType.Ring; // Defines whether this object is target or non-target.

    [Header("Player Tag")]
    [SerializeField] private string playerTag = "Player"; // Tag used to detect the player.

    private float spawnTime; // Time when the object was created.
    private bool reported = false; // Prevents counting the same object twice.

    private void Awake()
    {
        spawnTime = Time.time; // Used to estimate reaction time.
    }

    private float GetReactionTimeMs()
    {
        return Mathf.Round((Time.time - spawnTime) * 1000f * 100f) / 100f; // Convert seconds to ms.
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        ReportCollision();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag(playerTag))
            return;

        ReportCollision();
    }

    private void ReportCollision()
    {
        if (reported)
            return;

        if (TayyarLogic.Instance == null || TayyarLogic.Instance.IsSessionEnded)
            return;

        reported = true;

        float rtMs = GetReactionTimeMs();

        if (objectType == TayyarObjectType.Ring)
            TayyarLogic.Instance.RegisterTargetHit(rtMs); // Ring collision is a hit.
        else
            TayyarLogic.Instance.RegisterFalseAlarm(rtMs); // Bomb collision is a false alarm.

        Destroy(gameObject);
    }

    private void OnBecameInvisible()
    {
        if (reported)
            return;

        if (TayyarLogic.Instance == null || TayyarLogic.Instance.IsSessionEnded)
            return;

        reported = true;

        if (objectType == TayyarObjectType.Ring)
            TayyarLogic.Instance.RegisterMiss(); // Ring left the screen without being collected.
        else
            TayyarLogic.Instance.RegisterCorrectRejection(); // Bomb avoided successfully.

        Destroy(gameObject);
    }
}
