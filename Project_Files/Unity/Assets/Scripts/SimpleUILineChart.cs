using System.Collections.Generic;
using ArabicSupport;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleUILineChart : MonoBehaviour
{
    [Header("Chart Area")]
    [SerializeField] private RectTransform chartArea; // Area where chart lines and points are drawn.

    [Header("Prefabs")]
    [SerializeField] private Image pointPrefab; // Dot prefab for chart points.
    [SerializeField] private Image linePrefab; // Image prefab used as chart line segments.
    [SerializeField] private TMP_Text labelPrefab; // Text prefab for axis labels.
    [SerializeField] private TMP_Text legendTextPrefab;
    [SerializeField] private TMP_Text tooltipTextPrefab;

    [Header("Legend Area")]
    [SerializeField] private RectTransform legendArea; // Parent area for chart legend.

    [Header("Professional Compact Size")]
    [SerializeField] private bool useFixedSize = true;
    [SerializeField] private float fixedWidth = 500f;
    [SerializeField] private float fixedHeight = 190f;

    [Header("Padding")]
    [SerializeField] private float leftPadding = 58f;
    [SerializeField] private float rightPadding = 20f;
    [SerializeField] private float topPadding = 38f;
    [SerializeField] private float bottomPadding = 42f;

    [Header("Y Axis")]
    [SerializeField] private int yTickCount = 5;

    [Header("Axis Titles")]
    [SerializeField] private string xAxisTitle = "Session";
    [SerializeField] private string xAxisTitleArabic = "الجلسة";
    [SerializeField] private int axisTitleFontSize = 11;

    [Header("Visual Style")]
    [SerializeField] private float pointSize = 5f;
    [SerializeField] private float pointBorderSize = 7f;
    [SerializeField] private float lineThickness = 1.8f;
    [SerializeField] private float importantLineThickness = 2f;
    [SerializeField] private float importantPointSizeMultiplier = 1f;
    [SerializeField] private float importantBorderSizeMultiplier = 1f;
    [SerializeField] private float axisThickness = 1.2f;
    [SerializeField] private float gridThickness = 0.45f;
    [SerializeField] private int labelFontSize = 9;
    [SerializeField] private int legendFontSize = 9;

    [Header("Legend Layout")]
    [SerializeField] private float legendItemWidth = 120f;
    [SerializeField] private float legendSpacing = 145f;
    [SerializeField] private float legendYPosition = 0f;

    [Header("Tooltip Style")]
    [SerializeField] private int tooltipFontSize = 18;
    [SerializeField] private float pointTooltipWidth = 270f;
    [SerializeField] private float pointTooltipHeight = 120f;
    [SerializeField] private float legendTooltipWidth = 360f;
    [SerializeField] private float legendTooltipHeight = 110f;

    [Header("Arabic Fix")]
    [SerializeField] private bool useArabicFixForLabels = true;
    [SerializeField] private bool useArabicFixForTooltip = true;

    [Header("Fonts")]
    [SerializeField] private TMP_FontAsset numberFontAsset;
    [SerializeField] private TMP_FontAsset arabicFontAsset;

    [Header("Colors")]
    [SerializeField] private Color axisColor = new Color32(120, 120, 120, 255);
    [SerializeField] private Color gridColor = new Color32(225, 225, 225, 150);
    [SerializeField] private Color labelColor = new Color32(55, 55, 55, 255);
    [SerializeField] private Color tooltipBackgroundColor = new Color32(45, 45, 45, 235);
    [SerializeField] private Color tooltipTextColor = Color.white;

    [Header("Series Colors")]
    [SerializeField] private Color firstSeriesColor = new Color32(165, 214, 186, 255);
    [SerializeField] private Color secondSeriesColor = new Color32(174, 186, 221, 255);
    [SerializeField] private Color thirdSeriesColor = new Color32(239, 173, 136, 255);
    [SerializeField] private Color fourthSeriesColor = new Color32(212, 53, 102, 255);

    private bool _isArabic = false; // Tracks current chart language.

    private readonly List<GameObject> spawnedObjects = new List<GameObject>(); // Objects created by the chart and cleared on redraw.
    private RectTransform tooltipRect;
    private TMP_Text tooltipText;

    public void SetArabicMode(bool isArabic)
    {
        _isArabic = isArabic;
    }

    public void SetNumberFont(TMP_FontAsset font)
    {
        if (font != null)
            numberFontAsset = font;
    }

    public void SetArabicFont(TMP_FontAsset font)
    {
        if (font != null)
            arabicFontAsset = font;
    }

    private void ApplyLanguageFont(TMP_Text text)
    {
        if (text == null)
            return;

        if (_isArabic && arabicFontAsset != null)
        {
            text.font = arabicFontAsset;
            return;
        }

        if (numberFontAsset != null)
            text.font = numberFontAsset;
    }

    public void DrawChart(List<string> labels, List<ChartSeries> seriesList)
    {
        ClearChart(); // Remove old chart objects before drawing again.

        if (chartArea == null)
        {
            Debug.LogError("[SimpleUILineChart] Chart Area missing.");
            return;
        }

        if (useFixedSize)
            chartArea.sizeDelta = new Vector2(fixedWidth, fixedHeight);

        bool hasData = labels != null && labels.Count > 0
                    && seriesList != null && seriesList.Count > 0;

        DrawGrid(); // Draw horizontal grid lines and Y labels.
        DrawAxes(); // Draw chart axes.
        DrawAxisTitles(); // Draw X-axis title.

        if (!hasData)
        {
            Debug.LogWarning("[SimpleUILineChart] Empty chart.");
            return;
        }

        CreateTooltip(); // Create tooltip panel for hover details.
        DrawXAxisLabels(labels); // Draw session labels on X axis.

        for (int i = 0; i < seriesList.Count; i++)
            DrawSeries(labels, seriesList[i], GetSeriesColor(i));

        DrawLegend(seriesList); // Draw series names and colors.
    }

    private void DrawGrid()
    {
        int ticks = Mathf.Max(2, yTickCount);

        for (int i = 0; i <= ticks; i++)
        {
            float t = i / (float)ticks;
            float value = Mathf.Lerp(0f, 100f, t);
            Vector2 start = GetPoint(0f, t);

            bool isBase = i == 0;

            CreateLine(
                start,
                GetPoint(1f, t),
                isBase ? axisColor : gridColor,
                isBase ? axisThickness : gridThickness
            );

            CreateYAxisLabel(value, start);
        }
    }

    private void DrawAxes()
    {
        CreateLine(GetPoint(0f, 0f), GetPoint(0f, 1f), axisColor, axisThickness);
    }

    private void DrawXAxisLabels(List<string> labels)
    {
        int count = labels.Count;
        int step = GetXAxisStep(count);

        for (int i = 0; i < count; i++)
        {
            if (i != 0 && i != count - 1 && i % step != 0)
                continue;

            float xT = count == 1 ? 0.5f : i / (float)(count - 1);
            CreateXAxisLabel(labels[i], GetPoint(xT, 0f));
        }
    }

    private int GetXAxisStep(int count)
    {
        float width = (useFixedSize ? fixedWidth : chartArea.rect.width) - leftPadding - rightPadding;
        int maxLabels = Mathf.Max(2, Mathf.FloorToInt(width / 70f));

        return count <= maxLabels ? 1 : Mathf.CeilToInt(count / (float)maxLabels);
    }

    private void CreateYAxisLabel(float value, Vector2 pos)
    {
        if (labelPrefab == null)
            return;

        TMP_Text label = Instantiate(labelPrefab, chartArea, false);
        label.gameObject.SetActive(true);

        ApplyLanguageFont(label);

        label.text = _isArabic
            ? ToArabicIndicDigits(Mathf.RoundToInt(value))
            : Mathf.RoundToInt(value).ToString();

        label.color = labelColor;
        label.fontSize = labelFontSize;
        label.fontStyle = FontStyles.Bold;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Right;
        label.richText = false;
        label.raycastTarget = false;
        label.isRightToLeftText = false;

        if (useArabicFixForLabels)
            FixArabicText(label);

        RectTransform rect = label.GetComponent<RectTransform>();
        ResetRect(rect);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(58f, 16f);
        rect.anchoredPosition = new Vector2(pos.x - 8f, pos.y);

        spawnedObjects.Add(label.gameObject); // Track object so it can be cleared later.
    }

    private void CreateXAxisLabel(string text, Vector2 pos)
    {
        if (labelPrefab == null)
            return;

        TMP_Text label = Instantiate(labelPrefab, chartArea, false);
        label.gameObject.SetActive(true);

        ApplyLanguageFont(label);

        label.text = _isArabic ? ToArabicIndicDigits(text) : text;
        label.color = labelColor;
        label.fontSize = labelFontSize;
        label.fontStyle = FontStyles.Bold;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Center;
        label.richText = false;
        label.raycastTarget = false;
        label.isRightToLeftText = false;

        if (useArabicFixForLabels)
            FixArabicText(label);

        RectTransform rect = label.GetComponent<RectTransform>();
        ResetRect(rect);
        rect.sizeDelta = new Vector2(64f, 16f);
        rect.anchoredPosition = new Vector2(pos.x, pos.y - 17f);

        spawnedObjects.Add(label.gameObject); // Track object so it can be cleared later.
    }

    private void DrawAxisTitles()
    {
        if (labelPrefab == null)
            return;

        string title = _isArabic ? xAxisTitleArabic : xAxisTitle;

        if (string.IsNullOrWhiteSpace(title))
            return;

        TMP_Text titleText = Instantiate(labelPrefab, chartArea, false);
        titleText.gameObject.SetActive(true);

        ApplyLanguageFont(titleText);

        titleText.text = title;
        titleText.color = labelColor;
        titleText.fontSize = axisTitleFontSize;
        titleText.fontStyle = FontStyles.Bold;
        titleText.enableWordWrapping = false;
        titleText.overflowMode = TextOverflowModes.Overflow;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.richText = false;
        titleText.raycastTarget = false;
        titleText.isRightToLeftText = false;

        if (useArabicFixForLabels)
            FixArabicText(titleText);

        RectTransform rect = titleText.GetComponent<RectTransform>();
        ResetRect(rect);
        rect.sizeDelta = new Vector2(140f, 16f);
        rect.anchoredPosition = new Vector2(GetPoint(0.5f, 0f).x, GetPoint(0.5f, 0f).y - 34f);

        spawnedObjects.Add(titleText.gameObject);
    }

    private void DrawSeries(List<string> xLabels, ChartSeries series, Color color)
    {
        if (series == null || series.Values == null)
            return;

        int count = Mathf.Min(xLabels.Count, series.Values.Count);

        if (count <= 0)
            return;

        List<Vector2> points = new List<Vector2>(count);

        for (int i = 0; i < count; i++)
        {
            float xT = count == 1 ? 0.5f : i / (float)(count - 1);
            float yT = Mathf.InverseLerp(0f, 100f, Mathf.Clamp(series.Values[i], 0f, 100f));
            points.Add(GetPoint(xT, yT));
        }

        float thickness = series.IsImportant ? importantLineThickness : lineThickness;
        float dotSize = series.IsImportant ? pointSize * importantPointSizeMultiplier : pointSize;
        float borderSize = series.IsImportant ? pointBorderSize * importantBorderSizeMultiplier : pointBorderSize;

        for (int i = 0; i < points.Count - 1; i++)
            CreateLine(points[i], points[i + 1], color, thickness);

        for (int i = 0; i < points.Count; i++)
        {
            CreateDot(points[i], Color.white, borderSize, false, "", null, 0f, 0);
            CreateDot(points[i], color, dotSize, true, xLabels[i], series, series.Values[i], i);
        }
    }

    private void DrawLegend(List<ChartSeries> seriesList)
    {
        if (legendArea == null || legendTextPrefab == null)
            return;

        foreach (LayoutGroup layoutGroup in legendArea.GetComponents<LayoutGroup>())
            layoutGroup.enabled = false;

        ContentSizeFitter contentSizeFitter = legendArea.GetComponent<ContentSizeFitter>();

        if (contentSizeFitter != null)
            contentSizeFitter.enabled = false;

        int count = seriesList.Count;
        float startX = -(count - 1) * legendSpacing / 2f;

        for (int i = 0; i < count; i++)
        {
            ChartSeries series = seriesList[i];
            Color color = GetSeriesColor(i);
            float centerX = startX + i * legendSpacing;

            string name = (_isArabic && !string.IsNullOrEmpty(series.NameArabic))
                ? series.NameArabic
                : series.Name;

            TMP_Text label = Instantiate(legendTextPrefab, legendArea, false);
            label.gameObject.SetActive(true);

            ApplyLanguageFont(label);

            label.text = name;
            label.color = color;
            label.fontSize = legendFontSize;
            label.fontStyle = FontStyles.Bold;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = _isArabic ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            label.raycastTarget = true;
            label.richText = false;
            label.isRightToLeftText = false;

            if (useArabicFixForLabels)
                FixArabicText(label);

            RectTransform textRect = label.GetComponent<RectTransform>();
            ResetRect(textRect);
            textRect.sizeDelta = new Vector2(legendItemWidth - 14f, 18f);
            textRect.anchoredPosition = new Vector2(_isArabic ? centerX - 7f : centerX + 7f, legendYPosition);

            if (pointPrefab != null)
            {
                Image dot = Instantiate(pointPrefab, legendArea, false);
                dot.gameObject.SetActive(true);
                dot.color = color;
                dot.raycastTarget = false;

                RectTransform dotRect = dot.GetComponent<RectTransform>();
                ResetRect(dotRect);
                dotRect.sizeDelta = new Vector2(8f, 8f);

                float dotX = _isArabic
                    ? centerX + (legendItemWidth / 2f) - 3f
                    : centerX - (legendItemWidth / 2f) + 3f;

                dotRect.anchoredPosition = new Vector2(dotX, legendYPosition);
                spawnedObjects.Add(dot.gameObject);
            }

            ChartPointHover hover = label.gameObject.AddComponent<ChartPointHover>();
            Vector2 tooltipPosition = GetPoint(0.5f, 1f) + new Vector2(0f, -5f);
            hover.Initialize(() => ShowLegendTooltip(tooltipPosition, series), HideTooltip);

            spawnedObjects.Add(label.gameObject); // Track object so it can be cleared later.
        }
    }

    private void CreateTooltip()
    {
        GameObject panel = new GameObject("Chart Tooltip");
        panel.transform.SetParent(chartArea, false);
        panel.SetActive(false);

        Image background = panel.AddComponent<Image>();
        background.color = tooltipBackgroundColor;
        background.raycastTarget = false;

        tooltipRect = panel.GetComponent<RectTransform>();
        ResetRect(tooltipRect);
        tooltipRect.sizeDelta = new Vector2(pointTooltipWidth, pointTooltipHeight);

        TMP_Text sourcePrefab = tooltipTextPrefab != null ? tooltipTextPrefab : labelPrefab;

        if (sourcePrefab == null)
        {
            Debug.LogError("[SimpleUILineChart] Tooltip Text Prefab and Label Prefab are both missing.");
            return;
        }

        TMP_Text text = Instantiate(sourcePrefab, tooltipRect, false);
        text.gameObject.SetActive(true);

        ApplyLanguageFont(text);

        text.color = tooltipTextColor;
        text.fontSize = tooltipFontSize;
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = _isArabic ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;
        text.richText = false;
        text.raycastTarget = false;
        text.isRightToLeftText = false;

        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);

        tooltipText = text;
        spawnedObjects.Add(panel);
    }
    private void ShowPointTooltip(Vector2 pointPosition, string xLabel, ChartSeries series, float value, int index)
    {
        if (tooltipRect == null || tooltipText == null || series == null)
            return;

        tooltipRect.sizeDelta = new Vector2(pointTooltipWidth, pointTooltipHeight);
        ApplyLanguageFont(tooltipText);

        tooltipText.enableWordWrapping = true;
        tooltipText.overflowMode = TextOverflowModes.Overflow;
        tooltipText.alignment = _isArabic ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;
        tooltipText.raycastTarget = false;
        tooltipText.isRightToLeftText = false;

        if (_isArabic)
        {
            string name = !string.IsNullOrEmpty(series.NameArabic)
                ? series.NameArabic
                : series.Name;
            string valueLine = BuildRawValueLineArabic(series, index, value);

            // كل سطر يتعالج لحاله عشان الأرقام ما تختلط مع الكلمات
            string line1 = useArabicFixForTooltip ? FixArabic(name) : name;
            string line2 = useArabicFixForTooltip
                ? FixArabic("الجلسة:") + " " + ToArabicIndicDigits(xLabel)
                : "الجلسة: " + ToArabicIndicDigits(xLabel);
            string line3 = useArabicFixForTooltip ? FixArabic(valueLine) : valueLine;

            tooltipText.text = line1 + "\n" + line2 + "\n" + line3;
        }
        else
        {
            string name = series.Name;
            string valueLine = BuildRawValueLineEnglish(series, index, value);

            tooltipText.text =
                name + "\n" +
                "Session: " + xLabel + "\n" +
                valueLine;
        }
        Vector2 tooltipPos = pointPosition + new Vector2(0f, 90f);
        tooltipRect.anchoredPosition = tooltipPos;
        tooltipRect.gameObject.SetActive(true);
        tooltipRect.SetAsLastSibling();
    }
    private string BuildRawValueLineArabic(ChartSeries series, int index, float normalizedValue)
    {
        bool hasRaw = series.RawValues != null && index < series.RawValues.Count;
        bool hasRaw2 = series.RawValues2 != null && index < series.RawValues2.Count;
        bool hasUnit = !string.IsNullOrEmpty(series.RawUnitArabic);

        if (hasRaw && hasRaw2)
        {
            string raw = ToArabicIndicDigits(Mathf.RoundToInt(series.RawValues[index]));
            string total = ToArabicIndicDigits(Mathf.RoundToInt(series.RawValues2[index]));
            string unit = hasUnit ? " " + series.RawUnitArabic : "";
            return raw + " من " + total + unit;
        }

        if (hasRaw && hasUnit)
        {
            string raw = ToArabicIndicDigits(series.RawValues[index].ToString("F2"));
            return series.RawUnitArabic + ": " + raw;
        }
        return "النتيجة: " + ToArabicIndicDigits(Mathf.RoundToInt(normalizedValue)) + " / ١٠٠";
    }
    private string BuildRawValueLineEnglish(ChartSeries series, int index, float normalizedValue)
    {
        bool hasRaw = series.RawValues != null && index < series.RawValues.Count;
        bool hasRaw2 = series.RawValues2 != null && index < series.RawValues2.Count;
        bool hasUnit = !string.IsNullOrEmpty(series.RawUnit);

        if (hasRaw && hasRaw2)
        {
            int raw = Mathf.RoundToInt(series.RawValues[index]);
            int total = Mathf.RoundToInt(series.RawValues2[index]);
            string unit = hasUnit ? " " + series.RawUnit : "";
            return raw + " out of " + total + unit;
        }

        if (hasRaw && hasUnit)
        {
            return series.RawUnit + ": " + series.RawValues[index].ToString("F2");
        }

        return "Score: " + Mathf.RoundToInt(normalizedValue) + " / 100";
    }

    private void ShowLegendTooltip(Vector2 position, ChartSeries series)
    {
        if (tooltipRect == null || tooltipText == null || series == null)
            return;

        tooltipRect.sizeDelta = new Vector2(legendTooltipWidth, legendTooltipHeight);
        ApplyLanguageFont(tooltipText);

        tooltipText.enableWordWrapping = true;
        tooltipText.overflowMode = TextOverflowModes.Overflow;
        tooltipText.alignment = _isArabic ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;
        tooltipText.raycastTarget = false;
        tooltipText.isRightToLeftText = false;

        if (_isArabic)
        {
            string name = !string.IsNullOrEmpty(series.NameArabic)
                ? series.NameArabic
                : series.Name;

            string description = !string.IsNullOrEmpty(series.DescriptionArabic)
                ? series.DescriptionArabic
                : series.Description;

            // كل سطر لحاله
            string fixedName = useArabicFixForTooltip ? FixArabic(name) : name;
            string fixedDesc = useArabicFixForTooltip ? FixArabic(description) : description;
            tooltipText.text = fixedName + "\n" + fixedDesc;
        }
        else
        {
            tooltipText.text =
                series.Name + "\n" +
                series.Description;
        }

        tooltipRect.anchoredPosition = position;
        tooltipRect.gameObject.SetActive(true);
        tooltipRect.SetAsLastSibling();
    }

    private void HideTooltip()
    {
        if (tooltipRect != null)
            tooltipRect.gameObject.SetActive(false);
    }

    private void CreateDot(
        Vector2 position,
        Color color,
        float size,
        bool interactive,
        string xLabel,
        ChartSeries series,
        float value,
        int index)
    {
        if (pointPrefab == null)
            return;

        Image dot = Instantiate(pointPrefab, chartArea, false);
        dot.gameObject.SetActive(true);
        dot.color = color;
        dot.raycastTarget = interactive;

        RectTransform rect = dot.GetComponent<RectTransform>();
        ResetRect(rect);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(size, size);

        if (interactive)
        {
            ChartPointHover hover = dot.gameObject.AddComponent<ChartPointHover>();
            hover.Initialize(
                () => ShowPointTooltip(position, xLabel, series, value, index),
                HideTooltip
            );
        }

        spawnedObjects.Add(dot.gameObject);
    }

    private void CreateLine(Vector2 start, Vector2 end, Color color, float thickness)
    {
        if (linePrefab == null)
            return;

        Image line = Instantiate(linePrefab, chartArea, false);
        line.gameObject.SetActive(true);
        line.color = color;
        line.raycastTarget = false;

        RectTransform rect = line.GetComponent<RectTransform>();
        ResetRect(rect);

        Vector2 direction = end - start;

        rect.sizeDelta = new Vector2(direction.magnitude, thickness);
        rect.anchoredPosition = start + direction / 2f;
        rect.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
        );

        spawnedObjects.Add(line.gameObject);
    }

    private Vector2 GetPoint(float xT, float yT)
    {
        float width = useFixedSize ? fixedWidth : chartArea.rect.width;
        float height = useFixedSize ? fixedHeight : chartArea.rect.height;

        return new Vector2(
            Mathf.Lerp(-width / 2f + leftPadding, width / 2f - rightPadding, Mathf.Clamp01(xT)),
            Mathf.Lerp(-height / 2f + bottomPadding, height / 2f - topPadding, Mathf.Clamp01(yT))
        );
    }

    private Color GetSeriesColor(int index)
    {
        if (index == 0) return firstSeriesColor;
        if (index == 1) return secondSeriesColor;
        if (index == 2) return thirdSeriesColor;
        return fourthSeriesColor;
    }

    private void ResetRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private void ClearChart()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }

        spawnedObjects.Clear();
        tooltipRect = null;
        tooltipText = null;
    }

    // تحويل النص العربي باستخدام ArabicFixer مباشرة — كل سطر لحاله
    private static string FixArabic(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        return ArabicFixer.Fix(text, true, true);
    }

    private void FixArabicText(TMP_Text text)
    {
        if (!_isArabic || text == null)
            return;
        text.text = FixArabic(text.text);
    }

    private static string ToArabicIndicDigits(int number)
    {
        return ToArabicIndicDigits(number.ToString());
    }

    private static string ToArabicIndicDigits(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        char[] result = value.ToCharArray();

        for (int i = 0; i < result.Length; i++)
        {
            if (result[i] >= '0' && result[i] <= '9')
                result[i] = (char)('٠' + (result[i] - '0'));
        }

        return new string(result);
    }
}

