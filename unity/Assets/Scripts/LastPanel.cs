using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Agribot.VehicleConfiguration;
using AgriculturalMachinery.BagSystem;
using RG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public partial class LastPanel : WheelChangePanel
{
    private Func<VehicleConfigurationAuthoring> externalVehicle;
    private Func<VehicleConfigurationExportResult> externalExport;
    private Action<string> externalNotification;

    public static LastPanel CreateVehicleSelector(Transform canvas, Button output,
        Func<VehicleConfigurationAuthoring> buildVehicle, Action<string> notification)
    {
        return CreateSelector(canvas, output, buildVehicle, null, notification);
    }

    public static LastPanel CreatePresetSelector(Transform canvas, Button output,
        Func<VehicleConfigurationExportResult> export, Action<string> notification)
    {
        return CreateSelector(canvas, output, null, export, notification);
    }

    private static LastPanel CreateSelector(Transform canvas, Button output,
        Func<VehicleConfigurationAuthoring> buildVehicle, Func<VehicleConfigurationExportResult> export,
        Action<string> notification)
    {
        var root = new GameObject("VehicleSimulationSelector", typeof(RectTransform));
        root.SetActive(false);
        root.transform.SetParent(canvas, false);
        StretchRect(root.GetComponent<RectTransform>());
        var panel = root.AddComponent<LastPanel>();
        panel.externalVehicle = buildVehicle; panel.externalNotification = notification;
        panel.externalExport = export;
        panel.ExportRos_btn = output;
        panel.ExportRos_label = output.GetComponentInChildren<Text>();
        root.SetActive(true);
        output.onClick.AddListener(panel.BeginExportRosConfiguration);
        return panel;
    }

    private void Notify(string text)
    {
        if (externalNotification != null) externalNotification(text);
        else ShowPop(text);
    }
    private Button OpenBag_btn;
    private Button Open_Bomb_btn;
    private Button ExportRos_btn;
    private Text ExportRos_label;
    private Button stopSimulationButton;
    private Button exportPhysicalButton;
    private GameObject algorithmDialog;
    private readonly List<(FarmScenario scene, Button button)> sceneButtons = new();
    private readonly List<ComponentOptionVisual> componentOptions =
        new List<ComponentOptionVisual>();
    private RosSimulationAlgorithmProfile selectedAlgorithm;
    private RosSimulationComponentOption selectedPerception;
    private RosSimulationComponentOption selectedVisualPerception;
    private RosSimulationComponentOption selectedLocalization;
    private RosSimulationComponentOption selectedPlanner;
    private RosSimulationComponentOption selectedController;
    private Text algorithmDetailTitle;
    private Text algorithmDetailId;
    private Text algorithmDetailDescription;
    private Text algorithmPerceptionValue;
    private Text algorithmVisualPerceptionValue;
    private Text algorithmLocalizationValue;
    private Text algorithmPlannerValue;
    private Text algorithmControllerValue;
    private Text algorithmRouteValue;
    private Button launchSelectedAlgorithmButton;
    private WheelChangePanel bagPanel;
    private BombPanel bombPanel;
    private EquipmentBagController equipmentBagController;
    private EquipmentBagController_copy equipmentBagControllerCopy;
    private EquipmentSelectionController equipmentSelectionController;
    private EquipmentSelectionController_copy equipmentSelectionControllerCopy;
    private TweenHandle bombOpenTween;
    private Vector3 bombPanelOriginalScale = Vector3.one;

    private sealed class ComponentOptionVisual
    {
        public RosSimulationComponentOption Component;
        public Button Button;
        public Image Background;
        public Outline Border;
        public Text Label;
    }

    private static readonly Color DialogBackground =
        new Color(0.075f, 0.09f, 0.10f, 1f);
    private static readonly Color DialogSurface =
        new Color(0.105f, 0.125f, 0.135f, 1f);
    private static readonly Color DialogSurfaceRaised =
        new Color(0.15f, 0.175f, 0.185f, 1f);
    private static readonly Color DialogAccent =
        new Color(0.75f, 0.88f, 0.27f, 1f);
    private static readonly Color DialogText =
        new Color(0.94f, 0.96f, 0.96f, 1f);
    private static readonly Color DialogMutedText =
        new Color(0.64f, 0.70f, 0.72f, 1f);
    private static readonly Color DetailBackground =
        new Color(0.11f, 0.135f, 0.15f, 1f);
    private static readonly Color DetailText =
        new Color(0.94f, 0.96f, 0.96f, 1f);

    protected override void Awake()
    {
        if (externalVehicle != null || externalExport != null) { ConfigureAlgorithmDialog(); return; }
        base.Awake();
        Transform obsoleteStart = transform.Find("Begin_Start_btn");
        if (obsoleteStart != null)
        {
            obsoleteStart.gameObject.SetActive(false);
            Destroy(obsoleteStart.gameObject);
        }

        if (transform.childCount > 0)
        {
            OpenBag_btn = transform.GetChild(0).GetComponent<Button>();
        }

        Transform openBombTransform = transform.Find("Open_Bomb_btn");
        if (openBombTransform != null)
        {
            Open_Bomb_btn = openBombTransform.GetComponent<Button>();
        }

        Transform[] sceneTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (sceneTransform.name == "BagPanel")
            {
                bagPanel = sceneTransform.GetComponent<WheelChangePanel>();
                equipmentBagController = sceneTransform.GetComponentInChildren<EquipmentBagController>(true);
                equipmentBagControllerCopy = sceneTransform.GetComponentInChildren<EquipmentBagController_copy>(true);
                equipmentSelectionController = sceneTransform.GetComponentInChildren<EquipmentSelectionController>(true);
                equipmentSelectionControllerCopy = sceneTransform.GetComponentInChildren<EquipmentSelectionController_copy>(true);
            }
            else if (sceneTransform.name == "BombPanel")
            {
                bombPanel = sceneTransform.GetComponent<BombPanel>();
                bombPanelOriginalScale = sceneTransform.localScale;
            }
        }

        if (OpenBag_btn != null)
        {
            OpenBag_btn.onClick.AddListener(OpenBagPanel);
        }

        if (Open_Bomb_btn != null)
        {
            Open_Bomb_btn.onClick.AddListener(OpenBombPanel);
        }


        ConfigureExportButton();
        ConfigureHandoffButtons();
        ConfigureAlgorithmDialog();
    }

    private void OnDestroy()
    {
        sceneImportRequest?.Cancel();
        sceneImportRequest?.Dispose();
        foreach (var image in setupImages) if (image != null) Destroy(image);
        if (OpenBag_btn != null)
        {
            OpenBag_btn.onClick.RemoveListener(OpenBagPanel);
        }

        if (Open_Bomb_btn != null)
        {
            Open_Bomb_btn.onClick.RemoveListener(OpenBombPanel);
        }

        if (bombOpenTween.IsActive)
            bombOpenTween.Kill();

        if (bombPanel != null)
        {
            bombPanel.RemoveEscClickListener(ReturnToLastPanelFromBomb);
            bombPanel.transform.localScale = bombPanelOriginalScale;
        }

        if (ExportRos_btn != null)
        {
            ExportRos_btn.onClick.RemoveListener(BeginExportRosConfiguration);
        }

        RosSimulationHandoffUi.Unbind(stopSimulationButton, exportPhysicalButton);

        RemoveEscClickListener(OpenLastPanel);
    }

    private void OpenBagPanel()
    {
        if (bagPanel != null)
        {
            if (!(bagPanel is BagPanel_copy)) Hide();
            if (bagPanel is BagPanel concreteBagPanel)
                concreteBagPanel.OpenBag();
            else if (bagPanel is BagPanel_copy copiedBagPanel)
                copiedBagPanel.OpenBag();
            else
                bagPanel.Show();
        }
    }

    private void OpenBombPanel()
    {
        if (bombPanel == null)
        {
            Debug.LogWarning("LastPanel: BombPanel was not found.", this);
            return;
        }

        Hide();
        bombPanel.gameObject.SetActive(true);
        bombPanel.RemoveEscClickListener(ReturnToLastPanelFromBomb);
        bombPanel.AddEscClickListener(ReturnToLastPanelFromBomb);
        bombPanel.ShowEscButton();
        orbitCameraController?.LockToFixedView(-70f, 42f, 30f);

        if (bombOpenTween.IsActive)
            bombOpenTween.Kill();

        Transform bombTransform = bombPanel.transform;
        bombTransform.localScale = Vector3.zero;
        bombOpenTween = RTween.Scale(
                bombTransform,
                bombPanelOriginalScale,
                0.45f)
            .SetEase(EaseType.OutCirc)
            .OnComplete(() =>
            {
                bombOpenTween = default;
                if (bombPanel != null)
                    bombPanel.transform.localScale = bombPanelOriginalScale;
            });
    }

    private void ReturnToLastPanelFromBomb()
    {
        if (bombPanel != null)
            bombPanel.RemoveEscClickListener(ReturnToLastPanelFromBomb);

        OpenLastPanel();
    }

    public void OpenLastPanel()
    {
        Show();
        HideEscButton();
    }

    private void ConfigureExportButton()
    {
        Transform existing = transform.Find("Export_ROS_btn");
        if (existing == null && Open_Bomb_btn != null)
        {
            GameObject created = Instantiate(Open_Bomb_btn.gameObject, transform);
            created.name = "Export_ROS_btn";
            existing = created.transform;
        }

        if (existing == null)
        {
            Debug.LogError("LastPanel：无法创建 ROS 配置导出按钮。", this);
            return;
        }

        RectTransform rect = existing as RectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -63.5f);
        rect.localScale = Open_Bomb_btn != null
            ? Open_Bomb_btn.transform.localScale
            : Vector3.one;

        ExportRos_btn = existing.GetComponent<Button>();
        ExportRos_label = existing.GetComponentInChildren<Text>(true);
        if (ExportRos_label != null)
        {
            ExportRos_label.text = "启动仿真";
            ChineseUiFont.Apply(ExportRos_label);
        }

        ExportRos_btn.onClick.RemoveAllListeners();
        ExportRos_btn.onClick.AddListener(BeginExportRosConfiguration);
    }

    private void BeginExportRosConfiguration()
    {
        if (algorithmDialog == null)
            ConfigureAlgorithmDialog();

        if (algorithmDialog != null)
        {
            foreach (var entry in sceneButtons)
                SetScenarioButtonState(entry.button, entry.scene.id == FarmScenarioCatalog.SelectedId);
            algorithmDialog.SetActive(true);
            AssistantConversation.RecordUi("打开仿真配置", state => state.page = "simulation_setup");
            algorithmDialog.transform.SetAsLastSibling();
        }
    }

    public void ApplyRecommendation(NaturalRecommendation item)
    {
        FarmScenarioCatalog.RefreshImportedScenes();
        if (algorithmDialog == null) ConfigureAlgorithmDialog();
        BuildSceneGrid();
        FarmScenarioCatalog.SelectedId = item.scene;
        SelectAlgorithm(RosSimulationLauncher.FindAlgorithm(item.algorithm));
        BeginExportRosConfiguration();
        ShowSetupTab(true);
    }

    private void ConfigureHandoffButtons()
    {
        if (ExportRos_btn == null || Open_Bomb_btn == null)
            return;

        stopSimulationButton = CreateHandoffButton("Stop_Simulation_btn", "停止仿真");
        exportPhysicalButton = CreateHandoffButton("Export_Physical_btn", "导出真机配置");
        Button[] actions = { Open_Bomb_btn, ExportRos_btn,
            stopSimulationButton, exportPhysicalButton, OpenBag_btn };
        var toolbar = VehicleActionToolbarLayout.Create(transform, GetComponentInParent<Canvas>());
        for (int index = 0; index < actions.Length; index++)
        {
            if (actions[index] == null)
                continue;
            actions[index].transform.SetParent(toolbar.transform, false);
            GarageVisualStyle.ButtonStyle(actions[index], actions[index] == ExportRos_btn);
            RectTransform rect = actions[index].GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            var element = actions[index].GetComponent<LayoutElement>() ?? actions[index].gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = 0;
            element.flexibleWidth = 1;
        }
        RosSimulationHandoffUi.Bind(stopSimulationButton, exportPhysicalButton, ShowPop);
    }

    private Button CreateHandoffButton(string objectName, string title)
    {
        Transform existing = transform.Find(objectName);
        GameObject item = existing != null
            ? existing.gameObject
            : Instantiate(Open_Bomb_btn.gameObject, transform);
        item.name = objectName;
        Button button = item.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        Text label = item.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = title;
            ChineseUiFont.Apply(label);
        }
        return button;
    }

    private void ConfigureAlgorithmDialog() => BuildModernAlgorithmDialog();

    private void CreateComponentSelector(
        Transform parent,
        string groupLabel,
        IReadOnlyList<RosSimulationComponentOption> options,
        float anchorY)
    {
        GameObject group = CreateImageObject(
            parent,
            $"{groupLabel}Selector",
            new Color(0.06f, 0.075f, 0.08f, 1f));
        SetAnchoredRect(
            group.GetComponent<RectTransform>(),
            new Vector2(0.035f, anchorY),
            new Vector2(0.965f, anchorY + 0.17f),
            Vector2.zero,
            Vector2.zero);
        AddThinBorder(group, new Color(0.17f, 0.21f, 0.22f, 1f));

        CreateDialogText(
            group.transform,
            "Label",
            groupLabel,
            new Vector2(0.035f, 0.64f),
            new Vector2(0.965f, 0.97f),
            Vector2.zero,
            Vector2.zero,
            14,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            new Color(0.68f, 0.76f, 0.75f, 1f));

        GameObject row = new GameObject(
            "Options",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup));
        row.transform.SetParent(group.transform, false);
        SetAnchoredRect(
            row.GetComponent<RectTransform>(),
            new Vector2(0.025f, 0.09f),
            new Vector2(0.975f, 0.62f),
            Vector2.zero,
            Vector2.zero);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        foreach (RosSimulationComponentOption option in options)
            CreateComponentOption(row.transform, option);
    }

    private void CreateComponentOption(
        Transform parent,
        RosSimulationComponentOption component)
    {
        GameObject option = CreateImageObject(
            parent,
            $"Component_{component.Id}",
            DialogSurface);
        LayoutElement element = option.AddComponent<LayoutElement>();
        element.minHeight = 34f;
        element.flexibleWidth = 1f;
        Button button = option.AddComponent<Button>();
        button.targetGraphic = option.GetComponent<Image>();
        Outline border = option.AddComponent<Outline>();
        border.effectDistance = new Vector2(1f, -1f);
        Text label = CreateDialogText(
            option.transform,
            "Label",
            component.ShortName,
            new Vector2(0.04f, 0.05f),
            new Vector2(0.96f, 0.95f),
            Vector2.zero,
            Vector2.zero,
            14,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            DialogText);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 9;
        label.resizeTextMaxSize = 14;

        ComponentOptionVisual visual = new ComponentOptionVisual
        {
            Component = component,
            Button = button,
            Background = option.GetComponent<Image>(),
            Border = border,
            Label = label
        };
        componentOptions.Add(visual);
        button.onClick.AddListener(() => SelectComponent(component));
    }

    private Text CreateDetailRow(
        Transform parent,
        string objectName,
        string label,
        float anchorY)
    {
        GameObject row = CreateImageObject(
            parent,
            objectName,
            GarageVisualStyle.Raised);
        SetAnchoredRect(
            row.GetComponent<RectTransform>(),
            new Vector2(0.06f, anchorY),
            new Vector2(0.94f, anchorY + 0.085f),
            Vector2.zero,
            Vector2.zero);
        CreateDialogText(
            row.transform,
            "Label",
            label,
            new Vector2(0.035f, 0f),
            new Vector2(0.23f, 1f),
            Vector2.zero,
            Vector2.zero,
            14,
            FontStyle.Bold,
            TextAnchor.MiddleLeft,
            GarageVisualStyle.Muted);
        Text value = CreateDialogText(
            row.transform,
            "Value",
            string.Empty,
            new Vector2(0.25f, 0f),
            new Vector2(0.965f, 1f),
            Vector2.zero,
            Vector2.zero,
            15,
            FontStyle.Normal,
            TextAnchor.MiddleLeft,
            DetailText);
        value.resizeTextForBestFit = true;
        value.resizeTextMinSize = 11;
        value.resizeTextMaxSize = 15;
        return value;
    }

    private void SelectAlgorithm(RosSimulationAlgorithmProfile profile)
    {
        selectedPerception = profile.PerceptionComponent;
        selectedVisualPerception = profile.VisualPerceptionComponent;
        selectedLocalization = profile.LocalizationComponent;
        selectedPlanner = profile.PlannerComponent;
        selectedController = profile.ControllerComponent;
        RefreshSelectedAlgorithm();
    }

    private void SelectComponent(RosSimulationComponentOption component)
    {
        switch (component.Kind)
        {
            case RosSimulationComponentKind.Perception:
                selectedPerception = component;
                break;
            case RosSimulationComponentKind.VisualPerception:
                selectedVisualPerception = component;
                break;
            case RosSimulationComponentKind.Localization:
                selectedLocalization = component;
                break;
            case RosSimulationComponentKind.Planner:
                selectedPlanner = component;
                break;
            case RosSimulationComponentKind.Controller:
                selectedController = component;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        RefreshSelectedAlgorithm();
    }

    private void ClearAlgorithmSelection()
    {
        selectedPerception = selectedVisualPerception = selectedLocalization = selectedPlanner = selectedController = null;
        RefreshSelectedAlgorithm(false);
    }

    private void RefreshSelectedAlgorithm(bool recordChange = true)
    {
        bool complete = selectedPerception != null && selectedVisualPerception != null &&
            selectedLocalization != null && selectedPlanner != null && selectedController != null;
        selectedAlgorithm = complete ? RosSimulationLauncher.Compose(
            selectedPerception.Id,
            selectedVisualPerception.Id,
            selectedLocalization.Id,
            selectedPlanner.Id,
            selectedController.Id) : null;
        var state = AssistantConversation.State;
        string id = selectedAlgorithm?.Id ?? "";
        string perception = selectedPerception?.Id ?? "", vision = selectedVisualPerception?.Id ?? "";
        string localization = selectedLocalization?.Id ?? "", planner = selectedPlanner?.Id ?? "", controller = selectedController?.Id ?? "";
        bool changed = state.algorithm != id || state.perception != perception || state.vision != vision ||
            state.localization != localization || state.planner != planner || state.controller != controller;
        state.algorithm = id; state.perception = perception; state.vision = vision;
        state.localization = localization; state.planner = planner; state.controller = controller;
        if (recordChange && changed)
            AssistantConversation.RecordUi(complete ? "选择算法：" + selectedAlgorithm.DisplayName : "算法组件选择已更新");

        algorithmDetailTitle.text = selectedAlgorithm?.DisplayName ?? "未选择算法";
        algorithmDetailId.text = id;
        algorithmDetailDescription.text = selectedAlgorithm?.Description ?? "";
        algorithmPerceptionValue.text = selectedPerception?.DisplayName ?? "未选择";
        algorithmVisualPerceptionValue.text = selectedVisualPerception?.DisplayName ?? "未选择";
        algorithmLocalizationValue.text = selectedLocalization?.DisplayName ?? "未选择";
        algorithmPlannerValue.text = selectedPlanner?.DisplayName ?? "未选择";
        algorithmControllerValue.text = selectedController?.DisplayName ?? "未选择";
        algorithmRouteValue.text = selectedAlgorithm?.RouteMode ?? "未选择";
        RefreshLaunchAvailability();

        foreach (ComponentOptionVisual option in componentOptions)
        {
            bool selected = IsSelectedComponent(option.Component);
            Color normal = selected
                ? new Color(0.12f, 0.30f, 0.24f, 1f)
                : DialogSurface;
            Color highlighted = selected
                ? new Color(0.15f, 0.37f, 0.29f, 1f)
                : DialogSurfaceRaised;
            ColorBlock colors = option.Button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = new Color(0.09f, 0.45f, 0.32f, 1f);
            colors.selectedColor = highlighted;
            colors.disabledColor = new Color(0.10f, 0.11f, 0.12f, 0.45f);
            colors.colorMultiplier = 1f;
            option.Button.colors = colors;
            option.Background.color = normal;
            option.Border.effectColor = selected
                ? DialogAccent
                : new Color(0.22f, 0.26f, 0.27f, 1f);
            option.Label.color = selected ? Color.white : DialogText;
        }
    }

    private void RefreshLaunchAvailability()
    {
        if (launchSelectedAlgorithmButton != null)
            launchSelectedAlgorithmButton.interactable = selectedAlgorithm != null && FarmScenarioCatalog.HasSelection && !sceneImportBusy && !sceneDeleteBusy;
    }

    private bool IsSelectedComponent(RosSimulationComponentOption component)
    {
        switch (component.Kind)
        {
            case RosSimulationComponentKind.Perception:
                return selectedPerception?.Id == component.Id;
            case RosSimulationComponentKind.VisualPerception:
                return selectedVisualPerception?.Id == component.Id;
            case RosSimulationComponentKind.Localization:
                return selectedLocalization?.Id == component.Id;
            case RosSimulationComponentKind.Planner:
                return selectedPlanner?.Id == component.Id;
            case RosSimulationComponentKind.Controller:
                return selectedController?.Id == component.Id;
            default:
                return false;
        }
    }

    private static GameObject CreateImageObject(
        Transform parent,
        string objectName,
        Color color)
    {
        GameObject item = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        item.transform.SetParent(parent, false);
        item.GetComponent<Image>().color = color;
        return item;
    }

    private static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchoredRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void AddThinBorder(GameObject target, Color color)
    {
        Outline border = target.AddComponent<Outline>();
        border.effectColor = color;
        border.effectDistance = new Vector2(1f, -1f);
    }

    private Text CreateDialogText(
        Transform parent,
        string objectName,
        string content,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color)
    {
        GameObject item = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        item.transform.SetParent(parent, false);
        RectTransform rect = item.GetComponent<RectTransform>();
        SetAnchoredRect(rect, anchorMin, anchorMax, offsetMin, offsetMax);
        Text label = item.GetComponent<Text>();
        label.text = content;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        ChineseUiFont.Apply(label);
        return label;
    }

    private static void SetScenarioButtonState(Button button, bool selected)
    {
        button.GetComponent<Image>().color = Color.white;
        ColorBlock colors = button.colors;
        colors.normalColor = selected ? DialogAccent : DialogSurfaceRaised;
        colors.highlightedColor = selected ? DialogAccent : new Color(0.22f, 0.25f, 0.26f, 1f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    private Button CreateFlatButton(
        Transform parent,
        string objectName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string labelText,
        Color normalColor,
        Color highlightedColor,
        Color textColor,
        int fontSize,
        FontStyle fontStyle)
    {
        GameObject item = CreateImageObject(parent, objectName, normalColor);
        SetAnchoredRect(
            item.GetComponent<RectTransform>(),
            anchorMin,
            anchorMax,
            Vector2.zero,
            Vector2.zero);
        Button button = item.AddComponent<Button>();
        button.targetGraphic = item.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = DialogAccent;
        colors.selectedColor = highlightedColor;
        colors.disabledColor = new Color(0.2f, 0.22f, 0.23f, 0.45f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        Text label = CreateDialogText(
            item.transform,
            "Label",
            labelText,
            new Vector2(0.04f, 0.04f),
            new Vector2(0.96f, 0.96f),
            Vector2.zero,
            Vector2.zero,
            fontSize,
            fontStyle,
            TextAnchor.MiddleCenter,
            textColor);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(11, fontSize - 4);
        label.resizeTextMaxSize = fontSize;
        return button;
    }

    private void StartSelectedAlgorithm(string algorithmId)
    {
        if (algorithmDialog != null)
            algorithmDialog.SetActive(false);
        StartCoroutine(ExportRosConfiguration(algorithmId));
    }

    private IEnumerator ExportRosConfiguration(string algorithmId)
    {
        if (ExportRos_btn == null || !ExportRos_btn.interactable || RosSimulationHandoffUi.IsBusy)
            yield break;

        ExportRos_btn.interactable = false;
        if (ExportRos_label != null)
            ExportRos_label.text = "正在准备...";
        yield return null;

        try
        {
            if (externalVehicle == null && externalExport == null && !ValidateInstalledEquipment(out string installationError))
                throw new InvalidOperationException(installationError);

            VehicleConfigurationExportResult result =
                externalExport != null ? externalExport() :
                (externalVehicle != null ? externalVehicle() : AckermannRuntimeConfiguration.BuildOrRefresh()).ExportConfigurationBundle();
            RosSimulationLaunchResult simulation =
                RosSimulationLauncher.Start(result.JsonPath, algorithmId);
            var exported = JsonUtility.FromJson<VehicleConfigurationData>(File.ReadAllText(result.JsonPath));
            RosSimulationHandoffUi.SimulationStarted(
                exported.vehicleId == "ackermann_current" ? Path.Combine(Path.GetDirectoryName(result.JsonPath), "ackermann_physical_runtime.json") : null);
            Notify(
                $"ROS 配置导出成功：{result.ExportedMeshCount} 个网格\n" +
                $"算法：{simulation.Algorithm.DisplayName}\n" +
                $"同步与仿真任务已启动（PID {simulation.ProcessId}）\n" +
                result.JsonPath);
        }
        catch (Exception exception)
        {
            AssistantConversation.RecordUi("启动仿真未完成：" + exception.Message);
            Debug.LogException(exception, this);
            Notify($"ROS 配置导出失败：{exception.Message}");
        }
        finally
        {
            if (ExportRos_label != null)
                ExportRos_label.text = "启动仿真";
            if (ExportRos_btn != null)
                ExportRos_btn.interactable = true;
        }
    }

    private bool ValidateInstalledEquipment(out string message)
    {
        if (equipmentBagController == null && equipmentBagControllerCopy == null)
        {
            message = "没有找到装备背包控制器。";
            return false;
        }

        EquipmentSlotType[] requiredSlots =
        {
            EquipmentSlotType.Radar,
            EquipmentSlotType.RTK,
            EquipmentSlotType.Camera,
            EquipmentSlotType.Imu
        };
        string[] displayNames = { "雷达", "RTK", "相机", "IMU" };
        for (int index = 0; index < requiredSlots.Length; index++)
        {
            EquipmentData equipment = equipmentBagController != null
                ? equipmentBagController.GetEquipment(requiredSlots[index])
                : equipmentBagControllerCopy.GetEquipment(requiredSlots[index]);
            bool installed = equipmentBagController != null
                ? equipment != null && equipmentBagController.IsInstalled(equipment)
                : equipment != null && equipmentBagControllerCopy.IsInstalled(equipment);
            if (equipment == null || !installed)
            {
                message = $"请先安装{displayNames[index]}，再导出 ROS 配置。";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }
}
