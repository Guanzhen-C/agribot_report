using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    /// <summary>
    /// Builds the export authoring data from the currently configured Game scene.
    /// This keeps ROS export independent from editor-only initialization menus.
    /// </summary>
    public static class AckermannRuntimeConfiguration
    {
        public static VehicleConfigurationAuthoring BuildOrRefresh()
        {
            Transform vehicleRoot = RequireSceneTransform("VehicleRoot");
            var visual = Array.Find(vehicleRoot.GetComponentsInChildren<Transform>(true),
                item => item.name == "Car0Visual" || item.name == "Car0");
            return BuildOrRefresh(visual.gameObject, vehicleRoot);
        }

        public static VehicleConfigurationAuthoring BuildOrRefresh(GameObject visualRoot, Transform vehicleRoot)
        {
            var parts = vehicleRoot.GetComponentsInChildren<Transform>(true);
            Transform Require(string name) => Array.Find(parts, item => item.name == name)
                ?? throw new InvalidOperationException("Car0 缺少几何节点：" + name);
            Transform frontLeft = Require("FLcenter");
            Transform frontRight = Require("FRcenter");
            Transform rearLeft = Require("RLcenter");
            Transform rearRight = Require("RRcenter");

            Transform baseLink = GetOrCreateChild(vehicleRoot, "ROS_base_link");
            Vector3 rearMidpoint = (rearLeft.position + rearRight.position) * 0.5f;
            Vector3 frontMidpoint = (frontLeft.position + frontRight.position) * 0.5f;
            Vector3 forward = Vector3.ProjectOnPlane(
                frontMidpoint - rearMidpoint,
                Vector3.up).normalized;
            Vector3 left = Vector3.ProjectOnPlane(
                frontLeft.position - frontRight.position,
                Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.99f || left.sqrMagnitude < 0.99f)
            {
                throw new InvalidOperationException(
                    "无法根据当前四轮中心建立 base_link 坐标系。");
            }

            Vector3 up = Vector3.Cross(left, forward).normalized;
            if (Vector3.Dot(up, Vector3.up) < 0f)
                up = -up;
            baseLink.SetPositionAndRotation(
                rearMidpoint,
                Quaternion.LookRotation(forward, up));
            baseLink.localScale = Vector3.one;

            Transform sensorRoot = GetOrCreateChild(baseLink, "ROS_sensor_anchors");
            sensorRoot.localPosition = Vector3.zero;
            sensorRoot.localRotation = Quaternion.identity;
            sensorRoot.localScale = Vector3.one;

            // These are the calibrated measurement origins. The visual sensor
            // meshes can have different model origins and must not define TF.
            Transform imu = CreatePoseAnchor(
                sensorRoot,
                "imu_link",
                new Vector3(0.1425f, 0f, 0.143f),
                new Vector3(0.000572424f, -0.009139547f, -0.000002616f));
            Transform lidar = CreatePoseAnchor(
                sensorRoot,
                "lidar_link",
                new Vector3(0.48f, 0f, 0.233f),
                new Vector3(-0.007648487f, -0.001835661f, 0.000007020f));
            Transform camera = CreatePoseAnchor(
                sensorRoot,
                "right_camera_link",
                new Vector3(0.5755f, 0f, 0.154f),
                Vector3.zero);
            Transform cameraOptical = GetOrCreateChild(
                camera,
                "right_camera_optical_frame");
            RosFluConverter.SetLocalPoseFromRos(
                cameraOptical,
                Vector3.zero,
                new Vector3(-Mathf.PI * 0.5f, 0f, -Mathf.PI * 0.5f));
            Transform rtkMaster = CreatePoseAnchor(
                sensorRoot,
                "rtk_link",
                new Vector3(0.1425f, 0.2952585f, 0.78476f),
                Vector3.zero);
            Transform rtkSecondary = CreatePoseAnchor(
                sensorRoot,
                "rtk_secondary_link",
                new Vector3(0.1425f, -0.2952585f, 0.78476f),
                Vector3.zero);

            VehicleConfigurationAuthoring authoring =
                vehicleRoot.GetComponent<VehicleConfigurationAuthoring>();
            if (authoring == null)
            {
                authoring = vehicleRoot.gameObject
                    .AddComponent<VehicleConfigurationAuthoring>();
            }

            authoring.ConfigureAckermannBaseline(
                baseLink,
                visualRoot,
                frontLeft,
                frontRight,
                rearLeft,
                rearRight,
                new List<SensorAnchorDefinition>
                {
                    Sensor("imu", "imu", "Hipnuc N300Pro", "inertial",
                        "imu_link", imu, null, null, "/imu/data", 100f),
                    Sensor("lidar", "lidar", "LSLiDAR C16 V4", "mapping",
                        "lidar_link", lidar, null, null, "/lidar/points", 10f),
                    Sensor("right_camera", "camera", "Hikrobot MV-CU013-A0UC",
                        "visual_odometry", "right_camera_link", camera,
                        "right_camera_optical_frame", cameraOptical,
                        "/camera/rgb/image_raw", 10f),
                    Sensor("rtk_master", "gnss_antenna", "Dual-antenna RTK",
                        "master_left", "rtk_link", rtkMaster, null, null,
                        "/rtk/fix", 10f),
                    Sensor("rtk_secondary", "gnss_antenna", "Dual-antenna RTK",
                        "secondary_right", "rtk_secondary_link", rtkSecondary,
                        null, null, "/rtk/heading", 10f)
                });

            BindSensorVisuals(authoring);
            return authoring;
        }

        public static void BindSensorVisuals(VehicleConfigurationAuthoring authoring)
        {
            var roots = new Dictionary<string, string>
            {
                { "imu", "Imu_box" },
                { "lidar", "RADAR" },
                { "right_camera", "CarCamera" },
                { "rtk_master", "RTK" },
                { "rtk_secondary", "RTK" }
            };
            Transform[] children = authoring.VehicleVisualRoot.GetComponentsInChildren<Transform>(true);
            foreach (SensorAnchorDefinition sensor in authoring.Sensors)
            {
                if (!roots.TryGetValue(sensor.id, out string rootName))
                    continue;
                sensor.visualRoot = Array.Find(children, child => child.name == rootName);
            }
        }

        private static SensorAnchorDefinition Sensor(
            string id,
            string sensorType,
            string model,
            string role,
            string frameId,
            Transform anchor,
            string measurementFrameId,
            Transform measurementFrame,
            string topic,
            float updateRateHz)
        {
            return new SensorAnchorDefinition
            {
                id = id,
                sensorType = sensorType,
                model = model,
                role = role,
                frameId = frameId,
                anchor = anchor,
                measurementFrameId = measurementFrameId,
                measurementFrame = measurementFrame,
                topic = topic,
                updateRateHz = updateRateHz
            };
        }

        private static Transform CreatePoseAnchor(
            Transform parent,
            string name,
            Vector3 xyz,
            Vector3 rpy)
        {
            Transform anchor = GetOrCreateChild(parent, name);
            RosFluConverter.SetLocalPoseFromRos(anchor, xyz, rpy);
            return anchor;
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                return child;

            GameObject created = new GameObject(name);
            child = created.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform RequireSceneTransform(string name)
        {
            Transform inactiveMatch = null;
            foreach (Transform candidate in
                     Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate.name != name ||
                    !candidate.gameObject.scene.IsValid() ||
                    !candidate.gameObject.scene.isLoaded)
                {
                    continue;
                }

                if (candidate.gameObject.activeInHierarchy)
                    return candidate;
                inactiveMatch ??= candidate;
            }

            if (inactiveMatch != null)
                return inactiveMatch;
            throw new InvalidOperationException($"场景中缺少对象：{name}");
        }
    }
}
