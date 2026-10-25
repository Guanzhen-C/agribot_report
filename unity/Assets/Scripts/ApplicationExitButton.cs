using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ApplicationExitButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private static ApplicationExitButton instance;
    private GameObject tooltip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (instance != null) return;
        var overlay = new GameObject("ApplicationWindowControls", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        DontDestroyOnLoad(overlay);

        var root = GarageVisualStyle.Panel(overlay.transform, "ExitApplication", GarageVisualStyle.Raised);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-14, -14);
        rect.sizeDelta = new Vector2(48, 48);
        var button = root.AddComponent<Button>();
        GarageVisualStyle.SetButtonLabel(button, "\u00d7");
        GarageVisualStyle.ButtonStyle(button);
        var label = button.GetComponentInChildren<Text>();
        label.resizeTextForBestFit = false;
        label.fontSize = 32;
        button.onClick.AddListener(() => Application.Quit());

        instance = root.AddComponent<ApplicationExitButton>();
        instance.tooltip = GarageVisualStyle.Panel(root.transform, "ExitTooltip", GarageVisualStyle.Surface);
        instance.tooltip.GetComponent<Image>().raycastTarget = false;
        var tipRect = (RectTransform)instance.tooltip.transform;
        tipRect.anchorMin = tipRect.anchorMax = new Vector2(1, 0);
        tipRect.pivot = Vector2.one;
        tipRect.anchoredPosition = new Vector2(0, -8);
        tipRect.sizeDelta = new Vector2(112, 36);
        var tip = GarageVisualStyle.Label(instance.tooltip.transform, "Label", "退出软件", 16,
            Vector2.zero, Vector2.one);
        tip.alignment = TextAnchor.MiddleCenter;
        instance.tooltip.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => tooltip.SetActive(true);
    public void OnPointerExit(PointerEventData eventData) => tooltip.SetActive(false);
}
