using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    [Serializable]
    public sealed class SensorAnchorDefinition
    {
        public Transform visualRoot;
        public string id;
        public string sensorType;
        public string model;
        public string role;
        public string frameId;
        public Transform anchor;
        public string measurementFrameId;
        public Transform measurementFrame;
        public string topic;
        public float updateRateHz;
    }

    [DisallowMultipleComponent]
    public sealed class VehicleConfigurationAuthoring : MonoBehaviour
    {
        [Header("配置标识")]
        [SerializeField] private string vehicleId = "ackermann_current";
        [SerializeField] private string vehicleType = "ackermann";
        private float[] wheelRadii;
        private float[] wheelWidths;
        private Transform[] wheelVisualRoots;
        [SerializeField] private string displayName = "当前阿克曼农机";
        [SerializeField] private string profileVersion = "1.0.0";
        [SerializeField] private string calibrationState = "validated_ackermann_baseline";

        [Header("ROS FLU 基准")]
        [SerializeField] private Transform baseLink;
        [SerializeField] private string baseLinkReference = "rear_axle_midpoint";
        [SerializeField] private Transform frontLeftWheelCenter;
        [SerializeField] private Transform frontRightWheelCenter;
        [SerializeField] private Transform rearLeftWheelCenter;
        [SerializeField] private Transform rearRightWheelCenter;
        [SerializeField] private bool deriveGeometryFromWheelAnchors = true;

        [Header("车体尺寸（m，以 base_link 为基准）")]
        [SerializeField] private float bodyFrontM = 0.654818f;
        [SerializeField] private float bodyRearM = 0.1275f;
        [SerializeField] private float bodyLeftM = 0.335974f;
        [SerializeField] private float bodyRightM = 0.335974f;
        [SerializeField] private float wheelbaseM = 0.5265855f;
        [SerializeField] private float frontTrackM = 0.589931f;
        [SerializeField] private float rearTrackM = 0.590517f;
        [SerializeField] private float wheelRadiusM = 0.1275f;
        [SerializeField] private float wheelWidthM = 0.081431f;
        [SerializeField] private float maxSteeringAngleRad = 0.384f;

        [Header("导航与底盘限制")]
        [SerializeField] private float frontClearanceM = 0.10f;
        [SerializeField] private float rearClearanceM = 0.10f;
        [SerializeField] private float leftClearanceM = 0.15f;
        [SerializeField] private float rightClearanceM = 0.15f;
        [SerializeField] private float navigationMaxLinearSpeedMps = 0.30f;
        [SerializeField] private float navigationMaxAngularSpeedRadps = 0.230195f;
        [SerializeField] private bool allowReverse;
        [SerializeField] private float inflationRadiusM = 2.0f;
        [SerializeField] private float costScalingFactor = 1.0f;
        [SerializeField] private float chassisMaxLinearSpeedMps = 0.80f;
        [SerializeField] private float chassisMaxAngularSpeedRadps = 0.613854f;
        [SerializeField] private float minimumMotionSpeedMps = 0.02f;

        [Header("仿真动力学")]
        [SerializeField] private float massKg = 50f;
        [SerializeField] private Vector3 centerOfMass = new Vector3(0.2623915f, 0f, 0.0615505f);
        [SerializeField] private Vector3 inertiaDiagonal = new Vector3(1.914f, 2.672f, 4.084f);
        [SerializeField] private float maxAccelerationMps2 = 0.8f;
        [SerializeField] private float maxSteeringRateRadps = 1.2f;
        [SerializeField] private Vector3 collisionCenter = new Vector3(0.2623915f, 0f, 0.0615505f);
        [SerializeField] private Vector3 collisionSize = new Vector3(0.761611f, 0.631783f, 0.2458f);

        [Header("模型与传感器")]
        [SerializeField] private GameObject vehicleVisualRoot;
        [SerializeField] private bool exportVisualMesh = true;
        [SerializeField] private List<SensorAnchorDefinition> sensors = new List<SensorAnchorDefinition>();

        [Header("导出")]
        [SerializeField] private string exportDirectory = "ConfigExports/ackermann_current";
        [SerializeField, TextArea] private string notes =
            "Unity nominal geometry with the currently validated physical calibration.";

        public Transform BaseLink => baseLink;
        public GameObject VehicleVisualRoot => vehicleVisualRoot;
        public bool ExportVisualMesh => exportVisualMesh;
        public string VehicleId => vehicleId;
        public string ExportDirectory => exportDirectory;
        public IReadOnlyList<SensorAnchorDefinition> Sensors => sensors;

        public string ResolveExportDirectory()
        {
            if (Path.IsPathRooted(exportDirectory))
                return exportDirectory;
#if UNITY_EDITOR
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, exportDirectory));
#else
            return Path.Combine(Application.persistentDataPath, exportDirectory);
