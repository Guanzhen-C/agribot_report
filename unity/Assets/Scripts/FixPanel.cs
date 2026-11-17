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
        foreach (Transform option in transform)
        {
            string title, description;
            if (option.name == "note_1")
            {
                title = "调整左右轮距";
                description = "左右车轮中心距离（cm）";
            }
            else if (option.name.StartsWith("note_2"))
            {
                title = "调整前后轴距";
                description = "前、后车轴中心距离（cm）";
            }
            else if (option.name.StartsWith("note_3"))
            {
                title = "调整车轮半径";
                description = "车轮整体半径（cm）";
            }
            else if (option.name.StartsWith("note_4"))
            {
                title = "装备面板";
                description = "再次点击进入装备面板";
            }
            else if (option.name == "note_5")
            {
                title = "爆炸图";
                description = "再次点击进入爆炸图";
            }
            else continue;
            foreach (Text label in option.GetComponentsInChildren<Text>(true))
            {
                label.text = label.name == "content" ? description : title;
                label.fontStyle = FontStyle.Normal;
                label.fontSize = label.name == "content" ? 18 : 22;
            }
            foreach (var image in option.GetComponentsInChildren<Image>(true))
            {
                if (!image.name.StartsWith("head_bg")) continue;
                image.sprite = image.overrideSprite = null;
                image.color = GarageVisualStyle.Raised;
                GarageVisualStyle.Rect(image.transform, Vector2.zero, Vector2.one);
            }
        }
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
