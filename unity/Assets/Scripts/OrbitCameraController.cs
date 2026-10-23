using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CinemachineOrbitalFollow))]
public class OrbitCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
    [SerializeField] private CinemachineInputAxisController inputAxisController;
    [SerializeField] private UIHoverFade UIHFade;
    [SerializeField] private Transform CameraTarget;


    [Header("Rotation")]
    [SerializeField] private float horizontalSensitivity = 0.2f;
    // 设置鼠标垂直移动对垂直旋转角度的影响强度。
    [SerializeField] private float verticalSensitivity = 0.15f;
    [SerializeField] private float minVerticalAngle = 5f;
    [SerializeField] private float maxVerticalAngle = 70f;
    [SerializeField] private bool invertVertical;   // 指定是否反转鼠标垂直方向。


    // 在 Inspector 中显示旋转缓冲设置标题。
    [Header("Rotation Smoothing")]
    [SerializeField] private float rotationSmoothTime = 0.12f;
    // 限制缓冲期间摄像机每秒能够旋转的最大角度。
    [SerializeField] private float maxRotationSpeed = Mathf.Infinity;

    // 在 Inspector 中显示缩放设置标题。
    [Header("Zoom")]
    [SerializeField] private float minRadius = 5f;
    [SerializeField] private float maxRadius = 30f;
    [SerializeField] private float zoomSensitivity = 0.01f;
    [SerializeField] private float zoomSmoothTime = 0.12f;
    // 指定是否反转鼠标滚轮的缩放方向。
    [SerializeField] private bool invertZoom;

    // 在 Inspector 中显示 UI 设置标题。
    [Header("UI")]
    // 指定鼠标位于 UI 上方时是否阻止摄像机接收新输入。
    [SerializeField] private bool blockInputOverUI = true;

    // 在 Inspector 中显示按钮固定视角的参数标题。
    [Header("Fixed View")]
    [SerializeField] private float fixedHorizontalAngle = -45f;
    [SerializeField] private float fixedVerticalAngle = 28f;
    [SerializeField] private float fixedRadialAxis = 0.15f;
    // 固定视角过渡的独立缓动时间，不影响日常拖拽和缩放手感。
    [SerializeField, Min(0.05f)] private float fixedViewSmoothTime = 0.6f;

    // 保存经过平滑处理后实际应用的水平角度。
    private float horizontalAngle;
    private float targetHorizontalAngle;
    // 保存 SmoothDampAngle 内部使用的水平角速度。
    private float horizontalVelocity;
    // 保存经过平滑处理后实际应用的垂直角度。
    private float verticalAngle;
    private float targetVerticalAngle;  // 希望相机垂直角度
    // 保存 SmoothDamp 内部使用的垂直角速度。
    private float verticalVelocity;
    // 保存经过平滑处理后实际应用的轨道半径。
    private float currentRadius;
    // 保存鼠标滚轮输入希望摄像机到达的目标轨道半径。
    private float targetRadius;
    // 保存缩放 SmoothDamp 内部使用的半径变化速度。
    private float zoomVelocity;
    // 记录按钮是否已经把摄像机切换到固定视角。
    private bool isViewLocked;
    private float previousPinchDistance;
    private bool pinchActive;

    private float targetRadialAxis;
    private float currentRadialAxis;
    private float radialAxisVelocity;
    // 修理面板。该对象初始为未激活状态，必须通过 Inspector 绑定。
    [SerializeField] private FixPanel fixPanel;
    public struct PreviewView
    {
        public Transform Target;
        public float Horizontal, Vertical, Radius, Radial;
    }
    private Coroutine previewTransition;
    private readonly List<RaycastResult> pointerHits = new();

    public bool IsPointerOverGraphic(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        pointerHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, pointerHits);
        return pointerHits.Exists(hit => hit.module is GraphicRaycaster);
    }

    public PreviewView CapturePreviewView() => new()
    {
        Target = GetComponent<CinemachineCamera>().Target.TrackingTarget,
        Horizontal = orbitalFollow.HorizontalAxis.Value,
        Vertical = orbitalFollow.VerticalAxis.Value, Radius = orbitalFollow.Radius,
        Radial = orbitalFollow.RadialAxis.Value
    };

    public void HoldCurrentView()
    {
        StopPreviewTransition();
        InitializeCameraValues();
        isViewLocked = true;
        if (inputAxisController != null) inputAxisController.enabled = false;
    }

    public void RestorePreviewView(PreviewView view, float duration = .25f)
    {
        StopPreviewTransition();
        if (!isActiveAndEnabled || duration <= 0)
        {
            ChangeCameraTarget(view.Target);
            SetPreviewView(view);
            RestoreCameraInput();
            return;
        }
        previewTransition = StartCoroutine(BlendPreviewView(view, duration));
    }

    private System.Collections.IEnumerator BlendPreviewView(PreviewView target, float duration)
    {
        var start = CapturePreviewView();
        isViewLocked = true;
        if (inputAxisController != null) inputAxisController.enabled = false;
        ChangeCameraTarget(target.Target);
        float elapsed = 0;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration));
            SetPreviewView(new PreviewView
            {
                Horizontal = Mathf.LerpAngle(start.Horizontal, target.Horizontal, t),
                Vertical = Mathf.Lerp(start.Vertical, target.Vertical, t),
                Radius = Mathf.Lerp(start.Radius, target.Radius, t),
                Radial = Mathf.Lerp(start.Radial, target.Radial, t)
            });
            yield return null;
        }
        previewTransition = null;
        RestoreCameraInput();
    }

    private void SetPreviewView(PreviewView view)
    {
        var horizontal = orbitalFollow.HorizontalAxis;
        horizontal.Value = view.Horizontal;
        orbitalFollow.HorizontalAxis = horizontal;
        var vertical = orbitalFollow.VerticalAxis;
        vertical.Value = view.Vertical;
        orbitalFollow.VerticalAxis = vertical;
        orbitalFollow.Radius = view.Radius;
        var radial = orbitalFollow.RadialAxis;
        radial.Value = view.Radial;
        orbitalFollow.RadialAxis = radial;
        InitializeCameraValues();
    }

    private void StopPreviewTransition()
    {
        if (previewTransition == null) return;
        StopCoroutine(previewTransition);
        previewTransition = null;
    }


    // 当脚本首次添加或在 Inspector 中执行 Reset 时自动查找依赖组件。
    private void Reset()
    {
        orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
    }

    private void Awake()
    {
        if (orbitalFollow == null)
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();

        if (inputAxisController == null)
            inputAxisController = GetComponent<CinemachineInputAxisController>();

        if (CameraTarget == null)
            CameraTarget = GameObject.Find("CameraTarget").transform;

        // 当必须的轨道跟随组件仍然不存在时进入错误处理。
        if (orbitalFollow == null)
        {
            // 在 Console 中输出缺少组件的错误，并关联当前脚本对象。
            Debug.LogError("OrbitCameraController: CinemachineOrbitalFollow was not found.", this);
            // 禁用当前组件，避免后续代码访问空引用。
            enabled = false;
            return;
        }

        // 从 Cinemachine 组件读取角度和半径的初始值。
        InitializeCameraValues();
        if (UIHFade == null)
            UIHFade = GameObject.Find("setting_btn")?.GetComponent<UIHoverFade>();
    }

    private void OnEnable()
    {
        if (orbitalFollow != null)
            // 重新读取角度、目标角度、半径和缓冲速度。
            InitializeCameraValues();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Touchscreen touchscreen = Touchscreen.current;
        // 当没有鼠标或缺少轨道跟随组件时不执行摄像机控制。
        if (orbitalFollow == null)
            return;
        if (previewTransition != null) return;

        // Physics ray hits on the car must not count as UI, but slider drags must.
        Vector2 pointer = touchscreen != null && touchscreen.primaryTouch.press.isPressed
            ? touchscreen.primaryTouch.position.ReadValue()
            : mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        bool pointerOverUI = blockInputOverUI && IsPointerOverGraphic(pointer);

        // 仅在鼠标不位于 UI 上方时读取新的旋转和缩放输入。
        // 固定视角后不再接收鼠标旋转和滚轮缩放输入。
        if (!isViewLocked && !pointerOverUI)
        {
            // 读取鼠标左键拖动，并更新旋转目标角度。
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
                HandleTouchInput(touchscreen);
            else if (mouse != null)
                HandleRotationInput(mouse);
            // 读取鼠标滚轮，并更新缩放目标半径。
            if (mouse != null &&
                (touchscreen == null || !touchscreen.primaryTouch.press.isPressed))
                HandleZoomInput(mouse);
        }

        if (touchscreen == null || !touchscreen.primaryTouch.press.isPressed)
            pinchActive = false;

        // 即使停止输入，也让实际角度继续平滑追赶目标角度。
        UpdateRotation();
        // 即使停止滚轮输入，也让实际半径继续平滑;追赶目标半径。
        UpdateZoom();
        UpdateRadialAxis();
        // 将当前平滑角度和半径写入 Cinemachine 轨道跟随组件。
        ApplyOrbitValues();
    }

    // 读取鼠标拖动，并只修改目标角度而不立即改变实际镜头角度。
    private void HandleTouchInput(Touchscreen touchscreen)
    {
        var firstTouch = touchscreen.primaryTouch;
        var secondTouch = touchscreen.touches.Count > 1
            ? touchscreen.touches[1]
            : null;

        if (secondTouch != null && secondTouch.press.isPressed)
        {
            float pinchDistance = Vector2.Distance(
                firstTouch.position.ReadValue(),
                secondTouch.position.ReadValue());

            if (pinchActive)
            {
                float pinchDelta = pinchDistance - previousPinchDistance;
                float zoomDirection = invertZoom ? -1f : 1f;
                targetRadius *= Mathf.Exp(
                    -pinchDelta * zoomSensitivity * zoomDirection);
                targetRadius = Mathf.Clamp(targetRadius, minRadius, maxRadius);
            }

            previousPinchDistance = pinchDistance;
            pinchActive = true;
            return;
        }

        pinchActive = false;
        Vector2 touchDelta = firstTouch.delta.ReadValue();
        targetHorizontalAngle += touchDelta.x * horizontalSensitivity;
        float verticalDirection = invertVertical ? 1f : -1f;
        targetVerticalAngle += touchDelta.y * verticalSensitivity * verticalDirection;
        targetHorizontalAngle = NormalizeAngle(targetHorizontalAngle);
        targetVerticalAngle = Mathf.Clamp(
            targetVerticalAngle, minVerticalAngle, maxVerticalAngle);
    }

    private void HandleRotationInput(Mouse mouse)
    {
        // 只有按住鼠标左键时才接收旋转输入。
        if (!mouse.leftButton.isPressed)
            // 未按住左键时保持当前目标角度不变。
            return;

        // 读取鼠标在当前帧产生的二维位移。
        Vector2 mouseDelta = mouse.delta.ReadValue();
        // 根据水平灵敏度把鼠标水平位移累加到目标水平角度。
        targetHorizontalAngle += mouseDelta.x * horizontalSensitivity;

        // 根据反转选项确定垂直输入的方向符号。
        float verticalDirection = invertVertical ? 1f : -1f;
        // 根据垂直灵敏度和方向把鼠标垂直位移累加到目标垂直角度。
        targetVerticalAngle += mouseDelta.y * verticalSensitivity * verticalDirection;

        // 将目标水平角度整理到负 180 度至正 180 度之间。
        targetHorizontalAngle = NormalizeAngle(targetHorizontalAngle);
        // 将目标垂直角度限制在允许的上下观察范围内。
        targetVerticalAngle = Mathf.Clamp(targetVerticalAngle, minVerticalAngle, maxVerticalAngle);
    }

    /* 让当前镜头角度平滑追赶鼠标输入产生的目标角度 */
    private void UpdateRotation()
    {
        // 使用支持角度跨越正负 180 度的平滑函数更新水平角度。
        horizontalAngle = Mathf.SmoothDampAngle(
            // 传入当前实际水平角度。
            horizontalAngle,
            // 传入需要追赶的目标水平角度。
            targetHorizontalAngle,
            // 传入并更新水平平滑速度状态。
            ref horizontalVelocity,
            // 传入旋转缓冲时间。
            isViewLocked ? fixedViewSmoothTime : rotationSmoothTime,
            // 传入允许的最大旋转速度。
            maxRotationSpeed,
            // 使用不受 Time.timeScale 影响的帧间隔。
            Time.unscaledDeltaTime);

        // 使用平滑函数更新受范围限制的垂直角度。
        verticalAngle = Mathf.SmoothDamp(
            // 传入当前实际垂直角度。
            verticalAngle,
            // 传入需要追赶的目标垂直角度。
            targetVerticalAngle,
            // 传入并更新垂直平滑速度状态。
            ref verticalVelocity,
            // 传入旋转缓冲时间。
            isViewLocked ? fixedViewSmoothTime : rotationSmoothTime,
            // 传入允许的最大旋转速度。
            maxRotationSpeed,
            // 使用不受 Time.timeScale 影响的帧间隔。
            Time.unscaledDeltaTime);

        // 再次限制实际垂直角度，避免平滑过程越过边界。
        verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
    }

    /* 读取鼠标滚轮，并修改目标轨道半径。*/
    private void HandleZoomInput(Mouse mouse)
    {
        // 读取鼠标滚轮在当前帧的垂直增量。
        float scrollDelta = mouse.scroll.ReadValue().y;
        // 忽略接近零的滚轮噪声。
        if (Mathf.Abs(scrollDelta) < 0.01f)
            // 没有有效滚轮输入时保持目标半径不变。
            return;

        // 当启用反向缩放时进入方向反转处理。
        if (invertZoom)
            // 翻转滚轮增量的正负方向。
            scrollDelta = -scrollDelta;

        // 使用指数倍率更新目标半径，使缩放在不同距离下保持自然。
        targetRadius *= Mathf.Exp(-scrollDelta * zoomSensitivity);
        // 将目标半径限制在允许的最近和最远距离之间。
        targetRadius = Mathf.Clamp(targetRadius, minRadius, maxRadius);
    }

    // 让当前轨道半径平滑追赶滚轮输入产生的目标半径。
    private void UpdateZoom()
    {
        // 使用平滑阻尼计算当前帧的实际轨道半径。
        currentRadius = Mathf.SmoothDamp(
            // 传入当前实际轨道半径。
            currentRadius,
            // 传入需要追赶的目标轨道半径。
            targetRadius,
            // 传入并更新缩放平滑速度状态。
            ref zoomVelocity,
            // 传入缩放缓冲时间。
            isViewLocked ? fixedViewSmoothTime : zoomSmoothTime,
            // 不额外限制缩放过程的最大速度。
            Mathf.Infinity,
            // 使用不受 Time.timeScale 影响的帧间隔。
            Time.unscaledDeltaTime);
    }

    public void ChangeCameraTarget(Transform newTrans)
    {
        CinemachineCamera cinemachineCamera = GetComponent<CinemachineCamera>();
        if (cinemachineCamera == null)
        {
            Debug.LogError("OrbitCameraController: CinemachineCamera was not found.", this);
            return;
        }
        cinemachineCamera.Target.TrackingTarget = newTrans;
        cinemachineCamera.Target.LookAtTarget = newTrans;
        cinemachineCamera.Target.CustomLookAtTarget = newTrans != null;
    }

    public void ChangeCameraTarget()
    {
        CinemachineCamera cinemachineCamera = GetComponent<CinemachineCamera>();
        if (cinemachineCamera == null)
        {
            Debug.LogError("OrbitCameraController: CinemachineCamera was not found.", this);
            return;
        }
        cinemachineCamera.Target.TrackingTarget = CameraTarget;
        cinemachineCamera.Target.LookAtTarget = CameraTarget;
        cinemachineCamera.Target.CustomLookAtTarget = CameraTarget != null;
    }

    /// <summary>
    /// Locks only this vehicle's FreeLook camera while the equipment bag is open.
    /// </summary>
    public void FixCamera(float radius = 35f, float horizontal = -55f, float vertical = 20f)
    {
        StopPreviewTransition();
        isViewLocked = true;
        if (inputAxisController != null)
            inputAxisController.enabled = false;

        targetHorizontalAngle = NormalizeAngle(horizontal);
        horizontalVelocity = 0f;
        targetVerticalAngle = Mathf.Clamp(vertical, minVerticalAngle, maxVerticalAngle);
        verticalVelocity = 0f;
        targetRadius = Mathf.Clamp(radius, minRadius, maxRadius);
        zoomVelocity = 0f;
    }

    /// <summary>
    /// Restores this vehicle's normal camera input and target values.
    /// </summary>
    public void RestoreCameraInput(Transform trackingTarget)
    {
        ChangeCameraTarget(trackingTarget);
        RestoreCameraInput();
    }

    public void RestoreCameraInput()
    {
        StopPreviewTransition();
        isViewLocked = false;
        if (inputAxisController != null)
            inputAxisController.enabled = true;
        InitializeCameraValues();
    }


    /* 供 fix_border_img 的 Button OnClick 调用，并立即锁定到指定观察角度。*/
    public void LockToFixedView()
    {
        StopPreviewTransition();
        isViewLocked = true;
        if (inputAxisController != null)
            inputAxisController.enabled = false;

        targetHorizontalAngle = fixedHorizontalAngle;
        horizontalVelocity = 0f;     // 清除水平角速度，避免旧的拖动惯性继续影响镜头。

        // 保持平滑系统的目标值与固定垂直角一致。
        targetVerticalAngle = Mathf.Clamp(
            fixedVerticalAngle,
            minVerticalAngle,
            maxVerticalAngle);
        verticalVelocity = 0f;  // 清除垂直角速度，避免镜头锁定后继续缓动。

        // 径向轴独立于 Radius，按 Cinemachine 轴范围平滑收敛。
        targetRadius = Mathf.Clamp(
            fixedRadialAxis,
            minRadius,
            maxRadius);


        // 显示修理面板。
        if (fixPanel != null)
            fixPanel.Show();
        else
            Debug.LogError("OrbitCameraController: fixPanel 未在 Inspector 中绑定。", this);

        if (UIHFade != null && UIHFade.gameObject.activeInHierarchy)
            UIHFade.UnableBtns();

    }

    // 方法重载：方便别的脚本更换参数值调用
    public void LockToFixedView(float fixedH = -45f, float fixedVA = 28f, float fixedR = 0.15f)
    {
        StopPreviewTransition();
        isViewLocked = true;
        if (inputAxisController != null)
            inputAxisController.enabled = false;

        targetHorizontalAngle = fixedH;
        horizontalVelocity = 0f;     // 清除水平角速度，避免旧的拖动惯性继续影响镜头。

        // 保持平滑系统的目标值与固定垂直角一致。
        targetVerticalAngle = Mathf.Clamp(
            fixedVA,
            minVerticalAngle,
            maxVerticalAngle);
        verticalVelocity = 0f;  // 清除垂直角速度，避免镜头锁定后继续缓动。

        // 径向轴独立于 Radius，按 Cinemachine 轴范围平滑收敛。
        targetRadius = Mathf.Clamp(
            fixedR,
            minRadius,
            maxRadius);
        if (UIHFade != null && UIHFade.gameObject.activeInHierarchy)
            UIHFade.UnableBtns();

    }

    // 解除固定视角
    public void UnlockFixedView()
    {
        isViewLocked = false;

        if (inputAxisController != null)
            inputAxisController.enabled = true;

        InitializeCameraValues();

        // Restore the same hover controller disabled by LockToFixedView.
        // Keeping this state transition in one owner avoids relying on scene
        // button events that may reference a different vehicle instance.
        if (UIHFade != null)
            UIHFade.EnableBtns();
    }

    private void UpdateRadialAxis()
    {
        currentRadialAxis = Mathf.SmoothDamp(
            currentRadialAxis,
            targetRadialAxis,
            ref radialAxisVelocity,
            zoomSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
    }

    // 将脚本计算出的角度和半径应用到 Cinemachine 轨道跟随组件。
    private void ApplyOrbitValues()
    {
        // 复制 Cinemachine 当前的水平输入轴结构。
        InputAxis horizontalAxis = orbitalFollow.HorizontalAxis;
        // 把平滑后的水平角度写入轴结构。
        horizontalAxis.Value = horizontalAngle;
        // 将修改后的水平轴结构写回 Cinemachine 组件。
        orbitalFollow.HorizontalAxis = horizontalAxis;

        // 复制 Cinemachine 当前的垂直输入轴结构。
        InputAxis verticalAxis = orbitalFollow.VerticalAxis;
        // 使用脚本设置更新 Cinemachine 垂直轴的允许范围。
        verticalAxis.Range = new Vector2(minVerticalAngle, maxVerticalAngle);
        // 把平滑后的垂直角度写入轴结构。
        verticalAxis.Value = verticalAngle;
        // 将修改后的垂直轴结构写回 Cinemachine 组件。
        orbitalFollow.VerticalAxis = verticalAxis;

        // 把平滑后的缩放半径写入 Cinemachine 轨道跟随组件。
        orbitalFollow.Radius = currentRadius;

        InputAxis radialAxis = orbitalFollow.RadialAxis;
        radialAxis.Value = currentRadialAxis;
        orbitalFollow.RadialAxis = radialAxis;
    }

    // 从 Cinemachine 当前状态初始化脚本的所有运行时数值。                         
    private void InitializeCameraValues()
    {
        // 读取 Cinemachine 当前水平角度作为实际水平角度。
        horizontalAngle = orbitalFollow.HorizontalAxis.Value;
        // 让目标水平角度从当前实际角度开始，避免启用时跳动。
        targetHorizontalAngle = horizontalAngle;
        // 清除水平旋转缓冲速度，避免重新启用时残留运动。
        horizontalVelocity = 0f;

        // 读取 Cinemachine 当前垂直角度，并限制到脚本允许的范围。
        verticalAngle = Mathf.Clamp(
            // 传入 Cinemachine 当前垂直轴数值。
            orbitalFollow.VerticalAxis.Value,
            // 传入允许的最小垂直角度。
            minVerticalAngle,
            // 传入允许的最大垂直角度。
            maxVerticalAngle);
        // 让目标垂直角度从当前实际角度开始，避免启用时跳动。
        targetVerticalAngle = verticalAngle;
        // 清除垂直旋转缓冲速度，避免重新启用时残留运动。
        verticalVelocity = 0f;

        // 读取 Cinemachine 当前半径，并限制到脚本允许的范围。
        currentRadius = Mathf.Clamp(orbitalFollow.Radius, minRadius, maxRadius);
        // 让目标半径从当前实际半径开始，避免启用时缩放跳动。
        targetRadius = currentRadius;
        // 清除缩放缓冲速度，避免重新启用时残留缩放。
        zoomVelocity = 0f;

        currentRadialAxis = orbitalFollow.RadialAxis.Value;
        targetRadialAxis = currentRadialAxis;
        radialAxisVelocity = 0f;
    }

    // 将任意角度转换到负 180 度至正 180 度之间。
    private static float NormalizeAngle(float angle)
    {
        // 先偏移并循环角度，再恢复偏移以获得对称角度范围。
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    // 当 Inspector 数值发生变化时自动修正无效配置。
    private void OnValidate()
    {
        // 防止水平灵敏度成为负数。
        horizontalSensitivity = Mathf.Max(0f, horizontalSensitivity);
        // 防止垂直灵敏度成为负数。
        verticalSensitivity = Mathf.Max(0f, verticalSensitivity);
        // 确保最大垂直角度不会小于最小垂直角度。
        maxVerticalAngle = Mathf.Max(minVerticalAngle, maxVerticalAngle);
        // 防止旋转缓冲时间为零或负数。
        rotationSmoothTime = Mathf.Max(0.001f, rotationSmoothTime);
        // 防止最大旋转速度成为零或负数，同时允许其保持正无穷。
        maxRotationSpeed = Mathf.Max(0.01f, maxRotationSpeed);
        // 防止最小轨道半径接近零或成为负数。
        minRadius = Mathf.Max(0.01f, minRadius);
        // 确保最大轨道半径不会小于最小轨道半径。
        maxRadius = Mathf.Max(minRadius, maxRadius);
        // 防止缩放灵敏度成为零或负数。
        zoomSensitivity = Mathf.Max(0.00001f, zoomSensitivity);
        // 防止缩放缓冲时间为零或负数。
        zoomSmoothTime = Mathf.Max(0.001f, zoomSmoothTime);
        // 将固定垂直角限制在脚本允许的上下观察范围内。
        fixedVerticalAngle = Mathf.Clamp(
            fixedVerticalAngle,
            minVerticalAngle,
            maxVerticalAngle);
        fixedRadialAxis = Mathf.Max(0f, fixedRadialAxis);
        fixedViewSmoothTime = Mathf.Max(0.05f, fixedViewSmoothTime);
    }
}