#endif
        }

        public VehicleConfigurationData BuildConfiguration()
        {
            GeometrySnapshot geometry = ResolveGeometry();
            List<SensorData> sensorData = new List<SensorData>();
            foreach (SensorAnchorDefinition sensor in sensors)
            {
                SensorData item = new SensorData
                {
                    id = sensor.id,
                    sensorType = sensor.sensorType,
                    model = sensor.model,
                    role = sensor.role,
                    parentFrameId = "base_link",
                    frameId = sensor.frameId,
                    pose = RosFluConverter.RelativePose(baseLink, sensor.anchor),
                    topic = sensor.topic,
                    updateRateHz = sensor.updateRateHz
                };
                if (sensor.measurementFrame != null)
                {
                    item.measurementFrameId = sensor.measurementFrameId;
                    item.measurementFramePose = RosFluConverter.RelativePose(
                        sensor.anchor,
                        sensor.measurementFrame);
                }
                sensorData.Add(item);
            }

            string meshUri =
                $"package://agribot_vehicle_description/generated/{vehicleId}/models/vehicle_visual.obj";
            var configuration = new VehicleConfigurationData
            {
                vehicleId = vehicleId,
                vehicleType = vehicleType,
                displayName = displayName,
                baseFrame = new BaseFrameData
                {
                    frameId = "base_link",
                    reference = baseLinkReference
                },
                geometry = new GeometryData
                {
                    bodyExtents = new ExtentsData
                    {
                        frontM = bodyFrontM,
                        rearM = bodyRearM,
                        leftM = bodyLeftM,
                        rightM = bodyRightM
                    },
                    wheelbaseM = geometry.wheelbase,
                    frontTrackM = geometry.frontTrack,
                    rearTrackM = geometry.rearTrack,
                    wheelRadiusM = geometry.wheelRadius,
                    wheelWidthM = wheelWidthM,
                    maxSteeringAngleRad = maxSteeringAngleRad,
                    minTurningRadiusM = vehicleType == "ackermann"
                        ? geometry.wheelbase / Math.Tan(maxSteeringAngleRad) : 0.5,
                    wheelCenters = geometry.wheels
                },
                navigation = new NavigationData
                {
                    clearance = new ExtentsData
                    {
                        frontM = frontClearanceM,
                        rearM = rearClearanceM,
                        leftM = leftClearanceM,
                        rightM = rightClearanceM
                    },
                    maxLinearSpeedMps = navigationMaxLinearSpeedMps,
                    maxAngularSpeedRadps = navigationMaxAngularSpeedRadps,
                    allowReverse = allowReverse,
                    inflationRadiusM = inflationRadiusM,
                    costScalingFactor = costScalingFactor
                },
                chassis = new ChassisData
                {
                    commandMaxLinearSpeedMps = chassisMaxLinearSpeedMps,
                    commandMaxAngularSpeedRadps = chassisMaxAngularSpeedRadps,
                    minimumMotionSpeedMps = minimumMotionSpeedMps
                },
                dynamics = new DynamicsData
                {
                    massKg = massKg,
                    centerOfMass = RosFluConverter.ToDoubleArray(centerOfMass),
                    inertia = new InertiaData
                    {
                        ixx = inertiaDiagonal.x,
                        iyy = inertiaDiagonal.y,
                        izz = inertiaDiagonal.z
                    },
                    maxAccelerationMps2 = maxAccelerationMps2,
                    maxSteeringRateRadps = maxSteeringRateRadps
                },
                collision = new CollisionData
                {
                    center = RosFluConverter.ToDoubleArray(collisionCenter),
                    size = RosFluConverter.ToDoubleArray(collisionSize)
                },
                visuals = new[]
                {
                    new VisualData
                    {
                        name = "vehicle_body",
                        meshUri = meshUri,
                        pose = RosFluConverter.CreatePose(Vector3.zero, Vector3.zero)
                    }
                },
                sensors = sensorData.ToArray(),
                manipulators = BuildManipulators(),
                metadata = new MetadataData
                {
                    profileVersion = profileVersion,
                    calibrationState = calibrationState,
                    notes = notes
                }
            };
            SimulationAppearanceLibrary.ApplyContactGeometry(configuration);
            return configuration;
        }

        private ManipulatorData[] BuildManipulators()
        {
            if (vehicleId != "ackermann_current" && vehicleId != "unity_car3")
                return Array.Empty<ManipulatorData>();
            Transform hand = Array.Find(Resources.FindObjectsOfTypeAll<Transform>(), candidate =>
                candidate.gameObject.scene == gameObject.scene && candidate.name == "Hand" &&
                candidate.gameObject.activeInHierarchy &&
                (vehicleId == "ackermann_current" || candidate.IsChildOf(vehicleVisualRoot.transform)));
            if (hand == null) return Array.Empty<ManipulatorData>();
            Transform mount = VehicleManipulatorPreview.Attach(hand);
            if (Vector3.Dot(mount.up, baseLink.up) < 0.98f)
                throw new InvalidOperationException("机械臂底座必须竖直安装，请检查 Hand 的安装方向。");
            return new[] { new ManipulatorData
            {
                pose = RosFluConverter.RelativePose(baseLink, mount),
                demoEnabled = mount.GetComponent<VehicleManipulatorPreview>().DemoEnabled
            } };
        }

        public bool ValidateConfiguration(out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();
            if (baseLink == null)
                errors.Add("未设置 base_link 锚点。");
            if (vehicleVisualRoot == null)
                errors.Add("未设置车体可视模型根节点。");
            if (string.IsNullOrWhiteSpace(vehicleId))
                errors.Add("vehicleId 不能为空。");

            Transform[] wheels =
            {
                frontLeftWheelCenter,
                frontRightWheelCenter,
                rearLeftWheelCenter,
                rearRightWheelCenter
            };
            foreach (Transform wheel in wheels)
            {
                if (wheel == null)
                    errors.Add("四个车轮中心锚点必须全部设置。");
            }
            if (baseLink != null)
            {
                Vector3 scale = baseLink.lossyScale;
                if (Mathf.Abs(scale.x - 1f) > 1e-3f ||
                    Mathf.Abs(scale.y - 1f) > 1e-3f ||
                    Mathf.Abs(scale.z - 1f) > 1e-3f)
                {
                    errors.Add("base_link 的世界缩放必须为 (1,1,1)。");
                }
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> frames = new HashSet<string>(StringComparer.Ordinal);
            foreach (SensorAnchorDefinition sensor in sensors)
            {
                if (sensor == null || string.IsNullOrWhiteSpace(sensor.id))
                {
                    errors.Add("存在未填写 ID 的传感器。");
                    continue;
                }
                if (!ids.Add(sensor.id))
                    errors.Add($"传感器 ID 重复：{sensor.id}");
                if (sensor.anchor == null)
                    errors.Add($"传感器 {sensor.id} 未设置锚点。");
                if (string.IsNullOrWhiteSpace(sensor.frameId) || !frames.Add(sensor.frameId))
                    errors.Add($"传感器 {sensor.id} 的 frameId 为空或重复。");
                if (sensor.measurementFrame != null &&
                    (string.IsNullOrWhiteSpace(sensor.measurementFrameId) ||
                     !frames.Add(sensor.measurementFrameId)))
                {
                    errors.Add($"传感器 {sensor.id} 的测量坐标系为空或重复。");
                }
            }

            foreach (float value in new[]
                     {
                         bodyFrontM, bodyRearM, bodyLeftM, bodyRightM,
                         wheelbaseM, frontTrackM, rearTrackM, wheelRadiusM,
                         wheelWidthM, maxSteeringAngleRad,
                         navigationMaxLinearSpeedMps, chassisMaxLinearSpeedMps
                     })
            {
                if (value <= 0f)
                    errors.Add("车体、运动学和速度参数必须为正数。");
            }
            if (navigationMaxLinearSpeedMps > chassisMaxLinearSpeedMps)
                errors.Add("导航最大速度不能超过底盘指令上限。");
            if (navigationMaxAngularSpeedRadps > chassisMaxAngularSpeedRadps)
                errors.Add("导航最大角速度不能超过底盘指令上限。");

            if (errors.Count == 0)
            {
                GeometrySnapshot geometry = ResolveGeometry();
                float curvatureLimit = (float)(
                    navigationMaxLinearSpeedMps /
                    (geometry.wheelbase / Math.Tan(maxSteeringAngleRad)));
                if (navigationMaxAngularSpeedRadps > curvatureLimit + 1e-3f)
                {
                    warnings.Add(
                        $"导航最大角速度超过最大车速下的曲率限制 {curvatureLimit:F6} rad/s。");
                }
            }
            return errors.Count == 0;
        }

        public string ExportJson()
        {
            if (!ValidateConfiguration(out List<string> errors, out _))
                throw new InvalidOperationException(string.Join("\n", errors));
            string directory = ResolveExportDirectory();
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "vehicle_config.json");
            File.WriteAllText(path, SimulationAppearanceLibrary.Serialize(BuildConfiguration()));
            Debug.Log($"ROS 车辆配置已导出：{path}", this);
            return path;
        }

        public VehicleConfigurationExportResult ExportConfigurationBundle()
        {
            if (!ValidateConfiguration(
                    out List<string> errors,
                    out List<string> warnings))
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }

            foreach (string warning in warnings)
                Debug.LogWarning(warning, this);

            string jsonPath = ExportJson();
            string meshPath = null;
            int exportedMeshCount = 0;
            if (exportVisualMesh)
            {
                meshPath = Path.Combine(
                    Path.GetDirectoryName(jsonPath),
                    "vehicle_visual.obj");
                if (!SimulationAppearanceLibrary.CopyBodyAssets(vehicleId, Path.GetDirectoryName(jsonPath), out exportedMeshCount))
                    exportedMeshCount = VehicleMeshExporter.Export(
                        vehicleVisualRoot,
                        baseLink,
                        sensors,
                        meshPath,
                        wheelVisualRoots);
                exportedMeshCount += VehicleMeshExporter.ExportSensorVisuals(sensors, Path.GetDirectoryName(jsonPath));
            }

            return new VehicleConfigurationExportResult(
                jsonPath,
                meshPath,
                exportedMeshCount);
        }

        public void ConfigureAckermannBaseline(
            Transform configuredBaseLink,
            GameObject configuredVisualRoot,
            Transform frontLeft,
            Transform frontRight,
            Transform rearLeft,
            Transform rearRight,
            List<SensorAnchorDefinition> configuredSensors)
        {
            baseLink = configuredBaseLink;
            vehicleVisualRoot = configuredVisualRoot;
            frontLeftWheelCenter = frontLeft;
            frontRightWheelCenter = frontRight;
            rearLeftWheelCenter = rearLeft;
            rearRightWheelCenter = rearRight;
            sensors = configuredSensors ?? new List<SensorAnchorDefinition>();
        }

        private GeometrySnapshot ResolveGeometry()
        {
            WheelData[] wheels =
            {
                BuildWheel("front_left", frontLeftWheelCenter, vehicleType == "ackermann", vehicleType != "ackermann"),
                BuildWheel("front_right", frontRightWheelCenter, vehicleType == "ackermann", vehicleType != "ackermann"),
                BuildWheel("rear_left", rearLeftWheelCenter, false, true),
                BuildWheel("rear_right", rearRightWheelCenter, false, true)
            };
            if (wheelRadii != null)
                for (int i = 0; i < wheels.Length; i++)
                { wheels[i].radiusM = wheelRadii[i]; wheels[i].widthM = wheelWidths[i]; }
            float resolvedWheelbase = wheelbaseM;
            float resolvedFrontTrack = frontTrackM;
            float resolvedRearTrack = rearTrackM;
            float resolvedWheelRadius = wheelRadiusM;
            if (deriveGeometryFromWheelAnchors && baseLink != null &&
                frontLeftWheelCenter != null && frontRightWheelCenter != null &&
                rearLeftWheelCenter != null && rearRightWheelCenter != null)
            {
                resolvedWheelbase = (float)(
                    (wheels[0].position[0] + wheels[1].position[0] -
                     wheels[2].position[0] - wheels[3].position[0]) * 0.5);
                resolvedFrontTrack = (float)Math.Abs(wheels[0].position[1] - wheels[1].position[1]);
                resolvedRearTrack = (float)Math.Abs(wheels[2].position[1] - wheels[3].position[1]);
            }

            Component carChange = GetComponent("CarChange");
            if (carChange == null && vehicleVisualRoot != null)
                carChange = vehicleVisualRoot.GetComponent("CarChange");
            if (Application.isPlaying && carChange != null)
            {
                resolvedWheelbase = ReadPositiveProperty(carChange, "Cur_wheelDis", resolvedWheelbase);
                float runtimeTrack = ReadPositiveProperty(carChange, "CurWheelTrack", 0f);
                if (runtimeTrack > 0f)
                {
                    resolvedFrontTrack = runtimeTrack;
                    resolvedRearTrack = runtimeTrack;
                }
                resolvedWheelRadius = ReadPositiveProperty(carChange, "CurWheelRadius", resolvedWheelRadius);
            }
            return new GeometrySnapshot(
                resolvedWheelbase,
                resolvedFrontTrack,
                resolvedRearTrack,
                resolvedWheelRadius,
                wheels);
        }

        public void ConfigureSceneVehicle(string id, string label, string type,
            Transform reference, GameObject visualRoot, Transform[] centers,
            Transform[] visualWheels, float[] radii, float[] widths, Bounds bounds,
            Bounds chassisBounds, List<SensorAnchorDefinition> sensorDefinitions)
        {
            vehicleId = id; displayName = label; vehicleType = type;
            baseLinkReference = type == "ackermann" ? "rear_axle_midpoint" : "track_center_midpoint";
            exportDirectory = "ConfigExports/" + id;
            calibrationState = "unity_nominal_simulation_only";
            notes = "Geometry and sensor poses derived from the selected Unity vehicle; not a physical calibration.";
            ConfigureAckermannBaseline(reference, visualRoot, centers[0], centers[1], centers[2], centers[3], sensorDefinitions);
            wheelVisualRoots = visualWheels; wheelRadii = radii; wheelWidths = widths;
            bodyFrontM = bounds.max.x; bodyRearM = -bounds.min.x;
            bodyLeftM = bounds.max.y; bodyRightM = -bounds.min.y;
            wheelRadiusM = (radii[2] + radii[3]) * 0.5f;
            wheelWidthM = (widths[2] + widths[3]) * 0.5f;
            wheelbaseM = Vector3.Distance((centers[0].position + centers[1].position) * 0.5f,
                (centers[2].position + centers[3].position) * 0.5f);
            frontTrackM = Vector3.Distance(centers[0].position, centers[1].position);
            rearTrackM = Vector3.Distance(centers[2].position, centers[3].position);
            collisionCenter = chassisBounds.center; collisionSize = chassisBounds.size;
            centerOfMass = collisionCenter;
            // New CAD vehicles have no measured steering limits; do not inherit
            // the calibrated Car0 limit into their simulation-only profiles.
            maxSteeringAngleRad = 35f * Mathf.Deg2Rad;
            Vector3 size = chassisBounds.size;
            inertiaDiagonal = massKg / 12f * new Vector3(size.y * size.y + size.z * size.z,
                size.x * size.x + size.z * size.z, size.x * size.x + size.y * size.y);
            navigationMaxAngularSpeedRadps = type == "ackermann"
                ? navigationMaxLinearSpeedMps * Mathf.Tan(maxSteeringAngleRad) / wheelbaseM : 0.6f;
            chassisMaxAngularSpeedRadps = Mathf.Max(navigationMaxAngularSpeedRadps, 0.7f);
            inflationRadiusM = 0.5f; costScalingFactor = 3f;
        }

        private WheelData BuildWheel(string id, Transform anchor, bool steering, bool driven)
        {
            PoseData pose = baseLink != null && anchor != null
                ? RosFluConverter.RelativePose(baseLink, anchor)
                : new PoseData();
            return new WheelData
            {
                id = id,
                position = pose.xyz,
                steering = steering,
                driven = driven
            };
        }

        private static float ReadPositiveProperty(Component target, string propertyName, float fallback)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
                return fallback;
            object raw = property.GetValue(target);
            if (raw == null ||
                !float.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value) ||
                value <= 0f)
            {
                return fallback;
            }
            return value;
        }

        private readonly struct GeometrySnapshot
        {
            public readonly float wheelbase;
            public readonly float frontTrack;
            public readonly float rearTrack;
            public readonly float wheelRadius;
            public readonly WheelData[] wheels;

            public GeometrySnapshot(
                float wheelbase,
                float frontTrack,
                float rearTrack,
                float wheelRadius,
                WheelData[] wheels)
            {
                this.wheelbase = wheelbase;
                this.frontTrack = frontTrack;
                this.rearTrack = rearTrack;
                this.wheelRadius = wheelRadius;
                this.wheels = wheels;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (baseLink == null)
                return;
            const float axisLength = 0.2f;
            Vector3 origin = baseLink.position;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin, origin + baseLink.forward * axisLength);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, origin - baseLink.right * axisLength);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(origin, origin + baseLink.up * axisLength);
            Gizmos.color = Color.yellow;
            foreach (SensorAnchorDefinition sensor in sensors)
            {
                if (sensor != null && sensor.anchor != null)
                    Gizmos.DrawWireSphere(sensor.anchor.position, 0.025f);
            }
        }
    }

    public sealed class VehicleConfigurationExportResult
    {
        public string JsonPath { get; }
        public string MeshPath { get; }
        public int ExportedMeshCount { get; }

        public VehicleConfigurationExportResult(
            string jsonPath,
            string meshPath,
            int exportedMeshCount)
        {
            JsonPath = jsonPath;
            MeshPath = meshPath;
            ExportedMeshCount = exportedMeshCount;
        }
    }
}
