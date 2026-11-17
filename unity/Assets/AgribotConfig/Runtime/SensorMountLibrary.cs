using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public static class SensorMountLibrary
    {
        [Serializable] public sealed class Mount { public string id, frameId; public PoseData pose; }
        [Serializable] private sealed class Catalog
        { public int schemaVersion; public double rtkSurfaceOffsetM; public Mount[] tracked, ackermannRtk; }
        private static Catalog catalog;
        private static Catalog Data => catalog ??= JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("HilSensors/mounts").text);
        public static IReadOnlyList<Mount> Tracked => Data.tracked;
        public static IReadOnlyList<Mount> AckermannRtk => Data.ackermannRtk;

        private static GameObject Sensor(string id)
        {
            string folder = "HilSensors/" + id;
            var manifest = JsonUtility.FromJson<FarmVehicleLibrary.AssetManifest>(Resources.Load<TextAsset>(folder + "/manifest").text);
            return FarmVehicleLibrary.LoadPreview(manifest, folder + "/preview");
        }

        private static void SetPose(Transform target, PoseData pose) => RosFluConverter.SetLocalPoseFromRos(target,
            new Vector3((float)pose.xyz[0], (float)pose.xyz[1], (float)pose.xyz[2]),
            new Vector3((float)pose.rpy[0], (float)pose.rpy[1], (float)pose.rpy[2]));

        private static void Clear(Transform root)
        {
            foreach (Transform child in root.Cast<Transform>().ToArray())
            {
                child.gameObject.SetActive(false);
                foreach (var part in child.GetComponentsInChildren<Transform>(true))
                    if (part.name == "Mushroom1" || part.name == "Mushroom2") part.name = "Legacy_" + part.name;
                if (Application.isPlaying) UnityEngine.Object.Destroy(child.gameObject);
                else UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            foreach (var renderer in root.GetComponents<MeshRenderer>()) renderer.enabled = false;
        }

        public static Transform CreateRtk(Transform parent, PoseData master, PoseData secondary, string name = "RTK")
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            SetPose(root, master);
            for (int index = 0; index < 2; index++)
            {
                var antenna = new GameObject("Mushroom" + (index + 1)).transform;
                antenna.SetParent(parent, false);
                SetPose(antenna, index == 0 ? master : secondary);
                antenna.SetParent(root, true);
                Sensor("rtk_antenna").transform.SetParent(antenna, false);
            }
            return root;
        }

        private static void ReplaceRtk(Transform target, Transform reference, Mount[] mounts)
        {
            if (target.Find("ShortStemRTK") != null) return;
            Clear(target);
            target.SetParent(reference, false);
            SetPose(target, mounts[0].pose);
            var model = CreateRtk(reference, mounts[0].pose, mounts[1].pose, "ShortStemRTK");
            model.SetParent(target, true);
        }

        public static void ApplyAckermann(Transform car, Transform reference)
        {
            var target = car.GetComponentsInChildren<Transform>(true).First(t => t.name == "RTK");
            ReplaceRtk(target, reference, Data.ackermannRtk);
        }

        public static void ApplyTracked(Transform car, Transform reference)
        {
            foreach (var mount in Data.tracked.Where(m => !m.id.StartsWith("rtk_", StringComparison.Ordinal)))
            {
                string name = mount.id == "imu" ? "Imu_box" : mount.id == "lidar" ? "RADAR" : "CarCamera";
                var target = car.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
                Clear(target);
                target.SetParent(reference, false);
                SetPose(target, mount.pose);
                Sensor(mount.id).transform.SetParent(target, false);
            }
            var rtk = car.GetComponentsInChildren<Transform>(true).First(t => t.name == "RTK");
            ReplaceRtk(rtk, reference, Data.tracked.Where(m => m.id.StartsWith("rtk_", StringComparison.Ordinal)).ToArray());
        }

    }
}
