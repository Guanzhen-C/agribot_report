using System.Globalization;
using RG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class AxisPanel : WheelChangePanel
{
    [Header("Axis Distance Controls")]
    [SerializeField] private Button minAxisButton;
    [SerializeField] private Button maxAxisButton;
    [SerializeField] private Slider wheelRadiusSlider;
    [SerializeField] private Text axisDistanceValueText;
    [SerializeField] private InputField axisDistanceInput;
    [SerializeField] private Button sureButton;

    [Header("Bounce In")]
    [SerializeField, Min(0.01f)] private float bounceInDuration = 0.5f;

    [Header("Slide Out")]
    [SerializeField, Min(0.01f)] private float slideOutDuration = 0.5f;
    [SerializeField, Min(1f)] private float slideOutDistance = 2000f;

    private RectTransform rectTransform;
    private Vector3 visibleScale;
    private TweenHandle bounceTween;
    private TweenHandle slideTween;
    private WheelChangePanel escButtonSource;
    private bool visibleStateCached;

    protected override void Awake()
    {
        base.Awake();
        CacheVisibleState();
        BindEscButton(this);
        ResolveAxisControls();
        BindAxisControls();
        AddEscClickListener(Hide);
    }

    private void Start()
    {
        SyncAxisDistance(carChange != null
            ? carChange.CurWheelTrack
            : CarChange.MinWheelTrack);
    }

    private void OnEnable()
    {
        if (carChange != null)
            SyncAxisDistance(carChange.CurWheelTrack);
    }

    private void ResolveAxisControls()
    {
        if (minAxisButton == null)
            minAxisButton = FindChildComponent<Button>("Min_axis");

        if (maxAxisButton == null)
            maxAxisButton = FindChildComponent<Button>("Max_axis");

        if (wheelRadiusSlider == null)
            wheelRadiusSlider = FindChildComponent<Slider>("WheelRadiusSlider");

        if (axisDistanceValueText == null && wheelRadiusSlider != null)
        {
            Text[] texts = wheelRadiusSlider.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].name != "Value")
                    continue;

                axisDistanceValueText = texts[i];
                break;
            }
        }

        if (axisDistanceInput == null)
            axisDistanceInput = FindChildComponent<InputField>("txt_input");

        if (sureButton == null)
            sureButton = FindChildComponent<Button>("sure_btn");
    }

    private T FindChildComponent<T>(string objectName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].name == objectName)
                return components[i];
        }

        return null;
    }

    private void BindAxisControls()
    {
        if (carChange == null ||
            minAxisButton == null ||
            maxAxisButton == null ||
            wheelRadiusSlider == null ||
            axisDistanceValueText == null ||
            axisDistanceInput == null ||
            sureButton == null)
        {
            Debug.LogError(
                "AxisPanel：缺少 CarChange、Min_axis、Max_axis、WheelRadiusSlider、Value、txt_input 或 sure_btn 引用。",
                this);
            enabled = false;
            return;
        }

        wheelRadiusSlider.minValue = CarChange.MinWheelTrack;
        wheelRadiusSlider.maxValue = CarChange.MaxWheelTrack;
        wheelRadiusSlider.wholeNumbers = false;

        minAxisButton.onClick.AddListener(SetMinimumAxisDistance);
        maxAxisButton.onClick.AddListener(SetMaximumAxisDistance);
        wheelRadiusSlider.onValueChanged.AddListener(OnAxisSliderChanged);
        sureButton.onClick.AddListener(SubmitAxisDistanceInput);
        axisDistanceInput.onSubmit.AddListener(OnAxisInputSubmitted);
        carChange.WheelTrackChanged += SyncAxisDistance;
    }

    private void OnAxisInputSubmitted(string _)
    {
        SubmitAxisDistanceInput();
    }

    private void SubmitAxisDistanceInput()
    {
        string input = axisDistanceInput.text.Trim();
        if (!TryParseAxisCentimeters(input, out float centimeters))
        {
            ShowPop("请输入有效的数值！");
            return;
        }

        float minimumCentimeters = CarChange.MinWheelTrack * 100f;
        float maximumCentimeters = CarChange.MaxWheelTrack * 100f;
        if (centimeters < minimumCentimeters ||
            centimeters > maximumCentimeters)
        {
            ShowPop(
                $"请输入{minimumCentimeters:0.##}cm~{maximumCentimeters:0.##}cm范围内的轴距！");
            return;
        }

        SetAxisDistance(centimeters / 100f);
    }

    private static bool TryParseAxisCentimeters(
        string input,
        out float centimeters)
    {
        const NumberStyles styles = NumberStyles.Float;
        return float.TryParse(
                   input,
                   styles,
                   CultureInfo.InvariantCulture,
                   out centimeters) ||
               float.TryParse(
                   input,
                   styles,
                   CultureInfo.CurrentCulture,
                   out centimeters);
    }

    private void SetMinimumAxisDistance()
    {
        SetAxisDistance(CarChange.MinWheelTrack);
    }

    private void SetMaximumAxisDistance()
    {
        SetAxisDistance(CarChange.MaxWheelTrack);
    }

    private void SetAxisDistance(float distance)
    {
        distance = Mathf.Clamp(
            distance,
            CarChange.MinWheelTrack,
            CarChange.MaxWheelTrack);

        SyncAxisDistance(distance);
        carChange.SetWheelTrack(distance);
    }

    private void OnAxisSliderChanged(float distance)
    {
        UpdateAxisDistanceValue(distance);
        carChange.SetWheelTrack(distance);
    }

    private void SyncAxisDistance(float distance)
    {
        if (wheelRadiusSlider != null)
            wheelRadiusSlider.SetValueWithoutNotify(distance);

        UpdateAxisDistanceValue(distance);
    }

    private void UpdateAxisDistanceValue(float distance)
    {
        if (axisDistanceValueText == null)
            return;

        float centimeters = distance * 100f;
        axisDistanceValueText.text = $"当前轴距：{centimeters:0.##}cm";
    }

    public void ShowBounceIn()
    {
        // This method can be called while the panel is inactive, before Awake.
        CacheVisibleState();
        Show();

        if (slideTween.IsActive)
            slideTween.Kill();
        if (bounceTween.IsActive)
            bounceTween.Kill();

        if (rectTransform != null)
            rectTransform.anchoredPosition = visibleAnchoredPosition;

        transform.localScale = Vector3.zero;
        bounceTween = RTween.Scale(transform, visibleScale, bounceInDuration)
            .SetEase(EaseType.OutBounce);

        // Keep the panel visible if RTweens refuses to create a tween.
        if (!bounceTween.IsActive)
            transform.localScale = visibleScale;
    }

    private void CacheVisibleState()
    {
        if (visibleStateCached)
            return;

        rectTransform = transform as RectTransform;
        visibleScale = transform.localScale;

        if (visibleScale.sqrMagnitude < 0.000001f)
            visibleScale = Vector3.one;

        if (rectTransform != null)
            visibleAnchoredPosition = rectTransform.anchoredPosition;

        visibleStateCached = true;
    }

    public void BindEscButton(WheelChangePanel source)
    {
        if (escButtonSource == source)
            return;

        if (escButtonSource != null)
            escButtonSource.RemoveEscClickListener(HideSlideOut);

        escButtonSource = source;

        if (escButtonSource != null)
            escButtonSource.AddEscClickListener(HideSlideOut);
    }

    public void HideSlideOut()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (bounceTween.IsActive)
            bounceTween.Kill();
        if (slideTween.IsActive)
            slideTween.Kill();

        transform.localScale = visibleScale;
        HideEscButton();

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Vector2 targetPosition = visibleAnchoredPosition + Vector2.left * slideOutDistance;
        slideTween = RTween.UIMove(rectTransform, targetPosition, slideOutDuration)
            .SetEase(EaseType.InBack)
            .OnComplete(() =>
            {
                rectTransform.anchoredPosition = visibleAnchoredPosition;
                gameObject.SetActive(false);
            });
    }

    private void OnDestroy()
    {
        if (escButtonSource != null)
            escButtonSource.RemoveEscClickListener(HideSlideOut);

        if (minAxisButton != null)
            minAxisButton.onClick.RemoveListener(SetMinimumAxisDistance);

        if (maxAxisButton != null)
            maxAxisButton.onClick.RemoveListener(SetMaximumAxisDistance);

        if (wheelRadiusSlider != null)
            wheelRadiusSlider.onValueChanged.RemoveListener(OnAxisSliderChanged);

        if (sureButton != null)
            sureButton.onClick.RemoveListener(SubmitAxisDistanceInput);

        if (axisDistanceInput != null)
            axisDistanceInput.onSubmit.RemoveListener(OnAxisInputSubmitted);

        if (carChange != null)
            carChange.WheelTrackChanged -= SyncAxisDistance;
    }
}
