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
    private decimal previousInput;
    private decimal currentInput;

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

        GameObject inputObject = GameObject.Find("txt_input");
        distanceInput = inputObject != null
            ? inputObject.GetComponent<InputField>()
            : null;
        if (distanceInput != null)
        {
            distanceInput.contentType = InputField.ContentType.DecimalNumber;
            ChineseUiFont.Apply(distanceInput.textComponent);
            if (distanceInput.placeholder is Text placeholder)
            {
                placeholder.text = "请输入 0.42 - 0.60";
                ChineseUiFont.Apply(placeholder);
            }
            distanceInput.onValueChanged.AddListener(OnInputDistanceChanged);
            distanceInput.onEndEdit.AddListener(OnInputDistanceEndEdit);
        }
        else
        {
            Debug.LogError(
                "WheelDistancePanel：没有找到 txt_input 的 InputField 组件。",
                this);
        }

        if (Sure_btn != null)
        {
            Sure_btn.onClick.AddListener(() =>
            {
                if (EnsureValidInput(currentInput) &&
                    previousInput != currentInput)
                {
                    previousInput = currentInput;
                    OnWheelDistanceSliderChanged((float)currentInput);
                    SyncWheelDistance((float)currentInput);
                }
            });
        }

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

    private void OnInputDistanceEndEdit(string value)
    {
        bool pressedEnter =
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter);
        if (!pressedEnter || !EnsureValidInput(currentInput))
            return;

        previousInput = currentInput;
        OnWheelDistanceSliderChanged((float)currentInput);
        SyncWheelDistance((float)currentInput);
    }

    private void OnInputDistanceChanged(string value)
    {
        if (decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal number))
        {
            currentInput = number;
            Debug.Log($"有效轴距输入：{currentInput}");
        }
        else if (!string.IsNullOrWhiteSpace(value))
        {
            Debug.Log($"当前内容不是有效数值：{value}");
        }
    }

    private bool EnsureValidInput(decimal value)
    {
        if (distanceInput == null)
            return false;

        string text = distanceInput.text.Trim();
        if (!Regex.IsMatch(text, @"^\d+(\.\d{1,5})?$"))
        {
            ShowPop("请输入最多 5 位小数的数值。");
            return false;
        }

        if (!decimal.TryParse(
                text,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out value))
        {
            ShowPop("输入内容无法转换为有效数值。");
            return false;
        }

        if (!IsWheelDistanceInRange((float)value))
        {
            ShowPop(
                $"请输入 {MinWheelDistance:F2} m 至 " +
                $"{MaxWheelDistance:F2} m 范围内的轴距。");
            return false;
        }

        currentInput = value;
        return true;
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
        wheelDistanceValueText.text =
            $"前后轴距 <size=18>（以四轮中心为测量基准）</size>：{distance:F2} m";
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
            distanceInput.onValueChanged.RemoveListener(OnInputDistanceChanged);
            distanceInput.onEndEdit.RemoveListener(OnInputDistanceEndEdit);
        }
        if (carChange != null)
            carChange.WheelDistanceChanged -= SyncWheelDistance;
    }
}
