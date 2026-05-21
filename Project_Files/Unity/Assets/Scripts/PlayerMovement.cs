using UnityEngine;

// Handles vertical movement with sliding inertia and clamps the player within bounds
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float pushForce = 80f;      // How much speed is added per key press
    public float maxSpeed = 140f;      // Maximum sliding speed
    public float friction = 60f;       // How fast the player slows down
    public float topLimit = 80f;
    public float bottomLimit = -80f;

    private float currentSpeed = 0f;

    void Update()
    {
        // Give the player a push when pressing up
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            currentSpeed += pushForce;
        }

        // Give the player a push when pressing down
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            currentSpeed -= pushForce;
        }

        // Limit max speed
        currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed, maxSpeed);

        // Move player
        transform.Translate(Vector3.up * currentSpeed * Time.deltaTime);

        // Apply friction so the player slows down gradually
        currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, friction * Time.deltaTime);

        // Clamp position
        float clampedY = Mathf.Clamp(transform.position.y, bottomLimit, topLimit);
        transform.position = new Vector3(transform.position.x, clampedY, transform.position.z);

        // Stop movement when hitting bounds
        if (transform.position.y >= topLimit && currentSpeed > 0)
            currentSpeed = 0f;

        if (transform.position.y <= bottomLimit && currentSpeed < 0)
            currentSpeed = 0f;
    }
}