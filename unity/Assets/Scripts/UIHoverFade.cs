using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 当目标轮子可见且鼠标接近其屏幕投影位置时，显示设置入口。
/// 目标离开画面、转到背面或被其他物体遮挡时立即隐藏。
/// </summary>
public sealed class UIHoverFade : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private CanvasGroup targetCanvasGroup;     //fix_border_img
    [SerializeField] private CanvasGroup settingButtonCanvasGroup;      // setting_btn 自身的 CanvasGroup
    [SerializeField] private Transform visibilityTarget;    // 绑定Car0/Wheel_FR
    [SerializeField] private Renderer visibilityRenderer;   // 目标轮子的 Renderer，用其包围盒中心作为投影和遮挡检测点。
    [SerializeField] private Camera eventCamera;    // 最终画面输出相机。应绑定带 CinemachineBrain 的 Main Camera。
    // 接收 fix_border_img 点击并执行固定视角的轨道控制器。
    [SerializeField] private OrbitCameraController fixedViewController;

    [Header("Screen-space Proximity")]
    // 鼠标进入的半径后，。
    [SerializeField, Range(0.01f, 1f)] private float showRadiusRatio = 0.35f;   // “屏幕短边 × 该比例”,setting_btn 开始显现
    [SerializeField, Range(0.01f, 1f)] private float hideRadiusRatio = 0.45f;   // setting_btn 退出
    [SerializeField, Min(0f)] private float minimumVisibleDuration = 1.0f;  // 鼠标触发后 setting_btn 至少保持显示的时间；遮挡时不应用此时间。

    [Header("Border Close Hover")]
    [SerializeField, Min(0.001f)] private float borderHoverRadiusMeters = 0.25f;    // fix_border_img 不与 setting_btn 共用显现范围
    [SerializeField, Min(4f)] private float fixButtonClickRadiusPixels = 80f;
    [Header("Occlusion")]
    // 可阻挡目标轮子的物理层；默认检测全部层。
    [SerializeField] private LayerMask occlusionLayers = ~0;

    [Header("Fade")]
    [SerializeField, Min(0.01f)] private float settingButtonFadeInDuration = 0.3f;  //淡入所需时间
    [SerializeField, Min(0.01f)] private float settingButtonFadeOutDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.35f;    // fix_border_img完整淡入或淡出所需时间。
    // 使用非缩放时间后，即使游戏暂停，UI 渐变仍可继续播放。
    [SerializeField] private bool useUnscaledTime = true;

    // 复用射线结果数组，避免在 Update 中使用 RaycastAll 产生垃圾回收。
    private readonly RaycastHit[] occlusionHits = new RaycastHit[32];
    private Coroutine fadeCoroutine;    // 保存 fix_border_img 当前正在运行的淡入淡出协程。
   
    private bool isPointerNear;     // 记录鼠标是否位于 fix_border_img 的近距离触发范围。
    private bool isSettingButtonVisible;
    // 外部面板打开时暂停悬停显示，但继续播放淡出动画。
    private bool buttonsEnabled = true;
    private float visibleUntilTime;      // setting_btn 最短显示时间的截止时刻。

    public GameObject fix_btn;
    private Button fixButton;

    private void Awake()
    {
        // Inspector 未指定相机时，自动使用带 MainCamera 标签的最终渲染相机。
        if (eventCamera == null)
            eventCamera = Camera.main;

        // 默认从当前 setting_btn 对象取得自身 CanvasGroup。
        if (settingButtonCanvasGroup == null)
            settingButtonCanvasGroup = GetComponent<CanvasGroup>();

        // 只指定目标 Transform 时，自动寻找其子层级中的 Renderer。
        if (visibilityRenderer == null && visibilityTarget != null)
            visibilityRenderer = visibilityTarget.GetComponentInChildren<Renderer>();

        // Inspector 未指定时，自动查找场景中的轨道相机控制器。
        if (fixedViewController == null)
            // 缓存控制器引用，避免点击时重复执行场景搜索。
            fixedViewController = FindFirstObjectByType<OrbitCameraController>();

        if (fix_btn == null)
            fix_btn = GameObject.Find("fix_border_img");

        if (fix_btn != null)
            fixButton = fix_btn.GetComponent<Button>();

        // 缺少必要引用时禁用脚本，避免运行期间出现空引用异常。
        if (targetCanvasGroup == null ||
            settingButtonCanvasGroup == null ||
            visibilityTarget == null ||
            eventCamera == null ||
            fixButton == null)
        {
            Debug.LogError(
                "UIHoverFade: CanvasGroups, visibility target, Main Camera and fix button must be assigned.",
                this);
            enabled = false;
            return;
        }

        // 使用 Button 自身的矩形射线区域处理点击，避免以错误世界坐标和固定半径估算命中。
        fixButton.onClick.AddListener(OnFixButtonClicked);


        // 淡出使用 CanvasGroup，因此对象必须保持激活。
        targetCanvasGroup.gameObject.SetActive(true);
        // 初始化为完全隐藏且不阻挡 UI 射线的状态。
        SetImmediateHidden();
        
    }

    private void Update()
    {
        // 禁用期间不再进行悬停检测，但仍逐帧完成 setting_btn 的淡出。
        // fix_border_img 的淡出由其协程继续处理。
        if (!buttonsEnabled)
        {
            UpdateSettingButtonAlpha();
            return;
        }

        // 每帧重新取得 Renderer 中心，以兼容轮子移动或旋转。
        Vector3 targetPosition = GetTargetPosition();

        // 遮挡和画面范围具有最高优先级，不受最短显示时间影响。
        if (!IsTargetVisible(targetPosition))
        {
            SetImmediateHidden();
            return;
        }

        // setting_btn 的触发范围按屏幕短边百分比计算，可适配不同分辨率。
        bool settingPointerNear = IsPointerNearTarget(targetPosition);
        if (settingPointerNear)
            // 鼠标持续处于范围内时不断刷新最短显示截止时间。
            visibleUntilTime = CurrentTime + minimumVisibleDuration;

        // 鼠标在范围内，或尚未到达显示截止时间时，继续显示 setting_btn。
        isSettingButtonVisible =
            settingPointerNear || CurrentTime < visibleUntilTime;

        // 每帧让 setting_btn 的透明度向当前目标平滑收敛。
        UpdateSettingButtonAlpha();

        // 边框恢复为原来的近距离世界空间触发，不使用按钮的大范围百分比。
        bool borderPointerNear =
            isSettingButtonVisible &&
            (IsPointerWithinBorderWorldRadius() || IsPointerWithinFixButtonScreenRadius());

        // World Space UI 从侧面观察时矩形投影会被压窄，因此用固定屏幕像素半径补充点击命中。
        if (IsFixButtonFallbackPressed())
        {
            OnFixButtonClicked();
            return;
        }

        if (borderPointerNear == isPointerNear)
            return;

        // 只有近距离状态改变时才重启边框渐变，避免每帧创建协程。
        isPointerNear = borderPointerNear;
        StartTargetFade(isPointerNear ? 1f : 0f);
    }

    private Vector3 GetTargetPosition()
    {
        // Renderer 有效时使用可见几何体中心，比 Transform 原点更准确。
        if (visibilityRenderer != null && visibilityRenderer.enabled)
            return visibilityRenderer.bounds.center;

        // 没有可用 Renderer 时退回目标 Transform 的世界位置。
        return visibilityTarget.position;
    }

    private bool IsTargetVisible(Vector3 targetPosition)
    {
        // 目标对象未激活时不允许显示任何相关 UI。
        if (!visibilityTarget.gameObject.activeInHierarchy)
            return false;

        // Renderer 被关闭时视为目标不可见。
        if (visibilityRenderer != null && !visibilityRenderer.enabled)
            return false;

        // 先转换到 Viewport 坐标，检查目标是否在相机前方和画面范围内。
        Vector3 viewportPoint = eventCamera.WorldToViewportPoint(targetPosition);
        if (viewportPoint.z <= 0f ||
            viewportPoint.x < 0f || viewportPoint.x > 1f ||
            viewportPoint.y < 0f || viewportPoint.y > 1f)
        {
            return false;
        }

        // 从最终渲染相机向轮子中心构造遮挡检测射线。
        Vector3 origin = eventCamera.transform.position;
        Vector3 delta = targetPosition - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return true;

        // 使用无分配射线检测，查找相机与轮子之间的车身或场景物体。
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            delta / distance,
            occlusionHits,
            distance,
            occlusionLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = occlusionHits[i].transform;
            // 忽略目标轮子自身的 Collider，避免把目标误判为遮挡物。
            if (hitTransform == visibilityTarget ||
                hitTransform.IsChildOf(visibilityTarget))
            {
                continue;
            }

            // 任意其他碰撞体位于相机和轮子之间，都判定轮子被遮挡。
            return false;
        }

        // 目标在画面内且射线路径无阻挡时，才认为轮子真实可见。
        return true;
    }

    private bool IsPointerNearTarget(Vector3 targetPosition)
    {
        // 没有鼠标设备时无法进行屏幕接近判断。
        if (Mouse.current == null)
            return false;

        // 将轮子中心投影为最终 Game 画面的像素坐标。
        Vector3 screenPoint = eventCamera.WorldToScreenPoint(targetPosition);
        if (screenPoint.z <= 0f)
            return false;

        // 以屏幕短边为基准，避免超宽屏和不同比例显示器造成范围失真。
        float screenReference = Mathf.Min(Screen.width, Screen.height);
        // 已显示时采用更大的隐藏范围，构成迟滞区间。
        float radiusRatio =
            isSettingButtonVisible ? hideRadiusRatio : showRadiusRatio;
        float radiusPixels = screenReference * radiusRatio;

        // 读取新版输入系统中的鼠标屏幕像素位置。
        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Vector2 targetScreenPosition =
            new Vector2(screenPoint.x, screenPoint.y);

        // 使用平方距离比较，避免每帧计算平方根。
        return Vector2.SqrMagnitude(
                   pointerPosition - targetScreenPosition)
               <= radiusPixels * radiusPixels;
    }

    private bool IsPointerWithinBorderWorldRadius()
    {
        // fix_border_img 保留旧版的 World Space Canvas 平面距离判断。
        if (Mouse.current == null)
            return false;

        // 从 Main Camera 经过鼠标像素位置发射世界空间射线。
        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Ray pointerRay = eventCamera.ScreenPointToRay(pointerPosition);
        // 使用 setting_btn 的位置和朝向构造 World Space Canvas 平面。
        Plane canvasPlane = new Plane(transform.forward, transform.position);

        // 鼠标射线没有经过 Canvas 平面时，视为未靠近边框。
        if (!canvasPlane.Raycast(pointerRay, out float enterDistance))
            return false;

        // 计算射线与 Canvas 平面的交点，再与 setting_btn 中心比较世界距离。
        Vector3 pointerWorldPosition = pointerRay.GetPoint(enterDistance);
        return Vector3.Distance(pointerWorldPosition, transform.position)
               <= borderHoverRadiusMeters;
    }

    /// <summary>
    /// 使用屏幕空间圆形热区补偿 World Space UI 侧视时被压窄的问题。
    /// 该热区同时参与悬停显示和备用点击，因此侧面观察时仍然容易命中。
    /// </summary>
    private bool IsPointerWithinFixButtonScreenRadius()
    {
        if (Mouse.current == null || fix_btn == null || !fix_btn.activeInHierarchy)
            return false;

        Vector3 buttonScreenPosition =
            eventCamera.WorldToScreenPoint(fix_btn.transform.position);
        if (buttonScreenPosition.z <= 0f)
            return false;

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Vector2 buttonCenter = new Vector2(
            buttonScreenPosition.x,
            buttonScreenPosition.y);

        return Vector2.SqrMagnitude(pointerPosition - buttonCenter)
               <= fixButtonClickRadiusPixels * fixButtonClickRadiusPixels;
    }
    private void OnFixButtonClicked()
    {
        if (fixedViewController != null)
            fixedViewController.LockToFixedView();
        else
            Debug.LogError("UIHoverFade: OrbitCameraController was not found.", this);
    }

    private bool IsFixButtonFallbackPressed()
    {
        if (!isSettingButtonVisible ||
            targetCanvasGroup.alpha <= 0.1f ||
            Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame ||
            fix_btn == null ||
            !fix_btn.activeInHierarchy)
        {
            return false;
        }

        return IsPointerWithinFixButtonScreenRadius();
    }

    private void UpdateSettingButtonAlpha()
    {
        // 根据当前可见状态确定 setting_btn 的目标透明度。
        float targetAlpha = isSettingButtonVisible ? 1f : 0f;
        float deltaTime =
            useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // 淡入使用较短时长保证响应，淡出使用较长时长获得柔和消失效果。
        float activeFadeDuration = isSettingButtonVisible
            ? settingButtonFadeInDuration
            : settingButtonFadeOutDuration;

        // MoveTowards 支持显隐状态中途反向，并保证最终准确到达目标值。
        settingButtonCanvasGroup.alpha = Mathf.MoveTowards(
            settingButtonCanvasGroup.alpha,
            targetAlpha,
            deltaTime / activeFadeDuration);

        // 仅在可见期间允许 setting_btn 接收交互和 UI 射线。
        settingButtonCanvasGroup.interactable = isSettingButtonVisible;
        settingButtonCanvasGroup.blocksRaycasts = isSettingButtonVisible;
    }

    private void SetImmediateHidden()
    {
        // 遮挡或离开画面时清除所有保持状态，确保按钮不会残留。
        isPointerNear = false;
        isSettingButtonVisible = false;
        visibleUntilTime = 0f;

        settingButtonCanvasGroup.alpha = 0f;
        settingButtonCanvasGroup.interactable = false;
        settingButtonCanvasGroup.blocksRaycasts = false;

        // 停止可能仍在运行的边框渐变，避免下一帧重新写回透明度。
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        // 遮挡隐藏必须立即生效，因此直接把边框透明度设为零。
        targetCanvasGroup.alpha = 0f;
        targetCanvasGroup.interactable = false;
        targetCanvasGroup.blocksRaycasts = false;
    }

    private void StartTargetFade(float targetAlpha)
    {
        // 淡入开始时立即允许按钮交互，避免按钮已可见但仍无法点击。
        bool shouldReceiveInput = targetAlpha > 0.99f;
        // 根据本次渐变方向立即更新 Button 的可交互状态。
        targetCanvasGroup.interactable = shouldReceiveInput;
        // 根据本次渐变方向立即更新 UI 射线接收状态。
        targetCanvasGroup.blocksRaycasts = shouldReceiveInput;

        // 新状态到来时先终止旧动画，支持鼠标快速进出时平滑反向。
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = StartCoroutine(FadeTargetTo(targetAlpha));
    }

    private IEnumerator FadeTargetTo(float targetAlpha)
    {
        // 从当前透明度开始插值，避免中途反向时发生跳变。
        float startAlpha = targetCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            // 按设置选择缩放时间或非缩放时间。
            elapsed +=
                useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeDuration);
            // SmoothStep 让渐变起止速度更柔和。
            targetCanvasGroup.alpha =
                Mathf.SmoothStep(startAlpha, targetAlpha, progress);
            yield return null;
        }

        targetCanvasGroup.alpha = targetAlpha;
        // 边框完全显示后才开放点击，淡出和半透明阶段不接收射线。
        targetCanvasGroup.interactable = targetAlpha > 0.99f;
        targetCanvasGroup.blocksRaycasts = targetAlpha > 0.99f;
        fadeCoroutine = null;
    }

    private float CurrentTime =>
        useUnscaledTime ? Time.unscaledTime : Time.time;

    private void OnDisable()
    {
        // 组件停用时清理协程，防止重新启用后保留旧动画状态。
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        fadeCoroutine = null;
    }

    private void OnDestroy()
    {
        if (fixButton != null)
            fixButton.onClick.RemoveListener(OnFixButtonClicked);
    }

    private void OnValidate()
    {
        // Inspector 修改参数时自动约束数值，并保证隐藏范围不小于显示范围。
        showRadiusRatio = Mathf.Clamp(showRadiusRatio, 0.01f, 1f);
        hideRadiusRatio =
            Mathf.Clamp(hideRadiusRatio, showRadiusRatio, 1f);
        minimumVisibleDuration = Mathf.Max(0f, minimumVisibleDuration);
        borderHoverRadiusMeters =
            Mathf.Max(0.001f, borderHoverRadiusMeters);
        fixButtonClickRadiusPixels =
            Mathf.Max(4f, fixButtonClickRadiusPixels);
        // 防止 setting_btn 淡入时间为零或负数。
        settingButtonFadeInDuration =
            Mathf.Max(0.01f, settingButtonFadeInDuration);
        // 防止 setting_btn 淡出时间为零或负数。
        settingButtonFadeOutDuration =
            Mathf.Max(0.01f, settingButtonFadeOutDuration);
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
    }

    // 组件失活
    public void DisableBtns()
    {
        if (!buttonsEnabled)
            return;

        buttonsEnabled = false;
        isPointerNear = false;
        isSettingButtonVisible = false;
        visibleUntilTime = 0f;

        // 淡出开始后立即停止接收输入。
        settingButtonCanvasGroup.interactable = false;
        settingButtonCanvasGroup.blocksRaycasts = false;
        StartTargetFade(0f);
    }

    // 组件激活
    public void EnableBtns()
    {
        if (buttonsEnabled)
            return;

        // 先建立稳定的隐藏状态，再允许下一帧重新进行悬停检测。
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        isPointerNear = false;
        isSettingButtonVisible = false;
        visibleUntilTime = 0f;

        settingButtonCanvasGroup.alpha = 0f;
        settingButtonCanvasGroup.interactable = false;
        settingButtonCanvasGroup.blocksRaycasts = false;

        targetCanvasGroup.alpha = 0f;
        targetCanvasGroup.interactable = false;
        targetCanvasGroup.blocksRaycasts = false;

        buttonsEnabled = true;
    }

    // 保留旧接口，兼容 OrbitCameraController 和场景中已有的 UnityEvent。
    public void UnableBtns()
    {
        DisableBtns();
    }

    public void EnableBtn()
    {
        EnableBtns();
    }

}
