using UnityEngine;
using UnityEngine.UI;

public class CharacterSelector : MonoBehaviour
{
    [Header("Character Containers")]
    public Image ManContainer;
    public Image BoyContainer;
    public Image GirlContainer;

    [Header("Highlight Settings")]
    public Color HighlightColor;
    public Color NormalColor;

    private string selectedCharacterKey = "SelectedCharacter";

    private void Awake()
    {
        // Default selection ensures consistent behavior when entering the scene
        PlayerPrefs.SetString(selectedCharacterKey, "girlB");
    }

    private void Start()
    {
        // Highlight initial default character
        Highlight(GirlContainer);
    }

    public void SelectMan()
    {
        // Save the selected character
        PlayerPrefs.SetString(selectedCharacterKey, "manB");
        Highlight(ManContainer);
    }

    public void SelectBoy()
    {
        PlayerPrefs.SetString(selectedCharacterKey, "boyB");
        Highlight(BoyContainer);
    }

    public void SelectGirl()
    {
        PlayerPrefs.SetString(selectedCharacterKey, "girlB");
        Highlight(GirlContainer);
    }

    private void Highlight(Image selected)
    {
        // Reset colors
        ManContainer.color = NormalColor;
        BoyContainer.color = NormalColor;
        GirlContainer.color = NormalColor;

        // Highlight selected card
        selected.color = HighlightColor;

        // Reset scale
        ManContainer.transform.localScale = Vector3.one;
        BoyContainer.transform.localScale = Vector3.one;
        GirlContainer.transform.localScale = Vector3.one;

        // Apply highlight to chosen card
        selected.transform.localScale = new Vector3(1.05f, 1.05f, 1f);
    }
}
