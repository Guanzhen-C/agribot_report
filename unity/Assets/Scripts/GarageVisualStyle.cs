using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class GarageVisualStyle
{
    public static readonly Color Background = new(.055f, .065f, .075f);
    public static readonly Color Surface = new(.10f, .12f, .135f);
    public static readonly Color Raised = new(.16f, .19f, .20f);
    public static readonly Color Accent = new(.75f, .88f, .27f);
    public static readonly Color Text = new(.93f, .95f, .95f);
    public static readonly Color Muted = new(.56f, .63f, .65f);

    public static void Rect(Transform item, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)item; rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    public static GameObject Panel(Transform parent, string name, Color colour)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false); obj.GetComponent<Image>().color = colour; return obj;
    }
    public static Text Label(Transform parent, string name, string value, int size, Vector2 min, Vector2 max, Color? color = null)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
        Rect(obj.transform, min, max);
        var label = obj.GetComponent<Text>(); ChineseUiFont.Apply(label); label.text = value;
        label.fontSize = size; label.color = color ?? Text; label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleLeft; label.supportRichText = false;
        return label;
    }
    public static void ButtonStyle(Button button, bool primary = false)
    {
        if (button == null) return;
        var image = button.GetComponent<Image>();
        if (image != null) { image.sprite = null; image.overrideSprite = null; image.color = primary ? Accent : Raised; }
        button.transition = Selectable.Transition.ColorTint;
        button.spriteState = default;
        var colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(.85f, .95f, .80f);
        colors.selectedColor = Color.white; colors.pressedColor = new Color(.65f, .77f, .59f);
        button.colors = colors;
        foreach (var text in button.GetComponentsInChildren<Text>(true))
        {
            ChineseUiFont.Apply(text); text.fontSize = 18; text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12; text.resizeTextMaxSize = 18;
            text.color = primary ? Background : Text;
        }
    }

    public static void VehicleBackButton(Button button)
    {
        if (button == null) return;
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(24, -32);
        rect.sizeDelta = new Vector2(180, 64);
        rect.localScale = Vector3.one;
        SetButtonLabel(button, "返回车库");
        ButtonStyle(button);
    }

    public static void SetButtonLabel(Button button, string value)
    {
        var label = button.GetComponentInChildren<Text>(true);
        if (label == null)
            label = Label(button.transform, "Label", "", 18, Vector2.zero, Vector2.one);
        label.gameObject.SetActive(true);
        label.enabled = true;
        label.text = value;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        Rect(label.transform, Vector2.zero, Vector2.one);
        label.transform.localScale = Vector3.one;
        foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
            if (graphic != button.image && graphic != label) graphic.enabled = false;
    }

    public static void EquipmentPanel(Transform root)
    {
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.transform == root || image.name.Contains("Background") || image.name.Contains("Panel"))
            { image.sprite = null; image.overrideSprite = null; image.color = Surface; }
        }
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            ChineseUiFont.Apply(text); text.color = Text; text.fontSize = Mathf.Min(text.fontSize, 26);
            NormalizePanelTextScale(text.transform, root);
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            ChineseUiFont.Apply(text); text.color = Text; text.fontSize = Mathf.Min(text.fontSize, 26);
            NormalizePanelTextScale(text.transform, root);
        }
        foreach (var button in root.GetComponentsInChildren<Button>(true))
            ButtonStyle(button, button.name.Contains("Install") && !button.name.Contains("Uninstall"));
        var header = root.Find("Header");
        var background = header != null ? header.GetComponent<Image>() : null;
        if (background != null)
        { background.sprite = null; background.overrideSprite = null; background.color = Surface; }
        var close = header != null ? header.Find("CloseButton")?.GetComponent<Button>() : null;
        if (close != null)
        {
            SetButtonLabel(close, "关闭");
            ButtonStyle(close);
        }
    }

    private static void NormalizePanelTextScale(Transform label, Transform panel)
    {
        if (label.parent == null || label == panel) return;
        // Serialized panel and detail containers have unequal X/Y scales.
        // Keep glyphs uniform without changing the panel or equipment icons.
        Vector3 parentScale = label.parent.lossyScale;
        Vector3 panelScale = panel.lossyScale;
        float scale = Mathf.Min(Mathf.Abs(panelScale.x), Mathf.Abs(panelScale.y));
        if (Mathf.Abs(parentScale.x) < .0001f || Mathf.Abs(parentScale.y) < .0001f) return;
        label.localScale = new Vector3(scale / Mathf.Abs(parentScale.x), scale / Mathf.Abs(parentScale.y), 1f);
    }

    public static void NormalizePanelText(Transform root)
    {
        foreach (var label in root.GetComponentsInChildren<Text>(true)) NormalizePanelTextScale(label.transform, root);
        foreach (var label in root.GetComponentsInChildren<TMP_Text>(true)) NormalizePanelTextScale(label.transform, root);
    }
}
