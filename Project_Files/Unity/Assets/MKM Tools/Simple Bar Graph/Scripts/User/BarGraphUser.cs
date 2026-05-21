using MKMTools.SimpleBarGraph.Data;
using MKMTools.SimpleBarGraph.Render.Bar;
using MKMTools.SimpleBarGraph.User.Color;
using UnityEngine;

namespace MKMTools.SimpleBarGraph.User
{
    public class BarGraphUser : MonoBehaviour
    {
        [SerializeField] private BarGraph barGraph;
        [SerializeField] private SerialiseBarGraphData data;
        [SerializeField] private ColorUser colorUser;
        
        private void Start()
        {
            Plot();
        }

        public void Plot()
        {
            barGraph.PlotGraph(data, criticalValuesFound: CriticalValuesFound, barCreated: OnBarCreated);
        }

        private void OnBarCreated(IBar bar, BarRender render)
        {
            if (render is ITextBar textBar)
            {
                textBar.SetText(bar.Value);
            }

            if (colorUser != null && render is IColorBar colorBar)
            {
                colorBar.SetColor(colorUser.GetColor(bar, barGraph.MinMaxOnGraph));
            }
        }

        private void CriticalValuesFound(Vector2 minmax)
        {
            Debug.Log($"[{nameof(BarGraphUser)}] - Min: {minmax.x} Max: {minmax.y}");
            minmax.x = 0f;
            barGraph.MinMaxOnGraph = minmax;
        }
    }
}