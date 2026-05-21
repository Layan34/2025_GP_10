using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MKMTools.SimpleBarGraph.Data;
using MKMTools.SimpleBarGraph.Render.Bar;
using MKMTools.SimpleBarGraph.Render.XValue;
using MKMTools.SimpleBarGraph.Render.YValue;
using MKMTools.Common.Utils;

namespace MKMTools.SimpleBarGraph
{
    public class BarGraph : MonoBehaviour
    {
        private const string BarGraphMessage = "[" + nameof(BarGraph) + "] - ";

        public delegate void CriticalValuesFound(Vector2 minMax);

        public delegate void BarCreated(IBar bar, BarRender render);

        [Header("Deps")] [SerializeField] private HorizontalOrVerticalLayoutGroup graphContainer;
        [SerializeField] private HorizontalOrVerticalLayoutGroup horizontalContainer;
        [SerializeField] private HorizontalOrVerticalLayoutGroup verticalContainer;

        [Header("Prefabs")] [SerializeField] private YValueRender yValuePrefab;
        [SerializeField] private XValueRender xValuePrefab;
        [SerializeField] private BarRender barPrefab;

        [Header("Params")] [SerializeField, Min(float.Epsilon)]
        private float verticalDiscretization = 1;

        [SerializeField] private bool toRemoveHorisontalSpacing;

        [Header("Axis Formatting")]
        [SerializeField] private float yAxisLabelMultiplier = 1f;
        [SerializeField] private string yAxisLabelSuffix = "";
        [SerializeField] private bool yAxisLabelAsInt = true;

        private Vector2 _minMaxOnGraph;
        private Vector2 _minMax;
        private CriticalValuesFound _criticalValuesFound;
        private BarCreated _barCreated;

        private Pool<YValueRender> _yValues;
        private Pool<XValueRender> _xValues;
        private Pool<BarRender> _bars;

        private bool _initialized;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            if (xValuePrefab == null || yValuePrefab == null || barPrefab == null ||
                graphContainer == null || horizontalContainer == null || verticalContainer == null)
            {
                Debug.LogError($"{BarGraphMessage}Missing references in Inspector.", this);
                return;
            }

            // Clone once so we don't resize the original prefab transform
            xValuePrefab = Instantiate(xValuePrefab);

            _yValues = new Pool<YValueRender>(
                create: () => Instantiate(yValuePrefab, verticalContainer.transform),
                activate: go => go.gameObject.SetActive(true),
                deactivate: go => go.gameObject.SetActive(false)
            );

            _xValues = new Pool<XValueRender>(
                create: () => Instantiate(xValuePrefab, horizontalContainer.transform),
                activate: go => go.gameObject.SetActive(true),
                deactivate: go => go.gameObject.SetActive(false)
            );

            _bars = new Pool<BarRender>(
                create: () => Instantiate(barPrefab),
                activate: go => go.gameObject.SetActive(true),
                deactivate: go => go.gameObject.SetActive(false)
            );

            _initialized = true;
        }

        public void PlotGraph(IBarGraphData dataPoints, CriticalValuesFound criticalValuesFound = null, BarCreated barCreated = null)
        {
            EnsureInitialized();
            if (!_initialized) return;

            Clear();
            _criticalValuesFound = criticalValuesFound;
            _barCreated = barCreated;

            if (dataPoints == null || dataPoints.BarSets == null || dataPoints.BarSets.Count == 0) return;

            if (toRemoveHorisontalSpacing)
            {
                var totalWidth = ((RectTransform)horizontalContainer.transform).rect.width;
                var hTransform = (RectTransform)xValuePrefab.transform;
                hTransform.sizeDelta = new Vector2(totalWidth / dataPoints.BarSets.Count, hTransform.sizeDelta.y);
            }

            _minMaxOnGraph = _minMax = dataPoints.GetMinMaxValues();
            _criticalValuesFound?.Invoke(_minMaxOnGraph);

            PlotGrid(dataPoints);

            foreach (var barSet in dataPoints.BarSets)
            {
                PlotBarSet(barSet.Key, barSet.Value);
            }
        }

