using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Agribot.VehicleConfiguration;

public sealed class FarmGarageGrid : MonoBehaviour
{
    private RectTransform viewport, content;
    private GridLayoutGroup grid;
    private Vector2 lastSize;
    private string filter = "all";
    private readonly List<Sprite> icons = new();
    private readonly List<Texture2D> textures = new();

    public static void Extend(Transform mainPanel, IReadOnlyList<GameObject> vehicles, Action<GameObject> selectVehicle)
    {
        var cards = mainPanel.GetComponentsInChildren<Button>(true)
            .Where(b => b.name.EndsWith("_Img") && b.name.StartsWith("Car")).ToArray();
        if (cards.Length != 6) throw new InvalidOperationException("车库车型入口不完整。");
        foreach (var text in mainPanel.GetComponentsInChildren<Text>(true))
            if (text.GetComponentInParent<Button>() == null) text.gameObject.SetActive(false);
        var background = GarageVisualStyle.Panel(mainPanel, "GarageBackground", GarageVisualStyle.Background);
        GarageVisualStyle.Rect(background.transform, Vector2.zero, Vector2.one);
        background.transform.SetAsFirstSibling();
        GarageVisualStyle.Label(mainPanel, "GarageBrand", "AGRIBOT  /  农机实验室", 29,
            new Vector2(.035f, .92f), new Vector2(.72f, .99f));
        GarageVisualStyle.Label(mainPanel, "GarageSection", "车库", 20,
            new Vector2(.035f, .85f), new Vector2(.15f, .91f), GarageVisualStyle.Muted);
        var holder = new GameObject("GarageScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(FarmGarageGrid));
        holder.transform.SetParent(mainPanel, false);
        var view = holder.AddComponent<RectMask2D>();
        var rect = holder.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.035f, .055f); rect.anchorMax = new Vector2(.965f, .83f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        holder.GetComponent<Image>().color = Color.clear;
        var content = new GameObject("Vehicles", typeof(RectTransform), typeof(GridLayoutGroup)).GetComponent<RectTransform>();
        content.SetParent(rect, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero;
        var grid = content.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;
        foreach (var button in cards)
        {
            button.transform.SetParent(content, false);
            button.transform.localScale = Vector3.one;
            ConfigureCardColours(button);
        }
        var scroll = holder.GetComponent<ScrollRect>();
        scroll.viewport = rect; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 50;
        var bar = new GameObject("GarageScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        bar.transform.SetParent(mainPanel, false);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(.975f, .055f); barRect.anchorMax = new Vector2(.975f, .83f);
        barRect.sizeDelta = new Vector2(10, 0); barRect.anchoredPosition = Vector2.zero;
        bar.GetComponent<Image>().color = new Color(0, 0, 0, .10f);
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(bar.transform, false);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one;
        handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
        handle.GetComponent<Image>().color = GarageVisualStyle.Accent;
        var barControl = bar.GetComponent<Scrollbar>();
        barControl.handleRect = handle.GetComponent<RectTransform>();
        barControl.targetGraphic = handle.GetComponent<Image>();
        barControl.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = barControl;
        var layout = holder.GetComponent<FarmGarageGrid>();
        layout.viewport = rect; layout.content = content; layout.grid = grid;
        foreach (var card in cards)
        {
            string id = card.name.Substring(0, card.name.Length - "_Img".Length);
            string title = FarmVehicleLibrary.Presets.FirstOrDefault(p => p.garageName == id)?.displayName ??
                (id == "Car0" ? "阿克曼小车" : "履带小车");
            layout.ConfigureCard(card, id, title);
        }
        foreach (var vehicle in vehicles)
        {
            var preset = vehicle.GetComponent<FarmGarageVehicle>().Preset;
            var card = Instantiate(cards.First(b => b.name == "Car5_Img"), content);
            card.name = vehicle.name + "_Img";
            card.onClick = new Button.ButtonClickedEvent();
            card.onClick.AddListener(() => selectVehicle(vehicle));
            ConfigureCardColours(card);
            foreach (Transform child in card.transform) Destroy(child.gameObject);
            card.GetComponent<Image>().type = Image.Type.Simple;
            layout.ConfigureCard(card, preset.id, preset.displayName);
        }
        string[] filters = { "all", "ackermann", "tracked" };
        string[] labels = { "全部车型", "阿克曼转向", "履带差速" };
        for (int i = 0; i < filters.Length; i++)
        {
            string value = filters[i];
            var tab = GarageVisualStyle.Panel(mainPanel, "GarageFilter_" + value, GarageVisualStyle.Raised);
            GarageVisualStyle.Rect(tab.transform, new Vector2(.17f + i * .15f, .858f), new Vector2(.305f + i * .15f, .907f));
            var button = tab.AddComponent<Button>(); GarageVisualStyle.ButtonStyle(button);
            var label = GarageVisualStyle.Label(tab.transform, "Label", labels[i], 16, Vector2.zero, Vector2.one);
            label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() =>
            {
                layout.filter = value;
                foreach (Transform item in content)
                {
                    bool tracked = item.name == "Car3_Img" || item.name == "Car9_Img";
                    item.gameObject.SetActive(value == "all" || (value == "tracked") == tracked);
                }
                foreach (var other in mainPanel.GetComponentsInChildren<Button>())
                    if (other.name.StartsWith("GarageFilter_"))
                        other.GetComponent<Image>().color = other == button ? new Color(.28f, .35f, .15f) : GarageVisualStyle.Raised;
                layout.Refresh(); scroll.verticalNormalizedPosition = 1;
            });
        }
        GarageVisualStyle.Label(mainPanel, "GarageCount", "10 辆农机", 16,
            new Vector2(.82f, .85f), new Vector2(.965f, .91f), GarageVisualStyle.Muted).alignment = TextAnchor.MiddleRight;
        layout.Refresh();
    }

    private static void ConfigureCardColours(Button card)
    {
        card.transition = Selectable.Transition.ColorTint;
        card.spriteState = default;
        var image = card.GetComponent<Image>();
        image.overrideSprite = null;
        image.color = GarageVisualStyle.Surface;
        image.sprite = null;
        var colours = ColorBlock.defaultColorBlock;
        colours.normalColor = colours.selectedColor = Color.white;
        colours.highlightedColor = new Color(1.3f, 1.4f, 1.4f);
        colours.pressedColor = new Color(.8f, .9f, .7f, 1f);
        card.colors = colours;
        image.canvasRenderer.SetColor(Color.white);
    }

    private void ConfigureCard(Button card, string presetId, string displayName)
    {
        foreach (Transform child in card.transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        ConfigureImage(card, presetId);
        var title = new GameObject("VehicleName", typeof(RectTransform), typeof(Text));
        title.transform.SetParent(card.transform, false);
        var labelRect = title.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = new Vector2(1, 0);
        labelRect.pivot = new Vector2(.5f, 0); labelRect.sizeDelta = new Vector2(-30, 45);
        labelRect.anchoredPosition = Vector2.zero;
        var label = title.GetComponent<Text>();
        label.text = displayName; label.fontSize = 20; label.alignment = TextAnchor.MiddleLeft;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = GarageVisualStyle.Text; label.raycastTarget = false; ChineseUiFont.Apply(label);
        label.resizeTextForBestFit = true; label.resizeTextMinSize = 15; label.resizeTextMaxSize = 20;
        GarageVisualStyle.Label(card.transform, "VehicleId", card.name.Replace("_Img", ""), 14,
            new Vector2(.04f, .85f), new Vector2(.3f, .98f), GarageVisualStyle.Accent);
    }

    private void ConfigureImage(Button card, string id)
    {
        var texture = GarageThumbnailLibrary.LoadTexture(id);
        var icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
        textures.Add(texture);
        icons.Add(icon);
        var preview = new GameObject("VehiclePreview", typeof(RectTransform), typeof(Image));
        preview.transform.SetParent(card.transform, false);
        GarageVisualStyle.Rect(preview.transform, new Vector2(.02f, .15f), new Vector2(.98f, .95f));
        var image = preview.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
        image.sprite = icon;
        image.overrideSprite = null;
    }

    private void LateUpdate() { if (viewport != null && viewport.rect.size != lastSize) Refresh(); }
    private void Refresh()
    {
        lastSize = viewport.rect.size;
        float width = Mathf.Max(120, (lastSize.x - 32) / 3);
        grid.cellSize = new Vector2(width, width * .63f);
        grid.spacing = new Vector2(16, 16);
        content.sizeDelta = new Vector2(0, Mathf.Ceil(content.Cast<Transform>().Count(t => t.gameObject.activeSelf) / 3f) * (grid.cellSize.y + 16));
    }
    private void OnDestroy()
    {
        foreach (var icon in icons) if (icon != null) Destroy(icon);
        foreach (var texture in textures) if (texture != null) Destroy(texture);
    }
}
