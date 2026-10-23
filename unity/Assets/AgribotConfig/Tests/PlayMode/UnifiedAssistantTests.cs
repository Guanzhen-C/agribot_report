using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class UnifiedAssistantTests
    {
        private static Component Component(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.GetType().Name == name);
        private static object Invoke(Component component, string method, params object[] args) =>
            component.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(component, args);

        [Test]
        public void SessionTracksOrderedChatActionsSnapshotAndMemory()
        {
            AssistantConversation.Reset();
            AssistantConversation.User("我想用履带车，保留机械臂。");
            AssistantConversation.RecordUi("选择车辆：Car3", s => s.car = "Car3");
            AssistantConversation.EquipmentChanged("Hand", "机械臂", true);
            AssistantConversation.EquipmentChanged("RADAR", "雷达", true);
            AssistantConversation.Assistant(new NaturalRecommendationResponse { answer = "已了解你的偏好。", memory = "用户偏好履带车和机械臂。" });
            Assert.That(AssistantConversation.History().Length, Is.EqualTo(5));
            Assert.That(AssistantConversation.History()[1].content, Does.Contain("非执行指令"));
            Assert.That(AssistantConversation.State.implement, Is.EqualTo("lite6"));
            Assert.That(AssistantConversation.State.sensors, Does.Contain("Radar"));
            var snapshot = AssistantConversation.Snapshot();
            AssistantConversation.EquipmentChanged("RADAR", "雷达", false);
            Assert.That(snapshot.sensors, Does.Contain("Radar"));
            Assert.That(AssistantConversation.State.sensors, Is.Empty);
            Assert.That(AssistantConversation.Memory, Does.Contain("机械臂"));
            for (int i = 0; i < 60; ++i) AssistantConversation.RecordUi("界面事件 " + i);
            Assert.That(AssistantConversation.Entries.Count, Is.EqualTo(66));
            Assert.That(AssistantConversation.History().Length, Is.EqualTo(40));
            AssistantConversation.NewConversation();
            Assert.That(AssistantConversation.Entries.Count, Is.EqualTo(1));
            Assert.That(AssistantConversation.Memory, Is.Empty);
            Assert.That(AssistantConversation.State.car, Is.EqualTo("Car3"));
        }

        [UnityTest]
        public IEnumerator SingleAssistantAndConversationSurviveGarageAndSceneChanges()
        {
            AssistantConversation.Reset();
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var panel = Component("NaturalRecommendationPanel");
            var main = Component("MainPanel");
            Assert.That(GameObject.Find("NaturalRecommendationEntry"), Is.Null);
            Assert.That(GameObject.Find("AlgorithmAdviser"), Is.Null);
            AssistantConversation.User("我想在葡萄园使用履带车。");
            AssistantConversation.Assistant(new NaturalRecommendationResponse { answer = "可以考虑Car3。", memory = "用户想在葡萄园使用履带车。" });
            Invoke(main, "SelectRecommendedVehicle", "Car3");
            yield return new WaitForSeconds(1f);
            Assert.That(AssistantConversation.State.car, Is.EqualTo("Car3"));
            FarmScenarioCatalog.SelectedId = "vineyard";
            Assert.That(AssistantConversation.State.scene, Is.EqualTo("vineyard"));
            Invoke(panel, "Open");
            yield return null;
            Assert.That(GameObject.Find("UserMessage").GetComponent<Text>().text, Does.Contain("葡萄园"));
            Assert.That(GameObject.Find("AssistantAnswer").GetComponent<Text>().text, Does.Contain("Car3"));
            Invoke(panel, "Close");
            Invoke(main, "ShowMainPanel");
            yield return new WaitForSeconds(1f);
            Assert.That(AssistantConversation.State.page, Is.EqualTo("garage"));
            Assert.That(AssistantConversation.State.car, Is.Empty);
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Assert.That(Component("NaturalRecommendationPanel"), Is.SameAs(panel));
            Assert.That(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(x => x.GetType().Name == "NaturalRecommendationPanel"), Is.EqualTo(1));
            Assert.That(AssistantConversation.Memory, Does.Contain("葡萄园"));
            Invoke(panel, "Open");
            yield return null;
            Assert.That(GameObject.Find("AssistantAnswer"), Is.Not.Null);
            ScreenCapture.CaptureScreenshot("/tmp/agribot_unified_assistant.png");
            yield return null;
            Invoke(panel, "Close");
        }

        [UnityTest]
        public IEnumerator InterfaceEventsRemainInContextButNeverAppearAsChatMessages()
        {
            AssistantConversation.Reset();
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var panel = Component("NaturalRecommendationPanel");
            Invoke(panel, "Open");
            yield return null;
            Assert.That(GameObject.Find("AssistantGreeting"), Is.Not.Null);
            Assert.That(GameObject.Find("InterfaceEvent"), Is.Null);
            Assert.That(GameObject.Find("UserMessage"), Is.Null);
            AssistantConversation.User("我想用履带车。");
            AssistantConversation.Assistant(new NaturalRecommendationResponse { answer = "可以选择Car3。" });
            for (int i = 0; i < 100; ++i) AssistantConversation.RecordUi("安装装备事件 " + i);
            Invoke(panel, "Open");
            yield return null;
            Assert.That(GameObject.Find("AssistantGreeting"), Is.Null);
            Assert.That(GameObject.Find("InterfaceEvent"), Is.Null);
            Assert.That(GameObject.Find("UserMessage").GetComponent<Text>().text, Does.Contain("履带车"));
            Assert.That(GameObject.Find("AssistantAnswer").GetComponent<Text>().text, Does.Contain("Car3"));
            Assert.That(AssistantConversation.History().Any(t => t.content.Contains("安装装备事件")), Is.True);
            Invoke(panel, "Close");
        }

        [UnityTest]
        public IEnumerator StartupHasNoAlgorithmAndNewConversationClearsChatAndDraft()
        {
            const string preference = "Agribot.RosSimulation.SelectedAlgorithm";
            bool hadPreference = PlayerPrefs.HasKey(preference);
            string oldPreference = PlayerPrefs.GetString(preference, "");
            string simulationStatus = RosSimulationLauncher.ReadManagedStatus();
            try
            {
                PlayerPrefs.SetString(preference, "stvl_off_navsat_smac_hybrid_rpp");
                FarmScenarioCatalog.ResetSelection();
                AssistantConversation.Reset();
                yield return SceneManager.LoadSceneAsync("scene1");
                yield return null;
                var selector = Component("LastPanel");
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var selected = selector.GetType().GetField("selectedAlgorithm", flags);
                var launch = (Button)selector.GetType().GetField("launchSelectedAlgorithmButton", flags).GetValue(selector);
                Assert.That(selected.GetValue(selector), Is.Null);
                Assert.That(launch.interactable, Is.False);
                Assert.That(AssistantConversation.State.algorithm, Is.Empty);
                Assert.That(AssistantConversation.Entries.Any(e => e.content.StartsWith("选择算法")), Is.False);
                var profile = RosSimulationLauncher.FindAlgorithm(RosSimulationLauncher.DefaultAlgorithmId);
                Invoke(selector, "SelectComponent", profile.PerceptionComponent);
                Assert.That(launch.interactable, Is.False);
                Assert.That(AssistantConversation.State.algorithm, Is.Empty);
                Assert.That(AssistantConversation.State.perception, Is.EqualTo(profile.PerceptionComponent.Id));
                foreach (var component in new[] { profile.VisualPerceptionComponent, profile.LocalizationComponent,
                    profile.PlannerComponent, profile.ControllerComponent }) Invoke(selector, "SelectComponent", component);
                Assert.That(launch.interactable, Is.False, "A complete algorithm still needs a scene selected in this run");
                Invoke(selector, "BeginExportRosConfiguration");
                GameObject.Find("Scenario_orchard").GetComponent<Button>().onClick.Invoke();
                Assert.That(launch.interactable, Is.True);
                Assert.That(AssistantConversation.State.algorithm, Is.EqualTo(profile.Id));
                Assert.That(PlayerPrefs.GetString(preference), Is.EqualTo("stvl_off_navsat_smac_hybrid_rpp"),
                    "Selections must not be saved as the next startup choice");
                AssistantConversation.User("之前想使用机械臂。");
                AssistantConversation.Assistant(new NaturalRecommendationResponse { answer = "好的。", memory = "偏好机械臂。" });
                var panel = Component("NaturalRecommendationPanel");
                Invoke(panel, "Open");
                yield return null;
                var input = GameObject.Find("RecommendationInput").GetComponent<TMPro.TMP_InputField>();
                input.text = "未发送的内容";
                GameObject.Find("NewConversation").GetComponent<Button>().onClick.Invoke();
                yield return null;
                Assert.That(input.text, Is.Empty);
                Assert.That(AssistantConversation.Memory, Is.Empty);
                Assert.That(AssistantConversation.Entries.Any(e => e.kind == "chat"), Is.False);
                Assert.That(AssistantConversation.State.algorithm, Is.EqualTo(profile.Id),
                    "New chat must not silently change the actual simulation configuration");
                Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo("orchard"));
                Assert.That(GameObject.Find("AssistantGreeting"), Is.Not.Null);
                Assert.That(GameObject.Find("AssistantAnswer"), Is.Null);
                Invoke(panel, "Close");
                yield return SceneManager.LoadSceneAsync("scene1");
                yield return null;
                Assert.That(AssistantConversation.State.algorithm, Is.Empty);
                Assert.That(RosSimulationLauncher.ReadManagedStatus(), Is.EqualTo(simulationStatus));
            }
            finally
            {
                if (hadPreference) PlayerPrefs.SetString(preference, oldPreference);
                else PlayerPrefs.DeleteKey(preference);
            }
        }

        [UnityTest]
        public IEnumerator EntireAppStartsInGarageWithoutRestoringSceneOrInstalledEquipment()
        {
            var files = new Dictionary<string, string>();
            string simulationStatus = RosSimulationLauncher.ReadManagedStatus();
            try
            {
                foreach (string name in new[] { "equipment-bag-state.json", "equipment-bag-state-scene1.json", "equipment-bag-state-copy.json" })
                {
                    string path = Path.Combine(Application.persistentDataPath, name);
                    files[path] = File.Exists(path) ? File.ReadAllText(path) : null;
                    File.WriteAllText(path, "{\"version\":2,\"inventoryInitialized\":true,\"ownedItemIds\":[\"old-item\"],\"installedItemIds\":[\"old-item\"]}");
                }
                for (int startup = 0; startup < 2; ++startup)
                {
                    // The two callbacks run before the initial scene in each new player process.
                    FarmScenarioCatalog.ResetSelection();
                    AssistantConversation.Reset();
                    yield return SceneManager.LoadSceneAsync("scene1");
                    yield return null;
                    Assert.That(AssistantConversation.State.page, Is.EqualTo("garage"));
                    Assert.That(AssistantConversation.State.car, Is.Empty);
                    Assert.That(AssistantConversation.State.scene, Is.Empty);
                    Assert.That(AssistantConversation.State.algorithm, Is.Empty);
                    Assert.That(AssistantConversation.State.sensors, Is.Empty);
                    Assert.That(FarmScenarioCatalog.HasSelection, Is.False);
                    var main = Component("MainPanel");
                    Assert.That(main.GetType().GetField("visibleCar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(main), Is.Null);
                    var selector = Component("LastPanel");
                    var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                    Assert.That(selector.GetType().GetField("selectedAlgorithm", flags).GetValue(selector), Is.Null);
                    Assert.That(((Text)selector.GetType().GetField("currentScene", flags).GetValue(selector)).text, Does.Contain("未选择"));

                    Invoke(main, "SelectRecommendedVehicle", "Car3");
                    yield return new WaitForSeconds(1f);
                    Invoke(main, "OpenRecommendedEquipment");
                    yield return new WaitForSeconds(.8f);
                    var bag = Component("EquipmentBagController");
                    var getter = bag.GetType().GetMethod("GetEquipment");
                    var slotType = getter.GetParameters()[0].ParameterType;
                    foreach (string slot in new[] { "Radar", "RTK", "Camera", "Imu", "Hand" })
                    {
                        var equipment = getter.Invoke(bag, new[] { Enum.Parse(slotType, slot) });
                        Assert.That(equipment, Is.Not.Null, slot);
                        Assert.That(Invoke(bag, "IsInstalled", equipment), Is.False, slot + " must start uninstalled");
                    }
                    var radar = getter.Invoke(bag, new[] { Enum.Parse(slotType, "Radar") });
                    Assert.That(Invoke(bag, "InstallEquipment", radar), Is.True);
                    yield return new WaitForSeconds(.8f);
                    Invoke(main, "CloseEquipmentForRepair");
                    Invoke(main, "OpenRecommendedEquipment");
                    yield return new WaitForSeconds(.8f);
                    Assert.That(Invoke(bag, "IsInstalled", radar), Is.True, "Page changes preserve this run's assembly");
                    FarmScenarioCatalog.SelectedId = "vineyard";
                    foreach (var path in files.Keys)
                        Assert.That(File.ReadAllText(path), Does.Contain("old-item"), "Legacy files must not be overwritten");
                }
                Assert.That(RosSimulationLauncher.ReadManagedStatus(), Is.EqualTo(simulationStatus));
            }
            finally
            {
                foreach (var file in files)
                    if (file.Value != null) File.WriteAllText(file.Key, file.Value);
                    else if (File.Exists(file.Key)) File.Delete(file.Key);
            }
        }

        [UnityTest]
        public IEnumerator AlternativeAlgorithmRevalidatesWithoutStartingSimulation()
        {
            string status = RosSimulationLauncher.ReadManagedStatus();
            var item = new NaturalRecommendation { id = "Car3|vineyard|lite6@voxel_instance_segmentation_navsat_smac_2d_rpp",
                car = "Car3", preset = "unity_car3", algorithm = "voxel_instance_segmentation_navsat_smac_2d_rpp", action = "algorithm" };
            // Resolve once through the same CLI to get its current configuration version.
            var process = new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo("/usr/bin/python3")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                Arguments = "-m agribot_farm_sim.natural_recommendation --workspace-src " + NaturalRecommendationClient.WorkspaceSource + " --candidate-id " + item.id
            }};
            process.StartInfo.Environment["PYTHONPATH"] = System.IO.Path.Combine(NaturalRecommendationClient.WorkspaceSource, "agribot_farm_sim") + ":" +
                System.IO.Path.Combine(NaturalRecommendationClient.WorkspaceSource, "agribot_vehicle_config_tools");
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            while (!process.HasExited) yield return null;
            item.vehicleConfigSha256 = JsonUtility.FromJson<NaturalRecommendationResponse>(output.Result).recommendations[0].vehicleConfigSha256;
            process.Dispose();
            var task = NaturalRecommendationClient.Revalidate(item, CancellationToken.None);
            while (!task.IsCompleted) yield return null;
            Assert.That(task.IsFaulted, Is.False, task.Exception?.GetBaseException().Message);
            Assert.That(task.Result.action, Is.EqualTo("algorithm"));
            Assert.That(task.Result.algorithm, Is.EqualTo(item.algorithm));
            Assert.That(RosSimulationLauncher.ReadManagedStatus(), Is.EqualTo(status));
        }
    }
}
