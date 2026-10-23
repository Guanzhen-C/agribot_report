using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class SceneVehicleExplodedView : MonoBehaviour
{
    private sealed class Part
    {
        public Transform Transform;
        public Vector3 Position;
        public Vector3 Offset;
    }

    private readonly List<Part> parts = new List<Part>();
    private Slider slider;
    public int PartCount => parts.Count;

    public static SceneVehicleExplodedView Create(Transform canvas, GameObject buttonTemplate)
    {
        var root = new GameObject("SceneVehicleExplodedView", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(canvas, false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(510f, 100f);
        root.GetComponent<Image>().color = new Color(0.10f, 0.13f, 0.14f, 0.94f);

        var view = root.AddComponent<SceneVehicleExplodedView>();
        var title = new GameObject("ExplodedViewTitle", typeof(RectTransform), typeof(Text));
        title.transform.SetParent(root.transform, false);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.58f);
        titleRect.anchorMax = new Vector2(0.74f, 0.95f);
        titleRect.offsetMin = titleRect.offsetMax = Vector2.zero;
        var titleText = title.GetComponent<Text>();
        titleText.text = "爆炸图";
        titleText.fontSize = 19;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = Color.white;
        titleText.raycastTarget = false;
        ChineseUiFont.Apply(titleText);

        var sliderRoot = new GameObject("ExplodedViewSlider", typeof(RectTransform),
            typeof(Image), typeof(Slider));
        sliderRoot.transform.SetParent(root.transform, false);
        var sliderRect = sliderRoot.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.05f, 0.23f);
        sliderRect.anchorMax = new Vector2(0.73f, 0.39f);
        sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;
        sliderRoot.GetComponent<Image>().color = new Color(0.54f, 0.62f, 0.62f, 1f);
        var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handle.transform.SetParent(sliderRoot.transform, false);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20f, 34f);
        handle.GetComponent<Image>().color = Color.white;
        view.slider = sliderRoot.GetComponent<Slider>();
        view.slider.minValue = 0f;
        view.slider.maxValue = 1f;
        view.slider.handleRect = handleRect;
        view.slider.onValueChanged.AddListener(view.Apply);

        var close = Instantiate(buttonTemplate, root.transform);
        close.name = "CloseExplodedView";
        close.SetActive(true);
        var closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.77f, 0.18f);
        closeRect.anchorMax = new Vector2(0.97f, 0.78f);
        closeRect.offsetMin = closeRect.offsetMax = Vector2.zero;
        closeRect.localScale = Vector3.one;
        var closeButton = close.GetComponent<Button>();
        closeButton.onClick = new Button.ButtonClickedEvent();
        closeButton.onClick.AddListener(view.Close);
        var closeText = close.GetComponentInChildren<Text>(true);
        closeText.text = "收起";
        closeText.resizeTextForBestFit = true;
        closeText.raycastTarget = false;
        ChineseUiFont.Apply(closeText);

        root.SetActive(false);
        return view;
    }

    public void Open(GameObject vehicle)
    {
        Close();
        if (vehicle == null || !vehicle.activeInHierarchy)
            return;

        BuildParts(vehicle);
        if (parts.Count == 0)
            return;

        slider.SetValueWithoutNotify(0f);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Close()
    {
        Apply(0f);
        parts.Clear();
        if (slider != null)
            slider.SetValueWithoutNotify(0f);
        gameObject.SetActive(false);
    }

    private void OnDisable() => Apply(0f);

    private void OnDestroy()
    {
        Apply(0f);
        if (slider != null)
            slider.onValueChanged.RemoveListener(Apply);
    }

    private void BuildParts(GameObject vehicle)
    {
        bool originalAckermann = vehicle.GetComponent<Car0GarageVehicle>() != null;
        var candidates = originalAckermann
            ? BombPanel.GetVisualPartRoots(vehicle.transform).Where(item => item.gameObject.activeInHierarchy).ToList()
            : vehicle.GetComponentsInChildren<Renderer>()
                .Where(item => item is MeshRenderer || item is SkinnedMeshRenderer)
                .Select(item => item.transform).Distinct().ToList();

        Bounds vehicleBounds = new Bounds();
        bool hasBounds = false;
        foreach (Transform candidate in candidates)
        {
            Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                continue;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (!hasBounds) { vehicleBounds = bounds; hasBounds = true; }
            else vehicleBounds.Encapsulate(bounds);
        }
        if (!hasBounds)
            return;

        float distance = originalAckermann ? .96f : Mathf.Clamp(vehicleBounds.size.magnitude * 0.17f, 0.5f, 4f);
        foreach (Transform candidate in candidates)
        {
            Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                continue;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 direction = bounds.center - vehicleBounds.center;
            if (!originalAckermann) direction.y += vehicleBounds.size.y * 0.35f;
            if (direction.sqrMagnitude < 0.001f)
            {
                float angle = parts.Count * 2.4f;
                direction = new Vector3(Mathf.Cos(angle), 0.35f, Mathf.Sin(angle));
            }
            parts.Add(new Part { Transform = candidate, Position = candidate.localPosition,
                Offset = candidate.parent.InverseTransformVector(direction.normalized * distance) });
        }
    }

    private void Apply(float amount)
    {
        foreach (Part part in parts)
            if (part.Transform != null)
                part.Transform.localPosition = part.Position + part.Offset * amount;
    }
}
