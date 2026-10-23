using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Cinemachine;
using Agribot.VehicleConfiguration;
using AgriculturalMachinery.BagSystem;

/// <summary>
/// Main vehicle-selection panel.
/// The scene objects are resolved by name so the script also works when the
/// references are not assigned manually in the Inspector.
/// </summary>
public class MainPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject backButton;
    [SerializeField] private GameObject outputButton;
    [SerializeField] private GameObject openBagButton;
    [SerializeField] private float slideDuration = 0.3f;

    [Header("Cars")]
    private GameObject car0;
    [SerializeField] private GameObject car1;
    [SerializeField] private GameObject car2;
    [SerializeField] private GameObject car3;
    [SerializeField] private GameObject car4;
    [SerializeField] private GameObject car5;
    [SerializeField] private float bounceDuration = 0.55f;
    [SerializeField] private float fadeDuration = 0.35f;

    private readonly Dictionary<GameObject, Vector3> originalScales = new();
    private readonly Dictionary<Material, Color> originalMaterialColors = new();
    private readonly List<Button> boundButtons = new();

    private RectTransform panelRect;
    private RectTransform backButtonRect;
    private RectTransform outputButtonRect;
    private RectTransform openBagButtonRect;
    private RectTransform openBagParentRect;
    private Vector2 panelVisiblePosition;
    private Vector3 openBagButtonOriginalScale = Vector3.one;
    private BagPanel bagPanel;
    private GameObject visibleCar;
    private LastPanel vehicleSelector;
    private readonly List<GameObject> farmCars = new();
    private Button explosionButton;
    private Button stopSimulation;
    private Button exportPhysical;
    private RectTransform actionToolbar;
    private SceneVehicleExplodedView explodedView;
    private GameObject notificationPanel;
    private bool physicalExportBusy;
    private float selectedAt;
    private TweenHandle fadeTween;
    private TweenHandle openBagTween;
    private Camera overviewCamera;
    private Pose overviewPose;
    private LensSettings overviewLens;

    private void Awake()
    {
        panelRect = transform as RectTransform;
        panelVisiblePosition = panelRect != null ? panelRect.anchoredPosition : Vector2.zero;

        ResolveReferences();
        car0 = Car0GarageVehicle.Create(this);
        overviewCamera = Camera.main;
        if (overviewCamera != null)
        {
            if (overviewCamera.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                overviewCamera.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            overviewPose = new Pose(overviewCamera.transform.position, overviewCamera.transform.rotation);
            overviewLens = LensSettings.FromCamera(overviewCamera);
        }
        var cameraTemplate = car5;
        foreach (var preset in FarmVehicleLibrary.Presets.Where(p => !string.IsNullOrEmpty(p.garageName)))
        {
            var car = FarmGarageVehicle.Create(cameraTemplate, preset);
            if (car == null) throw new InvalidOperationException("缺少米制车辆资源：" + preset.id);
            // Reuse the original card and workflow, retaining authored source
            // models inactive for editing instead of exporting their miniature scale.
            switch (preset.garageName)
            {
                case "Car1": ReplaceMiniature(ref car1, car); break;
                case "Car2": ReplaceMiniature(ref car2, car); break;
                case "Car4": ReplaceMiniature(ref car4, car); break;
                case "Car5": ReplaceMiniature(ref car5, car); break;
                default: farmCars.Add(car); break;
            }
        }
        SimulationAppearanceLibrary.ApplyToSceneVehicle(car3);
        if (farmCars.Count > 0) FarmGarageGrid.Extend(transform, farmCars, SelectCarFromImage);
        ConfigureActionToolbar();
        BindButton("Car0_Img", () => SelectCarFromImage(car0));
        BindButton("Car1_Img", () => SelectCarFromImage(car1));
        BindButton("Car2_Img", () => SelectCarFromImage(car2));
        BindButton("Car3_Img", () => SelectCarFromImage(car3));
        BindButton("Car4_Img", () => SelectCarFromImage(car4));
        BindButton("Car5_Img", () => SelectCarFromImage(car5));
        BindButton("Back_btn", ShowMainPanel);
        vehicleSelector = LastPanel.CreatePresetSelector(GetComponentInParent<Canvas>().transform,
            outputButton.GetComponent<Button>(), ExportSelectedVehicle, ShowSimulationMessage);
        NaturalRecommendationPanel.Ensure(null);
        stopSimulation.onClick.AddListener(() => RosSimulationHandoffUi.RequestStop(ShowSimulationMessage));
        explosionButton.onClick.AddListener(OpenExplodedView);
        exportPhysical.onClick.AddListener(ExportSelectedPhysical);
        GarageVisualStyle.VehicleBackButton(backButton.GetComponent<Button>());

        bagPanel = FindSceneObject("BagPanel")?.GetComponent<BagPanel>();

        CacheCarState(car0);
        CacheCarState(car1);
        CacheCarState(car2);
        CacheCarState(car3);
        CacheCarState(car4);
        CacheCarState(car5);
        foreach (var car in farmCars) CacheCarState(car);
    }

    private static void ReplaceMiniature(ref GameObject authored, GameObject metric)
    {
        authored.SetActive(false);
        authored.name = "AuthoredMiniature_" + authored.name;
        authored = metric;
    }

    private void Start()
    {
        AssistantConversation.RecordUi("进入车库", state =>
        {
            state.page = "garage"; state.car = ""; state.implement = "none";
            state.sensors = Array.Empty<string>(); state.scene = FarmScenarioCatalog.SelectedId;
        });
        if (backButton != null)
            backButton.SetActive(false);

        if (outputButton != null)
            outputButton.SetActive(false);

        if (openBagButton != null)
            openBagButton.SetActive(false);
        if (actionToolbar != null)
            actionToolbar.gameObject.SetActive(false);
    }

    private VehicleConfigurationAuthoring BuildSelectedVehicle()
    {
        ValidateSelectedEquipment();
        var ackermann = visibleCar.GetComponent<Car0GarageVehicle>();
        if (ackermann != null) return ackermann.BuildConfiguration();
        return SceneVehicleConfiguration.Build(visibleCar);
    }

    private VehicleConfigurationExportResult ExportSelectedVehicle()
    {
        ValidateSelectedEquipment();
        var ackermann = visibleCar.GetComponent<Car0GarageVehicle>();
        if (ackermann != null) return ackermann.BuildConfiguration().ExportConfigurationBundle();
        var farm = visibleCar.GetComponent<FarmGarageVehicle>();
        return farm != null ? farm.Export() : SceneVehicleConfiguration.Build(visibleCar).ExportConfigurationBundle();
    }

    private void ExportSelectedPhysical()
    {
        if (visibleCar != null && visibleCar.GetComponent<Car0GarageVehicle>() != null)
            RosSimulationHandoffUi.RequestExport(ShowSimulationMessage);
        else RosSimulationHandoffUi.RequestDraftExport(ExportPhysicalDraft());
    }

    private void Update()
    {
        if (exportPhysical != null && visibleCar != null && actionToolbar.gameObject.activeInHierarchy)
            exportPhysical.interactable = visibleCar.GetComponent<Car0GarageVehicle>() != null
                ? RosSimulationHandoffUi.CanExport : !physicalExportBusy && !RosSimulationHandoffUi.IsBusy;
    }

    private void ValidateSelectedEquipment()
    {
        explodedView?.Close();
        if (visibleCar == null) throw new InvalidOperationException("请先选择车辆。");
        if (Time.time - selectedAt < bounceDuration)
            throw new InvalidOperationException("请等待车辆展示动画结束后再导出。");
        var controller = bagPanel.GetComponentInChildren<EquipmentBagController>(true);
        if (controller != null && controller.IsSceneAnimating)
            throw new InvalidOperationException("请等待装备安装动画结束后再导出。");
        foreach (var slot in new[] { EquipmentSlotType.Radar, EquipmentSlotType.RTK, EquipmentSlotType.Camera, EquipmentSlotType.Imu })
            if (controller == null || !controller.IsInstalled(controller.GetEquipment(slot)))
                throw new InvalidOperationException("请先安装雷达、RTK、相机和IMU，等待安装动画结束。");
    }

    private IEnumerator ExportPhysicalDraft()
    {
        if (physicalExportBusy) yield break;
        string jsonPath;
        string outputDirectory;
        try
        {
            if (Application.platform != RuntimePlatform.LinuxEditor &&
                Application.platform != RuntimePlatform.LinuxPlayer)
                throw new PlatformNotSupportedException("离线草案导出仅支持 Ubuntu 工作站。");
            if (RosSimulationLauncher.ReadManagedStatus() != "stopped")
                throw new InvalidOperationException("请先停止仿真，再导出真机配置草案。");
            var farm = visibleCar != null ? visibleCar.GetComponent<FarmGarageVehicle>() : null;
            string vehicleId;
            if (farm != null)
            {
                jsonPath = ExportSelectedVehicle().JsonPath;
                vehicleId = farm.SelectedPreset;
            }
            else
            {
                var authoring = BuildSelectedVehicle();
                jsonPath = authoring.ExportJson();
                vehicleId = authoring.VehicleId;
            }
            string exportRoot = Environment.GetEnvironmentVariable("AGRIBOT_PHYSICAL_EXPORT_ROOT");
            if (string.IsNullOrWhiteSpace(exportRoot))
                exportRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "unity_exports");
            outputDirectory = Path.Combine(exportRoot, vehicleId + "_physical_draft");
        }
        catch (Exception error)
        {
            ShowSimulationMessage(error.Message);
            yield break;
        }
        physicalExportBusy = true;
        exportPhysical.interactable = false;
        ShowSimulationMessage("正在导出当前车型的未标定配置草案");
        Task<string> task = Task.Run(() => RosSimulationLauncher.ExportPhysicalDraft(jsonPath, outputDirectory));
        while (!task.IsCompleted) yield return null;
        if (task.IsFaulted || task.IsCanceled)
        {
            string message = task.Exception?.GetBaseException().Message ?? "草案导出已取消";
            Debug.LogError(message);
            ShowSimulationMessage("导出失败：" + message);
        }
        else
        {
            Debug.Log(task.Result);
            ShowSimulationMessage("已导出未标定配置草案：" + outputDirectory);
            if (!Application.isBatchMode)
                Application.OpenURL(new Uri(outputDirectory).AbsoluteUri);
        }
        physicalExportBusy = false;
        exportPhysical.interactable = true;
    }

    private void ShowSimulationMessage(string message)
    {
        AssistantConversation.RecordUi("软件操作结果：" + message);
        Debug.Log(message);
        notificationPanel = GarageNotification.Show(transform, message).gameObject;
    }

    private void OnDestroy()
    {
        if (vehicleSelector != null) Destroy(vehicleSelector.gameObject);
        if (explodedView != null) Destroy(explodedView.gameObject);
        if (notificationPanel != null) Destroy(notificationPanel);
        if (panelRect != null)
            RTween.Kill(panelRect);
        if (backButtonRect != null)
            RTween.Kill(backButtonRect);
        if (outputButtonRect != null)
            RTween.Kill(outputButtonRect);
        if (openBagButtonRect != null)
            RTween.Kill(openBagButtonRect);
        RTween.Kill(fadeTween);
        RTween.Kill(openBagTween);

        foreach (Button button in boundButtons)
            button.onClick.RemoveAllListeners();
    }

    private void ResolveReferences()
    {
        backButton ??= FindSceneObject("Back_btn");
        backButtonRect = backButton != null ? backButton.GetComponent<RectTransform>() : null;

        outputButton ??= FindSceneObject("Output_btn");
        outputButtonRect = outputButton != null
            ? outputButton.GetComponent<RectTransform>()
            : null;

        openBagButton ??= FindSceneObject("OpenBag_btn");
        openBagButtonRect = openBagButton != null
            ? openBagButton.GetComponent<RectTransform>()
            : null;
        if (openBagButtonRect != null)
        {
            openBagButtonOriginalScale = openBagButtonRect.localScale;
            openBagParentRect = openBagButtonRect.parent as RectTransform;
        }

        foreach (GameObject action in new[] { backButton, outputButton, openBagButton })
            if (action != null)
                foreach (Text label in action.GetComponentsInChildren<Text>(true))
                    label.raycastTarget = false;

        car1 ??= FindSceneObject("Car1");
        car2 ??= FindSceneObject("Car2");
        car3 ??= FindSceneObject("Car3");
        car4 ??= FindSceneObject("Car4");
        car5 ??= FindSceneObject("Car5");
    }

    private void ConfigureActionToolbar()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || outputButton == null || openBagButton == null)
            return;

        int originalIndex = outputButton.transform.GetSiblingIndex();
        var toolbar = VehicleActionToolbarLayout.Create(canvas.transform, canvas);
        toolbar.transform.SetSiblingIndex(originalIndex);
        actionToolbar = toolbar.GetComponent<RectTransform>();

        explosionButton = CreateActionButton("Explode_NewVehicle_btn", "爆炸图");
        outputButton.transform.SetParent(actionToolbar, false);
        outputButton.GetComponentInChildren<Text>(true).text = "启动仿真";
        ConfigureActionButton(outputButton);
        stopSimulation = CreateActionButton("Stop_NewVehicle_Simulation", "停止仿真");
        exportPhysical = CreateActionButton("Export_NewVehicle_Physical", "导出真机配置");
        openBagButton.transform.SetParent(actionToolbar, false);
        ConfigureActionButton(openBagButton);
        openBagButtonOriginalScale = Vector3.one;

        explodedView = SceneVehicleExplodedView.Create(canvas.transform, outputButton);
        openBagButton.GetComponent<Button>().onClick.AddListener(() => explodedView.Close());
        toolbar.SetActive(false);
    }

    private Button CreateActionButton(string name, string label)
    {
        var item = Instantiate(outputButton, actionToolbar);
        item.name = name;
        var button = item.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        item.GetComponentInChildren<Text>(true).text = label;
        ConfigureActionButton(item);
        return button;
    }

    private static void ConfigureActionButton(GameObject item)
    {
        GarageVisualStyle.ButtonStyle(item.GetComponent<Button>(), item.name == "Output_btn");
        var rect = item.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        var element = item.GetComponent<LayoutElement>() ?? item.AddComponent<LayoutElement>();
        element.minWidth = 0f;
        element.preferredWidth = 0f;
        element.flexibleWidth = 1f;
        foreach (Text label in item.GetComponentsInChildren<Text>(true))
        {
            ChineseUiFont.Apply(label);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = Mathf.Max(17, label.fontSize);
            label.raycastTarget = false;
        }
    }

    private void OpenExplodedView()
    {
        if (visibleCar != null)
            explodedView.Open(visibleCar);
    }

    private void BindButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        GameObject target = FindSceneObject(objectName);
        Button button = target != null ? target.GetComponent<Button>() : null;
        if (button == null)
        {
            Debug.LogWarning($"MainPanel: Button '{objectName}' was not found.", this);
            return;
        }

        button.onClick.AddListener(action);
        boundButtons.Add(button);
    }

    private void CacheCarState(GameObject car)
    {
        if (car == null)
            return;

        originalScales[car] = car.transform.localScale;
        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.materials)
            {
                if (material != null && !originalMaterialColors.ContainsKey(material))
                    originalMaterialColors[material] = material.color;
            }
        }
    }

    private void ShowCar(GameObject car)
    {
        explodedView?.Close();
        if (car == null)
        {
            Debug.LogWarning("MainPanel: The selected car reference is missing.", this);
            return;
        }

        HideOtherCars(car);
        RestoreCarVisuals(car);

        visibleCar = car;
        AssistantConversation.RecordUi("选择车辆：" + car.name, state =>
        {
            state.car = car.name; state.page = "vehicle"; state.implement = "none";
            state.sensors = Array.Empty<string>();
        });
        selectedAt = Time.time;
        // Driving physics are not used by the configuration preview.
        DisablePreviewWheelColliders(car);
        car.SetActive(true);
        // Capture physical dimensions before the presentation scale animation.
        car.GetComponentInChildren<CarChange>(true)?.InitializeGeometry();
        Transform carTransform = car.transform;
        Vector3 targetScale = originalScales.TryGetValue(car, out Vector3 scale)
            ? scale
            : Vector3.one;

        RTween.Kill(carTransform);
        carTransform.localScale = targetScale * 0.05f;
        RTween.Scale(carTransform, targetScale, bounceDuration)
            .SetEase(EaseType.OutBack);

        HideMainPanel();
    }

    private void SelectCarFromImage(GameObject car)
    {
        ShowCar(car);
        ShowOpenBagButton();
    }

    public void SelectRecommendedVehicle(string id)
    {
        GameObject car = id switch
        {
            "Car0" => car0,
            "Car1" => car1, "Car2" => car2, "Car3" => car3, "Car4" => car4, "Car5" => car5,
            _ => farmCars.FirstOrDefault(item => item.name == id)
        };
        if (car == null) throw new InvalidOperationException("车库中未找到推荐车型：" + id);
        CloseBagFromButton();
        SelectCarFromImage(car);
    }

    public void OpenRecommendedEquipment() => OpenBagFromButton();
    public void OpenVehicleExplodedView() => OpenExplodedView();
    public void CloseEquipmentForRepair()
    {
        explodedView?.Close();
        bagPanel?.HideImmediately();
    }

    private void HideMainPanel()
    {
        if (panelRect == null)
            return;

        RTween.Kill(panelRect);
        Vector2 hiddenPosition = panelVisiblePosition + Vector2.left * 2000f;
        RTween.UIMove(panelRect, hiddenPosition, slideDuration)
            .SetEase(EaseType.InBack)
            .OnComplete(() =>
            {
                panelRect.anchoredPosition = panelVisiblePosition;
                gameObject.SetActive(false);
            });

        ShowBackButton();
    }

    private void ShowBackButton()
    {
        if (actionToolbar != null) actionToolbar.gameObject.SetActive(true);
        if (explosionButton != null) explosionButton.gameObject.SetActive(true);
        if (stopSimulation != null) stopSimulation.gameObject.SetActive(true);
        if (exportPhysical != null) exportPhysical.gameObject.SetActive(true);
        if (backButton == null)
            return;

        backButton.SetActive(true);
        if (backButtonRect != null)
        {
            RTween.Kill(backButtonRect);
        }

        if (outputButton != null)
            outputButton.SetActive(true);
    }

    private void ShowOpenBagButton()
    {
        if (openBagButton == null)
            return;

        // In scene1 the button is under a Canvas whose serialized scale is
        // zero. Activating only the button would therefore leave it invisible.
        if (openBagParentRect != null)
        {
            openBagParentRect.gameObject.SetActive(true);
            openBagParentRect.localScale = Vector3.one;
        }

        openBagButton.SetActive(true);
        if (openBagButtonRect == null)
            return;

        RTween.Kill(openBagButtonRect);
        openBagButtonRect.localScale = openBagButtonOriginalScale;
    }

    private void OpenBagFromButton()
    {
        AssistantConversation.RecordUi("打开装备面板", state => state.page = "equipment");
        explodedView?.Close();
        visibleCar?.GetComponent<Car0GarageVehicle>()?.CloseRepair();
        if (bagPanel == null)
            bagPanel = FindSceneObject("BagPanel")?.GetComponent<BagPanel>();

        if (bagPanel == null)
        {
            Debug.LogError("MainPanel: BagPanel is missing the BagPanel component.", this);
            return;
        }

        bagPanel.OpenBag();
    }

    private void CloseBagFromButton()
    {
        if (bagPanel == null)
            bagPanel = FindSceneObject("BagPanel")?.GetComponent<BagPanel>();

        if (bagPanel == null)
            return;

        bagPanel.ResetEquipmentState();
        bagPanel.Hide();
    }

    private void ShowMainPanel()
    {
        AssistantConversation.RecordUi("返回车库", state => { state.page = "garage"; state.car = ""; });
        explodedView?.Close();
        visibleCar?.GetComponent<Car0GarageVehicle>()?.CloseRepair();
        if (actionToolbar != null) actionToolbar.gameObject.SetActive(false);
        CloseBagFromButton();

        if (backButton != null)
            backButton.SetActive(false);

        if (outputButton != null)
            outputButton.SetActive(false);

        if (openBagButton != null)
        {
            RTween.Kill(openBagTween);
            openBagButton.SetActive(false);
        }

        HideVisibleCar(() =>
        {
            if (panelRect == null)
                return;

            gameObject.SetActive(true);
            bagPanel?.RestoreOriginalCameraTarget();
            RestoreOverviewCamera();
            RTween.Kill(panelRect);
            panelRect.anchoredPosition = panelVisiblePosition + Vector2.left * 2000f;
            RTween.UIMove(panelRect, panelVisiblePosition, slideDuration)
                .SetEase(EaseType.OutBack);
        });
    }

    private void RestoreOverviewCamera()
    {
        if (overviewCamera == null) return;
        // With no active vehicle camera, Cinemachine otherwise retains the last
        // vehicle view, which may be outside the restored, smaller garage.
        overviewCamera.GetComponent<CinemachineBrain>()?.ResetState();
        overviewCamera.transform.SetPositionAndRotation(overviewPose.position, overviewPose.rotation);
        overviewCamera.orthographic = overviewLens.Orthographic;
        overviewCamera.fieldOfView = overviewLens.FieldOfView;
        overviewCamera.orthographicSize = overviewLens.OrthographicSize;
        overviewCamera.nearClipPlane = overviewLens.NearClipPlane;
        overviewCamera.farClipPlane = overviewLens.FarClipPlane;
    }

    private void HideVisibleCar(Action onComplete)
    {
        if (visibleCar == null || !visibleCar.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        GameObject carToHide = visibleCar;
        visibleCar = null;
        FadeOutCar(carToHide, () =>
        {
            carToHide.SetActive(false);
            RestoreCarVisuals(carToHide);
            onComplete?.Invoke();
        });
    }

    private void HideOtherCars(GameObject selectedCar)
    {
        var cars = new[] { car0, car1, car2, car3, car4, car5 }.Concat(farmCars);
        foreach (GameObject car in cars)
        {
            if (car == null || car == selectedCar)
                continue;

            RTween.Kill(car.transform);
            car.SetActive(false);
            RestoreCarVisuals(car);
        }
    }

    private void FadeOutCar(GameObject car, Action onComplete)
    {
        RTween.Kill(fadeTween);

        Renderer[] renderers = car.GetComponentsInChildren<Renderer>(true);
        List<Material> materials = new();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.materials)
            {
                if (material != null && !materials.Contains(material))
                    materials.Add(material);
            }
        }

        if (materials.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        // A car can contain dozens of materials. Animate all of them from one
        // value tween so a single Back click cannot create dozens of concurrent
        // RTween jobs and trigger the active-tween warnings.
        fadeTween = RTween.Value(1f, 0f, fadeDuration, alpha =>
        {
            foreach (Material material in materials)
            {
                if (material == null)
                    continue;

                Color color = material.color;
                color.a = alpha;
                material.color = color;
            }
        }).OnComplete(() =>
        {
            fadeTween = default;
            onComplete?.Invoke();
        });
    }

    private static void DisablePreviewWheelColliders(GameObject car)
    {
        if (car == null)
            return;

        foreach (WheelCollider wheelCollider in car.GetComponentsInChildren<WheelCollider>(true))
            wheelCollider.enabled = false;
    }

    private void RestoreCarVisuals(GameObject car)
    {
        if (car == null)
            return;

        if (originalScales.TryGetValue(car, out Vector3 scale))
            car.transform.localScale = scale;

        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.materials)
            {
                if (material != null && originalMaterialColors.TryGetValue(material, out Color color))
                    material.color = color;
            }
        }
    }

    private static GameObject FindSceneObject(string objectName)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = FindChildRecursive(root.transform, objectName);
            if (match != null)
                return match.gameObject;
        }

        // A disabled root can be omitted by some editor/test scene snapshots.
        // Search loaded scene objects as a fallback, while excluding prefab
        // assets and objects from other scenes.
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate.name == objectName &&
                candidate.gameObject.scene.IsValid() &&
                candidate.gameObject.scene == scene)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        foreach (Transform child in parent)
        {
            Transform match = FindChildRecursive(child, objectName);
            if (match != null)
                return match;
        }

        return null;
    }
}
