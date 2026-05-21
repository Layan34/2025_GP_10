using System.Collections.Generic;
using UnityEngine;

public sealed class DashboardSessionsPresenter : MonoBehaviour
{
    [SerializeField] private Transform contentRoot; // Parent object that holds session rows.
    [SerializeField] private DashboardSessionRowUI sessionRowPrefab; // Hidden template used to create rows.
    [SerializeField] private RectTransform insightBox; // Box shown after the session list.

    private readonly List<GameObject> spawnedRows = new(); // Keeps track of rows created at runtime.

    private void Awake()
    {
        if (sessionRowPrefab != null)
            sessionRowPrefab.gameObject.SetActive(false);
    }

    public void Render(DashboardMetrics metrics)
    {
        ClearRows(); // Remove old rows before drawing new ones.

        if (sessionRowPrefab != null)
            sessionRowPrefab.gameObject.SetActive(false);

        if (metrics == null || metrics.Sessions == null || metrics.Sessions.Count == 0)
            return;

        foreach (DashboardSessionViewModel session in metrics.Sessions)
        {
            DashboardSessionRowUI row = Instantiate(sessionRowPrefab, contentRoot);
            row.gameObject.SetActive(true);
            row.transform.localScale = Vector3.one;
            row.Bind(session);
            spawnedRows.Add(row.gameObject);
        }

        if (insightBox != null)
            insightBox.SetAsLastSibling(); // Keep insights after all session rows.

        if (contentRoot is RectTransform rt)
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt); // Refresh the layout immediately.
    }

    private void ClearRows()
    {
        foreach (GameObject row in spawnedRows)
        {
            if (row != null)
                Destroy(row);
        }

        spawnedRows.Clear();
    }
}