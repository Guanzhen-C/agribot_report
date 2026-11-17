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

        private static void SelectAlgorithm(Button[] buttons, string id)
        {
            var profile = RosSimulationLauncher.FindAlgorithm(id);
            foreach (var component in new[] { profile.PerceptionComponent, profile.VisualPerceptionComponent,
                profile.LocalizationComponent, profile.PlannerComponent, profile.ControllerComponent })
                buttons.Single(button => button.name == "Component_"+component.Id).onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator EveryCarRequiresEverySensorAndAnAlgorithmBeforeEitherLaunchMode()
        {
            foreach (string carName in Enumerable.Range(0, 10).Select(index => "Car"+index))
            {
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
                buttons.Single(button => button.name == "Scenario_orchard").onClick.Invoke();
                var normal = buttons.Single(button => button.name == "Launch");
                var hil = buttons.Single(button => button.name == "LaunchHil");
                Assert.That(normal.interactable, Is.False, carName+" no sensors or algorithm");
                Assert.That(hil.interactable, Is.False);
                foreach (string slot in new[] { "Radar", "RTK", "Camera", "Imu" })
                {
                    Assert.That((bool)installation.Invoke(bag, new[] { Equipment(slot), (object)true }), Is.True);
                    yield return new WaitForSeconds(.65f);
                    Assert.That(normal.interactable, Is.False, carName+" must explicitly choose an algorithm");
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
                SelectAlgorithm(buttons, "stvl_off_fastlivo_rtk_smac_hybrid_mppi");
                yield return new WaitForSeconds(.3f);
                Assert.That(normal.interactable, Is.True, carName+" complete ordinary selection");
                if (carName == "Car0" || carName == "Car3" || carName == "Car4")
                {
                    string tool = carName == "Car0" ? "Hand" : carName == "Car3" ? "HilSprayer" : "DiscHarrow";
                    installation.Invoke(bag, new[] { Equipment(tool), (object)true });
                    yield return new WaitForSeconds(.8f);
                    string scene = carName == "Car4" ? "wide_headland" : "orchard";
                    buttons.Single(button => button.name == "Scenario_"+scene).onClick.Invoke();
                    string implement = carName == "Car0" ? "lite6" : carName == "Car3" ? "hil_sprayer" : "mounted_disc_harrow";
                    SelectAlgorithm(buttons, HilSimulationLauncher.Match(carName, implement, scene).algorithm);
                    yield return new WaitForSeconds(.3f);
                    Assert.That(hil.interactable, Is.True, carName+" reviewed HIL combination");
                }
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
                foreach (string slot in new[] { "Radar", "RTK", "Camera", "Imu" })
                {
                    installation.Invoke(bag, new[] { Equipment(slot), (object)false });
                    yield return new WaitForSeconds(.3f);
                    Assert.That(normal.interactable, Is.False, carName+" missing "+slot);
                    Assert.That(hil.interactable, Is.False, carName+" HIL missing "+slot);
                    normal.onClick.Invoke();
                    hil.onClick.Invoke();
                    Assert.That(File.Exists(Path.Combine(temporaryRoot, ".local/hil/state.json")), Is.False,
                        "Invalid button invocation must never submit a hardware request");
                    installation.Invoke(bag, new[] { Equipment(slot), (object)true });
                    yield return new WaitForSeconds(.65f);
                }
            }
        }
    }
}
