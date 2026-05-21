using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public sealed class StartSceneLoader : MonoBehaviour
{
    [Header("Save")]
    // Local save file used to decide where to route the player
    [SerializeField] private string saveFileName = "rakkiz_save.json";

    [Header("Routing")]
    // Scene if player name is not set
    [SerializeField] private string enterNameSceneName = "EnterNameScene";
    // Scene if tutorial was not completed
    [SerializeField] private string tutorialSceneName = "TutorialScene";
    // Main menu / game selection scene
    [SerializeField] private string gameSelectionSceneName = "GameSelection";

    [Header("Logo")]
    // Logo image that changes based on language
    [SerializeField] private Image logoImage;
    [SerializeField] private Sprite arabicLogo;
    [SerializeField] private Sprite englishLogo;

    [Header("Pop Animation")]
    // Simple logo pop-in animation settings
    [SerializeField] private float startScale = 0f;
    [SerializeField] private float endScale = 1f;
    [SerializeField] private float durationSeconds = 0.8f;
    [SerializeField] private float delayAfterPopSeconds = 2f;

    private float _timerSeconds;
    private bool _isAnimating = true;
    private bool _isLoading;

    private void Start()
    {
        // Start with scaled-down logo
        transform.localScale = Vector3.one * startScale;

        // Update logo based on selected language
        UpdateLogoByLanguage();
        LocalizationSettings.SelectedLocaleChanged += _ => UpdateLogoByLanguage();
    }

    // Switch logo depending on Arabic / English locale
    private void UpdateLogoByLanguage()
    {
        var locale = LocalizationSettings.SelectedLocale;
        if (locale == null || logoImage == null)
            return;

        bool isArabic = locale.Identifier.Code.StartsWith("ar");
        logoImage.sprite = isArabic ? arabicLogo : englishLogo;
    }

    private void Update()
    {
        if (!_isAnimating)
            return;

        _timerSeconds += Time.deltaTime;

        float t = Mathf.Clamp01(_timerSeconds / Mathf.Max(0.0001f, durationSeconds));
        t = Mathf.SmoothStep(0f, 1f, t);

        float scale = Mathf.Lerp(startScale, endScale, t);
        transform.localScale = new Vector3(scale, scale, scale);

        if (t >= 1f)
        {
            _isAnimating = false;
            Invoke(nameof(LoadRoutedScene), delayAfterPopSeconds);
        }
    }

    // Loads the next scene after animation
    private void LoadRoutedScene()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        SceneManager.LoadScene(DetermineNextScene());
    }

    // Decide which scene to load based on save data
    private string DetermineNextScene()
    {
        var repo = new RakkizSaveRepository(saveFileName);

        if (!repo.HasPlayerName())
            return enterNameSceneName;

        if (!repo.HasSeenTutorial())
            return tutorialSceneName;

        return gameSelectionSceneName;
    }
}
