using UnityEngine;

// Randomly spawns rings and bombs based on probability
public class Spawner : MonoBehaviour
{
    public GameObject ringPrefab;
    public GameObject bombPrefab;

    public float spawnX = 270f;
    public float interval = 2f;
    public float minY = -50f;
    public float maxY = 50f;

    public float bombProbability = 0.4f; // 40% Bomb – 60% Ring

   private void Start()
    {
        // spawn immediately
        SpawnObject();
        InvokeRepeating(nameof(SpawnObject), interval, interval);
    }


    private void SpawnObject()
    {
        Vector3 pos = new Vector3(spawnX, Random.Range(minY, maxY), 0);

        float r = Random.value;

        if (r < bombProbability)
        {
            Instantiate(bombPrefab, pos, Quaternion.identity);
        }
        else
        {
            Instantiate(ringPrefab, pos, Quaternion.identity);
        }
    }
}
