using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class GarageNotification : MonoBehaviour
{
    private RectTransform panel;
    private RectTransform canvasRect;
    private TMP_Text messageLabel;
    private ScrollRect scroll;
    private CanvasGroup group;
    private Coroutine transition;
    private float holdSeconds;

    public static GarageNotification Show(Transform context, string message, float duration = 6)
    {
        var canvas = context.GetComponentInParent<Canvas>().rootCanvas;
        var notice = canvas.GetComponentInChildren<GarageNotification>(true);
        if (notice == null) notice = Create(canvas);
        notice.Present(message, duration);
        return notice;
    }

    private static GarageNotification Create(Canvas canvas)
    {
        var root = GarageVisualStyle.Panel(canvas.transform, "GarageNotification", GarageVisualStyle.Surface);
        var notice = root.AddComponent<GarageNotification>();
        notice.group = root.GetComponent<CanvasGroup>();
        notice.panel = root.GetComponent<RectTransform>();
        notice.canvasRect = canvas.transform as RectTransform;
        notice.panel.anchorMin = notice.panel.anchorMax = notice.panel.pivot = Vector2.zero;
        notice.panel.anchoredPosition = new Vector2(24, 160);
        var accent = GarageVisualStyle.Panel(root.transform, "Accent", GarageVisualStyle.Accent);
        GarageVisualStyle.Rect(accent.transform, Vector2.zero, new Vector2(0, 1));
        ((RectTransform)accent.transform).sizeDelta = new Vector2(3, 0);
        accent.GetComponent<Image>().raycastTarget = false;
        var heading = GarageVisualStyle.Label(root.transform, "Title", "操作提示", 18,
            new Vector2(0, 1), Vector2.one);
        var titleRect = heading.rectTransform;
        titleRect.offsetMin = new Vector2(20, -40);
        titleRect.offsetMax = new Vector2(-60, -12);
        heading.fontStyle = FontStyle.Bold;

        var close = GarageVisualStyle.Panel(root.transform, "CloseNotification", GarageVisualStyle.Raised);
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-12, -10);
        closeRect.sizeDelta = new Vector2(32, 32);
        var button = close.AddComponent<Button>();
        GarageVisualStyle.SetButtonLabel(button, "\u00d7");
        GarageVisualStyle.ButtonStyle(button);
        button.onClick.AddListener(notice.Dismiss);

        var viewport = new GameObject("MessageViewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        GarageVisualStyle.Rect(viewport.transform, Vector2.zero, Vector2.one);
        ((RectTransform)viewport.transform).offsetMin = new Vector2(20, 16);
        ((RectTransform)viewport.transform).offsetMax = new Vector2(-20, -52);
        var message = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        message.transform.SetParent(viewport.transform, false);
        GarageVisualStyle.Rect(message.transform, new Vector2(0, 1), Vector2.one);
        notice.messageLabel = message.GetComponent<TextMeshProUGUI>();
        ChineseUiFont.Apply(notice.messageLabel);
        notice.messageLabel.fontSize = 18;
        notice.messageLabel.color = GarageVisualStyle.Text;
        notice.messageLabel.raycastTarget = false;
        notice.messageLabel.richText = false;
        notice.messageLabel.alignment = TextAlignmentOptions.TopLeft;
        notice.messageLabel.textWrappingMode = TextWrappingModes.Normal;
        notice.messageLabel.overflowMode = TextOverflowModes.Overflow;
        notice.messageLabel.lineSpacing = 4;
        notice.messageLabel.rectTransform.pivot = new Vector2(.5f, 1);
        notice.scroll = root.AddComponent<ScrollRect>();
        notice.scroll.viewport = viewport.transform as RectTransform;
        notice.scroll.content = notice.messageLabel.rectTransform;
        notice.scroll.horizontal = false;
        notice.scroll.movementType = ScrollRect.MovementType.Clamped;
        notice.scroll.scrollSensitivity = 24;
        return notice;
    }

    private void Present(string message, float duration)
    {
        if (transition != null) StopCoroutine(transition);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        messageLabel.text = message;
        holdSeconds = Mathf.Max(.1f, duration);
        Layout();
        scroll.verticalNormalizedPosition = 1;
        transition = StartCoroutine(Display());
    }

    private void LateUpdate() => Layout();

    private void Layout()
    {
        if (panel == null || canvasRect == null) return;
        float width = Mathf.Min(560, Mathf.Max(240, canvasRect.rect.width - 48));
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        float textHeight = messageLabel.GetPreferredValues(messageLabel.text, width - 40, Mathf.Infinity).y;
        float limit = Mathf.Max(110, Mathf.Min(260, canvasRect.rect.height - 240));
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(textHeight + 68, 110, limit));
        messageLabel.rectTransform.sizeDelta = new Vector2(0, Mathf.Max(32, textHeight));
    }

    private IEnumerator Display()
    {
        group.alpha = 0;
        group.interactable = group.blocksRaycasts = true;
        yield return Fade(1, .14f);
        yield return new WaitForSecondsRealtime(holdSeconds);
        yield return Fade(0, .18f);
        transition = null;
        gameObject.SetActive(false);
    }

    public void Dismiss()
    {
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(Close());
    }

    private IEnumerator Close()
    {
        group.interactable = group.blocksRaycasts = false;
        yield return Fade(0, .14f);
        transition = null;
        gameObject.SetActive(false);
    }

    private IEnumerator Fade(float target, float duration)
    {
        float start = group.alpha, elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        group.alpha = target;
    }

    private void OnDisable()
    {
        if (transition != null) StopCoroutine(transition);
        transition = null;
    }
}
