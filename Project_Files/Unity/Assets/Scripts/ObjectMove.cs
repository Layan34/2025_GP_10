using UnityEngine;

public class ObjectMove : MonoBehaviour
{
    public float speed = 50f; // Horizontal movement speed.

    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed; // Update speed from the difficulty manager.
    }

    void Update()
    {
        transform.position += Vector3.left * speed * Time.deltaTime; // Move object left every frame.
    }
}
