using System;
using System.Collections;
using RG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class WheelChangePanel : MonoBehaviour
{
    protected enum PanelShowEffect
    {
        Fade,
        ShakeUI,
        PunchUI
    }

    protected virtual PanelShowEffect ShowEffect => this switch
    {
        bottonPanel => PanelShowEffect.ShakeUI,
        WheelDistancePanel => PanelShowEffect.PunchUI,
        _ => PanelShowEffect.Fade
    };

    [Header("Fade")]
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.35f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("RTweens Panel Effects")]
    [SerializeField, Min(0f)] private float shakeStrength = 20f;
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.5f;
    [SerializeField, Min(1)] private int shakeVibrato = 20;
    [SerializeField] private Vector2 punchOffset = Vector2.up * 50f;
    [SerializeField, Min(0.01f)] private float punchDuration = 0.5f;
    [SerializeField, Min(1)] private int punchVibrato = 10;
    [SerializeField, Range(0f, 1f)] private float punchElasticity = 1f;
    [SerializeField, Min(0.01f)] private float panelSlideOutDuration = 0.5f;
    [SerializeField, Min(1f)] private float panelSlideOutDistance = 2000f;

    // bottomPanel also derives from WheelChangePanel. When it is nested below the
    // outer WheelChangePanel, use the outer component as the single fade setting
    // source so changing the visible panel's Inspector values actually takes effect.
    private WheelChangePanel parentFadeSettings;

    private CanvasGroup canvasGroup;
    private RectTransform panelRect;
    protected Vector2 visibleAnchoredPosition;
    private TweenHandle panelTween;
    private bool visiblePositionCached;
    private Coroutine fadeCoroutine;
    private EscButtonFade escButtonFade;
    private PopFade popFade;

    [FormerlySerializedAs("Esc_btn")]
    [SerializeField] private Button escButton;
    protected CarChange carChange;
    public GameObject Pop;
    protected OrbitCameraController orbitCameraController;

    protected virtual void Awake()
    {
        GarageVisualStyle.EquipmentPanel(transform);
        CacheVisiblePosition();
        CacheParentFadeSettings();
        EnsureCanvasGroup();
        EnsureEscButtonFade();
        GameObject carObject = GameObject.Find("Car0");
        if (carObject != null)
            carChange = carObject.GetComponentInChildren<CarChange>(true);
        EnsurePopFade();
        if (orbitCameraController == null)
        {
            GameObject freeLookCamera = GameObject.Find("FreeLook Camera");
            if (freeLookCamera != null)
                orbitCameraController = freeLookCamera.GetComponent<OrbitCameraController>();
        }

        if (orbitCameraController != null)
        {
            RemoveEscClickListener(orbitCameraController.UnlockFixedView);
            AddEscClickListener(orbitCameraController.UnlockFixedView);
        }
        //AddEscClickListener(HideEscButton);

        //AddEscClickListener(Hide);
    }

    protected void ShowPop(String text)
    {
        ShowPop(text, 2f);
    }

    protected void ShowPop(String text, float visibleDuration)
    {
        GarageNotification.Show(transform, text, visibleDuration);
    }

    protected void ShowDefaultPop(float visibleDuration)
    {
        var label = Pop != null ? Pop.GetComponentInChildren<Text>(true) : null;
        if (label != null) GarageNotification.Show(transform, label.text, visibleDuration);
    }
    public void Show()
    {
        CacheVisiblePosition();
        gameObject.SetActive(true);
        EnsureCanvasGroup();
        StopFade();
        StopPanelTween(true);
        ShowEscButton();

        if (ShowEffect == PanelShowEffect.Fade)
        {
            canvasGroup.alpha = 0f;
            StartFade(1f, FadeInDuration, false);
            return;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        panelTween = ShowEffect == PanelShowEffect.ShakeUI
            ? RTweenEffects.ShakeUIPosition(
                panelRect,
                shakeStrength,
                shakeDuration,
                shakeVibrato,
                90f,
                true)
            : RTweenEffects.PunchUIPosition(
                panelRect,
                punchOffset,
                punchDuration,
                punchVibrato,
                punchElasticity);

        if (!panelTween.IsActive)
        {
            CompleteShowEffect();
            return;
        }

        panelTween.OnComplete(CompleteShowEffect);
    }

    public void Hide()
    {
        if (!gameObject.activeInHierarchy)
            return;

        EnsureCanvasGroup();
        HideEscButton();

        if (ShowEffect == PanelShowEffect.Fade)
        {
            StartFade(0f, FadeOutDuration, true);
            return;
        }

        StartSlideOut();
    }

    public void ShowEscButton()
    {
        EnsureEscButtonFade();
        escButtonFade?.Show(FadeInDuration, UseUnscaledTime);
    }

    public void HideEscButton()
    {
        EnsureEscButtonFade();
        escButtonFade?.Hide(FadeOutDuration, UseUnscaledTime);
    }

    public void AddEscClickListener(UnityAction action)
    {
        if (escButton != null && action != null)
            escButton.onClick.AddListener(action);
    }

    public void RemoveEscClickListener(UnityAction action)
    {
        if (escButton != null && action != null)
            escButton.onClick.RemoveListener(action);
    }

    public void ShowImmediately()
    {
        CacheVisiblePosition();
        StopFade();
        StopPanelTween(true);
        gameObject.SetActive(true);
        SetVisibleImmediately(true);
        EnsureEscButtonFade();
        escButtonFade?.ShowImmediately();
    }

    public void HideImmediately()
    {
        StopFade();
        StopPanelTween(true);
        SetVisibleImmediately(false);
        gameObject.SetActive(false);
        EnsureEscButtonFade();
        escButtonFade?.HideImmediately();
    }

    private void CacheVisiblePosition()
    {
        if (visiblePositionCached)
            return;

        panelRect = transform as RectTransform;
        if (panelRect != null)
            visibleAnchoredPosition = panelRect.anchoredPosition;

        visiblePositionCached = true;
    }

    private void CompleteShowEffect()
    {
        panelTween = default;
        if (panelRect != null)
            panelRect.anchoredPosition = visibleAnchoredPosition;

        if (canvasGroup == null || !gameObject.activeInHierarchy)
            return;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void StartSlideOut()
    {
        StopFade();
        StopPanelTween(true);

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Vector2 targetPosition =
            visibleAnchoredPosition + Vector2.left * panelSlideOutDistance;
        panelTween = RTween.UIMove(
                panelRect,
                targetPosition,
                panelSlideOutDuration)
            .SetEase(EaseType.InBack)
            .OnComplete(() =>
            {
                panelTween = default;
                if (panelRect != null)
                    panelRect.anchoredPosition = visibleAnchoredPosition;

                gameObject.SetActive(false);
            });

        if (!panelTween.IsActive)
        {
            panelRect.anchoredPosition = visibleAnchoredPosition;
            gameObject.SetActive(false);
        }
    }

    private void StopPanelTween(bool restorePosition)
    {
        if (panelTween.IsActive)
            panelTween.Kill();

        panelTween = default;
        if (restorePosition && panelRect != null)
            panelRect.anchoredPosition = visibleAnchoredPosition;
    }

    private void StopFade()
    {
        if (fadeCoroutine == null)
            return;

        StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }

    private void StartFade(float targetAlpha, float duration, bool deactivateWhenComplete)
    {
        StopFade();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        fadeCoroutine = StartCoroutine(FadeTo(targetAlpha, duration, deactivateWhenComplete));
    }

    private IEnumerator FadeTo(float targetAlpha, float duration, bool deactivateWhenComplete)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
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

    private float FadeInDuration => parentFadeSettings != null
        ? parentFadeSettings.fadeInDuration
        : fadeInDuration;

    private float FadeOutDuration => parentFadeSettings != null
        ? parentFadeSettings.fadeOutDuration
        : fadeOutDuration;

    private bool UseUnscaledTime => parentFadeSettings != null
        ? parentFadeSettings.useUnscaledTime
        : useUnscaledTime;

    private void CacheParentFadeSettings()
    {
        Transform current = transform.parent;
        while (current != null && parentFadeSettings == null)
        {
            parentFadeSettings = current.GetComponent<WheelChangePanel>();
            current = current.parent;
        }
    }

    private void EnsureEscButtonFade()
    {
        if (escButton == null || escButtonFade != null)
            return;

        escButtonFade = escButton.GetComponent<EscButtonFade>();
        if (escButtonFade == null)
            escButtonFade = escButton.gameObject.AddComponent<EscButtonFade>();
    }

    private void EnsurePopFade()
    {
        if (Pop == null || popFade != null)
            return;

        popFade = Pop.GetComponent<PopFade>();
        if (popFade == null)
            popFade = Pop.AddComponent<PopFade>();
    }

    
    private void OnDisable()
    {
        StopFade();
        StopPanelTween(true);
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0.01f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0.01f, fadeOutDuration);
        shakeStrength = Mathf.Max(0f, shakeStrength);
        shakeDuration = Mathf.Max(0.01f, shakeDuration);
        shakeVibrato = Mathf.Max(1, shakeVibrato);
        punchDuration = Mathf.Max(0.01f, punchDuration);
        punchVibrato = Mathf.Max(1, punchVibrato);
        punchElasticity = Mathf.Clamp01(punchElasticity);
        panelSlideOutDuration = Mathf.Max(0.01f, panelSlideOutDuration);
        panelSlideOutDistance = Mathf.Max(1f, panelSlideOutDistance);
    }
}

