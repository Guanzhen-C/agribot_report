using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class FarmVehicleLibraryTests
    {
        [Test]
        public void CatalogHasCompatibleTractorCombinations()
        {
            Assert.That(FarmVehicleLibrary.Presets.Select(item => item.id), Is.EquivalentTo(AllPresetIds()));
            foreach (var preset in FarmVehicleLibrary.Presets)
            {
                if (preset.implementId == "none") StringAssert.StartsWith("Car", preset.garageName);
                else
                {
                    Assert.That(preset.garageName, Is.Null.Or.Empty);
                    Assert.That(preset.implementObjectName.StartsWith("Car", StringComparison.OrdinalIgnoreCase), Is.False);
                }
            }
            Assert.Throws<ArgumentException>(() => FarmVehicleLibrary.AssetDirectory("../unknown"));
        }

        [TestCaseSource(nameof(AllPresetIds))]
        public void CanonicalExportPreservesGeometryAndExtendedParameters(string id)
        {
            if (!FarmVehicleLibrary.IsInstalled(id)) Assert.Ignore("Local CAD assets not installed.");
            string temp = Path.Combine(Path.GetTempPath(), "agribot-farm-test-" + Guid.NewGuid());
            try
            {
                var result = FarmVehicleLibrary.Export(id, temp);
                string source = FarmVehicleLibrary.AssetDirectory(id);
                Assert.That(File.ReadAllBytes(result.JsonPath), Is.EqualTo(File.ReadAllBytes(Path.Combine(source, "vehicle_config.json"))));
                string json = File.ReadAllText(result.JsonPath);
                StringAssert.Contains("simulationVelocityFromPose", json);
                StringAssert.Contains("massKg", json);
                if (FarmVehicleLibrary.Presets.Single(p => p.id == id).implementId != "none")
                    StringAssert.Contains(id == "cab_trailer" ? "articulated_transport" : "fixed_transport", json);
                foreach (var file in FarmVehicleLibrary.Manifest(id).files)
                    Assert.That(FarmVehicleLibrary.Digest(File.ReadAllBytes(Path.Combine(temp, id, file.name))), Is.EqualTo(file.sha256));
            }
            finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
        }

        [TestCaseSource(nameof(AllPresetIds))]
        public void PreviewMatchesRosGeometryWithoutRescaling(string id)
        {
            if (!FarmVehicleLibrary.IsInstalled(id)) Assert.Ignore("Local CAD assets not installed.");
            GameObject root = FarmVehicleLibrary.CreatePreview(id);
            try
            {
                var manifest = FarmVehicleLibrary.Manifest(id);
                var meshes = root.GetComponentsInChildren<MeshFilter>();
                Assert.That(meshes.Length, Is.EqualTo(manifest.meshGroups));
                Assert.That(meshes.Sum(item => item.sharedMesh.triangles.Length / 3), Is.EqualTo(manifest.triangles));
                Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                foreach (var filter in meshes)
                {
                    Assert.That(filter.transform.localScale, Is.EqualTo(Vector3.one));
                    foreach (var vertex in filter.sharedMesh.vertices)
                    {
                        Vector3 point = RosFluConverter.UnityToRos(vertex);
                        minimum = Vector3.Min(minimum, point); maximum = Vector3.Max(maximum, point);
                    }
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    Assert.That(minimum[axis], Is.EqualTo(manifest.minimumRos[axis]).Within(.00001));
                    Assert.That(maximum[axis], Is.EqualTo(manifest.maximumRos[axis]).Within(.00001));
                }
                string names = string.Join(" ", meshes.Select(item => item.name));
                StringAssert.Contains("imu", names);
                StringAssert.Contains("camera", names);
                StringAssert.Contains("lidar", names);
                foreach (var sensor in FarmVehicleLibrary.Configuration(id).sensors)
                {
                    if (sensor.id == "rtk_secondary") continue; // Included in the dual-antenna CAD assembly.
                    var filters = meshes.Where(item => item.name == sensor.frameId).ToArray();
                    Assert.That(filters, Is.Not.Empty, sensor.frameId);
                    Bounds bounds = filters[0].sharedMesh.bounds;
                    foreach (var filter in filters.Skip(1)) bounds.Encapsulate(filter.sharedMesh.bounds);
                    Assert.That(bounds.size.magnitude, Is.LessThan(sensor.id == "rtk_master" ? 1.1f : .2f), sensor.frameId);
                    var center = RosFluConverter.UnityToRos(bounds.center);
                    Assert.That(Math.Abs(center.x - sensor.pose.xyz[0]), Is.LessThan(.1), sensor.frameId);
                    Assert.That(bounds.size.magnitude, Is.GreaterThan(.01f));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        public static IEnumerable<string> AllPresetIds()
        {
            foreach (var id in new[] { "unity_car1", "unity_car2", "unity_car4", "unity_car5",
                "compact_tractor", "cab_tractor", "ride_on_mower", "tracked_harvester",
                "mower_front_cutter", "harvester_header", "cab_trailer" }) yield return id;
            foreach (var prefix in new[] { "car1", "car2", "car4", "car5", "tractor", "cab" })
                foreach (var tool in new[] { "chisel", "harrow", "groove", "cultivator", "crusher", "interrow", "garlic", "mulch" })
                    if (prefix != "tractor" || tool != "crusher" && tool != "interrow") yield return prefix + "_" + tool;
        }
    }
}
