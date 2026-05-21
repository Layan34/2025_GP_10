using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.Localization.Settings;
using ArabicSupport;

public class DifficultyIndicatorUI : MonoBehaviour
{
    public static DifficultyIndicatorUI Instance;

    [Header("Full-screen border Image")]
    public Image borderImage; // Border that flashes during difficulty change.

    [Header("Difficulty label (shown only on difficulty change)")]
    public TextMeshProUGUI difficultyLabel; // Shows current difficulty level.

    [Header("Notification text (flashes on change)")]
    public TextMeshProUGUI levelUpText; // Shows the difficulty change message.

    [Tooltip("Seconds the border + text are visible")]
    public float flashDuration = 1.5f; // How long the notification stays visible.

    private static readonly Color BorderColor = new Color(1f, 1f, 1f, 1f);

    private static readonly Color ColorEasy   = new Color(0.20f, 0.65f, 0.35f, 1f); // calm green
    private static readonly Color ColorMedium = new Color(0.85f, 0.72f, 0.20f, 1f); // calm yellow
    private static readonly Color ColorHard   = new Color(0.85f, 0.55f, 0.20f, 1f); // calm orange

    private string _currentDifficulty = "easy"; // Stores the last displayed difficulty.
    private Coroutine _flashCoroutine; // Keeps only one flash animation running.

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        SetBorderAlpha(0f); // Hide the border.
        if (difficultyLabel != null) difficultyLabel.gameObject.SetActive(false);
        if (levelUpText     != null) levelUpText.gameObject.SetActive(false);
    }

    public void OnDifficultyChanged(string newLevel)
    {
        // Determine direction
        int prevRank = LevelRank(_currentDifficulty);
        int newRank  = LevelRank(newLevel);

        if (newRank == prevRank) return; // No actual difficulty change.

        bool improved = newRank > prevRank; // True when difficulty increased.
        _currentDifficulty = newLevel;

        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashNotification(improved));
    }


    private IEnumerator FlashNotification(bool improved)
    {
        bool isAr  = IsArabicLocale();
        Color col  = LevelColor(_currentDifficulty);

        // Border
        if (borderImage != null)
        {
            Color c = BorderColor; c.a = 0f;
            borderImage.color = c;
        }

        // Difficulty label
        if (difficultyLabel != null)
        {
            difficultyLabel.text  = LevelLabel(_currentDifficulty, isAr);
            difficultyLabel.color = col;
            difficultyLabel.gameObject.SetActive(true);
            SetTextAlpha(difficultyLabel, 0f);
        }

        // Notification message
        if (levelUpText != null)
        {
            levelUpText.text = NotificationMsg(_currentDifficulty, improved, isAr);
            levelUpText.gameObject.SetActive(true);
            SetTextAlpha(levelUpText, 0f);
        }

        float half = flashDuration / 2f; // Half for fade-in and half for fade-out.
        float t;
        const float maxBorderAlpha = 0.3f; // Keep the border soft and not too strong.

        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / half);
            SetBorderAlpha(a * maxBorderAlpha);
            if (difficultyLabel != null) SetTextAlpha(difficultyLabel, a);
            if (levelUpText     != null) SetTextAlpha(levelUpText,     a);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(1f - (t / half));
            SetBorderAlpha(a * maxBorderAlpha);
            if (difficultyLabel != null) SetTextAlpha(difficultyLabel, a);
            if (levelUpText     != null) SetTextAlpha(levelUpText,     a);
            yield return null;
        }

        SetBorderAlpha(0f); // Hide the border.
        if (difficultyLabel != null) difficultyLabel.gameObject.SetActive(false);
        if (levelUpText     != null) levelUpText.gameObject.SetActive(false);
        _flashCoroutine = null;
    }


    private static int LevelRank(string level)
    {
        switch (level)
        {
            case "hard":   return 2;
            case "medium": return 1;
            default:       return 0; // easy
        }
    }

    private static Color LevelColor(string level)
    {
        switch (level)
        {
            case "hard":   return ColorHard;
            case "medium": return ColorMedium;
            default:       return ColorEasy;
        }
    }

    private static string LevelLabel(string level, bool isAr)
    {
        if (isAr)
        {
            switch (level)
            {
                case "hard":   return ArabicFixer.Fix("المستوى: صعب",   false, false);
                case "medium": return ArabicFixer.Fix("المستوى: متوسط", false, false);
                default:       return ArabicFixer.Fix("المستوى: سهل",   false, false);
            }
        }
        else
        {
            switch (level)
            {
                case "hard":   return "Level: Hard";
                case "medium": return "Level: Medium";
                default:       return "Level: Easy";
            }
        }
    }

    private static string NotificationMsg(string level, bool improved, bool isAr)
    {
        if (isAr)
        {
            if (improved)
            {
                switch (level)
                {
                    case "hard":   return ArabicFixer.Fix("!أحسنت! انتقلت للمستوى الصعب",    false, false);
                    case "medium": return ArabicFixer.Fix("!جيد! انتقلت للمستوى المتوسط",    false, false);
                    default:       return ArabicFixer.Fix("!انتقلت للمستوى السهل",            false, false);
                }
            }
            else
            {
                switch (level)
                {
                    case "medium": return ArabicFixer.Fix("!واصل! عدت للمستوى المتوسط",      false, false);
                    default:       return ArabicFixer.Fix("!واصل المحاولة! عدت للمستوى السهل", false, false);
                }
            }
        }
        else
        {
            if (improved)
            {
                switch (level)
                {
                    case "hard":   return "Great job! Moving to Hard!";
                    case "medium": return "Good! Moving to Medium!";
                    default:       return "Moving to Easy!";
                }
            }
            else
            {
                switch (level)
                {
                    case "medium": return "Keep going! Back to Medium!";
                    default:       return "Keep trying! Back to Easy!";
                }
            }
        }
    }

    private void SetBorderAlpha(float a)
    {
        if (borderImage == null) return;
        Color c = borderImage.color; c.a = a;
        borderImage.color = c;
    }

    private static void SetTextAlpha(TextMeshProUGUI tmp, float a)
    {
        Color c = tmp.color; c.a = a;
        tmp.color = c;
    }

    private bool IsArabicLocale()
    {
        var locale = LocalizationSettings.SelectedLocale;
        return locale != null && locale.Identifier.Code.StartsWith("ar");
    }
}