[RequireComponent(typeof(CanvasGroup))]
public sealed class PopFade : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    public void Show(float fadeInDuration, float visibleDuration, float fadeOutDuration, bool useUnscaledTime)
    {
        EnsureCanvasGroup();

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        fadeCoroutine = StartCoroutine(ShowRoutine(
            fadeInDuration,
            visibleDuration,
            fadeOutDuration,
            useUnscaledTime));
    }

    private IEnumerator ShowRoutine(float fadeInDuration, float visibleDuration, float fadeOutDuration, bool useUnscaledTime)
    {
        yield return FadeTo(1f, fadeInDuration, useUnscaledTime);

        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(visibleDuration);
        else
            yield return new WaitForSeconds(visibleDuration);

        yield return FadeTo(0f, fadeOutDuration, useUnscaledTime);

        fadeCoroutine = null;
        gameObject.SetActive(false);
    }

    private IEnumerator FadeTo(float targetAlpha, float duration, bool useUnscaledTime)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnDisable()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }
}

[RequireComponent(typeof(CanvasGroup))]
public sealed class EscButtonFade : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    public void Show(float duration, bool useUnscaledTime)
    {
        EnsureCanvasGroup();
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        canvasGroup.alpha = 0f;
        SetInteraction(false);
        gameObject.SetActive(true);
        fadeCoroutine = StartCoroutine(FadeTo(1f, duration, useUnscaledTime, false));
    }

    public void Hide(float duration, bool useUnscaledTime)
    {
        if (!gameObject.activeInHierarchy)
            return;

        EnsureCanvasGroup();
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        SetInteraction(false);
        fadeCoroutine = StartCoroutine(FadeTo(0f, duration, useUnscaledTime, true));
    }

    public void ShowImmediately()
    {
        EnsureCanvasGroup();
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        SetInteraction(true);
    }

    public void HideImmediately()
    {
        EnsureCanvasGroup();
        canvasGroup.alpha = 0f;
        SetInteraction(false);
        gameObject.SetActive(false);
    }

    private IEnumerator FadeTo(float targetAlpha, float duration, bool useUnscaledTime, bool deactivateWhenComplete)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        fadeCoroutine = null;

        if (deactivateWhenComplete)
        {
            gameObject.SetActive(false);
            yield break;
        }

        SetInteraction(true);
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void SetInteraction(bool enabled)
    {
        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private void OnDisable()
    {
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }
}
