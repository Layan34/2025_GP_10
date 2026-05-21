using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BarItemUI : MonoBehaviour
{
    [SerializeField] private LayoutElement layoutElement; // Controls the bar height in the layout
    [SerializeField] private TMP_Text label;              // Text label displayed under the bar

    public void SetHeight(float height)
    {
        layoutElement.preferredHeight = Mathf.Max(0f, height); // Apply non-negative bar height
    }

    public void SetLabel(string text)
    {
        if (label != null)
            label.text = text; // Update bar label text
    }
}
