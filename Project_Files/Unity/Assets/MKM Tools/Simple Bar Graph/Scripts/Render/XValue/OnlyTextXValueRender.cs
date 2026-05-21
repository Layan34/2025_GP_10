using System.Collections.Generic;
using MKMTools.SimpleBarGraph.Data;
using TMPro;

namespace MKMTools.SimpleBarGraph.Render.XValue
{
    public class OnlyTextXValueRender : XValueRender
    {
        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponentInChildren<TMP_Text>();
        }

        public override void Render(string data, ICollection<IBar> bars)
        {
            if (_text != null)
            {
                _text.text = data;
            }
        }
    }
}