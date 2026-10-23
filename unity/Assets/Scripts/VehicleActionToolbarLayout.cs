using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class VehicleActionToolbarLayout : MonoBehaviour
{
    private RectTransform canvasRect;
    private RectTransform toolbarRect;

    public static GameObject Create(Transform parent, Canvas canvas)
    {
        var toolbar = new GameObject("VehicleActionToolbar", typeof(RectTransform),
            typeof(GridLayoutGroup), typeof(VehicleActionToolbarLayout));
        toolbar.transform.SetParent(parent, false);
        var rect = toolbar.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(220, -32);
        rect.sizeDelta = new Vector2(1050, 80);
        var layout = toolbar.GetComponent<GridLayoutGroup>();
        layout.spacing = new Vector2(10, 10);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.childAlignment = TextAnchor.UpperLeft;
        toolbar.GetComponent<VehicleActionToolbarLayout>().SetCanvas(canvas.transform as RectTransform);
        return toolbar;
    }

    public void SetCanvas(RectTransform canvas)
    {
        canvasRect = canvas;
        toolbarRect = transform as RectTransform;
        UpdateWidth();
    }

    private void LateUpdate() => UpdateWidth();

    private void UpdateWidth()
    {
        if (canvasRect == null || toolbarRect == null || canvasRect.rect.width <= 0f)
            return;

        Arrange(canvasRect.rect.width);
    }

    public void Arrange(float canvasWidth)
    {
        float width = Mathf.Max(140, Mathf.Min(1050, canvasWidth - 244));
        int columns = Mathf.Clamp(Mathf.FloorToInt((width + 10) / 160), 1, 5);
        var layout = GetComponent<GridLayoutGroup>();
        layout.constraintCount = columns;
        layout.cellSize = new Vector2((width - (columns - 1) * 10) / columns, 64);
        toolbarRect.sizeDelta = new Vector2(width, Mathf.Ceil(5f / columns) * 74 - 10);
    }

    public static void PositionSidePanel(RectTransform panel)
    {
        if (panel == null) return;
        var canvas = panel.GetComponentInParent<Canvas>();
        var bounds = canvas != null ? canvas.transform as RectTransform : null;
        if (bounds == null) return;
        float top = 112;
        foreach (var toolbar in FindObjectsByType<VehicleActionToolbarLayout>(FindObjectsSortMode.None))
        {
            if (!toolbar.gameObject.activeInHierarchy) continue;
            var corners = new Vector3[4];
            ((RectTransform)toolbar.transform).GetWorldCorners(corners);
            var sourceCanvas = toolbar.GetComponentInParent<Canvas>();
            var screen = RectTransformUtility.WorldToScreenPoint(sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : sourceCanvas.worldCamera, corners[0]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var point);
            top = Mathf.Max(top, bounds.rect.yMax - point.y + 14);
        }
        float width = Mathf.Min(520, Mathf.Max(280, bounds.rect.width - 260));
        panel.anchorMin = new Vector2(1, 0);
        panel.anchorMax = Vector2.one;
        panel.offsetMin = new Vector2(-24 - width, 80);
        panel.offsetMax = new Vector2(-24, -top);
        foreach (var grid in panel.GetComponentsInChildren<GridLayoutGroup>(true))
        {
            if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) continue;
            int count = Mathf.Max(1, grid.constraintCount);
            float available = ((RectTransform)grid.transform).rect.width;
            if (available <= 0) continue;
            float cell = Mathf.Max(24, (available - grid.padding.horizontal - grid.spacing.x * (count - 1)) / count);
            grid.cellSize = new Vector2(cell, cell);
        }
    }
}
