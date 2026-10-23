using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Agribot.VehicleConfiguration.Editor
{
    public static class AckermannConfigurationBootstrap
    {
        private const string MainScene = "Assets/Scenes/scene2.unity";

        [MenuItem("农机配置/初始化当前阿克曼配置")]
        public static void ConfigureCurrentScene()
        {
            VehicleConfigurationAuthoring authoring = Configure(false);
            Selection.activeObject = authoring.gameObject;
            EditorGUIUtility.PingObject(authoring.gameObject);
            EditorUtility.DisplayDialog(
                "初始化完成",
                "已建立后轴中心 base_link、四轮锚点和传感器坐标锚点。请在 Inspector 校验后导出。",
                "确定");
        }

        [MenuItem("农机配置/校验并导出当前阿克曼配置")]
        public static void ConfigureAndExportFromMenu()
        {
            VehicleConfigurationAuthoring authoring = Configure(false);
            Export(authoring);
            EditorUtility.DisplayDialog(
                "导出完成",
                $"阿克曼配置已导出到：\n{authoring.ResolveExportDirectory()}",
                "确定");
        }

        // Unity batch mode entry point used by ROS integration checks.
        public static void ConfigureAndExport()
        {
            try
            {
                VehicleConfigurationAuthoring authoring = Configure(true);
                Export(authoring);
                Debug.Log($"AGRIBOT_EXPORT_OK={authoring.ResolveExportDirectory()}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(2);
            }
        }

        private static VehicleConfigurationAuthoring Configure(bool batchMode)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScene)
                scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);

            Transform vehicleRoot = RequireSceneTransform("VehicleRoot");
            GameObject visualRoot = RequireSceneTransform("Car0").gameObject;
            Transform frontLeft = RequireSceneTransform("FLcenter");
            Transform frontRight = RequireSceneTransform("FRcenter");
            Transform rearLeft = RequireSceneTransform("RLcenter");
            Transform rearRight = RequireSceneTransform("RRcenter");

            Transform baseLink = GetOrCreateChild(vehicleRoot, "ROS_base_link");
            Vector3 rearMidpoint = (rearLeft.position + rearRight.position) * 0.5f;
            Vector3 frontMidpoint = (frontLeft.position + frontRight.position) * 0.5f;
            Vector3 forward = Vector3.ProjectOnPlane(frontMidpoint - rearMidpoint, Vector3.up).normalized;
            Vector3 left = Vector3.ProjectOnPlane(frontLeft.position - frontRight.position, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.99f || left.sqrMagnitude < 0.99f)
                throw new InvalidOperationException("无法根据四轮中心建立 base_link 坐标系。");
            Vector3 up = Vector3.Cross(left, forward).normalized;
            if (Vector3.Dot(up, Vector3.up) < 0f)
                up = -up;
            baseLink.SetPositionAndRotation(rearMidpoint, Quaternion.LookRotation(forward, up));
            baseLink.localScale = Vector3.one;

            Transform sensorRoot = GetOrCreateChild(baseLink, "ROS_sensor_anchors");
            sensorRoot.localPosition = Vector3.zero;
            sensorRoot.localRotation = Quaternion.identity;
            sensorRoot.localScale = Vector3.one;

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
            Transform cameraOptical = GetOrCreateChild(camera, "right_camera_optical_frame");
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
                authoring = Undo.AddComponent<VehicleConfigurationAuthoring>(vehicleRoot.gameObject);
            authoring.ConfigureAckermannBaseline(
                baseLink,
                visualRoot,
                frontLeft,
                frontRight,
                rearLeft,
                rearRight,
                new List<SensorAnchorDefinition>
                {
                    Sensor("imu", "imu", "Hipnuc N300Pro", "inertial", "imu_link", imu, null, null, "/imu/data", 100f),
                    Sensor("lidar", "lidar", "LSLiDAR C16 V4", "mapping", "lidar_link", lidar, null, null, "/lidar/points", 10f),
                    Sensor("right_camera", "camera", "Hikrobot MV-CU013-A0UC", "visual_odometry", "right_camera_link", camera, "right_camera_optical_frame", cameraOptical, "/camera/rgb/image_raw", 10f),
                    Sensor("rtk_master", "gnss_antenna", "Dual-antenna RTK", "master_left", "rtk_link", rtkMaster, null, null, "/rtk/fix", 10f),
                    Sensor("rtk_secondary", "gnss_antenna", "Dual-antenna RTK", "secondary_right", "rtk_secondary_link", rtkSecondary, null, null, "/rtk/heading", 10f)
                });
            AckermannRuntimeConfiguration.BindSensorVisuals(authoring);
            EditorUtility.SetDirty(authoring);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new IOException($"无法保存场景：{scene.path}");
            if (!batchMode)
                AssetDatabase.SaveAssets();
            return authoring;
        }

        private static void Export(VehicleConfigurationAuthoring authoring)
        {
            if (!authoring.ValidateConfiguration(
                    out List<string> errors,
                    out List<string> warnings))
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
            foreach (string warning in warnings)
                Debug.LogWarning(warning, authoring);
            string jsonPath = authoring.ExportJson();
            if (authoring.ExportVisualMesh)
            {
                ObjMeshExporter.Export(
                    authoring.VehicleVisualRoot,
                    authoring.BaseLink,
                    authoring.Sensors,
                    Path.Combine(Path.GetDirectoryName(jsonPath), "vehicle_visual.obj"));
                VehicleMeshExporter.ExportSensorVisuals(authoring.Sensors, Path.GetDirectoryName(jsonPath));
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
            Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
            child = created.transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform RequireSceneTransform(string name)
        {
            foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (candidate.name == name && candidate.gameObject.scene.IsValid())
                    return candidate;
            }
            throw new InvalidOperationException($"场景中缺少对象：{name}");
        }
    }
}
