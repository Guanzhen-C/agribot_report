using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Editor
{
    public static class VehicleSceneCompatibilityAudit
    {
        [Serializable] private sealed class Report { public List<VehicleReport> vehicles = new(); }
        [Serializable] private sealed class VehicleReport
        {
            public string name;
            public Vector3 scale;
            public long triangleCount;
            public List<string> hierarchy = new();
        }

        public static void RunBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/scene1.unity", OpenSceneMode.Single);
            var cars = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => Enumerable.Range(1, 5).Any(id => item.name == $"Car{id}")).ToArray();
            var paths = new HashSet<string>();
            foreach (var car in cars)
                foreach (var filter in car.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null) paths.Add(AssetDatabase.GetAssetPath(filter.sharedMesh));
            foreach (string path in paths)
                if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
                { importer.isReadable = true; importer.SaveAndReimport(); }

            var report = new Report();
            foreach (var car in cars)
            {
                var item = new VehicleReport { name = car.name, scale = car.lossyScale };
                foreach (var child in car.GetComponentsInChildren<Transform>(true))
                {
                    string path = child.name;
                    for (var parent = child.parent; parent != null && parent != car.parent; parent = parent.parent)
                        path = parent.name + "/" + path;
                    var renderer = child.GetComponent<MeshRenderer>();
                    string bounds = renderer == null ? "" : $" bounds={renderer.bounds.center:F4}/{renderer.bounds.size:F4}";
                    item.hierarchy.Add($"{path} world={child.position:F4} scale={child.lossyScale:F4}{bounds}");
                }
                foreach (var filter in car.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null)
                        for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) item.triangleCount += filter.sharedMesh.GetIndexCount(i) / 3;
                report.vehicles.Add(item);
            }
            string output = "/tmp/agribot_unity_vehicle_compatibility.json";
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log("AGRIBOT_COMPATIBILITY_REPORT=" + output);
        }
    }
}
