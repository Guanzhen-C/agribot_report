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
    public sealed class FuxiFarmUiTests
    {
        [Serializable] private sealed class Catalog { public string schemaVersion = "1.0"; public FarmScenario[] scenarios; }
        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(item => item.gameObject.scene.IsValid() && item.name == name).gameObject;

        [UnityTest] public IEnumerator SelectingFixedFarmAppliesTheAcceptedRecipeAndRestoresOtherScenes()
        {
            string previous = Environment.GetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT");
            string directory = Path.Combine(Path.GetTempPath(), "agribot_fuxi_ui_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "scene.json"), "{}");
            File.WriteAllText(Path.Combine(directory, "profile.json"), "{}");
            var scene = new FarmScenario { id = "scan_fuxi_ui", name = "伏羲农场", imported = true,
                spawn = new[] { 0f, 0f, 0f }, sceneManifest = Path.Combine(directory, "scene.json"),
                simulationProfile = Path.Combine(directory, "profile.json"), fixedVehicleId = "unity_car3",
                fixedAlgorithmId = "stvl_off_navsat_smac_hybrid_rpp" };
            File.WriteAllText(Path.Combine(directory, "catalog.json"), JsonUtility.ToJson(new Catalog { scenarios = new[] { scene } }));
            try
            {
                Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", directory);
                FarmScenarioCatalog.ResetSelection(); AssistantConversation.Reset();
                yield return SceneManager.LoadSceneAsync("scene1"); yield return null;
                Find("Car3_Img").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.8f);
                var dialog = Find("ROS_Algorithm_Selection"); dialog.SetActive(true);
                Find("Scenario_" + scene.id).GetComponent<Button>().onClick.Invoke(); yield return null;
                Assert.That(AssistantConversation.State.car, Is.EqualTo("Car3"));
                Assert.That(AssistantConversation.State.algorithm, Is.EqualTo(scene.fixedAlgorithmId));
                Assert.That(Find("Launch").GetComponent<Button>().interactable, Is.True);
                var panel = Find("AlgorithmDetails");
                Assert.That(panel.GetComponentsInChildren<Text>(true).Any(text => text.text == "预设104个经停点"), Is.True);
                var controller = Find("Component_mppi").GetComponent<Button>();
                Assert.That(controller.interactable, Is.True);
                controller.onClick.Invoke(); yield return null;
                string manualChoice = AssistantConversation.State.algorithm;
                Assert.That(manualChoice, Is.Not.EqualTo(scene.fixedAlgorithmId));
                Find("Scenario_" + scene.id).GetComponent<Button>().onClick.Invoke(); yield return null;
                Assert.That(AssistantConversation.State.algorithm, Is.EqualTo(manualChoice));
                Find("Scenario_orchard").GetComponent<Button>().onClick.Invoke(); yield return null;
                Assert.That(FarmScenarioCatalog.Selected.HasFixedRoute, Is.False);
                Assert.That(panel.GetComponentsInChildren<Text>(true).Any(text => text.text == "预设104个经停点"), Is.False);
            }
            finally
            {
                Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", previous);
                FarmScenarioCatalog.ResetSelection(); AssistantConversation.Reset();
                Directory.Delete(directory, true);
            }
        }
    }
}
