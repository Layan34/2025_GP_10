using UnityEngine;

// This script Ensures background sprite fully fits the visible camera area
public class FitBackground : MonoBehaviour
{
    void Start()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            return;

        float width = sr.sprite.bounds.size.x;
        float height = sr.sprite.bounds.size.y;

        // Get the world dimensions of the camera
        float worldHeight = Camera.main.orthographicSize * 2f;
        float worldWidth = worldHeight * Screen.width / Screen.height;

        // Apply scale transformation
        Vector3 scale = transform.localScale;
        scale.x = worldWidth / width;
        scale.y = worldHeight / height;

        transform.localScale = scale;
    }
}
