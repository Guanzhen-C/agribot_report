using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public static class SceneVehicleConfiguration
    {
        private static readonly string[] CenterNames = { "FLcenter", "FRcenter", "RLcenter", "RRcenter" };
        private static readonly string[] WheelNames = { "FLwheel", "FRwheel", "RLwheel", "RRwheel" };

        public static VehicleConfigurationAuthoring Build(GameObject car)
        {
            if (car == null || !car.activeInHierarchy) throw new InvalidOperationException("请先选择车辆。");
            bool tracked = car.name == "Car3";
            Transform[] children = car.GetComponentsInChildren<Transform>(true);
            Transform[] centers = CenterNames.Select(name => Require(children, name)).ToArray();
            Vector3 rear = (centers[2].position + centers[3].position) * 0.5f;
            Vector3 front = (centers[0].position + centers[1].position) * 0.5f;
            Vector3 forward = Vector3.ProjectOnPlane(front - rear, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.99f) throw new InvalidOperationException("四轮中心无法确定车头方向。");
            Transform reference = car.transform.Find("ROS_base_link");
            if (reference == null) { reference = new GameObject("ROS_base_link").transform; reference.SetParent(car.transform); }
            reference.SetPositionAndRotation(tracked ? (front + rear) * 0.5f : rear,
                Quaternion.LookRotation(forward, Vector3.up));
            reference.localScale = Vector3.one;
            // Interpret left/right spatially; one upstream tractor has reversed rear labels.
            for (int i = 0; i < 4; i += 2)
                if (reference.InverseTransformPoint(centers[i].position).x > reference.InverseTransformPoint(centers[i + 1].position).x)
                    (centers[i], centers[i + 1]) = (centers[i + 1], centers[i]);

            Transform[] wheels = new Transform[4];
            float[] radii = new float[4], widths = new float[4];
            for (int i = 0; i < 4; i++)
            {
                wheels[i] = centers[i].parent.name.Trim().EndsWith("wheel", StringComparison.Ordinal)
                    ? centers[i].parent : Require(children, WheelNames[i]);
                Bounds wheelBounds = MeshBounds(wheels[i], reference, null);
                radii[i] = Mathf.Max(0.025f, wheelBounds.size.z * 0.5f);
                widths[i] = Mathf.Max(0.025f, wheelBounds.size.y);
            }

            Transform body = Require(children, car.name.ToLowerInvariant());
            Bounds bounds = MeshBounds(body, reference, null);
            Bounds chassis = MeshBounds(body, reference, wheels);
            if (tracked)
            {
                // The supplied virtual rollers sit inside a rigid visual track shell.
                // Use rollers reaching its contact plane; the shell remains visual-only.
                for (int i = 0; i < 4; i++)
                    radii[i] = Mathf.Max(0.05f, (float)RosFluConverter.RelativePose(reference, centers[i]).xyz[2] - bounds.min.z);
            }
            float lower = Mathf.Max(chassis.min.z, bounds.min.z + 0.08f);
            chassis.SetMinMax(new Vector3(chassis.min.x, chassis.min.y, lower), chassis.max);
            var sensors = new List<SensorAnchorDefinition>();
            foreach (var item in new[] {
                ("Imu_box", "imu", "imu", "N300", "inertial", "imu_link", "/imu/data", 200f),
                ("RADAR", "lidar", "lidar", "C16", "perception", "lidar_link", "/lidar/points", 10f),
                ("CarCamera", "right_camera", "camera", "Hikrobot", "visual", "right_camera_link", "/camera/rgb/image_raw", 10f) })
            {
                Transform visual = Require(children, item.Item1);
                Transform anchor = Child(reference, item.Item6);
                anchor.SetPositionAndRotation(visual.position, reference.rotation);
                var sensor = new SensorAnchorDefinition { id = item.Item2, sensorType = item.Item3, model = item.Item4,
                    role = item.Item5, frameId = item.Item6, topic = item.Item7, updateRateHz = item.Item8,
                    anchor = anchor, visualRoot = visual };
                if (item.Item3 == "camera")
                {
                    sensor.measurementFrameId = "right_camera_optical_frame";
                    sensor.measurementFrame = Child(anchor, sensor.measurementFrameId);
                    RosFluConverter.SetLocalPoseFromRos(sensor.measurementFrame, Vector3.zero,
                        new Vector3(-Mathf.PI / 2, 0, -Mathf.PI / 2));
                }
                sensors.Add(sensor);
            }
            Transform rtk = Require(children, "RTK");
            Transform[] antennas = rtk.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "Mushroom1" || t.name == "Mushroom2")
                .OrderBy(t => reference.InverseTransformPoint(t.position).x).ToArray();
            if (antennas.Length != 2) throw new InvalidOperationException("RTK需要两个天线中心：Mushroom1、Mushroom2。");
            for (int i = 0; i < 2; i++)
            {
                string id = i == 0 ? "rtk_master" : "rtk_secondary";
                string frame = i == 0 ? "rtk_link" : "rtk_secondary_link";
                Transform anchor = Child(reference, frame);
                anchor.SetPositionAndRotation(antennas[i].position, reference.rotation);
                sensors.Add(new SensorAnchorDefinition { id = id, sensorType = "gnss_antenna", model = "RTK",
                    role = i == 0 ? "master_left" : "secondary_right", frameId = frame, anchor = anchor,
                    visualRoot = rtk, topic = i == 0 ? "/rtk/fix" : "/rtk/secondary", updateRateHz = 10f });
            }
            var authoring = car.GetComponent<VehicleConfigurationAuthoring>() ?? car.AddComponent<VehicleConfigurationAuthoring>();
            authoring.ConfigureSceneVehicle("unity_" + car.name.ToLowerInvariant(), car.name,
                tracked ? "tracked" : "ackermann", reference, car, centers, wheels, radii, widths, bounds, chassis, sensors);
            return authoring;
        }

        private static Transform Require(IEnumerable<Transform> children, string name) =>
            children.FirstOrDefault(t => t.name.Trim() == name) ?? throw new InvalidOperationException("车辆缺少节点：" + name);

        private static Transform Child(Transform parent, string name)
        {
            Transform value = parent.Find(name);
            if (value != null) return value;
            value = new GameObject(name).transform; value.SetParent(parent, false); return value;
        }

        private static Bounds MeshBounds(Transform root, Transform reference, Transform[] excluded)
        {
            bool initialized = false;
            Bounds result = new Bounds();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || (excluded != null && excluded.Any(t => filter.transform.IsChildOf(t)))) continue;
                Matrix4x4 matrix = reference.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = RosFluConverter.UnityToRos(matrix.MultiplyPoint3x4(vertex));
                    if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                    else result.Encapsulate(point);
                }
            }
            if (!initialized) throw new InvalidOperationException("车辆网格不可读或为空：" + root.name);
            return result;
        }
    }
}
