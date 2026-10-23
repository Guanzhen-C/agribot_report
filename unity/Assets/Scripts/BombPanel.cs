using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BombPanel : WheelChangePanel
{
    private const float MaxExplosionDistance = 0.96f;

    private sealed class ExplosionPart
    {
        public Transform Transform;
        public Vector3 InitialLocalPosition;
        public Vector3 MaximumLocalOffset;
    }

    private static readonly string[] WheelColliderNames =
    {
        "WheelCollider_FL",
        "WheelCollider_FR",
        "WheelCollider_RL",
        "WheelCollider_RR"
    };

    private Rigidbody carRigidbody;
    private bool rigidbodyStateCaptured;
    private bool originalIsKinematic;
    private Vector3 originalLinearVelocity;
    private Vector3 originalAngularVelocity;
    private Slider bombSlider;
    private readonly List<ExplosionPart> explosionParts = new List<ExplosionPart>();
    private Canvas carUiCanvas;
    private bool carUiCanvasStateCaptured;
    private bool originalCarUiCanvasActive;

    protected override void Awake()
    {
        base.Awake();
        AddEscClickListener(Hide);
        InitializeExplosionControl();
        //ShowEscButton();
    }

    private void OnEnable()
    {
        RestorePanelVisibility();
        UnAbleCollider();
        DisableCarUiCanvas();

        if (bombSlider == null)
            InitializeExplosionControl();

        ResetExplosion();
    }

    private void RestorePanelVisibility()
    {
        CanvasGroup panelCanvasGroup = GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
            return;

        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;
    }

    private void UnAbleCollider()
    {
        SetCollidersEnabled(false);
        DisableCarRigidbody();
        SetVehicleRootLocalY(-22.45f);
    }

    private void EnAbleCollider()
    {
        SetCollidersEnabled(true);
        RestoreCarRigidbody();
        SetVehicleRootLocalY(-23.33f);
    }

    private void DisableCarRigidbody()
    {
        GameObject car = GameObject.Find("Car0");
        carRigidbody = car != null ? car.GetComponentInParent<Rigidbody>() : null;

        if (carRigidbody == null)
        {
            Debug.LogWarning("BombPanel: the car Rigidbody was not found.", this);
            return;
        }

        if (!rigidbodyStateCaptured)
        {
            originalIsKinematic = carRigidbody.isKinematic;
            originalLinearVelocity = carRigidbody.linearVelocity;
            originalAngularVelocity = carRigidbody.angularVelocity;
            rigidbodyStateCaptured = true;
        }

        carRigidbody.linearVelocity = Vector3.zero;
        carRigidbody.angularVelocity = Vector3.zero;
        carRigidbody.isKinematic = true;
    }

    private void RestoreCarRigidbody()
    {
        if (!rigidbodyStateCaptured || carRigidbody == null)
            return;

        carRigidbody.isKinematic = originalIsKinematic;

        if (!originalIsKinematic)
        {
            carRigidbody.linearVelocity = originalLinearVelocity;
            carRigidbody.angularVelocity = originalAngularVelocity;
            carRigidbody.WakeUp();
        }

        rigidbodyStateCaptured = false;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        GameObject car = GameObject.Find("Car0");
        if (car == null)
        {
            Debug.LogWarning("BombPanel: Car0 was not found.", this);
            return;
        }

        foreach (string colliderName in WheelColliderNames)
        {
            Transform target = FindChildByName(car.transform, colliderName);
            WheelCollider wheelCollider = target != null
                ? target.GetComponent<WheelCollider>()
                : null;

            if (wheelCollider != null)
                wheelCollider.enabled = enabled;
            else
                Debug.LogWarning($"BombPanel: {colliderName}/WheelCollider was not found.", this);
        }

        Transform carBody = FindChildByName(car.transform, "CarBody");
        BoxCollider[] bodyColliders = carBody != null
            ? carBody.GetComponentsInChildren<BoxCollider>(true)
            : null;

        if (bodyColliders == null || bodyColliders.Length == 0)
        {
            Debug.LogWarning("BombPanel: no BoxCollider was found under CarBody.", this);
            return;
        }

        foreach (BoxCollider bodyCollider in bodyColliders)
            bodyCollider.enabled = enabled;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private void SetVehicleRootLocalY(float y)
    {
        GameObject vehicleRoot = GameObject.Find("VehicleRoot");
        if (vehicleRoot == null)
        {
            Debug.LogWarning("BombPanel: VehicleRoot was not found.", this);
            return;
        }

        Vector3 localPosition = vehicleRoot.transform.localPosition;
        localPosition.y = y;
        vehicleRoot.transform.localPosition = localPosition;
    }

    private void InitializeExplosionControl()
    {
        Slider[] sliders = GetComponentsInChildren<Slider>(true);
        foreach (Slider slider in sliders)
        {
            if (slider.name == "BombSlider")
            {
                bombSlider = slider;
                break;
            }
        }

        if (bombSlider == null)
        {
            Debug.LogWarning("BombPanel: BombSlider was not found under BombPanel.", this);
            return;
        }

        bombSlider.minValue = 0f;
        bombSlider.maxValue = 1f;
        bombSlider.wholeNumbers = false;
        bombSlider.SetValueWithoutNotify(0f);
        bombSlider.onValueChanged.RemoveListener(OnBombSliderValueChanged);
        bombSlider.onValueChanged.AddListener(OnBombSliderValueChanged);

        BuildExplosionParts();
        ApplyExplosion(0f);
    }

    private void OnBombSliderValueChanged(float value)
    {
        if (orbitCameraController != null)
            orbitCameraController.UnlockFixedView();

        ApplyExplosion(value);
    }

    private void BuildExplosionParts()
    {
        explosionParts.Clear();

        GameObject car = GameObject.Find("Car0");
        if (car == null)
        {
            Debug.LogWarning("BombPanel: Car0 was not found for the exploded view.", this);
            return;
        }

        Renderer[] carRenderers = car.GetComponentsInChildren<Renderer>(true);
        if (carRenderers.Length == 0)
        {
            Debug.LogWarning("BombPanel: no vehicle renderers were found.", this);
            return;
        }

        Bounds carBounds = carRenderers[0].bounds;
        for (int i = 1; i < carRenderers.Length; i++)
            carBounds.Encapsulate(carRenderers[i].bounds);

        foreach (var part in GetVisualPartRoots(car.transform))
            AddExplosionPart(part, carBounds.center);
    }

    public static List<Transform> GetVisualPartRoots(Transform vehicle)
    {
        var parts = new List<Transform>();
        var added = new HashSet<Transform>();
        foreach (string name in new[] { "CarBody", "QianQiao", "HouQiao" })
        {
            var group = FindChildByName(vehicle, name);
            if (group == null) continue;
            foreach (Transform child in group)
                if (!IsPhysicsOrControlObject(child) && child.GetComponentInChildren<Renderer>(true) != null && added.Add(child))
                    parts.Add(child);
        }
        return parts;
    }

    private void AddExplosionPart(Transform child, Vector3 vehicleCenter)
    {
        Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds partBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            partBounds.Encapsulate(renderers[i].bounds);

        Vector3 direction = partBounds.center - vehicleCenter;
        if (direction.sqrMagnitude < 0.0001f)
        {
            float angle = explosionParts.Count * 137.5f * Mathf.Deg2Rad;
            direction = new Vector3(Mathf.Cos(angle), 0.35f, Mathf.Sin(angle));
        }

        direction.Normalize();
        explosionParts.Add(new ExplosionPart
        {
            Transform = child,
            InitialLocalPosition = child.localPosition,
            MaximumLocalOffset = child.parent.InverseTransformVector(
                direction * MaxExplosionDistance)
        });
    }

    private void ApplyExplosion(float value)
    {
        float amount = Mathf.Clamp01(value);
        foreach (ExplosionPart part in explosionParts)
        {
            if (part.Transform == null)
                continue;

            part.Transform.localPosition =
                part.InitialLocalPosition + part.MaximumLocalOffset * amount;
        }
    }

    private void ResetExplosion()
    {
        if (bombSlider != null)
            bombSlider.SetValueWithoutNotify(0f);

        ApplyExplosion(0f);
    }

    private void DisableCarUiCanvas()
    {
        carUiCanvas = carChange != null ? carChange.UICanvas : null;
        if (carUiCanvas == null)
            return;

        if (!carUiCanvasStateCaptured)
        {
            originalCarUiCanvasActive = carUiCanvas.gameObject.activeSelf;
            carUiCanvasStateCaptured = true;
        }

        carUiCanvas.gameObject.SetActive(false);
    }

    private void RestoreCarUiCanvas()
    {
        if (!carUiCanvasStateCaptured || carUiCanvas == null)
            return;

        carUiCanvas.gameObject.SetActive(originalCarUiCanvasActive);
        carUiCanvasStateCaptured = false;
    }

    private void OnDisable()
    {
        RestoreCarUiCanvas();
    }

    private static bool IsPhysicsOrControlObject(Transform target)
    {
        return target.GetComponent<WheelCollider>() != null ||
               target.GetComponent<Rigidbody>() != null ||
               target.GetComponent<Canvas>() != null;
    }

    private void OnDestroy()
    {
        if (bombSlider != null)
            bombSlider.onValueChanged.RemoveListener(OnBombSliderValueChanged);

        RestoreCarUiCanvas();
        RemoveEscClickListener(Hide);
    }

    public new void Hide()
    {
        ResetExplosion();
        RestoreCarUiCanvas();
        EnAbleCollider();
        base.Hide();
    }
}
