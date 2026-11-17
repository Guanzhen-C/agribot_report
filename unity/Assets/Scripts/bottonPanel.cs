using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class bottonPanel : WheelChangePanel
{
    private const float MinWheelRadius = 0.07f;
    private const float NormalWheelRadius = 0.1275f;
    private const float MaxWheelRadius = 0.17f;

    [Header("Radius Presets")]
    public UnityEngine.UI.Button MinRadius_btn;
    public UnityEngine.UI.Button NormalRadius_btn;
    public UnityEngine.UI.Button MaxRadius_btn;

    [Header("Radius Slider")]
    [SerializeField] private Slider wheelRadiusSlider;
    [SerializeField] private Text wheelRadiusValueText;
    private InputField txt_input;
    public UnityEngine.UI.Button Sure_btn;

    private GameObject cameraTarget;

    protected override void Awake()
    {
        base.Awake();
        cameraTarget = GameObject.Find("CameraTarget");

        if (carChange == null)
        {
            Debug.LogError("bottonPanel: Car0/CarChange was not found.", this);
            return;
        }
       
        EnsureRadiusSlider();
        wheelRadiusSlider.minValue = MinWheelRadius;
        wheelRadiusSlider.maxValue = MaxWheelRadius;
        wheelRadiusSlider.wholeNumbers = false;
        wheelRadiusSlider.onValueChanged.AddListener(ApplyWheelRadius);

        MinRadius_btn.onClick.AddListener(() => SetRadiusPreset(MinWheelRadius));
        NormalRadius_btn.onClick.AddListener(() => SetRadiusPreset(NormalWheelRadius));
        MaxRadius_btn.onClick.AddListener(() => SetRadiusPreset(MaxWheelRadius));
        SetPresetLabel(MinRadius_btn, "最小半径", MinWheelRadius);
        SetPresetLabel(NormalRadius_btn, "标准半径", NormalWheelRadius);
        SetPresetLabel(MaxRadius_btn, "最大半径", MaxWheelRadius);
        if (Sure_btn != null) Sure_btn.onClick.AddListener(SubmitRadiusInput);

        txt_input = GetComponentInChildren<InputField>(true);
        if (txt_input != null)
        {
            txt_input.contentType = InputField.ContentType.DecimalNumber;
            if (txt_input.placeholder is Text placeholder) placeholder.text = "7 - 17 cm";
            txt_input.onSubmit.AddListener(OnRadiusInputSubmitted);
            ConfigureDimensionInput(txt_input, Sure_btn, "车轮半径");
        }
        else
        {
            Debug.LogError("bottonPanel: 未找到 txt_input 或 InputField 组件。", this);
        }

        SyncRadius(carChange.CurWheelRadius);
        AddEscClickListener(Hide);
    }

    private void OnEnable()
    {
        if (carChange == null || wheelRadiusSlider == null)
            return;

        SyncRadius(carChange.CurWheelRadius);
    }

    private static void SetPresetLabel(Button button, string title, float meters)
    {
        var label = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (label != null) label.text = $"{title}：{meters * 100f:0.##} cm";
    }

    private void OnRadiusInputSubmitted(string value) => SubmitRadiusInput();

    private void SubmitRadiusInput()
    {
        if (txt_input == null) return;
        string text = txt_input.text.Trim();
        if (!Regex.IsMatch(text, @"^\d+(\.\d{1,5})?$"))
        {
            ShowPop("请输入最多 5 位小数的数值。");
            return;
        }
        if (!decimal.TryParse(
            text,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out decimal centimeters))
        {
            ShowPop("输入内容无法转换为有效数值。");
            return;
        }

        float meters = (float)(centimeters / 100m);
        if (!IsRadiusInRange(meters))
        {
            ShowPop("请输入 7 cm 至 17 cm 范围内的车轮半径。");
            return;
        }
        SetRadiusPreset(meters);
    }

    private static bool IsRadiusInRange(float radius) => radius >= MinWheelRadius && radius <= MaxWheelRadius;

    private void SetRadiusPreset(float radius)
    {
        ApplyWheelRadius(radius);
    }

    private void ApplyWheelRadius(float radius)
    {
        if (!IsRadiusInRange(radius)) return;
        float wheelScale = radius / NormalWheelRadius;
        carChange.ChangeWheels(wheelScale, radius);
        SyncRadius(radius);

        if (cameraTarget == null)
            return;

        Vector3 position = cameraTarget.transform.localPosition;
        // 沿用 Min_r 的相机目标偏移；恢复到普通尺寸后保持原来的位置。
        float minToNormal = Mathf.InverseLerp(
            MinWheelRadius,
            NormalWheelRadius,
            radius);
        position.z = Mathf.Lerp(0.008f, 0.00718f, minToNormal);
        position.y = Mathf.Lerp(0.8f, 0.718f, minToNormal);
        cameraTarget.transform.localPosition = position;
    }

    private void SyncRadius(float radius)
    {
        wheelRadiusSlider.SetValueWithoutNotify(radius);
        if (wheelRadiusValueText != null)
            wheelRadiusValueText.text = $"车轮半径：{radius * 100f:0.##} cm";
        if (txt_input != null)
            txt_input.SetTextWithoutNotify((radius * 100f).ToString("0.#####", CultureInfo.InvariantCulture));
    }

    private void OnDestroy()
    {
        if (txt_input != null) txt_input.onSubmit.RemoveListener(OnRadiusInputSubmitted);
        if (Sure_btn != null) Sure_btn.onClick.RemoveListener(SubmitRadiusInput);
        if (wheelRadiusSlider != null) wheelRadiusSlider.onValueChanged.RemoveListener(ApplyWheelRadius);
    }

    public GameObject EnsureRadiusSlider()
    {
        if (wheelRadiusSlider != null)
            return wheelRadiusSlider.gameObject;

        Transform existingSlider = transform.Find("WheelRadiusSlider");
        if (existingSlider != null)
        {
            wheelRadiusSlider = existingSlider.GetComponent<Slider>();
            Transform existingValue = existingSlider.Find("Value");
            if (existingValue != null)
                wheelRadiusValueText = existingValue.GetComponent<Text>();

            if (wheelRadiusSlider != null)
                return existingSlider.gameObject;
        }

        GameObject root = CreateUiObject("WheelRadiusSlider", transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, 42f);
        rootRect.sizeDelta = new Vector2(700f, 50f);

        GameObject background = CreateUiObject("Background", root.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(0f, 10f);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.12f, 0.16f, 0.15f, 0.9f);

        GameObject fillArea = CreateUiObject("Fill Area", root.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.offsetMin = new Vector2(8f, -5f);
        fillAreaRect.offsetMax = new Vector2(-8f, 5f);

        GameObject fill = CreateUiObject("Fill", fillArea.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.22f, 0.78f, 0.55f, 1f);

        GameObject handleArea = CreateUiObject("Handle Slide Area", root.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(12f, 0f);
        handleAreaRect.offsetMax = new Vector2(-12f, 0f);

        GameObject handle = CreateUiObject("HandleDriver", handleArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(26f, 36f);

        GameObject handleVisual = CreateUiObject("HandleVisual", handle.transform);
        RectTransform handleVisualRect = handleVisual.GetComponent<RectTransform>();
        handleVisualRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleVisualRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleVisualRect.pivot = new Vector2(0.5f, 0.5f);
        handleVisualRect.anchoredPosition = Vector2.zero;
        handleVisualRect.sizeDelta = new Vector2(26f, 36f);
        Image handleImage = handleVisual.AddComponent<Image>();
        handleImage.color = Color.white;

        wheelRadiusSlider = root.AddComponent<Slider>();
        wheelRadiusSlider.fillRect = fillRect;
        wheelRadiusSlider.handleRect = handleRect;
        wheelRadiusSlider.targetGraphic = handleImage;
        wheelRadiusSlider.direction = Slider.Direction.LeftToRight;

        GameObject label = CreateUiObject("Value", root.transform);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 5f);
        labelRect.sizeDelta = new Vector2(320f, 34f);

        wheelRadiusValueText = label.AddComponent<Text>();
        wheelRadiusValueText.font = ChineseUiFont.LegacyFont;
        wheelRadiusValueText.fontSize = 24;
        wheelRadiusValueText.alignment = TextAnchor.MiddleCenter;
        wheelRadiusValueText.color = Color.white;
        wheelRadiusValueText.raycastTarget = false;

        return root;
    }

    /// <summary>
    /// 把旧版直接受 Slider 驱动的 Handle 迁移为 Driver + Visual 两层结构。
    /// Slider 只改变 Driver 的位置；Visual 可以自由调整尺寸、形状和局部偏移。
    /// </summary>
    public GameObject EnsureHandleVisual()
    {
        if (wheelRadiusSlider == null || wheelRadiusSlider.handleRect == null)
            return null;

        RectTransform driver = wheelRadiusSlider.handleRect;
        Transform existingVisual = driver.Find("HandleVisual");
        if (existingVisual != null)
            return existingVisual.gameObject;

        Image oldImage = driver.GetComponent<Image>();
        Vector2 visualSize = driver.sizeDelta;
        if (visualSize.x <= 0f || visualSize.y <= 0f)
            visualSize = new Vector2(26f, 36f);

        driver.name = "HandleDriver";

        GameObject visual = CreateUiObject("HandleVisual", driver);
        RectTransform visualRect = visual.GetComponent<RectTransform>();
        visualRect.anchorMin = new Vector2(0.5f, 0.5f);
        visualRect.anchorMax = new Vector2(0.5f, 0.5f);
        visualRect.pivot = new Vector2(0.5f, 0.5f);
        visualRect.anchoredPosition = Vector2.zero;
        visualRect.sizeDelta = visualSize;

        Image visualImage = visual.AddComponent<Image>();
        if (oldImage != null)
        {
            visualImage.sprite = oldImage.sprite;
            visualImage.overrideSprite = oldImage.overrideSprite;
            visualImage.color = oldImage.color;
            visualImage.material = oldImage.material;
            visualImage.type = oldImage.type;
            visualImage.preserveAspect = oldImage.preserveAspect;
            visualImage.fillCenter = oldImage.fillCenter;
            visualImage.fillMethod = oldImage.fillMethod;
            visualImage.fillAmount = oldImage.fillAmount;
            visualImage.fillClockwise = oldImage.fillClockwise;
            visualImage.fillOrigin = oldImage.fillOrigin;

            oldImage.enabled = false;
            oldImage.raycastTarget = false;
        }

        wheelRadiusSlider.targetGraphic = visualImage;
        return visual;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        return child;
    }

}
