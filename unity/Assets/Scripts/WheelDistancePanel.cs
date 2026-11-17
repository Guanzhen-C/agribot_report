using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class WheelDistancePanel : WheelChangePanel
{
    private const float MinWheelDistance = 0.42f;
    private const float NormalWheelDistance = 0.5258473f;
    private const float MaxWheelDistance = 0.60f;

    private InputField distanceInput;

    public Button Sure_btn;

    [Header("Distance Controls")]
    [SerializeField] private Button minDistanceButton;
    [SerializeField] private Button normalDistanceButton;
    [SerializeField] private Button maxDistanceButton;
    [SerializeField] private Slider wheelDistanceSlider;
    [SerializeField] private Text wheelDistanceValueText;

    public Button MinDistanceButton => minDistanceButton;
    public Button NormalDistanceButton => normalDistanceButton;
    public Button MaxDistanceButton => maxDistanceButton;
    public Slider WheelDistanceSlider => wheelDistanceSlider;

    protected override void Awake()
    {
        base.Awake();

        if (carChange == null)
        {
            Debug.LogError("WheelDistancePanel：没有找到 CarChange。", this);
            enabled = false;
            return;
        }

        if (wheelDistanceSlider == null)
        {
            Debug.LogError("WheelDistancePanel：没有绑定 WheelDisSlider。", this);
            enabled = false;
            return;
        }

        ChineseUiFont.Apply(wheelDistanceValueText);
        SetPresetLabel(minDistanceButton, "最小轴距", MinWheelDistance);
        SetPresetLabel(normalDistanceButton, "标准轴距", NormalWheelDistance);
        SetPresetLabel(maxDistanceButton, "最大轴距", MaxWheelDistance);
        wheelDistanceSlider.minValue = MinWheelDistance;
        wheelDistanceSlider.maxValue = MaxWheelDistance;
        wheelDistanceSlider.wholeNumbers = false;
        wheelDistanceSlider.onValueChanged.AddListener(
            OnWheelDistanceSliderChanged);

        minDistanceButton.onClick.AddListener(() =>
        {
            carChange.ChangeWheelsDis(MinWheelDistance);
            SyncWheelDistance(MinWheelDistance);
        });
        normalDistanceButton.onClick.AddListener(() =>
        {
            carChange.ChangeWheelsDis(NormalWheelDistance);
            SyncWheelDistance(NormalWheelDistance);
        });
        maxDistanceButton.onClick.AddListener(() =>
        {
            carChange.ChangeWheelsDis(MaxWheelDistance);
            SyncWheelDistance(MaxWheelDistance);
        });

        distanceInput = GetComponentInChildren<InputField>(true);
        if (distanceInput != null)
        {
            distanceInput.contentType = InputField.ContentType.DecimalNumber;
            ChineseUiFont.Apply(distanceInput.textComponent);
            if (distanceInput.placeholder is Text placeholder)
            {
                placeholder.text = "42 - 60 cm";
                ChineseUiFont.Apply(placeholder);
            }
            distanceInput.onSubmit.AddListener(OnDistanceInputSubmitted);
            ConfigureDimensionInput(distanceInput, Sure_btn, "前后轴距");
        }
        else
        {
            Debug.LogError(
                "WheelDistancePanel：没有找到 txt_input 的 InputField 组件。",
                this);
        }

        if (Sure_btn != null)
        {
            Sure_btn.onClick.AddListener(SubmitDistanceInput);
        }

        carChange.WheelDistanceChanged += SyncWheelDistance;
        AddEscClickListener(Hide);
    }

    private void Start()
    {
        SyncWheelDistance(carChange.Cur_wheelDis);
    }

    private void OnEnable()
    {
        if (carChange != null)
            SyncWheelDistance(carChange.Cur_wheelDis);
    }

    private void OnDistanceInputSubmitted(string value)
    {
        SubmitDistanceInput();
    }

    private static void SetPresetLabel(Button button, string title, float meters)
    {
        Text label = button != null ? button.GetComponentInChildren<Text>(true) : null;
        if (label != null)
            label.text = $"{title}：{meters * 100f:0.##} cm";
    }

    private void SubmitDistanceInput()
    {
        if (distanceInput == null)
            return;

        string text = distanceInput.text.Trim();
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
        if (!IsWheelDistanceInRange(meters))
        {
            ShowPop(
                $"请输入 {MinWheelDistance * 100f:0.##} cm 至 " +
                $"{MaxWheelDistance * 100f:0.##} cm 范围内的前后轴距。");
            return;
        }

        OnWheelDistanceSliderChanged(meters);
        SyncWheelDistance(meters);
    }

    private static bool IsWheelDistanceInRange(float distance)
    {
        return distance >= MinWheelDistance &&
               distance <= MaxWheelDistance;
    }

    private void OnWheelDistanceSliderChanged(float distance)
    {
        if (!IsWheelDistanceInRange(distance))
            return;

        UpdateWheelDistanceValue(distance);
        carChange.ChangeWheelsDis(distance);
    }

    private void SyncWheelDistance(float distance)
    {
        if (wheelDistanceSlider == null ||
            !IsWheelDistanceInRange(distance))
        {
            return;
        }

        wheelDistanceSlider.SetValueWithoutNotify(distance);
        UpdateWheelDistanceValue(distance);
    }

    private void UpdateWheelDistanceValue(float distance)
    {
        if (wheelDistanceValueText == null)
            return;

        ChineseUiFont.Apply(wheelDistanceValueText);
        wheelDistanceValueText.supportRichText = false;
        wheelDistanceValueText.text = $"前后轴距：{distance * 100f:0.##} cm";
        if (distanceInput != null)
            distanceInput.SetTextWithoutNotify((distance * 100f).ToString("0.#####", CultureInfo.InvariantCulture));
    }

    private void OnDestroy()
    {
        if (wheelDistanceSlider != null)
        {
            wheelDistanceSlider.onValueChanged.RemoveListener(
                OnWheelDistanceSliderChanged);
        }
        if (distanceInput != null)
        {
            distanceInput.onSubmit.RemoveListener(OnDistanceInputSubmitted);
        }
        if (Sure_btn != null)
            Sure_btn.onClick.RemoveListener(SubmitDistanceInput);
        if (carChange != null)
            carChange.WheelDistanceChanged -= SyncWheelDistance;
    }
}
