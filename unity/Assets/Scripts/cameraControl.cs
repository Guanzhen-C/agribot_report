using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CinemachineOrbitalFollow))]
public class cameraControl : MonoBehaviour
{
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private float rotationSensitivity = 0.2f;
    [SerializeField] private float minVerticalAngle = 1f;
    [SerializeField] private float maxVerticalAngle = 70f;
    [SerializeField] private float minRadius = 15f;
    [SerializeField] private float maxRadius = 71f;
    [SerializeField] private float zoomSensitivity = 0.01f;
    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField, Min(0.05f)] private float fixedViewSmoothTime = 0.6f;

    private float targetHorizontal;
    private float targetVertical;
    private float targetRadius;
    private float horizontalVelocity;
    private float verticalVelocity;
    private float radiusVelocity;
    private bool inputLocked;

    private void Awake()
    {
        if (orbitalFollow == null)
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();

        if (cinemachineCamera == null)
            cinemachineCamera = GetComponent<CinemachineCamera>();

        var axisController = GetComponent<CinemachineInputAxisController>();
        if (axisController != null)
            axisController.enabled = false;

        targetHorizontal = orbitalFollow.HorizontalAxis.Value;
        targetVertical = Mathf.Clamp(orbitalFollow.VerticalAxis.Value, minVerticalAngle, maxVerticalAngle);
        targetRadius = Mathf.Clamp(orbitalFollow.Radius, minRadius, maxRadius);
    }

    private void Update()
    {
        if (orbitalFollow == null)
            return;

        if (!inputLocked && Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            targetHorizontal += delta.x * rotationSensitivity;
            targetVertical = Mathf.Clamp(
                targetVertical - delta.y * rotationSensitivity,
                minVerticalAngle,
                maxVerticalAngle);
        }

        float scroll = Mouse.current != null && !inputLocked
            ? Mouse.current.scroll.ReadValue().y
            : 0f;
        if (!inputLocked && Mathf.Abs(scroll) > 0.01f)
            targetRadius = Mathf.Clamp(
                targetRadius * Mathf.Exp(-scroll * zoomSensitivity),
                minRadius,
                maxRadius);

        float horizontal = Mathf.SmoothDampAngle(
            orbitalFollow.HorizontalAxis.Value,
            targetHorizontal,
            ref horizontalVelocity,
            inputLocked ? fixedViewSmoothTime : smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
        float vertical = Mathf.SmoothDamp(
            orbitalFollow.VerticalAxis.Value,
            targetVertical,
            ref verticalVelocity,
            inputLocked ? fixedViewSmoothTime : smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
        float radius = Mathf.SmoothDamp(
            orbitalFollow.Radius,
            targetRadius,
            ref radiusVelocity,
            inputLocked ? fixedViewSmoothTime : smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);

        var horizontalAxis = orbitalFollow.HorizontalAxis;
        horizontalAxis.Value = horizontal;
        orbitalFollow.HorizontalAxis = horizontalAxis;

        var verticalAxis = orbitalFollow.VerticalAxis;
        verticalAxis.Range = new Vector2(minVerticalAngle, maxVerticalAngle);
        verticalAxis.Value = vertical;
        orbitalFollow.VerticalAxis = verticalAxis;
        orbitalFollow.Radius = radius;
    }

    /// <summary>
    /// Locks this vehicle's camera while the equipment bag is open.
    /// Only the camera component on the current vehicle is affected.
    /// </summary>
    public void HoldCurrentView()
    {
        inputLocked = true;
        targetHorizontal = orbitalFollow.HorizontalAxis.Value;
        targetVertical = orbitalFollow.VerticalAxis.Value;
        targetRadius = orbitalFollow.Radius;
        horizontalVelocity = verticalVelocity = radiusVelocity = 0f;
    }

    public void FixCamera(float radius = 35f, float horizontal = -55f, float vertical = 20f)
    {
        inputLocked = true;
        targetHorizontal = NormalizeAngle(horizontal);
        targetVertical = Mathf.Clamp(vertical, minVerticalAngle, maxVerticalAngle);
        targetRadius = Mathf.Clamp(radius, minRadius, maxRadius);
        horizontalVelocity = 0f;
        verticalVelocity = 0f;
        radiusVelocity = 0f;
    }

    /// <summary>
    /// Restores the normal mouse/scroll input for this vehicle and updates both
    /// Cinemachine target references to the supplied target.
    /// </summary>
    public void RestoreCameraInput(Transform trackingTarget)
    {
        ChangeCameraTarget(trackingTarget);
        RestoreCameraInput();
    }

    public void RestoreCameraInput()
    {
        inputLocked = false;
        targetHorizontal = orbitalFollow.HorizontalAxis.Value;
        targetVertical = orbitalFollow.VerticalAxis.Value;
        targetRadius = orbitalFollow.Radius;
        horizontalVelocity = 0f;
        verticalVelocity = 0f;
        radiusVelocity = 0f;
    }

    public void ChangeCameraTarget(Transform newTarget)
    {
        if (cinemachineCamera == null)
            cinemachineCamera = GetComponent<CinemachineCamera>();

        if (cinemachineCamera == null)
            return;

        cinemachineCamera.Target.TrackingTarget = newTarget;
        cinemachineCamera.Target.LookAtTarget = newTarget;
        cinemachineCamera.Target.CustomLookAtTarget = newTarget != null;
    }

    public bool IsInputLocked => inputLocked;

    public void ConfigurePreviewRange(float minimum, float maximum, float initial)
    {
        minRadius = Mathf.Max(.01f, minimum);
        maxRadius = Mathf.Max(minRadius, maximum);
        if (orbitalFollow == null) orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        targetRadius = Mathf.Clamp(initial, minRadius, maxRadius);
        radiusVelocity = 0f;
        orbitalFollow.Radius = targetRadius;
        var radial = orbitalFollow.RadialAxis;
        radial.Value = radial.Center = 1f;
        radial.Range = new Vector2(.5f, 2f);
        orbitalFollow.RadialAxis = radial;
        var vertical = orbitalFollow.VerticalAxis;
        vertical.Value = 25f;
        orbitalFollow.VerticalAxis = vertical;
        targetVertical = 25f;
        transform.localScale = Vector3.one;
    }

    private void ApplyImmediate(float horizontal, float vertical, float radius)
    {
        var horizontalAxis = orbitalFollow.HorizontalAxis;
        horizontalAxis.Value = horizontal;
        orbitalFollow.HorizontalAxis = horizontalAxis;

        var verticalAxis = orbitalFollow.VerticalAxis;
        verticalAxis.Range = new Vector2(minVerticalAngle, maxVerticalAngle);
        verticalAxis.Value = vertical;
        orbitalFollow.VerticalAxis = verticalAxis;
        orbitalFollow.Radius = radius;
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    private void OnValidate()
    {
        maxRadius = Mathf.Max(minRadius, maxRadius, 35f);
        smoothTime = Mathf.Max(0.001f, smoothTime);
        fixedViewSmoothTime = Mathf.Max(0.05f, fixedViewSmoothTime);
    }
}
