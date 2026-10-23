using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    // Authored scene vehicles keep their measurement anchors and equipment UI.
    // An explicitly inspected contact profile can also replace legacy track rollers.
    public static class SimulationAppearanceLibrary
    {
        private const string PreviewName = "ROS_SimulationAppearance";
        private static readonly string[] EquipmentNames = { "RADAR", "RTK", "Imu_box", "CarCamera", "Hand" };

        public static bool Supports(string id) => id == "unity_car3";
        private static string DirectoryFor(string id) => Path.Combine(Application.streamingAssetsPath, "LocalVehicleAppearances", id);
        private static FarmVehicleLibrary.AssetManifest Manifest(string id) => FarmVehicleLibrary.ReadManifest(DirectoryFor(id), id);

        public static GameObject CreateBodyPreview(string id)
        {
            if (!Supports(id)) throw new ArgumentException("Unknown scene appearance", nameof(id));
            Source(id);
            return FarmVehicleLibrary.LoadPreview(Manifest(id), "LocalVehicleAppearances/" + id + "/preview",
                link => link == "base_link" || link.StartsWith("display_", StringComparison.Ordinal));
        }

        private static JObject Source(string id)
        {
            var manifest = Manifest(id);
            foreach (var file in manifest.files)
                if (FarmVehicleLibrary.Digest(File.ReadAllBytes(Path.Combine(DirectoryFor(id), file.name))) != file.sha256)
                    throw new InvalidDataException("履带外观资源校验失败：" + file.name);
            var source = JObject.Parse(File.ReadAllText(Path.Combine(DirectoryFor(id), "vehicle_config.json")));
            if ((string)source["vehicleId"] != id) throw new InvalidDataException("履带外观车型不匹配。");
            return source;
        }

        public static void ApplyContactGeometry(VehicleConfigurationData config)
        {
            if (!Supports(config.vehicleId)) return;
            var source = Source(config.vehicleId);
            if (source["metadata"]?["simulationTrackContactGeometry"] == null) return;
            var profile = JsonUtility.FromJson<VehicleConfigurationData>(source.ToString()).geometry;
            // Keep the existing base reference, body, sensor and equipment poses.
            config.geometry.wheelCenters = profile.wheelCenters;
            config.geometry.wheelbaseM = profile.wheelbaseM;
            config.geometry.frontTrackM = profile.frontTrackM;
            config.geometry.rearTrackM = profile.rearTrackM;
            config.geometry.wheelRadiusM = profile.wheelRadiusM;
            config.geometry.wheelWidthM = profile.wheelWidthM;
        }

        public static string Serialize(VehicleConfigurationData config)
        {
            if (!Supports(config.vehicleId)) return JsonUtility.ToJson(config, true) + "\n";
            var target = JObject.Parse(JsonUtility.ToJson(config));
            var source = Source(config.vehicleId);
            var expected = JsonUtility.FromJson<VehicleConfigurationData>(source.ToString());
            // A fixed appearance must never silently change the contact geometry.
            foreach (var wheel in config.geometry.wheelCenters)
            {
                var original = expected.geometry.wheelCenters.Single(item => item.id == wheel.id);
                if (Math.Abs(wheel.radiusM - original.radiusM) > .001 ||
                    wheel.position.Where((value, axis) => Math.Abs(value - original.position[axis]) > .001).Any())
                    throw new InvalidOperationException("当前履带轮心或轮径已改变，请先重新适配履带外观。");
            }
            target["visuals"] = source["visuals"].DeepClone();
            foreach (var item in ((JObject)source["metadata"]).Properties().Where(item =>
                         item.Name == "simulationVisualRotors" || item.Name == "simulationVisualBelts" ||
                         item.Name == "simulationAppearanceRevision" || item.Name == "simulationTrackVisualFit" ||
                         item.Name == "simulationTrackContactGeometry"))
                target["metadata"][item.Name] = item.Value.DeepClone();
            return target.ToString(Formatting.Indented) + "\n";
        }

        public static bool CopyBodyAssets(string id, string destination, out int meshes)
        {
            meshes = 0;
            if (!Supports(id)) return false;
            Source(id);
            foreach (var file in Manifest(id).files.Where(file => file.name != "vehicle_config.json" &&
                         !file.name.StartsWith("sensor_", StringComparison.Ordinal)))
            {
                File.Copy(Path.Combine(DirectoryFor(id), file.name), Path.Combine(destination, file.name), true);
                if (file.name.EndsWith(".obj", StringComparison.Ordinal)) meshes++;
            }
            return true;
        }

        public static void ApplyToSceneVehicle(GameObject car)
        {
            if (car == null || car.name != "Car3") return;
            var existing = car.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == PreviewName);
            if (existing != null) return;
            var children = car.GetComponentsInChildren<Transform>(true);
            Transform Find(string name) => children.First(t => t.name.Trim() == name);
            var front = (Find("FLcenter").position + Find("FRcenter").position) / 2;
            var rear = (Find("RLcenter").position + Find("RRcenter").position) / 2;
            var reference = car.transform.Find("ROS_base_link");
            if (reference == null) { reference = new GameObject("ROS_base_link").transform; reference.SetParent(car.transform); }
            reference.SetPositionAndRotation((front + rear) / 2,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(front - rear, Vector3.up).normalized, Vector3.up));
            reference.localScale = Vector3.one;
            var preview = CreateBodyPreview("unity_car3");
            foreach (var renderer in car.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool equipment = false;
                for (var current = renderer.transform; current != null && current != car.transform; current = current.parent)
                    if (EquipmentNames.Contains(current.name)) { equipment = true; break; }
                if (!equipment) renderer.enabled = false;
            }
            preview.name = PreviewName;
            preview.transform.SetParent(reference, false);
        }
    }
}
