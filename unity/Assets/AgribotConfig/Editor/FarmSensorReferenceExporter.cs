using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Editor
{
    public static class FarmSensorReferenceExporter
    {
        [Serializable] private sealed class Report { public List<Vehicle> vehicles = new(); }
        [Serializable] private sealed class Vehicle
        {
            public string name;
            public Vector3 scale, position;
            public List<Part> parts = new();
        }
        [Serializable] private sealed class Part
        {
            public string name, hierarchy;
            public Vector3 position, scale, minimum, maximum;
            public int meshCount;
        }

        public static void Export()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run outside Play mode.");
            string output = Environment.GetEnvironmentVariable("AGRIBOT_SENSOR_REFERENCE_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local/share/agribot/farm_assets/unity_sensor_references");
            Directory.CreateDirectory(output);
            var report = new Report();
            EditorSceneManager.OpenScene("Assets/Scenes/scene1.unity");
            foreach (string name in new[] { "Car1", "Car2", "Car3", "Car4", "Car5" })
            {
                var car = Resources.FindObjectsOfTypeAll<Transform>().First(t => t.gameObject.scene.IsValid() && t.name == name).gameObject;
                car.SetActive(true);
                foreach (string sensor in new[] { "RADAR", "CarCamera", "Imu_box", "RTK" })
                    car.GetComponentsInChildren<Transform>(true).First(t => t.name == sensor).gameObject.SetActive(true);
                var authoring = SceneVehicleConfiguration.Build(car);
                string folder = Path.Combine(output, name);
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "configuration.json"), JsonUtility.ToJson(authoring.BuildConfiguration(), true));
                VehicleMeshExporter.ExportSensorVisuals(authoring.Sensors, folder);
                var vehicle = new Vehicle { name = name, position = car.transform.position, scale = car.transform.lossyScale };
                foreach (Transform item in car.GetComponentsInChildren<Transform>(true))
                {
                    if (item.parent != car.transform && !new[] { "RADAR", "CarCamera", "Imu_box", "RTK", "Mushroom1", "Mushroom2" }.Contains(item.name)) continue;
                    var filters = item.GetComponentsInChildren<MeshFilter>(true);
                    var points = filters.Where(f => f.sharedMesh != null).SelectMany(f => f.sharedMesh.vertices
                        .Select(p => RosFluConverter.UnityToRos(authoring.BaseLink.InverseTransformPoint(f.transform.TransformPoint(p))))).ToArray();
                    vehicle.parts.Add(new Part { name = item.name, hierarchy = item.parent?.name,
                        position = RosFluConverter.UnityToRos(authoring.BaseLink.InverseTransformPoint(item.position)),
                        scale = item.lossyScale, meshCount = filters.Length,
                        minimum = points.Length == 0 ? Vector3.zero : new Vector3(points.Min(p => p.x), points.Min(p => p.y), points.Min(p => p.z)),
                        maximum = points.Length == 0 ? Vector3.zero : new Vector3(points.Max(p => p.x), points.Max(p => p.y), points.Max(p => p.z)) });
                }
                report.vehicles.Add(vehicle);
                car.SetActive(false);
            }
            File.WriteAllText(Path.Combine(output, "installation_references.json"), JsonUtility.ToJson(report, true));
            Debug.Log("FARM_SENSOR_REFERENCE_EXPORTED=" + output);
        }
    }
}
