using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class WorldTargetUI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 50f);
    [SerializeField, Range(0f, 0.25f)] private float viewportPadding = 0.02f;

    private RectTransform buttonRect;
    private RectTransform canvasRect;
    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;
    private Renderer targetRenderer;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    public Camera TargetCamera
    {
        get => targetCamera;
        set => targetCamera = value;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnTransformParentChanged()
    {
        CacheReferences();
    }

    private void LateUpdate()
    {
        if (target == null || targetCamera == null || buttonRect == null || canvasRect == null)
        {
            SetVisible(false);
            return;
        }

        if (targetRenderer == null)
            targetRenderer = target.GetComponentInChildren<Renderer>();

        if (!target.gameObject.activeInHierarchy ||
            (targetRenderer != null && !targetRenderer.enabled))
        {
            SetVisible(false);
            return;
        }

        Vector3 worldPoint = targetRenderer != null ? targetRenderer.bounds.center : target.position;
        Vector3 viewportPoint = targetCamera.WorldToViewportPoint(worldPoint);
        bool isVisible = viewportPoint.z > 0f &&
                         viewportPoint.x >= viewportPadding &&
                         viewportPoint.x <= 1f - viewportPadding &&
                         viewportPoint.y >= viewportPadding &&
                         viewportPoint.y <= 1f - viewportPadding;

        SetVisible(isVisible);
        if (!isVisible)
            return;

        Vector3 screenPoint = targetCamera.WorldToScreenPoint(worldPoint);

        Camera canvasCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, canvasCamera, out Vector2 canvasPosition))
        {
            buttonRect.anchoredPosition = canvasPosition + screenOffset;
        }
    }

    private void CacheReferences()
    {
        buttonRect = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();
        canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
        canvasGroup = GetComponent<CanvasGroup>();
        targetRenderer = target != null ? target.GetComponentInChildren<Renderer>() : null;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
