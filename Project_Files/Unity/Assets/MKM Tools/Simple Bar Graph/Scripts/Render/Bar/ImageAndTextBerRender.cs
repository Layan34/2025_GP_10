using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MKMTools.SimpleBarGraph.Render.Bar
{
    public class ImageAndTextBerRender : BarRender, IColorBar, ITextBar
    {
        [SerializeField] private string format = "{0:F2}";
        private Image[] _barCopy;
        private TMP_Text _text;

        private void Awake()
        {
            _barCopy = GetComponentsInChildren<Image>();
            _text = GetComponentInChildren<TMP_Text>();
        }
        
        void IColorBar.SetColor(Color color)
        {
            foreach (var image in _barCopy)
            {
                image.color = color;
            }
        }

        void ITextBar.SetText(float text)
        {
            _text.text = string.Format(format, text);
        }
    }
}