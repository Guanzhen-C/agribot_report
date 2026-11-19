using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class SimulationLaunchGateTests
    {
        private string previousRoot, temporaryRoot;
        [SetUp] public void IsolateSession()
        {
            previousRoot = Environment.GetEnvironmentVariable("AGRIBOT_REPORT_ROOT");
            temporaryRoot = Path.Combine(Path.GetTempPath(), "agribot-gates-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            Environment.SetEnvironmentVariable("AGRIBOT_REPORT_ROOT", temporaryRoot);
        }
        [TearDown] public void RestoreSession()
        {
            Environment.SetEnvironmentVariable("AGRIBOT_REPORT_ROOT", previousRoot);
            Directory.Delete(temporaryRoot, true);
        }
        private static GameObject Find(string name) => UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == name).gameObject;

        internal static void SelectAlgorithm(Button[] buttons, string id)
        {
            var profile = RosSimulationLauncher.FindAlgorithm(id);
            foreach (var component in new[] { profile.PerceptionComponent, profile.VisualPerceptionComponent,
                profile.LocalizationComponent, profile.PlannerComponent, profile.ControllerComponent })
                buttons.Single(button => button.name == "Component_"+component.Id).onClick.Invoke();
        }

        internal static void AssertLaunchWarning(Button button, string expected)
        {
            Assert.That(button.IsInteractable(), Is.True);
            Assert.That(button.image.color.g, Is.GreaterThan(.7f), "Missing equipment must leave the button highlighted");
            button.onClick.Invoke();
            var notice = Find("GarageNotification");
            Assert.That(notice.activeInHierarchy, Is.True);
            Assert.That(notice.GetComponentInChildren<TMPro.TMP_Text>().text, Does.Contain(expected));
        }

        internal static void AssertLaunchUnavailable(Button button)
        {
            Assert.That(button.IsInteractable(), Is.False, button.name);
            Assert.That(button.image.color.g, Is.LessThan(.4f), button.name + " must look disabled");
            GameObject.Find("GarageNotification")?.SetActive(false);
            button.onClick.Invoke();
            Assert.That(GameObject.Find("GarageNotification"), Is.Null, "An unavailable launch should not show a warning");
        }

        [UnityTest]
        public IEnumerator EveryCarRequiresEverySensorAndAnAlgorithmBeforeEitherLaunchMode()
        {
            foreach (string carName in Enumerable.Range(0, 10).Select(index => "Car"+index))
            {
                FarmScenarioCatalog.ResetSelection();
                yield return SceneManager.LoadSceneAsync("scene1");
                yield return null;
                Find(carName+"_Img").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.8f);
                var car = Find(carName);
                Find("OpenBag_btn").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.6f);
                var bag = Find("BagPanel").GetComponentsInChildren<MonoBehaviour>(true)
                    .Single(item => item.GetType().Name == "EquipmentBagController");
                var getter = bag.GetType().GetMethod("GetEquipment");
                var installation = bag.GetType().GetMethod("SetInstallationState");
                object Equipment(string slot) => getter.Invoke(bag, new[] {
                    Enum.Parse(getter.GetParameters()[0].ParameterType, slot) });
                Find("BagPanel").transform.Find("Header/CloseButton").GetComponent<Button>().onClick.Invoke();
                var toolbar = Find("VehicleActionToolbar");
                toolbar.GetComponentsInChildren<Button>(true).Single(button => button.name == "Output_btn").onClick.Invoke();
                yield return null;
                var dialog = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Single(item => item.name == "ROS_Algorithm_Selection" && item.gameObject.activeInHierarchy);
                var buttons = dialog.GetComponentsInChildren<Button>(true);
                var normal = buttons.Single(button => button.name == "Launch");
                var hil = buttons.Single(button => button.name == "LaunchHil");
                AssertLaunchUnavailable(normal);
                AssertLaunchUnavailable(hil);
                var orchard = buttons.Single(button => button.name == "Scenario_orchard");
                orchard.onClick.Invoke();
                AssertLaunchUnavailable(normal);
                AssertLaunchUnavailable(hil);
                buttons.Single(button => button.name == "Component_stvl").onClick.Invoke();
                AssertLaunchUnavailable(normal);
                AssertLaunchUnavailable(hil);
                SelectAlgorithm(buttons, "stvl_off_fastlivo_rtk_smac_hybrid_mppi");
                if (carName == "Car0")
                {
                    FarmScenarioCatalog.ResetSelection();
                    yield return new WaitForSeconds(.3f);
                    AssertLaunchUnavailable(normal);
                    AssertLaunchUnavailable(hil);
                    orchard.onClick.Invoke();
                }
                AssertLaunchUnavailable(hil);
                AssertLaunchWarning(normal, "请先安装雷达、RTK、相机、IMU，再启动仿真。");
                foreach (string slot in new[] { "Radar", "RTK", "Camera", "Imu" })
                {
                    Assert.That((bool)installation.Invoke(bag, new[] { Equipment(slot), (object)true }), Is.True);
                    yield return new WaitForSeconds(.65f);
                    if (slot == "RTK")
                        AssertLaunchWarning(normal, "请先安装相机、IMU，再启动仿真。");
                }
                var rtk = car.GetComponentsInChildren<Transform>(true).Single(part => part.name == "RTK");
                var rtkMeshes = rtk.GetComponentsInChildren<MeshFilter>().Where(filter =>
                    filter.GetComponent<MeshRenderer>().enabled).ToArray();
                var points = rtkMeshes.SelectMany(filter => filter.sharedMesh.vertices.Select(vertex =>
                    rtk.InverseTransformPoint(filter.transform.TransformPoint(vertex)))).ToArray();
                Assert.That(points.Max(point => point.y)-points.Min(point => point.y),
                    Is.EqualTo(.1228f).Within(.0001f), carName+" RTK must contain only short stems");
                Assert.That(rtkMeshes.Sum(filter => filter.sharedMesh.triangles.Length/3), Is.EqualTo(6416),
                    carName+" must show two complete HIL antennas without the old support rack");
                if (carName == "Car0" || carName == "Car3")
                {
                    VehicleConfigurationAuthoring authoring;
                    if (carName == "Car3") authoring = SceneVehicleConfiguration.Build(car);
                    else
                    {
                        var controller = car.GetComponents<MonoBehaviour>()
                            .Single(item => item.GetType().Name == "Car0GarageVehicle");
                        authoring = (VehicleConfigurationAuthoring)controller.GetType()
                            .GetMethod("BuildConfiguration").Invoke(controller, null);
                    }
                    Assert.That(authoring.Sensors.All(sensor => sensor.visualRoot != null), Is.True,
                        carName+" must retain sensor visual bindings after reparenting");
                    string exportDirectory = Path.Combine(temporaryRoot, carName);
                    Directory.CreateDirectory(exportDirectory);
                    VehicleMeshExporter.ExportSensorVisuals(new[] { authoring.Sensors.Single(sensor =>
                        sensor.id == "rtk_master") }, exportDirectory);
                    var heights = File.ReadLines(Path.Combine(exportDirectory, "sensor_rtk_master_visual.obj"))
                        .Where(line => line.StartsWith("v ", StringComparison.Ordinal))
                        .Select(line => float.Parse(line.Split(' ')[3], CultureInfo.InvariantCulture)).ToArray();
                    Assert.That(heights.Max()-heights.Min(), Is.EqualTo(.1228f).Within(.0001f),
                        carName+" exported RTK geometry must agree with the visible short stems");
                }
                Assert.That(normal.interactable, Is.True, carName+" complete ordinary selection");
                AssertLaunchUnavailable(hil);
                if (carName == "Car3")
                {
                    var config = SceneVehicleConfiguration.Build(car).BuildConfiguration();
                    foreach (var expected in SensorMountLibrary.Tracked)
                    {
                        var actual = config.sensors.Single(sensor => sensor.id == expected.id);
                        for (int axis = 0; axis < 3; axis++)
                            Assert.That(actual.pose.xyz[axis], Is.EqualTo(expected.pose.xyz[axis]).Within(.00002), expected.id);
                    }
                }
                foreach (var entry in new[] { ("Radar", "雷达"), ("RTK", "RTK"), ("Camera", "相机"), ("Imu", "IMU") })
                {
                    installation.Invoke(bag, new[] { Equipment(entry.Item1), (object)false });
                    yield return new WaitForSeconds(.3f);
                    AssertLaunchWarning(normal, "安装"+entry.Item2);
                    Assert.That(dialog.gameObject.activeInHierarchy, Is.True, "Invalid clicks must leave configuration open");
                    installation.Invoke(bag, new[] { Equipment(entry.Item1), (object)true });
                    yield return new WaitForSeconds(.65f);
                }
                foreach (var profile in HilSimulationLauncher.Profiles.Where(profile => profile.car == carName))
                {
                    installation.Invoke(bag, new[] { Equipment(profile.unityType), (object)true });
                    if (carName == "Car4")
                        buttons.Single(button => button.name == (profile.transmission == "cvt_auto" ? "CVT" : "PowerShift"))
                            .onClick.Invoke();
                    SelectAlgorithm(buttons, profile.algorithm);
                    buttons.Single(button => button.name == "Scenario_arable_field").onClick.Invoke();
                    AssertLaunchUnavailable(hil);
                    buttons.Single(button => button.name == "Scenario_"+profile.scene).onClick.Invoke();
                    Assert.That(hil.interactable, Is.True, profile.id);
                    if (HilImplementLibrary.Supports(profile.implement)) AssertLaunchUnavailable(normal);
                    foreach (string slot in new[] { "Radar", "Camera" })
                        installation.Invoke(bag, new[] { Equipment(slot), (object)false });
                    yield return new WaitForSeconds(.65f);
                    AssertLaunchWarning(hil, "请先安装雷达、相机，再启动仿真。");
                    foreach (string slot in new[] { "Radar", "Camera" })
                        installation.Invoke(bag, new[] { Equipment(slot), (object)true });
                    yield return new WaitForSeconds(.65f);
                }
                Assert.That(File.Exists(Path.Combine(temporaryRoot, ".local/hil/state.json")), Is.False,
                    "Invalid button invocation must never submit a hardware request");
            }
        }
    }
}