public class ChartPointHover : MonoBehaviour,
    UnityEngine.EventSystems.IPointerEnterHandler,
    UnityEngine.EventSystems.IPointerExitHandler
{
    private System.Action onEnter;
    private System.Action onExit;

    public void Initialize(System.Action enterAction, System.Action exitAction)
    {
        onEnter = enterAction;
        onExit = exitAction;
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        onEnter?.Invoke();
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        onExit?.Invoke();
    }
}

[System.Serializable]
public class ChartSeries
{
    public string Name;
    public string NameArabic;
    public List<float> Values;           // القيم المنورملايزة (0-100) للرسم
    public string Description;
    public string DescriptionArabic;
    public string UnitLabel;
    public bool IsImportant;
    public List<float> RawValues;        // القيمة الأساسية (مثل عدد الإجابات الصحيحة)
    public List<float> RawValues2;       // القيمة الثانية عند الحاجة (مثل العدد الكلي)
    public string RawUnit;               // الوحدة بالإنجليزي (مثل "correct answers")
    public string RawUnitArabic;         // الوحدة بالعربي (مثل "إجابة صحيحة")

    public ChartSeries(
        string name,
        string nameArabic,
        List<float> values,
        string description,
        string descriptionArabic,
        string unitLabel = "",
        bool isImportant = false)
    {
        Name = name;
        NameArabic = nameArabic;
        Values = values;
        Description = description;
        DescriptionArabic = descriptionArabic;
        UnitLabel = unitLabel;
        IsImportant = isImportant;
    }

    public ChartSeries(
        string name,
        List<float> values,
        string description,
        string unitLabel = "",
        bool isImportant = false)
    {
        Name = name;
        NameArabic = "";
        Values = values;
        Description = description;
        DescriptionArabic = "";
        UnitLabel = unitLabel;
        IsImportant = isImportant;
    }
}