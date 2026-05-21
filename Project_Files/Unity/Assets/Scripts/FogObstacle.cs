using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FogObstacle : MonoBehaviour
{
    public static FogObstacle Instance;

    [Header("Fog Image")]
    [SerializeField] private Image fogImage; // UI image used as the fog overlay.

    [Header("Fog Alpha Levels")]
    [SerializeField] private float alphaLow    = 0.00f; // No fog for easy/low difficulty.
    [SerializeField] private float alphaMedium = 0.25f; // Light fog for medium difficulty.
    [SerializeField] private float alphaHigh   = 0.60f; // Stronger fog for high/hard difficulty.

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 1.2f; // Time used to smoothly change fog opacity.

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null; // Clear singleton when this object is destroyed.
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(Instance.gameObject); // Keep only one fog controller.

        Instance = this;

        if (fogImage != null)
            SetAlphaImmediate(0f); // Start with hidden fog.
    }

    public void SetDifficulty(string zone)
    {
        float targetAlpha = zone switch
        {
            "low"    => alphaLow,
            "medium" => alphaMedium,
            "high"   => alphaHigh,
            _        => alphaMedium
        };

        StopAllCoroutines(); // Stop any previous fade before starting a new one.
        StartCoroutine(FadeTo(targetAlpha));

        Debug.Log($"[FogObstacle] EEG Zone={zone} | Alpha={targetAlpha}");
    }

    public void SetDifficultyByLevel(string level)
    {
        float targetAlpha = level switch
        {
            "easy"   => alphaLow,
            "medium" => alphaMedium,
            "hard"   => alphaHigh,
            _        => alphaLow
        };

        StopAllCoroutines(); // Prevent two fade animations from running together.
        StartCoroutine(FadeTo(targetAlpha));

        Debug.Log($"[FogObstacle] Hit Level={level} | Alpha={targetAlpha}");
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (fogImage == null)
            yield break; // No image means there is nothing to update.

        float startAlpha = fogImage.color.a;
        float elapsed    = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration); // Convert elapsed time to 0-1.
            SetAlphaImmediate(Mathf.Lerp(startAlpha, targetAlpha, t)); // Smooth opacity change.
            yield return null;
        }

        SetAlphaImmediate(targetAlpha); // Ensure final value is exact.
    }

    private void SetAlphaImmediate(float alpha)
    {
        if (fogImage == null)
            return;

        Color c = fogImage.color;
        c.a = alpha;
        fogImage.color = c;
    }
}
