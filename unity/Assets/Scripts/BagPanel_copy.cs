using RG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class BagPanel_copy : WheelChangePanel
{
    protected override PanelShowEffect ShowEffect => PanelShowEffect.PunchUI;
    private Canvas frontRightWheelCanvas;
    private LastPanel lastPanel;
    private Button closeButton;


    protected override void Awake()
    {
        base.Awake();
        if (orbitCameraController != null)
        {
            RemoveEscClickListener(orbitCameraController.ChangeCameraTarget);
            AddEscClickListener(orbitCameraController.ChangeCameraTarget);
            orbitCameraController.ChangeCameraTarget();
        }

        ResolveCloseTargets();
        AddEscClickListener(Hide);
        closeButton = transform.Find("Header/CloseButton")?.GetComponent<Button>();
        if (closeButton != null) closeButton.onClick.AddListener(CloseFromHeader);
    }

    private void OnDestroy()
    {
        RemoveEscClickListener(Hide);
        if (closeButton != null) closeButton.onClick.RemoveListener(CloseFromHeader);
    }

    private void CloseFromHeader()
    {
        orbitCameraController?.ChangeCameraTarget();
        orbitCameraController?.UnlockFixedView();
        Hide();
    }

    public void OpenBag()
    {
        GarageVisualStyle.EquipmentPanel(transform);
        Show();
        HideEscButton();
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
        if (!gameObject.activeInHierarchy)
            return;

        HideEscButton();
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
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
                gameObject.SetActive(false);
                CompleteCloseActions();
            });
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

}
