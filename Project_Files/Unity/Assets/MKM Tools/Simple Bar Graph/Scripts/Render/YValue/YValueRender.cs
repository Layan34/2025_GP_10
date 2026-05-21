using TMPro;
using UnityEngine;

namespace MKMTools.SimpleBarGraph.Render.YValue
{
    public class YValueRender : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;

        public void Render(float value, string suffix, bool asInt)
        {
            if (label == null) return;

            string text = asInt
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.##");

            label.text = string.IsNullOrEmpty(suffix) ? text : $"{text} {suffix}";
        }

        // Keep old signature if the library calls it
        public virtual void Render(float value)
        {
            if (label == null) return;
            label.text = Mathf.RoundToInt(value).ToString();
        }

    }
}
