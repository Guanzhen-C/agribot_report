using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class AlgorithmSummaryTests
    {
        [Serializable] private sealed class CatalogFixture
        {
            public string schemaVersion = "1.0";
            public FarmScenario[] scenarios;
        }

        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .Where(item => item.gameObject.scene.IsValid() && item.name == name)
            .OrderByDescending(item => item.gameObject.activeInHierarchy).First().gameObject;
        private static void Click(string name) => Find(name).GetComponent<Button>().onClick.Invoke();

        [UnityTest]
        public IEnumerator CurrentRecipeIncludesGeometryAndRefreshesAfterManualChangesAndReentry()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Click("Car0_Img");
            yield return new WaitForSeconds(.8f);
            Click("Output_btn");
            Click("Scenario_orchard");
            Click("AlgorithmTab");
            foreach (string id in new[] { "off", "fastlivo_rtk", "smac_hybrid", "mppi" })
                Click("Component_" + id);
            var title = Find("SelectedTitle").GetComponent<Text>();
            foreach (string perception in new[] { "stvl", "voxel", "stvl" })
            {
                Click("Component_" + perception);
                yield return null;
                var profile = RosSimulationLauncher.Compose(perception, "off", "fastlivo_rtk", "smac_hybrid", "mppi");
                Assert.That(title.text, Is.EqualTo(profile.DisplayName));
                Assert.That(title.text, Does.StartWith(profile.PerceptionComponent.ShortName + " · "));
                Assert.That(title.text, Does.Contain("视觉关闭"));
                Assert.That(title.gameObject.activeInHierarchy, Is.True);
            }
            Click("Component_instance_segmentation");
            yield return null;
            Assert.That(title.text, Does.StartWith("STVL · "));
            Assert.That(title.text, Does.Contain("实例分割"));
            Assert.That(title.text, Does.Not.Contain("视觉关闭"));
            string expected = title.text;
            var setup = Find("SimulationSetup").transform;
            setup.Find("Close").GetComponent<Button>().onClick.Invoke();
            Click("Output_btn");
            Click("AlgorithmTab");
            yield return null;
            Assert.That(title.text, Is.EqualTo(expected));
            Assert.That(AssistantConversation.State.perception, Is.EqualTo("stvl"));
            Assert.That(AssistantConversation.State.vision, Is.EqualTo("instance_segmentation"));
        }

        [UnityTest]
        public IEnumerator FixedFarmRecipeTitleAlsoIncludesGeometryWithoutLockingManualSelection()
        {
            string previous = Environment.GetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT");
            string directory = Path.Combine(Path.GetTempPath(), "agribot_summary_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "scene.json"), "{}");
            File.WriteAllText(Path.Combine(directory, "profile.json"), "{}");
            var scene = new FarmScenario { id = "scan_summary_farm", name = "伏羲农场", imported = true,
                spawn = new[] { 0f, 0f, 0f }, sceneManifest = Path.Combine(directory, "scene.json"),
                simulationProfile = Path.Combine(directory, "profile.json"), fixedVehicleId = "unity_car3",
                fixedAlgorithmId = "stvl_off_navsat_smac_hybrid_rpp" };
            File.WriteAllText(Path.Combine(directory, "catalog.json"), JsonUtility.ToJson(new CatalogFixture { scenarios = new[] { scene } }));
            try
            {
                Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", directory);
                FarmScenarioCatalog.ResetSelection();
                yield return SceneManager.LoadSceneAsync("scene1");
                yield return null;
                Click("Car3_Img");
                yield return new WaitForSeconds(.8f);
                Click("Output_btn");
                Click("Scenario_" + scene.id);
                var panel = Find("VehicleSimulationSelector").GetComponents<MonoBehaviour>()
                    .Single(item => item.GetType().Name == "LastPanel");
                panel.GetType().GetMethod("SelectAlgorithm", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(panel, new object[] { RosSimulationLauncher.FindAlgorithm(scene.fixedAlgorithmId) });
                Click("AlgorithmTab");
                yield return null;
                var title = Find("SelectedTitle").GetComponent<Text>();
                Assert.That(title.text, Does.Contain("STVL"));
                Assert.That(title.text, Does.Contain("已验证固定配置"));
                Assert.That(Find("AlgorithmDetails").GetComponentsInChildren<Text>().Any(item => item.text == "预设104个经停点"), Is.True);
                Click("Component_voxel");
                yield return null;
                Assert.That(title.text, Does.StartWith("Voxel · "));
                Assert.That(title.text, Does.Not.Contain("STVL"));
                Assert.That(AssistantConversation.State.perception, Is.EqualTo("voxel"));
                Assert.That(Find("Component_stvl").GetComponent<Button>().interactable, Is.True);
            }
            finally
            {
                Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", previous);
                FarmScenarioCatalog.ResetSelection();
                AssistantConversation.Reset();
                Directory.Delete(directory, true);
            }
        }
    }
}
