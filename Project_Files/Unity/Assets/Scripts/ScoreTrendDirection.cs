using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;

public class ScoreTrendDirection : MonoBehaviour
{
    [SerializeField] private HorizontalLayoutGroup layout; // Layout group that holds score trend texts.

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged; // Update direction when language changes.
        ApplyDirection();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged; // Avoid duplicate language callbacks.
    }

    private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        ApplyDirection();
    }

    private void ApplyDirection()
    {
        bool isArabic = LocalizationSettings.SelectedLocale != null &&
                        LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ar");

        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.reverseArrangement = isArabic; // Reverse order for Arabic layout.
    }
}
