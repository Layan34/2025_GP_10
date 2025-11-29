using UnityEngine;
using UnityEngine.SceneManagement;

// Handles logo pop animation before entering the naming screen
public class StartSceneLoader : MonoBehaviour
{
    [Header("Pop Animation Settings")]
    public float startScale = 0f;
    public float endScale = 1f;
    public float duration = 0.8f;

    [Header("Next Scene Settings")]
    public string nextSceneName = "EnterNameScene";
    public float delayAfterPop = 2f;

    private float timer = 0f;
    private bool animating = true;
    private bool loadingNext = false;

    private void Start()
    {
        // Start logo small
        transform.localScale = new Vector3(startScale, startScale, startScale);
    }

    private void Update()
    {
        // Run the pop animation
        if (animating)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            t = Mathf.SmoothStep(0f, 1f, t);

            float scale = Mathf.Lerp(startScale, endScale, t);
            transform.localScale = new Vector3(scale, scale, scale);

            // When animation finishes
            if (t >= 1f)
            {
                animating = false;
                Invoke(nameof(LoadNextScene), delayAfterPop);
            }
        }
    }

    // Loads the next scene
    private void LoadNextScene()
    {
        if (!loadingNext)
        {
            loadingNext = true;
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
