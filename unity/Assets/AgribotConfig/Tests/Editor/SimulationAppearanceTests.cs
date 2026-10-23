using System;
using System.IO;
using System.Linq;
using Agribot.VehicleConfiguration;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class SimulationAppearanceTests
    {
        [Test]
        public void CompactTracksKeepSceneAnchorsAndExportAnimationAssets()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/scene1.unity", OpenSceneMode.Single);
            var car = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(t => t.name == "Car3").gameObject;
            car.SetActive(true);
            foreach (var child in car.GetComponentsInChildren<Transform>(true))
                if (new[] { "RADAR", "RTK", "Imu_box", "CarCamera" }.Contains(child.name)) child.gameObject.SetActive(true);
            var authoring = SceneVehicleConfiguration.Build(car);
            var original = authoring.BuildConfiguration();
            var sensorPositions = authoring.Sensors.Select(sensor => sensor.anchor.position).ToArray();
            var sensorRenderers = authoring.Sensors.SelectMany(sensor => sensor.visualRoot.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            var enabled = sensorRenderers.Select(renderer => renderer.enabled).ToArray();
            SimulationAppearanceLibrary.ApplyToSceneVehicle(car);
            SimulationAppearanceLibrary.ApplyToSceneVehicle(car);
            Assert.That(car.GetComponentsInChildren<Transform>(true).Count(t => t.name == "ROS_SimulationAppearance"), Is.EqualTo(1));
            var preview = car.GetComponentsInChildren<Transform>(true).Single(t => t.name == "ROS_SimulationAppearance");
            Assert.That(preview.GetComponentsInChildren<MeshFilter>().Count(filter => filter.name.StartsWith("display_")), Is.GreaterThanOrEqualTo(14));
            CollectionAssert.AreEqual(enabled, sensorRenderers.Select(renderer => renderer.enabled));
            var current = SceneVehicleConfiguration.Build(car).BuildConfiguration();
            foreach (var field in new[] { "geometry", "sensors", "navigation", "dynamics", "collision", "manipulators" })
                Assert.That(JToken.DeepEquals(JObject.Parse(JsonUtility.ToJson(original))[field],
                    JObject.Parse(JsonUtility.ToJson(current))[field]), Is.True, field);
            CollectionAssert.AreEqual(sensorPositions, authoring.Sensors.Select(sensor => sensor.anchor.position));
            var result = authoring.ExportConfigurationBundle();
            var exported = JObject.Parse(File.ReadAllText(result.JsonPath));
            Assert.That((string)exported["metadata"]["simulationAppearanceRevision"], Is.EqualTo("compact_articulated_tracks_v1"));
            Assert.That(exported["metadata"]["simulationVisualRotors"].Count(), Is.EqualTo(14));
            Assert.That(exported["metadata"]["simulationVisualBelts"].Sum(belt => belt["segments"].Count()), Is.EqualTo(128));
            foreach (var part in exported["metadata"]["simulationVisualRotors"].Concat(
                         exported["metadata"]["simulationVisualBelts"].SelectMany(belt => belt["segments"])))
                Assert.That(File.Exists(Path.Combine(Path.GetDirectoryName(result.JsonPath), (string)part["meshFile"])), Is.True);
            Assert.That(File.ReadAllText(result.MeshPath), Does.Contain("compact_track_support_"));
            Assert.That(File.ReadAllText(result.MeshPath), Does.Not.Contain("甘蔗履带"));
        }

        [Test]
        public void ChangedContactGeometryDoesNotSilentlyReuseFixedAppearance()
        {
            string source = Path.Combine(Application.streamingAssetsPath, "LocalVehicleAppearances/unity_car3/vehicle_config.json");
            var config = JsonUtility.FromJson<VehicleConfigurationData>(File.ReadAllText(source));
            config.geometry.wheelCenters[0].radiusM += .02;
            Assert.Throws<InvalidOperationException>(() => SimulationAppearanceLibrary.Serialize(config));
        }

        [Test]
        public void CompactContactCentersUseInspectedIdlersAndExportSameGeometry()
        {
            string source = Path.Combine(Application.streamingAssetsPath, "LocalVehicleAppearances/unity_car3/vehicle_config.json");
            var config = JsonUtility.FromJson<VehicleConfigurationData>(File.ReadAllText(source));
            var expected = JObject.Parse(File.ReadAllText(source));
            config.geometry.wheelCenters[0].position = new[] { .201, .2945, 0.0 };
            SimulationAppearanceLibrary.ApplyContactGeometry(config);
            Assert.That(config.geometry.wheelbaseM, Is.EqualTo(.8370031).Within(1e-6));
            Assert.That(config.geometry.wheelCenters[0].position[0], Is.EqualTo(.44893837).Within(1e-6));
            var exported = JObject.Parse(SimulationAppearanceLibrary.Serialize(config));
            Assert.That(JToken.DeepEquals(exported["geometry"], expected["geometry"]), Is.True);
            Assert.That((bool)exported["metadata"]["simulationTrackContactGeometry"]["physicalMeasurement"], Is.False);
        }

        [TestCase("tracked_harvester")]
        [TestCase("harvester_header")]
        public void HarvesterExportsCompleteMovingTracksAndTexture(string id)
        {
            string destination = Path.Combine(Path.GetTempPath(), "agribot-tracks-" + Guid.NewGuid());
            try
            {
                var export = FarmVehicleLibrary.Export(id, destination);
                var config = JObject.Parse(File.ReadAllText(export.JsonPath));
                Assert.That(config["metadata"]["simulationVisualRotors"].Count(), Is.EqualTo(16));
                Assert.That(config["metadata"]["simulationVisualBelts"].Sum(belt => belt["segments"].Count()), Is.EqualTo(128));
                Assert.That(File.Exists(Path.Combine(destination, id, "harvester_yellow_albedo.png")), Is.True);
                StringAssert.Contains("harvester_yellow_albedo.png", File.ReadAllText(Path.Combine(destination, id, "vehicle_visual.mtl")));
                var preview = FarmVehicleLibrary.CreatePreview(id);
                try
                {
                    Assert.That(preview.GetComponentsInChildren<MeshFilter>().Any(item => item.name.Contains("display_rear")), Is.True);
                    Assert.That(preview.GetComponentsInChildren<MeshRenderer>().Any(item => item.name == "base_link" &&
                        item.sharedMaterial.color.r > .4f && item.sharedMaterial.color.g > .2f &&
                        item.sharedMaterial.color.b < .2f), Is.True, "车体漆面不能丢失黄色贴图的颜色");
                }
                finally { UnityEngine.Object.DestroyImmediate(preview); }
            }
            finally { if (Directory.Exists(destination)) Directory.Delete(destination, true); }
        }
    }
}
