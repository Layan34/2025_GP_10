using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class InputFieldDirectionFixer : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;

    private TMP_Text placeholderText;

    void Awake()
    {
        placeholderText = inputField.placeholder as TMP_Text;
    }

    void Start()
    {
        UpdateDirection();
        LocalizationSettings.SelectedLocaleChanged += _ => UpdateDirection();
    }

    void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= _ => UpdateDirection();
    }

    void UpdateDirection()
    {
        bool isArabic = LocalizationSettings.SelectedLocale.Identifier.Code == "ar";

        inputField.textComponent.isRightToLeftText = isArabic;
        inputField.textComponent.alignment =
            isArabic ? TextAlignmentOptions.MidlineRight
                     : TextAlignmentOptions.MidlineLeft;

        if (placeholderText != null)
        {
            placeholderText.alignment =
                isArabic ? TextAlignmentOptions.MidlineRight
                         : TextAlignmentOptions.MidlineLeft;
        }
    }
}
