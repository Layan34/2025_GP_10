using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MKMTools.SimpleBarGraph.Render.Bar
{
    public class OnlyTextBarRender: BarRender, ITextBar
    {
        [SerializeField] private string format = "{0:F2}";
        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponentInChildren<TMP_Text>();
        }

        void ITextBar.SetText(float text)
        {
            _text.text = string.Format(format, text);
        }
    }
}