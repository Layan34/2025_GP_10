using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DashboardTabs : MonoBehaviour
{
    [Header("Panels")]
    public GameObject profilePanel; // Profile tab panel.
    public GameObject sessionsPanel; // Sessions tab panel.
    public GameObject chartPanel; // Chart tab panel.

    [Header("Buttons")]
    public Button profileBtn;
    public Button sessionsBtn;
    public Button chartBtn;

    [Header("Loading")]
    public GameObject loadingIndicator; // Small loading object shown while switching tabs.

    [Header("Scroll Views")]
    public ScrollRect profileScrollRect;
    public ScrollRect sessionsScrollRect;
    public ScrollRect chartScrollRect;

    [Header("Optional Refresh Scripts")]
    public JsonProgressLoader jsonProgressLoader;

    private Color activeColor = new Color(0.102f, 0.478f, 0.290f, 1f);
    private Color inactiveColor = Color.white;
    private Color activeTextColor = Color.white;
    private Color inactiveTextColor = new Color(0.180f, 0.490f, 0.345f, 1f);

    private Coroutine currentRoutine; // Stores the active tab loading coroutine.

    private void Start()
    {
        ShowProfile(); // Open profile tab by default.
    }

    public void ShowProfile()
    {
        OpenPanel(profilePanel, profileBtn, profileScrollRect);
    }

    public void ShowSessions()
    {
        OpenPanel(sessionsPanel, sessionsBtn, sessionsScrollRect);
    }

    public void ShowChart()
    {
        OpenPanel(chartPanel, chartBtn, chartScrollRect);
    }

    private void OpenPanel(GameObject targetPanel, Button activeBtn, ScrollRect targetScroll)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(LoadPanel(targetPanel, activeBtn, targetScroll));
    }

    private IEnumerator LoadPanel(GameObject targetPanel, Button activeBtn, ScrollRect targetScroll)
    {
        SetActiveButton(activeBtn); // Highlight the selected tab button.

        if (profilePanel != null)
            profilePanel.SetActive(false);

        if (sessionsPanel != null)
            sessionsPanel.SetActive(false);

        if (chartPanel != null)
            chartPanel.SetActive(false);

        if (loadingIndicator != null)
            loadingIndicator.SetActive(true);

        yield return new WaitForSeconds(0.15f); // Short delay so the loading indicator is visible.

        if (loadingIndicator != null)
            loadingIndicator.SetActive(false);

        if (targetPanel != null)
            targetPanel.SetActive(true);

        yield return null;

        RefreshPanelData(targetPanel); // Reload panel-specific data if needed.

        ResetScrollToStart(targetScroll);

        yield return new WaitForEndOfFrame();
        ResetScrollToStart(targetScroll);

        yield return null;
        ResetScrollToStart(targetScroll);
    }

    private void RefreshPanelData(GameObject targetPanel)
    {
        if (targetPanel == chartPanel && jsonProgressLoader != null)
            jsonProgressLoader.LoadJsonSessionsAndDrawChart();
    }

    private void ResetScrollToStart(ScrollRect scrollRect)
    {
        if (scrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();

        if (scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

            Vector2 position = scrollRect.content.anchoredPosition;
            position.y = 0f;
            scrollRect.content.anchoredPosition = position;
        }

        if (scrollRect.vertical)
            scrollRect.verticalNormalizedPosition = 1f; // Start at the top.

        if (scrollRect.horizontal)
            scrollRect.horizontalNormalizedPosition = 1f; // Start position for horizontal/Arabic layouts.

        Canvas.ForceUpdateCanvases();
    }

    private void SetActiveButton(Button active)
    {
        Button[] allBtns = { profileBtn, sessionsBtn, chartBtn };

        foreach (Button btn in allBtns)
        {
            if (btn == null)
                continue;

            bool isActive = btn == active;

            Image image = btn.GetComponent<Image>();
            if (image != null)
                image.color = isActive ? activeColor : inactiveColor;

            TMPro.TextMeshProUGUI text = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (text != null)
                text.color = isActive ? activeTextColor : inactiveTextColor;
        }
    }
}