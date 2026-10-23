using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public class AuthoredManipulatorTests
    {
        [TestCase("Car0")]
        [TestCase("Car3")]
        public void AuthoredHandExportsOneMountedArmWithoutDuplicatingBodyMesh(string carName)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + (carName == "Car0" ? "scene2" : "scene1") + ".unity");
            var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var car = transforms.First(t => t.name == carName).gameObject;
            car.SetActive(true);
            var hand = transforms.First(t => t.name == "Hand").gameObject;
            hand.SetActive(false);
            var authoring = carName == "Car0" ? AckermannRuntimeConfiguration.BuildOrRefresh() : SceneVehicleConfiguration.Build(car);
            var baseline = authoring.BuildConfiguration();
            Assert.That(baseline.manipulators, Is.Empty);
            string directory = Path.Combine(Path.GetTempPath(), "agribot_authored_hand_" + carName);
            Directory.CreateDirectory(directory);
            string meshPath = Path.Combine(directory, "vehicle.obj");
            int before = VehicleMeshExporter.Export(authoring.VehicleVisualRoot, authoring.BaseLink,
                authoring.Sensors, meshPath);
            string baselineMesh = File.ReadAllText(meshPath);
            hand.SetActive(true);
            var installed = authoring.BuildConfiguration();
            Assert.That(installed.manipulators.Length, Is.EqualTo(1));
            Assert.That(installed.manipulators[0].parentFrameId, Is.EqualTo("base_link"));
            var mount = hand.transform.Find("ROS_manipulator_preview");
            Assert.That(Vector3.Dot(mount.up, authoring.BaseLink.up), Is.GreaterThan(0.98f));
            Assert.That(mount.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(10));
            int after = VehicleMeshExporter.Export(authoring.VehicleVisualRoot, authoring.BaseLink,
                authoring.Sensors, meshPath);
            Assert.That(after, Is.EqualTo(before));
            Assert.That(File.ReadAllText(meshPath), Is.EqualTo(baselineMesh));
            Assert.That(JsonUtility.ToJson(installed.geometry), Is.EqualTo(JsonUtility.ToJson(baseline.geometry)));
            Assert.That(installed.sensors.Select(s => JsonUtility.ToJson(s)), Is.EqualTo(baseline.sensors.Select(s => JsonUtility.ToJson(s))));
            authoring.ExportConfigurationBundle();
            hand.SetActive(false);
            Assert.That(authoring.BuildConfiguration().manipulators, Is.Empty);
        }
    }
}
