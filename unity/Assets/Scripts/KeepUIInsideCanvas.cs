using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class KeepUIInsideCanvas : MonoBehaviour
{
    [SerializeField, Min(0f)] private float margin = 20f;

    private RectTransform rectTransform;
    private RectTransform canvasRectTransform;
    private readonly Vector3[] worldCorners = new Vector3[4];

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        KeepInsideCanvas();
    }

    private void LateUpdate()
    {
        KeepInsideCanvas();
    }

    private void CacheReferences()
    {
        rectTransform = GetComponent<RectTransform>();
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRectTransform = canvas != null ? canvas.GetComponent<RectTransform>() : null;
    }

    private void KeepInsideCanvas()
    {
        if (rectTransform == null || canvasRectTransform == null)
            return;

        rectTransform.GetWorldCorners(worldCorners);

        Vector2 bottomLeft = canvasRectTransform.InverseTransformPoint(worldCorners[0]);
        Vector2 topRight = canvasRectTransform.InverseTransformPoint(worldCorners[2]);
        Rect canvasRect = canvasRectTransform.rect;

        float left = canvasRect.xMin + margin;
        float right = canvasRect.xMax - margin;
        float bottom = canvasRect.yMin + margin;
        float top = canvasRect.yMax - margin;

        Vector2 correction = Vector2.zero;
        if (bottomLeft.x < left)
            correction.x = left - bottomLeft.x;
        else if (topRight.x > right)
            correction.x = right - topRight.x;

        if (bottomLeft.y < bottom)
            correction.y = bottom - bottomLeft.y;
        else if (topRight.y > top)
            correction.y = top - topRight.y;

        if (correction.sqrMagnitude <= 0.0001f)
            return;

        Vector3 worldCorrection = canvasRectTransform.TransformVector(correction);
        Vector3 parentCorrection = rectTransform.parent != null
            ? rectTransform.parent.InverseTransformVector(worldCorrection)
            : worldCorrection;
        rectTransform.anchoredPosition += (Vector2)parentCorrection;
    }
}
