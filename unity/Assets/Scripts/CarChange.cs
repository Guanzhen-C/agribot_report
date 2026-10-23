using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CarChange : MonoBehaviour
{
    public const float MinWheelTrack = 0.59f;
    public const float MaxWheelTrack = 0.70f;

    private readonly string[] wheelColliderNames = { "WheelCollider_FL", "WheelCollider_FR", "WheelCollider_RL", "WheelCollider_RR" };
    private readonly string[] wheelNames = { "FLwheel", "FRwheel", "RLwheel", "RRwheel" };
    private Renderer FR_renderer;
    // private Transform carBody;

    [Header("UI")]
    [SerializeField] private Canvas UIcanvas;
    public Canvas UICanvas => UIcanvas;

    [Header("运行时状态")]
    [SerializeField] private float cur_wheelDis;
    public float Cur_wheelDis => cur_wheelDis;
    public event Action<float> WheelDistanceChanged;

    [SerializeField] private float curWheelTrack = MinWheelTrack;
    public float CurWheelTrack => curWheelTrack;
    public event Action<float> WheelTrackChanged;

    [SerializeField] private float curWheelRadius;
    public float CurWheelRadius => curWheelRadius;

    private BoxCollider bodyCollider;
    private Rigidbody carRigidbody;

    //获取前桥、后桥
    private Transform truck;
    private Transform QianQiao;
    private Transform HouQiaoController;
    private Transform HouQiao;

    private WheelCollider[] wheelColliders;
    private GameObject[] wheels;
    private GameObject CarBody;


    // 永久保存启动时的坐标。
    // 后续不能把修改后的坐标重新写入这两个变量。


    private Vector3 originalBodyColliderLocalPos;
    private Vector3 originCarBodyLocalPos;
    private Vector3 originalUIcanvasPos;
    private Vector3 originalUIWorldPos;

    // 防止用户在一次物理调整尚未结束时重复启动协程。
    private bool wheelDistanceAdjustmentRunning;

    // 如果用户连续点击按钮，保存最后一次请求。
    private float pendingWheelDistance;

    // 用于判断协程运行期间是否收到了新请求。
    private int adjustmentRequestVersion;

    private Vector3[] centersPos;
    private readonly string[] centersName = { "FLcenter", "FRcenter", "RLcenter", "RRcenter" };

    private float qianOffsetZ;
    private float houOffsetZ;
    private float qianWheelOffsetZ;
    private float houWheelOffsetZ;

    private Vector3 QianQiao_curPos;
    private Vector3 HouQiao_curPos;

    private Vector3 initialQianQiaoLocalPosition;
    private Vector3 initialHouQiaoControllerLocalPosition;
    private Vector3 wheelDistanceAxisLocal;
    private float wheelDistanceWorldUnitsPerLocalUnit;
    private float initialWheelDistance;
    private float originalWheelHalfHeight;

    private bool wheelTrackAdjustmentRunning;
    private float pendingWheelTrack;
    private int wheelTrackRequestVersion;

    private readonly string[] axleScaleRootNames =
    {
        "QianQiaoL_Dad",
        "QianQiao_R_Dad",
        "HouQiao_L_Dad",
        "HouQiao_R_Dad"
    };

    private Transform[] wheelRoots;
    private Transform[] wheelCenterMarkers;
    private Transform[] axleScaleRoots;
    private Vector3[] originalAxleScales;
    private float[] axleHalfExtents;
    private Vector3[] originalWheelRootLocalPositions;
    private Vector3[] wheelPairTrackAxesLocal;
    private float[] wheelPairBaselineTracks;

    private WheelChangePanel wheelChangePanel;
    private bool[] originalWheelColliderStates;
    private bool originBodyColliderState;
    private bool originalIsKinematic;
    private Vector3 originalLinearVelocity;
    private Vector3 originalAngularVelocity;
    private bool physicsAdjustmentActive;
    private bool geometryInitialized;

    private sealed class CarChildInitialState
    {
        public Transform transform;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public bool activeSelf;
        public Collider collider;
        public bool colliderEnabled;
        public WheelCollider wheelCollider;
        public float wheelColliderRadius;
    }

    private readonly List<CarChildInitialState> carChildInitialStates =
        new List<CarChildInitialState>();
    private bool hasRecordedCarChildInitialStates;


    void Awake()
    {
        Debug.Log("UICanvas的世界坐标：" + UIcanvas.transform.position);

        if (bodyCollider == null) bodyCollider = GameObject.Find("BodyCollider").GetComponent<BoxCollider>();
        originalUIcanvasPos = UIcanvas.transform.localPosition;
        originalUIWorldPos = UIcanvas.transform.position;

        if (QianQiao == null)
            QianQiao = GameObject.Find("QianQiao").transform;
        if (HouQiaoController == null)
            HouQiaoController = GameObject.Find("HouQiao_Controller").transform;
        if (HouQiao == null)
            HouQiao = GameObject.Find("HouQiao").transform;
        truck = QianQiao != null ? QianQiao.parent : null;

        carRigidbody = GetComponentInParent<Rigidbody>();
        wheelColliders = new WheelCollider[wheelColliderNames.Length];
        wheels = new GameObject[wheelColliderNames.Length];
        centersPos = new Vector3[centersName.Length];
        wheelRoots = new Transform[wheelNames.Length];
        wheelCenterMarkers = new Transform[centersName.Length];
        axleScaleRoots = new Transform[axleScaleRootNames.Length];
        originalAxleScales = new Vector3[axleScaleRootNames.Length];
        axleHalfExtents = new float[axleScaleRootNames.Length];
        originalWheelRootLocalPositions = new Vector3[wheelNames.Length];
        wheelPairTrackAxesLocal = new Vector3[2];
        wheelPairBaselineTracks = new float[2];
        CarBody = GameObject.Find("CarBody");

        for (int i = 0; i < wheelColliderNames.Length; i++)
        {
            GameObject wheelColliderObject = GameObject.Find(wheelColliderNames[i]);
            GameObject centerObject = GameObject.Find(centersName[i]);
            wheelCenterMarkers[i] = centerObject != null
                ? centerObject.transform
                : null;
            if (centerObject != null)
                centersPos[i] = centerObject.transform.position;
            wheels[i] = GameObject.Find(wheelNames[i]);
            wheelRoots[i] = wheels[i] != null
                ? wheels[i].transform.parent
                : null;
            if (i == 1)
                FR_renderer = wheels[1].GetComponent<Renderer>();

            if (wheelColliderObject != null)
            {
                wheelColliders[i] = wheelColliderObject.GetComponent<WheelCollider>();

            }
        }

        if (wheelColliders.Length > 0 && wheelColliders[0] != null)
            curWheelRadius = wheelColliders[0].radius;

        for (int i = 0; i < axleScaleRootNames.Length; i++)
        {
            GameObject axleScaleRoot = GameObject.Find(axleScaleRootNames[i]);
            axleScaleRoots[i] = axleScaleRoot != null
                ? axleScaleRoot.transform
                : null;
        }
        wheelChangePanel = Array.Find(transform.root.GetComponentsInChildren<WheelChangePanel>(true),
            panel => panel.name == "WheelChangePanel");
        if (wheelChangePanel == null)
            wheelChangePanel = GameObject.Find("WheelChangePanel")?.GetComponent<WheelChangePanel>();

        wheelChangePanel?.AddEscClickListener(EndPhysicsAdjustment);

        RecordCar0ChildrenInitialStates();

    }

    private void OnDestroy()
    {
        if (wheelChangePanel != null)
            wheelChangePanel.RemoveEscClickListener(EndPhysicsAdjustment);
    }

    public void FinishAdjustment()
    {
        StopAllCoroutines();
        wheelDistanceAdjustmentRunning = wheelTrackAdjustmentRunning = false;
        if (physicsAdjustmentActive) EndPhysicsAdjustment();
    }

    // 程序启动时只记录一次 CarChange 用到的 Car0 子物体初始状态。
    private void RecordCar0ChildrenInitialStates()
    {
        if (hasRecordedCarChildInitialStates)
            return;

        carChildInitialStates.Clear();
        HashSet<Transform> recordedTransforms = new HashSet<Transform>();

        RecordCarChildInitialState(bodyCollider != null
            ? bodyCollider.transform
            : null, recordedTransforms);
        RecordCarChildInitialState(QianQiao, recordedTransforms);
        RecordCarChildInitialState(HouQiaoController, recordedTransforms);
        RecordCarChildInitialState(HouQiao, recordedTransforms);
        RecordCarChildInitialState(CarBody != null
            ? CarBody.transform
            : null, recordedTransforms);

        for (int i = 0; i < wheels.Length; i++)
        {
            RecordCarChildInitialState(wheels[i] != null
                ? wheels[i].transform
                : null, recordedTransforms);
            RecordCarChildInitialState(wheelRoots[i], recordedTransforms);
            RecordCarChildInitialState(wheelCenterMarkers[i], recordedTransforms);
            RecordCarChildInitialState(wheelColliders[i] != null
                ? wheelColliders[i].transform
                : null, recordedTransforms);
        }

        for (int i = 0; i < axleScaleRoots.Length; i++)
            RecordCarChildInitialState(axleScaleRoots[i], recordedTransforms);

        hasRecordedCarChildInitialStates = carChildInitialStates.Count > 0;
    }

    private void RecordCarChildInitialState(
        Transform target,
        HashSet<Transform> recordedTransforms)
    {
        if (target == null ||
            target == transform ||
            !target.IsChildOf(transform) ||
            !recordedTransforms.Add(target))
        {
            return;
        }

        Collider targetCollider = target.GetComponent<Collider>();
        WheelCollider targetWheelCollider =
            target.GetComponent<WheelCollider>();

        carChildInitialStates.Add(new CarChildInitialState
        {
            transform = target,
            localPosition = target.localPosition,
            localRotation = target.localRotation,
            localScale = target.localScale,
            activeSelf = target.gameObject.activeSelf,
            collider = targetCollider,
            colliderEnabled = targetCollider != null && targetCollider.enabled,
            wheelCollider = targetWheelCollider,
            wheelColliderRadius = targetWheelCollider != null
                ? targetWheelCollider.radius
                : 0f
        });
    }

    // 恢复 CarChange 用到的 Car0 子物体到程序启动时的状态。
    public void ReturnAll()
    {
        if (!hasRecordedCarChildInitialStates)
        {
            Debug.LogWarning("CarChange：尚未记录 Car0 子物体初始状态。", this);
            return;
        }

        StopAllCoroutines();
        wheelDistanceAdjustmentRunning = false;
        wheelTrackAdjustmentRunning = false;

        if (physicsAdjustmentActive)
            EndPhysicsAdjustment();

        for (int i = 0; i < carChildInitialStates.Count; i++)
        {
            CarChildInitialState state = carChildInitialStates[i];
            if (state.transform == null)
                continue;

            state.transform.localPosition = state.localPosition;
            state.transform.localRotation = state.localRotation;
            state.transform.localScale = state.localScale;

            if (state.collider != null)
                state.collider.enabled = state.colliderEnabled;
            if (state.wheelCollider != null)
                state.wheelCollider.radius = state.wheelColliderRadius;
        }

        for (int i = 0; i < carChildInitialStates.Count; i++)
        {
            CarChildInitialState state = carChildInitialStates[i];
            if (state.transform != null)
                state.transform.gameObject.SetActive(state.activeSelf);
        }

        Physics.SyncTransforms();

        for (int i = 0; i < wheelColliders.Length; i++)
        {
            if (wheelColliders[i] == null)
                continue;

            curWheelRadius = wheelColliders[i].radius;
            break;
        }

        SyncWheelCenterMarkersAndCache();
        cur_wheelDis = MeasureWheelDistance();
        pendingWheelDistance = cur_wheelDis;
        curWheelTrack = MeasureWheelTrack();
        pendingWheelTrack = curWheelTrack;

        WheelDistanceChanged?.Invoke(cur_wheelDis);
        WheelTrackChanged?.Invoke(curWheelTrack);
        UIcanvas.transform.localPosition = originalUIcanvasPos;
    }

    void Start()
    {
        InitializeGeometry();
    }

    public void InitializeGeometry()
    {
        if (geometryInitialized) return;
        if (!ValidateRequiredReferences())
        {
            enabled = false;
            return;
        }
        geometryInitialized = true;

        originalBodyColliderLocalPos = bodyCollider.transform.localPosition;

        if (UIcanvas != null)
        {
            originalUIcanvasPos = UIcanvas.transform.localPosition;
        }

        originCarBodyLocalPos = CarBody.transform.localPosition;
        originalWheelHalfHeight = FR_renderer.bounds.extents.y;

        // WheelDistancePanel 原有功能使用固定的 Truck 本地坐标基线。
        initialQianQiaoLocalPosition = QianQiao.localPosition;
        initialHouQiaoControllerLocalPosition =
            HouQiaoController.localPosition;
        Vector3 wheelDistanceAxisWorld =
            (GetAxleCenter(0, 1) - GetAxleCenter(2, 3)).normalized;
        wheelDistanceAxisLocal = truck
            .InverseTransformDirection(wheelDistanceAxisWorld).normalized;
        wheelDistanceWorldUnitsPerLocalUnit = Mathf.Max(
            0.0001f,
            truck.TransformVector(wheelDistanceAxisLocal).magnitude);
        initialWheelDistance = MeasureWheelDistance();
        cur_wheelDis = initialWheelDistance;
        pendingWheelDistance = cur_wheelDis;
        WheelDistanceChanged?.Invoke(cur_wheelDis);

        // AxisPanel 独立的左右轮距基线。
        CaptureWheelTrackBaseline();
        SetWheelPairTrack(0, 1, MinWheelTrack);
        SetWheelPairTrack(2, 3, MinWheelTrack);
        ApplyAxleScale(MinWheelTrack);
        SyncWheelCenterMarkersAndCache();
        curWheelTrack = MinWheelTrack;
        pendingWheelTrack = curWheelTrack;
        WheelTrackChanged?.Invoke(curWheelTrack);

    }
    private void Update()
    {


    }
    private bool ValidateRequiredReferences()
    {
        bool valid = true;

        if (bodyCollider == null)
        {
            Debug.LogError("CarChange：没有找到 BodyCollider。", this);
            valid = false;
        }
        if (QianQiao == null)
        {
            Debug.LogError("CarChange：没有找到 QianQiao。", this);
            valid = false;
        }
        if (HouQiaoController == null)
        {
            Debug.LogError("CarChange：没有找到 HouQiao_Controller。", this);
            valid = false;
        }
        if (HouQiao == null)
        {
            Debug.LogError("CarChange：没有找到 HouQiao。", this);
            valid = false;
        }
        if (truck == null)
        {
            Debug.LogError("CarChange：没有找到 Truck。", this);
            valid = false;
        }
        if (carRigidbody == null)
        {
            Debug.LogError(
                "CarChange：没有在当前对象或父级找到 Rigidbody。",
                this
            );

            valid = false;
        }
        if (CarBody == null)
        {
            Debug.LogError("CarChange：没有找到 CarBody。", this);
            valid = false;
        }
        if (FR_renderer == null)
        {
            Debug.LogError("CarChange：没有找到 FRwheel 的 Renderer。", this);
            valid = false;
        }
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            if (wheelColliders[i] == null)
            {
                Debug.LogError(
                    $"CarChange：没有找到 {wheelColliderNames[i]} 的 WheelCollider。",
                    this
                );

                valid = false;
            }
        }
        for (int i = 0; i < wheelRoots.Length; i++)
        {
            if (wheelRoots[i] == null)
            {
                Debug.LogError($"CarChange：没有找到 {wheelNames[i]} 的车轮根节点。", this);
                valid = false;
            }

            if (wheelCenterMarkers[i] == null)
            {
                Debug.LogError($"CarChange：没有找到 {centersName[i]}。", this);
                valid = false;
            }
        }
        for (int i = 0; i < axleScaleRoots.Length; i++)
        {
            if (axleScaleRoots[i] == null)
            {
                Debug.LogError($"CarChange：没有找到 {axleScaleRootNames[i]}。", this);
                valid = false;
            }
        }
        return valid;
    }


    /* 调整轮子大小 */
    public void ChangeWheels(float newscale, float col_Radius)
    {
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i].transform.localScale = Vector3.one * newscale;  // 新
        }

        float offsetH = FR_renderer.bounds.extents.y - originalWheelHalfHeight;
        if (col_Radius >= 10.5 && col_Radius <= 17)
        {
            Vector3 localHeightOffset = CarBody.transform.parent
                .InverseTransformVector(Vector3.up * offsetH);
            CarBody.transform.localPosition =
                originCarBodyLocalPos + localHeightOffset;
        }

        // 随之改变碰撞器半径大小
        foreach (string wheelColliderName in wheelColliderNames)
        {
            GameObject wheelCollider = GameObject.Find(wheelColliderName);
            if (wheelCollider != null)
                wheelCollider.GetComponent<WheelCollider>().radius = col_Radius;
        }

        curWheelRadius = col_Radius;
        Agribot.VehicleConfiguration.AssistantConversation.RecordUi("调整车轮半径，模型参数值：" + col_Radius);

        UIcanvas.transform.position = new Vector3(
            FR_renderer.bounds.max.x,
            FR_renderer.bounds.max.y + 0.045f,
            FR_renderer.bounds.center.z
          );

        Debug.Log("UICanvas的世界坐标：" + UIcanvas.transform.position);

    }


    /* AxisPanel：调整左右轮距 */
    public void SetWheelTrack(float wheelTrack)
    {
        if (!ValidateWheelTrack(wheelTrack))
            return;

        BeginPhysicsAdjustment();

        pendingWheelTrack = wheelTrack;
        wheelTrackRequestVersion++;

        if (wheelTrackAdjustmentRunning)
            return;

        StartCoroutine(ChangeWheelTrackRoutine());
    }

    public void ChangeWheelsDis(float newdis)
    {
        if (!ValidateDistance(newdis))
            return;

        BeginPhysicsAdjustment();

        // 保存最后一次请求。
        pendingWheelDistance = newdis;
        adjustmentRequestVersion++;

        // 已经存在调整协程时，不再重复启动。
        // 当前协程会读取最新的 pendingWheelDistance。
        if (wheelDistanceAdjustmentRunning)
            return;

        StartCoroutine(ChangeWheelsDistanceRoutine());

    }

    private bool ValidateDistance(float newDistance)
    {
        if (newDistance <= 0f)
        {
            Debug.LogError(
                $"轮距必须大于 0，当前输入：{newDistance}",
                this
            );

            return false;
        }

        if (QianQiao == null ||
            HouQiaoController == null ||
            carRigidbody == null)
        {
            Debug.LogError(
                "车辆引用不完整，无法调整轮距。",
                this
            );

            return false;
        }

        return true;
    }

    private bool ValidateWheelTrack(float wheelTrack)
    {
        if (wheelTrack < MinWheelTrack || wheelTrack > MaxWheelTrack)
        {
            Debug.LogError(
                $"左右轮距必须在 {MinWheelTrack:F2}m 到 {MaxWheelTrack:F2}m 之间，当前输入：{wheelTrack:F3}m",
                this);
            return false;
        }

        if (carRigidbody == null)
        {
            Debug.LogError("车辆引用不完整，无法调整左右轮距。", this);
            return false;
        }

        return true;
    }

    // 可以执行多次，只更新最终轮距
    private IEnumerator ChangeWheelsDistanceRoutine()
    {
        wheelDistanceAdjustmentRunning = true;

        // 如果用户快速点击了多个按钮，
        // 在恢复物理以前始终应用最后一次请求。
        while (true)
        {
            int appliedRequestVersion = adjustmentRequestVersion;

            ApplyWheelDistance(pendingWheelDistance);

            // 让 Unity 物理系统获取新的 Transform。
            Physics.SyncTransforms();

            // 等待一个物理帧，让旧接触状态失效。
            yield return new WaitForFixedUpdate();

            // 等待期间没有新请求，可以结束调整。
            if (appliedRequestVersion == adjustmentRequestVersion) break;

        }
        wheelDistanceAdjustmentRunning = false;

        Debug.Log(
            $"轮距调整完成，当前轮距：{cur_wheelDis:F6}",
            this
        );
    }

    // 每次打开的调整会话只执行一次
    private void BeginPhysicsAdjustment()
    {
        if (physicsAdjustmentActive)
            return;

        originalIsKinematic = carRigidbody.isKinematic;
        originalLinearVelocity = carRigidbody.linearVelocity;
        originalAngularVelocity = carRigidbody.angularVelocity;


        originalWheelColliderStates = new bool[wheelColliders.Length];
        originBodyColliderState = bodyCollider.enabled;
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            if (wheelColliders[i] != null)
                originalWheelColliderStates[i] = wheelColliders[i].enabled;
        }

        carRigidbody.isKinematic = true;
        SetWheelCollidersEnabled(false);
        bodyCollider.enabled = false;///保存下状态 
        physicsAdjustmentActive = true;
    }

    private void EndPhysicsAdjustment()
    {
        if (!physicsAdjustmentActive)
            return;

        RestorePhysicsSystem(
            originalWheelColliderStates,
            originalIsKinematic,
            originBodyColliderState,
            originalLinearVelocity,
            originalAngularVelocity
        );

        physicsAdjustmentActive = false;
    }


    // 恢复物理系统
    private void RestorePhysicsSystem(
        bool[] originalWheelColliderStates,
        bool originalIsKinematic,
        bool originBodyColliderState,
        Vector3 originalLinearVelocity,
        Vector3 originalAngularVelocity)
    {
        // 按原来的状态恢复 WheelCollider。
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            if (wheelColliders[i] != null)
            {
                wheelColliders[i].enabled = originalWheelColliderStates[i];
            }
        }
        bodyCollider.enabled = originBodyColliderState;
        Physics.SyncTransforms();

        // 恢复 Rigidbody 原来的运动学状态。
        carRigidbody.isKinematic = originalIsKinematic;

        if (!originalIsKinematic)
        {
            // 恢复调整前的运动速度。
            carRigidbody.linearVelocity = originalLinearVelocity;
            carRigidbody.angularVelocity = originalAngularVelocity;
            carRigidbody.WakeUp();
        }
    }

    private void CaptureWheelTrackBaseline()
    {
        for (int i = 0; i < wheelRoots.Length; i++)
            originalWheelRootLocalPositions[i] = wheelRoots[i].localPosition;

        CaptureWheelPairTrackBaseline(0, 0, 1);
        CaptureWheelPairTrackBaseline(1, 2, 3);

        for (int i = 0; i < axleScaleRoots.Length; i++)
        {
            Transform axleRoot = axleScaleRoots[i];
            originalAxleScales[i] = axleRoot.localScale;

            Vector3 scaleAxis = i < 2
                ? axleRoot.right.normalized
                : axleRoot.up.normalized;
            axleHalfExtents[i] = GetProjectedHalfExtent(axleRoot, scaleAxis);
        }
    }

    private void CaptureWheelPairTrackBaseline(
        int pairIndex,
        int leftIndex,
        int rightIndex)
    {
        Transform pairParent = wheelRoots[leftIndex].parent;
        Vector3 leftCenterLocal = pairParent.InverseTransformPoint(
            wheels[leftIndex].transform.position);
        Vector3 rightCenterLocal = pairParent.InverseTransformPoint(
            wheels[rightIndex].transform.position);

        wheelPairTrackAxesLocal[pairIndex] =
            (rightCenterLocal - leftCenterLocal).normalized;
        wheelPairBaselineTracks[pairIndex] = Vector3.Distance(
            wheels[leftIndex].transform.position,
            wheels[rightIndex].transform.position);
    }

    private static float GetProjectedHalfExtent(
        Transform root,
        Vector3 worldAxis)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return 0.001f;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 axis = new Vector3(
            Mathf.Abs(worldAxis.x),
            Mathf.Abs(worldAxis.y),
            Mathf.Abs(worldAxis.z));
        return Mathf.Max(0.001f, Vector3.Dot(bounds.extents, axis));
    }

    private void SetWheelPairTrack(
        int leftIndex,
        int rightIndex,
        float targetTrack)
    {
        int pairIndex = leftIndex == 0 ? 0 : 1;
        Transform pairParent = wheelRoots[leftIndex].parent;
        Vector3 localTrackAxis = wheelPairTrackAxesLocal[pairIndex];
        float worldUnitsPerLocalUnit = Mathf.Max(
            0.0001f,
            pairParent.TransformVector(localTrackAxis).magnitude);
        float localHalfDelta =
            (targetTrack - wheelPairBaselineTracks[pairIndex]) *
            0.5f / worldUnitsPerLocalUnit;

        wheelRoots[leftIndex].localPosition =
            originalWheelRootLocalPositions[leftIndex] -
            localTrackAxis * localHalfDelta;
        wheelRoots[rightIndex].localPosition =
            originalWheelRootLocalPositions[rightIndex] +
            localTrackAxis * localHalfDelta;
    }

    private IEnumerator ChangeWheelTrackRoutine()
    {
        wheelTrackAdjustmentRunning = true;

        while (true)
        {
            int appliedRequestVersion = wheelTrackRequestVersion;

            ApplyWheelTrack(pendingWheelTrack);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            if (appliedRequestVersion == wheelTrackRequestVersion)
                break;
        }

        wheelTrackAdjustmentRunning = false;
        Debug.Log(
            $"左右轮距调整完成，当前左右轮距：{curWheelTrack:F6}",
            this);
    }

    private void ApplyAxleScale(float targetTrack)
    {
        float outwardExtension =
            Mathf.Max(0f, targetTrack - MinWheelTrack) * 0.5f;

        for (int i = 0; i < axleScaleRoots.Length; i++)
        {
            Vector3 scale = originalAxleScales[i];
            float scaleMultiplier =
                1f + outwardExtension / axleHalfExtents[i];

            if (i < 2)
                scale.x = originalAxleScales[i].x * scaleMultiplier;
            else
                scale.y = originalAxleScales[i].y * scaleMultiplier;

            axleScaleRoots[i].localScale = scale;
        }
    }

    private Vector3 GetAxleCenter(int leftIndex, int rightIndex)
    {
        return (
            wheels[leftIndex].transform.position +
            wheels[rightIndex].transform.position) * 0.5f;
    }

    private float MeasureWheelDistance()
    {
        return Vector3.Distance(
            GetAxleCenter(0, 1),
            GetAxleCenter(2, 3));
    }

    private float MeasureWheelTrack()
    {
        float frontTrack = Vector3.Distance(
            wheels[0].transform.position,
            wheels[1].transform.position);
        float rearTrack = Vector3.Distance(
            wheels[2].transform.position,
            wheels[3].transform.position);
        return (frontTrack + rearTrack) * 0.5f;
    }

    private void SyncWheelCenterMarkersAndCache()
    {
        for (int i = 0; i < wheels.Length; i++)
        {
            Vector3 wheelCenter = wheels[i].transform.position;
            centersPos[i] = wheelCenter;
            wheelCenterMarkers[i].position = wheelCenter;
        }
    }

    // WheelDistancePanel：改变前后轮距。
    private void ApplyWheelDistance(float targetDis)
    {
        if (targetDis >= 0.42f && targetDis <= 0.60f)
        {
            float deltaFromInitial = targetDis - initialWheelDistance;
            float localHalfDelta = deltaFromInitial * 0.5f /
                wheelDistanceWorldUnitsPerLocalUnit;
            Debug.Log("targetDis:" + targetDis);
            Debug.Log("cur_wheelDis:" + cur_wheelDis);
            Debug.Log("deltaFromInitial:" + deltaFromInitial);

            QianQiao.localPosition =
                initialQianQiaoLocalPosition +
                wheelDistanceAxisLocal * localHalfDelta;
            HouQiaoController.localPosition =
                initialHouQiaoControllerLocalPosition -
                wheelDistanceAxisLocal * localHalfDelta;

            SyncWheelCenterMarkersAndCache();

            UIcanvas.transform.position = new Vector3(
                FR_renderer.bounds.max.x,
                FR_renderer.bounds.max.y + 0.055f,
                FR_renderer.bounds.center.z);

            cur_wheelDis = targetDis;
            Agribot.VehicleConfiguration.AssistantConversation.RecordUi("调整前后轴距，模型参数值：" + targetDis);
            WheelDistanceChanged?.Invoke(cur_wheelDis);
        }
    }

    // AxisPanel：改变左右轮距和桥体缩放。
    private void ApplyWheelTrack(float targetTrack)
    {
        SetWheelPairTrack(0, 1, targetTrack);
        SetWheelPairTrack(2, 3, targetTrack);
        ApplyAxleScale(targetTrack);
        SyncWheelCenterMarkersAndCache();

        UIcanvas.transform.position = new Vector3(
            FR_renderer.bounds.max.x,
            FR_renderer.bounds.max.y + 0.055f,
            FR_renderer.bounds.center.z
          );

        curWheelTrack = targetTrack;
        Agribot.VehicleConfiguration.AssistantConversation.RecordUi("调整左右轮距，模型参数值：" + targetTrack);
        WheelTrackChanged?.Invoke(curWheelTrack);
    }

    //暂停轮胎悬架与地面物理。
    private void SetWheelCollidersEnabled(bool value)
    {
        foreach (WheelCollider wheelCollider in wheelColliders)
        {
            if (wheelCollider != null)
                wheelCollider.enabled = value;
        }
    }
}
