using UnityEngine;

public class PigeonMove : MonoBehaviour
{
    public float speed = 3.5f;
    private GameManager gameManager;

    void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();

        // نخلي الحمامة تواجه اليسار في 2D
        Vector3 theScale = transform.localScale;
        theScale.x = -Mathf.Abs(theScale.x); // تعكس الاتجاه
        transform.localScale = theScale;
    }

    void Update()
    {
        // تحرك الحمامة يسار على محور X
        transform.Translate(Vector3.left * speed * Time.deltaTime, Space.World);

        // إذا طلعت برا الشاشة من اليسار احذفها
        if (transform.position.x < -12f)
        {
            if (gameManager != null)
            Destroy(gameObject);
        }
    }
}