        private void PlotGrid(IBarGraphData dataPoints)
        {
            var horizontalSpacing = 0f;

            if (!toRemoveHorisontalSpacing)
            {
                var totalWidth = ((RectTransform)horizontalContainer.transform).rect.width;
                var hTransform = (RectTransform)xValuePrefab.transform;
                var nameWidth = hTransform.rect.width;
                var namesCount = dataPoints.BarSets.Count;
                var totalSpacingsCount = namesCount - 1;

                // Change 2: Avoid division by zero when there is only one bar-set/label.
                if (totalSpacingsCount <= 0)
                {
                    horizontalSpacing = 0f;
                }
                else
                {
                    horizontalSpacing = (totalWidth - nameWidth * namesCount) / totalSpacingsCount;
                }
            }

            graphContainer.spacing = horizontalContainer.spacing = horizontalSpacing;

            foreach (var barSet in dataPoints.BarSets)
            {
                DrawHorizontalNumber(barSet.Key, barSet.Value);
            }

            if (yValuePrefab != null)
            {
                var totalHeight = ((RectTransform)verticalContainer.transform).rect.height;
                var vTransform = (RectTransform)yValuePrefab.transform;
                var lineHeight = vTransform.rect.height;
                var lineCount = Mathf.CeilToInt((_minMaxOnGraph.y - _minMaxOnGraph.x) / Discretization) + 1;
                var totalSpacingsCountV = lineCount - 1;
                verticalContainer.spacing = (totalHeight - lineCount * lineHeight) / totalSpacingsCountV;

                for (float number = _minMaxOnGraph.x; number <= _minMaxOnGraph.y; number += Discretization)
                {
                    DrawVerticalNumber(number);
                }
            }
        }

        private void DrawVerticalNumber(float data)
        {
            var line = _yValues.PoolObject();
            line.Render(data);
        }



        private void DrawHorizontalNumber(string number, ICollection<IBar> bars)
        {
            var line = _xValues.PoolObject();
            line.Render(number, bars);
        }

        private void PlotBarSet(string key, ICollection<IBar> set)
        {
            if (set == null)
            {
                Debug.LogWarning(BarGraphMessage + $"Empty BarSet with key {key}", this);
                return;
            }

            var graphContainerTransform = (RectTransform)graphContainer.transform;
            var graphSize = graphContainerTransform.rect;

            var barsContainer = new GameObject(key);
            barsContainer.transform.SetParent(graphContainerTransform);

            var barContainerTransform = barsContainer.AddComponent<RectTransform>();
            var hTransform = (RectTransform)xValuePrefab.transform;
            barContainerTransform.sizeDelta = new Vector2(hTransform.rect.width, graphSize.height);

            var barContainerLayout = barsContainer.AddComponent<HorizontalLayoutGroup>();
            barContainerLayout.spacing = 0;
            barContainerLayout.childAlignment = TextAnchor.LowerCenter;
            barContainerLayout.childScaleHeight = barContainerLayout.childScaleWidth = true;
            barContainerLayout.childControlHeight = barContainerLayout.childControlWidth = false;
            barContainerLayout.childForceExpandHeight = barContainerLayout.childForceExpandWidth = false;

            foreach (var bar in set)
            {
                // Change 1: Avoid division by zero when min == max (single data point or all equal).
                float range = _minMaxOnGraph.y - _minMaxOnGraph.x;
                if (range <= Mathf.Epsilon)
                {
                    range = 1f; // safe fallback range
                }

                float height = ((bar.Value - _minMaxOnGraph.x) / range) * graphSize.height;
                float width = barContainerTransform.sizeDelta.x / set.Count;

                var barGo = _bars.PoolObject();
                var barTransform = (RectTransform)barGo.transform;
                barTransform.SetParent(barContainerTransform);
                barTransform.sizeDelta = new Vector2(width, height);

                _barCreated?.Invoke(bar, barGo);
            }
        }

        public void Clear()
        {
            _xValues?.Deactivate();
            _yValues?.Deactivate();
            _bars?.Deactivate();
        }

        public BarGraph SetYValuePrefab(YValueRender yValueRender)
        {
            this.yValuePrefab = yValueRender;
            return this;
        }

        public BarGraph SetXValuePrefab(XValueRender xValueRender)
        {
            this.xValuePrefab = xValueRender;
            return this;
        }

        public BarGraph SetBarPrefab(BarRender barRender)
        {
            this.barPrefab = barRender;
            return this;
        }

        public float Discretization
        {
            get => verticalDiscretization;
            set
            {
                if (value < float.Epsilon)
                {
                    Debug.LogError($"{BarGraphMessage}Disscretization is greater than {float.Epsilon}", this);
                    return;
                }
                verticalDiscretization = value;
            }
        }

        public Vector2 MinMax => _minMax;

        public Vector2 MinMaxOnGraph
        {
            get => _minMaxOnGraph;
            set
            {
                if (value.y < value.x)
                {
                    Debug.LogError(BarGraphMessage + "Max is lower than Min", this);
                    return;
                }

                _minMaxOnGraph = value;
            }
        }
    }
}
