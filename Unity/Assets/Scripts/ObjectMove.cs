using UnityEngine;

public class ObjectMove : MonoBehaviour
{
    public float speed = 50f;

    // Handles continuous leftward movement for moving obstacles or objects
    void Update()
    {
        transform.position += Vector3.left * speed * Time.deltaTime;
    }
}
