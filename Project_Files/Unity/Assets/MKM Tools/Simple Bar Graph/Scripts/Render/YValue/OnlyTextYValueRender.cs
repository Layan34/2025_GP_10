using System;
using TMPro;
using UnityEngine;

namespace MKMTools.SimpleBarGraph.Render.YValue
{
    public class OnlyTextYValueRender : YValueRender
    {
        [Header("Formatting")]
        [SerializeField] private float multiplier = 1f;
        [SerializeField] private string suffix = "";
        [SerializeField] private bool asInt = true;

        [SerializeField] private string floatFormat = "0.##";

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponentInChildren<TMP_Text>();
        }

        public override void Render(float data)
        {
            if (_text == null) return;

            float scaled = data * multiplier;

            string number = asInt
                ? Mathf.RoundToInt(scaled).ToString()
                : scaled.ToString(floatFormat);

            _text.text = string.IsNullOrEmpty(suffix) ? number : $"{number} {suffix}";
        }
    }
}
