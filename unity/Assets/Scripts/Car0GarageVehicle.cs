using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Agribot.VehicleConfiguration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Car0 contributes only its calibrated authoring and repair capability; all
// navigation, equipment, algorithms and handoff UI belong to MainPanel.
public sealed class Car0GarageVehicle : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Transform configurationRoot;
    [SerializeField] private Canvas repairCanvas;
    [SerializeField] private FixPanel repairPanel;
    [SerializeField] private WheelChangePanel geometryPanel;
    [SerializeField] private OrbitCameraController orbitCamera;
    [SerializeField] private CarChange carChange;
    private MainPanel owner;
    private float previousClick = float.NegativeInfinity;
    private Vector2 previousPosition;
    private readonly List<RaycastResult> uiHits = new();
    private OrbitCameraController.PreviewView repairView;
    private bool hasRepairView;
    private CanvasGroup repairGroup;
    private Coroutine closingRepair;
    public CarChange Geometry => carChange;
    public OrbitCameraController Orbit => orbitCamera;
    public FixPanel RepairPanel => repairPanel;
    public GameObject VisualRoot => visualRoot;

    public static GameObject Create(MainPanel owner)
    {
        var source = Resources.Load<GameObject>("LocalGarageVehicles/Car0");
        if (source == null) throw new InvalidOperationException("Car0 共用车库资源尚未生成，请重新构建。 ");
        var car = Instantiate(source);
        car.name = "Car0";
        car.GetComponent<Car0GarageVehicle>().owner = owner;
        return car;
    }

    public VehicleConfigurationAuthoring BuildConfiguration() =>
        AckermannRuntimeConfiguration.BuildOrRefresh(visualRoot, configurationRoot);

    private void Awake()
    {
        repairGroup = repairCanvas.GetComponent<CanvasGroup>();
        if (repairGroup == null) repairGroup = repairCanvas.gameObject.AddComponent<CanvasGroup>();
        var escape = repairCanvas.transform.Find("Esc_btn")?.GetComponent<Button>();
        if (escape != null)
        {
            GarageVisualStyle.SetButtonLabel(escape, "返回");
            GarageVisualStyle.ButtonStyle(escape);
            var rect = escape.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24, 24);
            rect.sizeDelta = new Vector2(160, 56);
            escape.onClick = new Button.ButtonClickedEvent();
            escape.onClick.AddListener(CloseRepairAnimated);
        }
        var close = repairPanel.transform.Find("Header/CloseButton")?.GetComponent<Button>();
        if (close != null)
        {
            close.onClick = new Button.ButtonClickedEvent();
            close.onClick.AddListener(CloseRepairAnimated);
        }
        CloseRepair();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        HandleBodyClick(eventData.position, Time.unscaledTime);
    }

    public void HandleBodyClick(Vector2 position, float now)
    {
        if (EventSystem.current != null)
        {
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            if (uiHits.Any(hit => hit.module is GraphicRaycaster))
            { previousClick = float.NegativeInfinity; return; }
        }
        var camera = Camera.main;
        if (camera == null) return;
        var hits = Physics.RaycastAll(camera.ScreenPointToRay(position)).OrderBy(hit => hit.distance);
        var hit = hits.FirstOrDefault(item => item.collider != null &&
            item.collider.transform.IsChildOf(configurationRoot));
        // RaycastHit.transform may be the parent rigidbody, not the hit surface.
        var surface = hit.collider != null ? hit.collider.transform : null;
        var bodyRoot = Find("CarBody");
        bool body = surface != null && (surface.name == "BodyCollider" ||
            surface == bodyRoot || bodyRoot != null && surface.IsChildOf(bodyRoot));
        if (!body) { previousClick = float.NegativeInfinity; return; }
        RegisterBodyClick(position, now);
    }

    public void RegisterBodyClick(Vector2 position, float now)
    {
        if (now - previousClick <= .35f && Vector2.Distance(position, previousPosition) <= 18)
        {
            previousClick = float.NegativeInfinity;
            OpenRepair();
        }
        else { previousClick = now; previousPosition = position; }
    }

    public void OpenRepair()
    {
        if (closingRepair != null) return;
        AssistantConversation.RecordUi("打开 Car0 修理面板", state => state.page = "repair");
        owner?.CloseEquipmentForRepair();
        if (!hasRepairView)
        {
            repairView = orbitCamera.CapturePreviewView();
            hasRepairView = true;
        }
        repairCanvas.transform.localScale = Vector3.one;
        repairGroup.alpha = 1;
        repairGroup.interactable = repairGroup.blocksRaycasts = true;
        geometryPanel.ShowImmediately();
        geometryPanel.HideEscButton();
        orbitCamera.HoldCurrentView();
        repairPanel.Show();
    }

    public void OpenEquipment() => owner?.OpenRecommendedEquipment();
    public void OpenExplodedView() => owner?.OpenVehicleExplodedView();

    public void CloseRepair()
    {
        if (closingRepair != null) StopCoroutine(closingRepair);
        closingRepair = null;
        ClearRepairUi();
        if (orbitCamera != null)
        {
            if (hasRepairView) orbitCamera.RestorePreviewView(repairView, 0);
            else orbitCamera.RestoreCameraInput();
        }
        hasRepairView = false;
    }

    public void CloseRepairAnimated()
    {
        if (closingRepair != null || !hasRepairView) return;
        AssistantConversation.RecordUi("关闭修理面板", state => state.page = "vehicle");
        closingRepair = StartCoroutine(FadeRepairOut());
    }

    private IEnumerator FadeRepairOut()
    {
        repairGroup.interactable = false;
        orbitCamera.RestorePreviewView(repairView);
        float elapsed = 0;
        while (elapsed < .25f)
        {
            elapsed += Time.unscaledDeltaTime;
            repairGroup.alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .25f));
            yield return null;
        }
        ClearRepairUi();
        hasRepairView = false;
        closingRepair = null;
    }

    private void ClearRepairUi()
    {
        if (repairPanel != null) repairPanel.HideImmediately();
        if (geometryPanel != null)
        {
            foreach (var panel in geometryPanel.GetComponentsInChildren<WheelChangePanel>(true)) panel.HideImmediately();
            geometryPanel.gameObject.SetActive(true);
        }
        if (carChange != null) carChange.FinishAdjustment();
        if (repairGroup != null)
        {
            repairGroup.alpha = 1;
            repairGroup.interactable = repairGroup.blocksRaycasts = true;
        }
        previousClick = float.NegativeInfinity;
    }

    private Transform Find(string name) => configurationRoot.GetComponentsInChildren<Transform>(true)
        .FirstOrDefault(item => item.name == name);
    private void OnDisable() => CloseRepair();
}
