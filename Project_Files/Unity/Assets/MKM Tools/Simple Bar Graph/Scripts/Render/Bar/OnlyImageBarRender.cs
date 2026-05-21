using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MKMTools.SimpleBarGraph.Render.Bar
{
    public class OnlyImageBarRender : BarRender, IColorBar
    {
        [SerializeField] private List<Color> colors;
        private Image[] _barCopy;

        private void Awake()
        {
            _barCopy = GetComponentsInChildren<Image>();
        }

        void IColorBar.SetColor(Color color)
        {
            foreach (var image in _barCopy)
            {
                image.color = color;
            }
        }
    }
}