using UnityEngine;

// Handles vertical movement and clamps the player within bounds
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 50f;
    public float topLimit = 80f;
    public float bottomLimit = -80f;

    void Update()
    {
        float verticalInput = Input.GetAxis("Vertical");

        transform.Translate(Vector3.up * verticalInput * moveSpeed * Time.deltaTime);

        float clampedY = Mathf.Clamp(transform.position.y, bottomLimit, topLimit);
        transform.position = new Vector3(transform.position.x, clampedY, transform.position.z);
    }
}
