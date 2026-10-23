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
    private decimal oldInput;
    private decimal currentInput;
    public UnityEngine.UI.Button Sure_btn;

    //private CarChange carChange;
    private GameObject cameraTarget;

    protected override void Awake()
    {
        base.Awake();
        cameraTarget = GameObject.Find("CameraTarget");

        GameObject car = GameObject.Find("Car0");
        /*if (car != null)
            carChange = car.GetComponent<CarChange>();*/

        if (carChange == null)
        {
            Debug.LogError("bottonPanel: Car0/CarChange was not found.", this);
            return;
        }
       
        EnsureRadiusSlider();
        wheelRadiusSlider.minValue = MinWheelRadius;
        wheelRadiusSlider.maxValue = MaxWheelRadius;
        wheelRadiusSlider.wholeNumbers = false;
        wheelRadiusSlider.SetValueWithoutNotify(NormalWheelRadius);
        wheelRadiusSlider.onValueChanged.AddListener(ApplyWheelRadius);
        UpdateRadiusLabel(NormalWheelRadius);

        MinRadius_btn.onClick.AddListener(() => SetRadiusPreset(MinWheelRadius));
        NormalRadius_btn.onClick.AddListener(() => SetRadiusPreset(NormalWheelRadius));
        MaxRadius_btn.onClick.AddListener(() => SetRadiusPreset(MaxWheelRadius));

        Sure_btn.onClick.AddListener(() =>
        {
            if (oldInput != currentInput && EnSureRightNum(currentInput))
            {
                oldInput = currentInput;
                SetRadiusPreset((float)currentInput);
                Debug.Log("currenInput = " + currentInput);
            }
        });

        


        // 用户输入半径
        txt_input = GameObject.Find("txt_input").GetComponent<InputField>();
        if (txt_input != null)
        {
            //只允许数字和小数点
            txt_input.contentType = InputField.ContentType.DecimalNumber;
            txt_input.onValueChanged.AddListener(OnInputValueChanged);    // 赋值
            txt_input.onEndEdit.AddListener(OnInputEndEdit);
        }
        else
        {
            Debug.LogError("bottonPanel: 未找到 txt_input 或 InputField 组件。", this);
        }

        AddEscClickListener(Hide);
    }

    private void OnEnable()
    {
        if (carChange == null || wheelRadiusSlider == null)
            return;

        float radius = carChange.CurWheelRadius;
        wheelRadiusSlider.SetValueWithoutNotify(radius);
        UpdateRadiusLabel(radius);
    }
    
    private void OnInputEndEdit(string txt)
    {
        bool pressEnter =
             Input.GetKeyDown(KeyCode.Return) ||
             Input.GetKeyDown(KeyCode.KeypadEnter);

        if (!pressEnter)
            return;

        EnSureRightNum(currentInput);
        oldInput = currentInput;
        SetRadiusPreset((float)currentInput);
    }
    private void OnInputValueChanged(string value)
    {
        if (decimal.TryParse(value, out decimal number))
        {
            currentInput = number * (decimal)0.01f;  // 用户输入是cm,后端准换成m做判断
            Debug.Log($"有效数值：{currentInput}");
        }
        else
        {
            Debug.Log($"当前内容不是完整数值：{value}");
        }
    }

    /// <summary>
    /// 判断InputField中的内容是否合法。
    /// </summary>
    private bool EnSureRightNum(decimal num)
    {
        if (txt_input == null) return false;
        string text = txt_input.text.Trim();
        if (!Regex.IsMatch(text, @"^\d+(\.\d{1,5})?$"))
        {
            ShowPop("最多输入5位小数！");
            return false;
        }
        // 转换为decimal
        bool success = decimal.TryParse(
            text,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out num);
        if (!success)
        {
            ShowPop("转换成合法正数失败！");
            return false;
        }

        // 只允许正数，不允许0和负数
        if ((float)num >= NormalWheelRadius * 1.34f * 100 || (float)num <= NormalWheelRadius * 0.55f *100)  // 这里单位是 m
        {
            float minR = NormalWheelRadius * 0.55f * 100;
            float maxR = NormalWheelRadius * 1.34f * 100;
            ShowPop("请输入有效半径范围"+ minR.ToString("F1") +"cm~"+ maxR.ToString("F1") + "cm的半径！");
            return false;
        }
        return true;
    }
    private void SetRadiusPreset(float radius)
    {
        // 按钮通过 Slider 进入同一个更新入口，避免两套状态不同步。
        wheelRadiusSlider.value = radius;
    }

    private void ApplyWheelRadius(float radius)
    {
        float wheelScale = radius / NormalWheelRadius;
        carChange.ChangeWheels(wheelScale, radius);
        UpdateRadiusLabel(radius);

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

    private void UpdateRadiusLabel(float radius)
    {
        if (wheelRadiusValueText != null)
            wheelRadiusValueText.text = $"轮胎半径：{radius * 100f:F2} cm";
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

    private void OnDestroy()
    {
        if (wheelRadiusSlider != null)
            wheelRadiusSlider.onValueChanged.RemoveListener(ApplyWheelRadius);


    }
}
