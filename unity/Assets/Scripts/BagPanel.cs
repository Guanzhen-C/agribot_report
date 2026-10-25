using RG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using AgriculturalMachinery.BagSystem;

public class BagPanel : WheelChangePanel
{
    protected override PanelShowEffect ShowEffect => PanelShowEffect.Fade;

    [SerializeField] private bool keepCameraTargetUntilMainPanel;

    private Canvas frontRightWheelCanvas;
    private LastPanel lastPanel;
    private cameraControl currentCameraControl;
    private OrbitCameraController legacyCameraController;
    private Transform previousCameraTarget;
    private GameObject currentCar;
    private Button closeButton;

    private void LateUpdate() => VehicleActionToolbarLayout.PositionSidePanel(transform as RectTransform);

   
    protected override void Awake()
    {
        base.Awake();
        //ShowPop("农机车恢复初始比例以安置装备！");
        ResolveCurrentCarCamera();

        ResolveCloseTargets();
        GarageVisualStyle.EquipmentPanel(transform);
        AddEscClickListener(Hide);

        closeButton = FindButton("CloseButton");
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }

    }

    private void OnDestroy()
    {
        RemoveEscClickListener(Hide);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
    }

    public void OpenBag()
    {
        ResolveCurrentCarCamera();
        if (currentCameraControl != null || legacyCameraController != null)
        {
            previousCameraTarget = GetTargetForCurrentCar(false);
            var farm = currentCar.GetComponent<FarmGarageVehicle>();
            if (currentCameraControl != null)
            {
                if (farm != null) currentCameraControl.FixCamera(farm.PreviewRadius, -45f, 25f);
                else currentCameraControl.HoldCurrentView();
            }
            else
                legacyCameraController.HoldCurrentView();

            Transform bagTarget = GetTargetForCurrentCar(true);
            if (farm != null && bagTarget != null)
            {
                if (currentCameraControl != null)
                    currentCameraControl.ChangeCameraTarget(bagTarget);
                else
                    legacyCameraController.ChangeCameraTarget(bagTarget);
            }
        }

        Show();
        GarageVisualStyle.NormalizePanelText(transform);
    }

    public void ResetEquipmentState()
    {
        GetComponent<EquipmentBagController>()?.UninstallAllEquipment();
        GetComponent<EquipmentSelectionController>()?.ClearSelection();
    }

    public void ShowHandUnavailablePop()
    {
        if (Pop == null)
            return;

        RectTransform popRect = Pop.transform as RectTransform;
        CanvasGroup popGroup = Pop.GetComponent<CanvasGroup>();
        if (popGroup == null)
            popGroup = Pop.AddComponent<CanvasGroup>();

        if (popRect != null)
            RTween.Kill(popRect);
        RTween.Kill(popGroup);
        Pop.SetActive(true);
        popGroup.alpha = 1f;
        popGroup.interactable = false;
        popGroup.blocksRaycasts = false;

        if (popRect == null)
            return;

        Vector2 targetPosition = popRect.anchoredPosition;
        popRect.anchoredPosition = targetPosition + Vector2.right * 2000f;

        RTween.UIMove(popRect, targetPosition, 0.5f)
            .SetEase(EaseType.OutBack)
            .OnComplete(() =>
            {
                if (popGroup == null || !Pop)
                    return;

                // Keep the original six-second visible duration, including the slide-in time.
                RTween.DelayedCall(5.5f, () =>
                {
                    if (popGroup == null || !Pop)
                        return;

                    RTween.Alpha(popGroup, 0f, 0.35f)
                        .OnComplete(() =>
                        {
                            if (!Pop)
                                return;

                            popGroup.alpha = 1f;
                            Pop.SetActive(false);
                        });
                });
            });
    }

    private void OnEnable()
    {
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    public new void Hide()
    {
        if (Agribot.VehicleConfiguration.AssistantConversation.State.page == "equipment")
            Agribot.VehicleConfiguration.AssistantConversation.RecordUi("关闭装备面板", state => state.page = "vehicle");
        if (!gameObject.activeInHierarchy)
            return;

        HideEscButton();
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            if (keepCameraTargetUntilMainPanel)
                ReleaseCameraInput();
            else
                RestoreCurrentCamera();
            gameObject.SetActive(false);
            CompleteCloseActions();
            return;
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        RTween.Kill(canvasGroup);
        RTween.Alpha(canvasGroup, 0f, 0.35f)
            .OnComplete(() =>
            {
                if (keepCameraTargetUntilMainPanel)
                    ReleaseCameraInput();
                else
                    RestoreCurrentCamera();
                gameObject.SetActive(false);
                CompleteCloseActions();
            });
    }

    private void ResolveCurrentCarCamera()
    {
        currentCar = FindCurrentCar();
        currentCameraControl = null;
        legacyCameraController = null;
        if (currentCar == null)
            return;

        currentCameraControl = currentCar.GetComponentInChildren<cameraControl>(true);
        if (currentCameraControl == null)
        {
            OrbitCameraController[] cameras =
                currentCar.GetComponentsInChildren<OrbitCameraController>(true);
            if (cameras.Length > 0)
                legacyCameraController = cameras[0];
        }
    }

    private void ReleaseCameraInput()
    {
        if (currentCameraControl == null && legacyCameraController == null)
            ResolveCurrentCarCamera();

        if (currentCameraControl != null || legacyCameraController != null)
        {
            if (currentCameraControl != null)
                currentCameraControl.RestoreCameraInput();
            else
                legacyCameraController.RestoreCameraInput();
        }
    }

    private void RestoreCurrentCamera()
    {
        if (currentCameraControl == null && legacyCameraController == null)
            ResolveCurrentCarCamera();

        if (currentCameraControl != null || legacyCameraController != null)
        {
            Transform target = previousCameraTarget != null
                ? previousCameraTarget
                : GetTargetForCurrentCar(false);
            if (currentCameraControl != null)
                currentCameraControl.RestoreCameraInput(target);
            else
                legacyCameraController.RestoreCameraInput(target);
        }

        previousCameraTarget = null;
    }

    public void RestoreOriginalCameraTarget()
    {
        if (!keepCameraTargetUntilMainPanel || previousCameraTarget == null)
            return;

        if (currentCameraControl == null && legacyCameraController == null)
            ResolveCurrentCarCamera();

        if (currentCameraControl != null)
            currentCameraControl.ChangeCameraTarget(previousCameraTarget);
        else if (legacyCameraController != null)
            legacyCameraController.ChangeCameraTarget(previousCameraTarget);

        previousCameraTarget = null;
    }

    private Transform GetTargetForCurrentCar(bool equipmentView)
    {
        if (currentCar == null)
            return null;

        var farm = currentCar.GetComponent<FarmGarageVehicle>();
        if (farm != null) return equipmentView ? farm.EquipmentTarget : farm.ViewTarget;

        string carName = currentCar.name;
        string targetName = equipmentView
            ? carName switch
            {
                "Car0" => "CameraTarget2",
                "Car1" => "target11",
                "Car2" => "target22",
                "Car3" => "target33",
                "Car4" => "target44",
                "Car5" => "target55",
                _ => null
            }
            : carName switch
            {
                "Car0" => "CameraTarget",
                "Car1" => "target1",
                "Car2" => "target2",
                "Car3" => "target3",
                "Car4" => "target4",
                "Car5" => "target5",
                _ => null
            };

        return FindChild(currentCar.transform, targetName);
    }

    private static GameObject FindCurrentCar()
    {
        foreach (string name in new[] { "Car0", "Car1", "Car2", "Car3", "Car4", "Car5" })
        {
            GameObject car = GameObject.Find(name);
            if (car != null && car.activeInHierarchy)
                return car;
        }

        return FarmGarageVehicle.Visible;
    }

    private Button FindButton(string objectName)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == objectName)
                return button;
        }

        return null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;
        if (root.name == childName)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void ResolveCloseTargets()
    {
        GameObject car = GameObject.Find("Car0");
        Transform wheel = car != null
            ? car.transform.Find("Truck/QianQiao/Wheel_FR")
            : null;
        if (wheel != null)
        {
            frontRightWheelCanvas = wheel.GetComponentInChildren<Canvas>(true);
        }

        LastPanel[] panels = FindObjectsByType<LastPanel>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (panels.Length > 0)
        {
            lastPanel = panels[0];
        }
    }

    private void CompleteCloseActions()
    {
        if (frontRightWheelCanvas == null || lastPanel == null)
        {
            ResolveCloseTargets();
        }

        if (frontRightWheelCanvas != null)
        {
            frontRightWheelCanvas.gameObject.SetActive(false);
        }

        if (lastPanel != null)
        {
            lastPanel.OpenLastPanel();
        }
    }

    void Update()
    {
        
    }
}
