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
            Assert.That(hil.GetComponent<Button>().interactable, Is.True);
            Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo("orchard"));
        }
    }
}
