using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class AttentionButtonsLayoutController : MonoBehaviour
{
    [Header("Layout Group")]
    public HorizontalLayoutGroup layoutGroup;

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        ApplyLayout(); 
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    void OnLocaleChanged(Locale _)
    {
        ApplyLayout();
    }

        void ApplyLayout()
    {
        if (layoutGroup == null) return;

        bool isArabic = LocalizationSettings.SelectedLocale != null &&
                        LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ar");

        layoutGroup.reverseArrangement = isArabic;
        layoutGroup.childAlignment = isArabic
            ? TextAnchor.MiddleCenter  
            : TextAnchor.MiddleCenter; 
        RectTransform rt = layoutGroup.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }
}