using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class HilUiTests
    {
        private string previousRoot, temporaryRoot;

        [SetUp]
        public void UseAnIsolatedUiSession()
        {
            previousRoot = Environment.GetEnvironmentVariable("AGRIBOT_REPORT_ROOT");
            temporaryRoot = Path.Combine(Path.GetTempPath(), "agribot-hil-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            Environment.SetEnvironmentVariable("AGRIBOT_REPORT_ROOT", temporaryRoot);
        }

        [TearDown]
        public void RestoreUiSession()
        {
            Environment.SetEnvironmentVariable("AGRIBOT_REPORT_ROOT", previousRoot);
            Directory.Delete(temporaryRoot, true);
        }

        private static GameObject Find(string name) => UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).First(item => item.name == name).gameObject;

        internal static IEnumerator Capture(string name)
        {
            string directory = Environment.GetEnvironmentVariable("AGRIBOT_HIL_UI_DIAGNOSTICS");
            if (string.IsNullOrEmpty(directory)) yield break;
            Directory.CreateDirectory(directory);
            var camera = Camera.main;
            var overlays = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                .Select(canvas => (canvas, canvas.worldCamera, canvas.planeDistance)).ToArray();
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (var (canvas, _, _) in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane+.001f;
                }
                Canvas.ForceUpdateCanvases();
                yield return null;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(directory, name+".png"), texture.EncodeToPNG());
            }
            finally
            {
                foreach (var (canvas, worldCamera, planeDistance) in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = worldCamera; canvas.planeDistance = planeDistance;
                }
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator AckermannShowsBothAntennasAheadOfTheRearCargoTray()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Find("Car0_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            Find("OpenBag_btn").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.6f);
            var bag = Find("BagPanel").GetComponentsInChildren<MonoBehaviour>(true)
                .Single(item => item.GetType().Name == "EquipmentBagController");
            var getter = bag.GetType().GetMethod("GetEquipment");
            var equipment = getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, "RTK") });
            Assert.That((bool)bag.GetType().GetMethod("SetInstallationState").Invoke(bag,
                new[] { equipment, (object)true }), Is.True);
            yield return new WaitForSeconds(.8f);
            Find("BagPanel").transform.Find("Header/CloseButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.5f);
            var controller = Find("Car0").GetComponents<MonoBehaviour>().Single(item => item.GetType().Name == "Car0GarageVehicle");
            var authoring = (VehicleConfigurationAuthoring)controller.GetType().GetMethod("BuildConfiguration").Invoke(controller, null);
            foreach (var mount in SensorMountLibrary.AckermannRtk)
            {
                var antenna = authoring.GetComponentsInChildren<Transform>().Single(item =>
                    item.name == (mount.id == "rtk_master" ? "Mushroom1" : "Mushroom2"));
                var pose = RosFluConverter.RelativePose(authoring.BaseLink, antenna);
                for (int axis = 0; axis < 3; axis++)
                    Assert.That(pose.xyz[axis], Is.EqualTo(mount.pose.xyz[axis]).Within(.00001));
                Assert.That(pose.xyz[0], Is.EqualTo(.12).Within(.00001), "Leave the original rear tray untouched");
            }
            var camera = Camera.main;
            var brain = camera.GetComponents<Behaviour>().Single(item => item.GetType().Name == "CinemachineBrain");
            brain.enabled = false;
            camera.transform.position = authoring.BaseLink.TransformPoint(RosFluConverter.RosToUnity(new Vector3(-.7f, -.65f, 1f)));
            camera.transform.LookAt(authoring.BaseLink.TransformPoint(RosFluConverter.RosToUnity(new Vector3(.2f, 0, .1f))));
            yield return Capture("ackermann_forward_deck_rtk");
            brain.enabled = true;
        }

        [UnityTest]
        public IEnumerator TrackedToolsUseOriginalPosesAndSwitchExclusively()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Find("Car3_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(1f);
            var car = Find("Car3");
            Vector3 scale = car.transform.localScale;
            Find("OpenBag_btn").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            var bag = Find("BagPanel").GetComponentsInChildren<MonoBehaviour>(true)
                .First(item => item.GetType().Name == "EquipmentBagController");
            var getter = bag.GetType().GetMethod("GetEquipment");
            var install = bag.GetType().GetMethod("SetInstallationState");
            foreach (string name in new[] { "HilSprayer_slot", "HilShredder_slot" })
                Assert.That(Find(name).GetComponent<Image>().color,
                    Is.EqualTo(Find("Hand_slot").GetComponent<Image>().color), "Shared equipment background: "+name);
            Find("HilSprayer_slot").GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0;
            yield return null;
            yield return Capture("hil_equipment_backgrounds");
            foreach (var item in new[] { ("HilSprayer", "hil_sprayer"), ("HilShredder", "hil_shredder") })
            {
                var data = getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, item.Item1) });
                Assert.That(data, Is.Not.Null);
                Assert.That((bool)install.Invoke(bag, new[] { data, (object)true }), Is.True);
                yield return null;
                var mount = car.transform.Find("ROS_base_link/"+item.Item1);
                Assert.That(mount, Is.Not.Null);
                Assert.That(mount.gameObject.activeSelf, Is.True);
                Assert.That(mount.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(mount.GetComponentsInChildren<MeshFilter>().Length, Is.GreaterThan(0));
                Assert.That(AssistantConversation.State.implement, Is.EqualTo(item.Item2));
                Assert.That(car.transform.localScale, Is.EqualTo(scale));
            }
            Assert.That(car.transform.Find("ROS_base_link/HilSprayer").gameObject.activeSelf, Is.False);
            Find("BagPanel").transform.Find("Header/CloseButton").GetComponent<Button>().onClick.Invoke();
            var toolbar = Find("VehicleActionToolbar");
            Assert.That(toolbar.GetComponentsInChildren<Button>(true)
                .Any(button => button.name == "Start_Hil_Simulation"), Is.False);
            toolbar.GetComponentsInChildren<Button>(true).Single(button => button.name == "Output_btn")
                .onClick.Invoke();
            yield return null;
            var dialog = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Single(item => item.name == "ROS_Algorithm_Selection" && item.gameObject.activeInHierarchy);
            dialog.GetComponentsInChildren<Button>(true).Single(button => button.name == "Scenario_orchard")
                .onClick.Invoke();
            var buttons = dialog.GetComponentsInChildren<Button>(true);
            var normal = buttons.Single(button => button.name == "Launch").GetComponent<RectTransform>();
            var hil = buttons.Single(button => button.name == "LaunchHil").GetComponent<RectTransform>();
            Assert.That(AssistantConversation.State.car, Is.EqualTo("Car3"));
            Assert.That(AssistantConversation.State.implement, Is.EqualTo("hil_shredder"));
            Assert.That(HilSimulationLauncher.ReadStatus().phase, Is.EqualTo("stopped"));
            Assert.That(normal.anchorMax.x, Is.LessThan(hil.anchorMin.x));
            Assert.That(normal.GetComponent<Button>().interactable, Is.False);
            Assert.That(hil.GetComponent<Button>().interactable, Is.False, "No sensors or algorithm selected");
            var profile = RosSimulationLauncher.FindAlgorithm(HilSimulationLauncher.Match("Car3", "hil_shredder", "orchard").algorithm);
            foreach (var component in new[] { profile.PerceptionComponent, profile.VisualPerceptionComponent,
                profile.LocalizationComponent, profile.PlannerComponent, profile.ControllerComponent })
                buttons.Single(button => button.name == "Component_" + component.Id).onClick.Invoke();
            SimulationLaunchGateTests.AssertLaunchWarning(hil.GetComponent<Button>(), "请先安装雷达、RTK、相机、IMU，再启动仿真。");
            foreach (string sensor in new[] { "Radar", "RTK", "Camera", "Imu" })
            {
                var data = getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, sensor) });
                Assert.That((bool)install.Invoke(bag, new[] { data, (object)true }), Is.True);
            }
            yield return new WaitForSeconds(1f);
            Assert.That(hil.GetComponent<Button>().interactable, Is.True);
            var wrong = RosSimulationLauncher.Localizations.First(option => option.Id != profile.LocalizationComponent.Id);
            buttons.Single(button => button.name == "Component_" + wrong.Id).onClick.Invoke();
            SimulationLaunchGateTests.AssertLaunchUnavailable(hil.GetComponent<Button>());
            buttons.Single(button => button.name == "Component_" + profile.LocalizationComponent.Id).onClick.Invoke();
            Assert.That(hil.GetComponent<Button>().interactable, Is.True);
            var radar = getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, "Radar") });
            install.Invoke(bag, new[] { radar, (object)false });
            yield return new WaitForSeconds(.3f);
            SimulationLaunchGateTests.AssertLaunchWarning(hil.GetComponent<Button>(), "请先安装雷达，再启动仿真。");
            Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo("orchard"));
        }

        [UnityTest]
        public IEnumerator FlashDialogUsesReportedProgressWithoutStartingHardware()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var directory = Path.Combine(temporaryRoot, ".local", "hil");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "state.json");
            File.WriteAllText(path, "{\"run_id\":\"ui-fixture\",\"phase\":\"flashing\",\"flash_progress\":0.7,\"flash_stage\":\"固件写入完成\"}");
            var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())
                .First(item => item.FullName == "Agribot.VehicleConfiguration.HilSimulationUi");
            type.GetMethod("Observe").Invoke(null, null);
            yield return new WaitForSecondsRealtime(.8f);
            var canvas = Find("HIL_FlashProgressCanvas");
            Assert.That(canvas.activeInHierarchy, Is.True);
            var fill = canvas.GetComponentsInChildren<Image>().Single(item => item.name == "ProgressFill");
            Assert.That(fill.sprite, Is.Not.Null);
            Assert.That(fill.fillAmount, Is.EqualTo(.7f).Within(.001));
            Assert.That(canvas.GetComponentsInChildren<Text>().Single(text => text.name == "Percent").text, Is.EqualTo("70%"));
            var panel = canvas.GetComponentsInChildren<RectTransform>().Single(item => item.name == "HilFlashProgress");
            Assert.That(panel.rect.width, Is.GreaterThan(0));
            Assert.That(panel.rect.width, Is.LessThanOrEqualTo(((RectTransform)canvas.transform).rect.width-32));
            yield return Capture("hil_flash_progress");
            File.WriteAllText(path, "{\"run_id\":\"ui-fixture\",\"phase\":\"configuring_can\"}");
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(canvas.activeSelf, Is.False);
        }
    }
}
