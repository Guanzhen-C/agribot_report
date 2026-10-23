using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
public sealed class WorldSpaceTargetUI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.08f, 0f);
    [SerializeField, Range(0f, 0.25f)] private float viewportPadding = 0.08f;
    [SerializeField, Min(0f)] private float occlusionRadius = 0.04f;
    [SerializeField] private LayerMask occlusionLayers = ~0;

    private CanvasGroup canvasGroup;
    private Renderer targetRenderer;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        CacheTargetRenderer();
    }

    private void LateUpdate()
    {
        if (target == null || targetCamera == null || !target.gameObject.activeInHierarchy)
        {
            SetVisible(false);
            return;
        }

        if (targetRenderer == null)
            CacheTargetRenderer();

        if (targetRenderer != null && !targetRenderer.enabled)
        {
            SetVisible(false);
            return;
        }

        Bounds bounds = targetRenderer != null
            ? targetRenderer.bounds
            : new Bounds(target.position, Vector3.zero);

        Vector3 anchorPosition = bounds.center + Vector3.up * bounds.extents.y + worldOffset;
        transform.position = anchorPosition;
        transform.rotation = targetCamera.transform.rotation;

        Vector3 viewportPoint = targetCamera.WorldToViewportPoint(bounds.center);
        bool insideSafeViewport = viewportPoint.z > 0f &&
                                  viewportPoint.x >= viewportPadding &&
                                  viewportPoint.x <= 1f - viewportPadding &&
                                  viewportPoint.y >= viewportPadding &&
                                  viewportPoint.y <= 1f - viewportPadding;

        bool occluded = insideSafeViewport && IsOccluded(anchorPosition);
        SetVisible(insideSafeViewport && !occluded);
    }

    private bool IsOccluded(Vector3 anchorPosition)
    {
        Vector3 origin = targetCamera.transform.position;
        Vector3 delta = anchorPosition - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return false;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            occlusionRadius,
            delta / distance,
            distance,
            occlusionLayers,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == target || hitTransform.IsChildOf(target))
                continue;

            return true;
        }

        return false;
    }

    private void CacheTargetRenderer()
    {
        targetRenderer = target != null ? target.GetComponentInChildren<Renderer>() : null;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
