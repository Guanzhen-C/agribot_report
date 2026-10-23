using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public class SceneVehiclesExportTests
    {
        [Test]
        public void AckermannSeparatesSensorVisualsWithoutMovingMeasurementOrigins()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/scene2.unity", OpenSceneMode.Single);
            var authoring = AckermannRuntimeConfiguration.BuildOrRefresh();
            var camera = authoring.Sensors.First(sensor => sensor.id == "right_camera");
            Assert.That(camera.visualRoot, Is.Not.Null);
            var position = authoring.BaseLink.InverseTransformPoint(camera.anchor.position);
            foreach (var sensor in authoring.Sensors)
            {
                Assert.That(sensor.visualRoot, Is.Not.Null, sensor.id);
                sensor.visualRoot.gameObject.SetActive(true);
            }
            var export = authoring.ExportConfigurationBundle();
            var body = File.ReadAllText(export.MeshPath);
            Assert.That(body, Does.Not.Contain("o FrontOpticalGlass"));
            Assert.That(body, Does.Not.Contain("o InnerAperture"));
            foreach (string id in new[] { "imu", "lidar", "right_camera", "rtk_master" })
            {
                string path = Path.Combine(Path.GetDirectoryName(export.MeshPath), "sensor_" + id + "_visual.obj");
                Assert.That(File.Exists(path), Is.True, id);
                Assert.That(File.ReadAllText(path), Does.Contain("\nf "), id);
            }
            Assert.That(File.ReadAllText(Path.Combine(Path.GetDirectoryName(export.MeshPath),
                "sensor_right_camera_visual.obj")), Does.Contain("o FrontOpticalGlass"));
            Assert.That(Vector3.Distance(position,
                authoring.BaseLink.InverseTransformPoint(camera.anchor.position)), Is.LessThan(0.000001f));
        }

        [TestCase("Car1")]
        [TestCase("Car2")]
        [TestCase("Car3")]
        [TestCase("Car4")]
        [TestCase("Car5")]
        public void NewVehicleExportsOwnGeometryAndSensors(string name)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/scene1.unity", OpenSceneMode.Single);
            var car = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .First(t => t.name == name).gameObject;
            car.SetActive(true);
            foreach (var child in car.GetComponentsInChildren<Transform>(true))
                if (new[] { "RADAR", "RTK", "Imu_box", "CarCamera" }.Contains(child.name)) child.gameObject.SetActive(true);
            var authoring = SceneVehicleConfiguration.Build(car);
            Assert.That(authoring.ValidateConfiguration(out var errors, out _), Is.True, string.Join("\n", errors));
            var config = authoring.BuildConfiguration();
            Assert.That(config.vehicleId, Is.EqualTo("unity_" + name.ToLowerInvariant()));
            Assert.That(config.vehicleType, Is.EqualTo(name == "Car3" ? "tracked" : "ackermann"));
            Assert.That(config.geometry.wheelCenters[0].position[1], Is.GreaterThan(0));
            Assert.That(config.geometry.wheelCenters[2].position[1], Is.GreaterThan(0));
            Assert.That(config.geometry.wheelbaseM, Is.GreaterThan(0.3));
            Assert.That(config.sensors.Length, Is.EqualTo(5));
            Assert.That(config.metadata.calibrationState, Is.EqualTo("unity_nominal_simulation_only"));
            var export = authoring.ExportConfigurationBundle();
            Assert.That(File.Exists(export.MeshPath), Is.True);
            var mesh = File.ReadAllText(export.MeshPath);
            Assert.That(mesh, Does.Not.Contain("o FrontOpticalGlass"));
            Assert.That(mesh, Does.Not.Contain("o ZHAO_1_1_4_4_2"));
            Assert.That(mesh, Does.Not.Contain("o Mushroom"));
            foreach (string sensor in new[] { "imu", "lidar", "right_camera", "rtk_master" })
            {
                string sensorPath = Path.Combine(Path.GetDirectoryName(export.MeshPath), "sensor_" + sensor + "_visual.obj");
                Assert.That(File.Exists(sensorPath), Is.True, sensor);
                Assert.That(File.Exists(Path.ChangeExtension(sensorPath, ".mtl")), Is.True, sensor);
                Assert.That(File.ReadAllText(sensorPath), Does.Contain("\nf "), sensor);
            }
        }
    }
}
