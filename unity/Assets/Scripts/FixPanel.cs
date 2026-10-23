using System.Collections;
using RG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class FixPanel : MonoBehaviour
{
    [Header("Fade Out")]
    [SerializeField, Min(0.01f)] private float fadeOutDuration = .22f;
    [SerializeField] private bool useUnscaledTime = true;

    private CanvasGroup canvasGroup;
    private RectTransform panelRect;
    private Vector2 restingAnchoredPosition;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        GarageVisualStyle.EquipmentPanel(transform);
        foreach (Transform child in transform)
        {
            var background = child.GetComponent<Image>();
            if (background == null || child.GetComponent<Button>() != null) continue;
            background.sprite = null;
            background.overrideSprite = null;
            background.color = GarageVisualStyle.Surface;
        }
        var close = transform.Find("Header/CloseButton")?.GetComponent<Button>();
        if (close != null)
        {
            GarageVisualStyle.SetButtonLabel(close, "关闭");
            GarageVisualStyle.ButtonStyle(close);
        }
        canvasGroup = GetComponent<CanvasGroup>();
        panelRect = transform as RectTransform;
        restingAnchoredPosition = panelRect.anchoredPosition;
        SetVisibleImmediately(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        GarageVisualStyle.NormalizePanelText(transform);
        EnsureCanvasGroup();
        StopFade();
        StopMoveWave(true);

        canvasGroup.alpha = 0;
        StartFade(1, .18f, false);
    }

    private void LateUpdate() => VehicleActionToolbarLayout.PositionSidePanel(transform as RectTransform);

    public void Hide()
    {
        if (!gameObject.activeInHierarchy)
            return;

        EnsureCanvasGroup();
        StopMoveWave(true);
        StartFade(0f, fadeOutDuration, true);
    }

    public void HideImmediately()
    {
        StopFade();
        StopMoveWave(true);
        SetVisibleImmediately(false);
        gameObject.SetActive(false);
    }

    private void StopFade()
    {
        if (fadeCoroutine == null)
            return;

        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }

    private void StopMoveWave(bool restorePosition)
    {
        if (panelRect == null)
            return;

        RTween.Kill(panelRect);
        if (restorePosition)
            panelRect.anchoredPosition = restingAnchoredPosition;
    }

    private void StartFade(float targetAlpha, float duration, bool deactivateWhenComplete)
    {
        StopFade();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        fadeCoroutine = StartCoroutine(
            FadeTo(targetAlpha, duration, deactivateWhenComplete));
    }

    private IEnumerator FadeTo(float targetAlpha, float duration, bool deactivateWhenComplete)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.SmoothStep(startAlpha, targetAlpha, progress);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeCoroutine = null;
        if (deactivateWhenComplete)
        {
            gameObject.SetActive(false);
            yield break;
        }

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void SetVisibleImmediately(bool visible)
    {
        EnsureCanvasGroup();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnDisable()
    {
        StopFade();
        StopMoveWave(true);
    }

    private void OnValidate()
    {
        fadeOutDuration = Mathf.Max(0.01f, fadeOutDuration);
    }
}
