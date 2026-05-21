using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class BarChartUI : MonoBehaviour
{
    [Header("Bars")]
    [SerializeField] private RectTransform barsParent;   // Parent container for bar UI elements
    [SerializeField] private BarItemUI barPrefab;        // Prefab used to create each bar
    [SerializeField] private float maxBarHeight = 220f;  // Maximum visual height for a bar

    [Header("Y Axis")]
    [SerializeField] private RectTransform yAxisParent;  // Parent container for Y-axis labels
    [SerializeField] private TMP_Text yAxisTickPrefab;   // Prefab for Y-axis tick labels
    [SerializeField, Range(2, 10)] private int yAxisTicks = 5; // Number of Y-axis divisions

    private readonly List<BarItemUI> _spawned = new();   // Spawned bars
    private readonly List<TMP_Text> _yTicks = new();     // Spawned Y-axis ticks

    public void Render(IReadOnlyList<float> values, IReadOnlyList<string> labels = null)
    {
        ClearBars(); // Remove previously rendered bars

        float maxValue = GetMax(values); // Determine maximum value for normalization
        RenderYAxis(maxValue);           

        for (int i = 0; i < values.Count; i++)
        {
            float height = CalculateHeight(values[i], maxValue);

            BarItemUI bar = Instantiate(barPrefab, barsParent);
            bar.SetHeight(height);
            bar.SetLabel(labels != null && i < labels.Count ? labels[i] : string.Empty);

            _spawned.Add(bar);
        }
    }

    private float CalculateHeight(float value, float maxValue)
    {
        if (maxValue <= 0f) return 0f; 
        float normalized = Mathf.Clamp01(value / maxValue);
        return normalized * maxBarHeight;
    }

    private void RenderYAxis(float maxValue)
    {
        ClearYAxis(); 

        if (yAxisParent == null || yAxisTickPrefab == null)
            return;

        float safeMax = Mathf.Max(0f, maxValue); 

        for (int i = yAxisTicks; i >= 0; i--)
        {
            float t = i / (float)yAxisTicks;
            float tickValue = t * safeMax;

            TMP_Text tick = Instantiate(yAxisTickPrefab, yAxisParent);
            tick.text = tickValue.ToString("0"); 
            _yTicks.Add(tick);
        }
    }

    private void ClearBars()
    {
        foreach (var item in _spawned)
            Destroy(item.gameObject);

        _spawned.Clear();
    }

    private void ClearYAxis()
    {
        foreach (var t in _yTicks)
            Destroy(t.gameObject);

        _yTicks.Clear();
    }

    private float GetMax(IReadOnlyList<float> values)
    {
        float max = 0f;
        for (int i = 0; i < values.Count; i++)
            if (values[i] > max) max = values[i];
        return max;
    }
}
