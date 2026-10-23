using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class ImportedSceneUiTests
    {
        [Serializable] private sealed class Catalog { public string schemaVersion = "1.0"; public FarmScenario[] scenarios; }
        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(item => item.gameObject.scene.IsValid() && item.name == name).gameObject;
        [UnityTest] public IEnumerator ImportDialogUsesEditableChineseNameAndDoesNotAutoImport()
        {
            yield return SceneManager.LoadSceneAsync("scene1"); yield return null;
            Find("Car0_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            var panel = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(item => item.GetType().Name == "LastPanel");
            var dialog = Find("ROS_Algorithm_Selection"); dialog.SetActive(true);
            Find("ImportScene").GetComponent<Button>().onClick.Invoke(); yield return null;
            var input = Find("SceneNameInput").GetComponent<TMP_InputField>();
            input.text = "自动化所园区";
            Assert.That(input.text, Is.EqualTo("自动化所园区"));
            Assert.That(input.characterLimit, Is.EqualTo(48));
            Assert.That(Find("StartImport").GetComponent<Button>().interactable, Is.False);
            Assert.That(Find("SceneViewport").GetComponent<ScrollRect>().vertical, Is.True);
            Find("CancelImport").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(Find("SceneImportDialog").activeInHierarchy, Is.False);
            Assert.That(FarmScenarioCatalog.HasSelection, Is.False);
            Assert.That(panel, Is.Not.Null);
        }

        [UnityTest] public IEnumerator ImportedAssistantPlanRevalidatesAndAppliesWithoutStartingSimulation()
        {
            string previous = Environment.GetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT");
            string directory = Path.Combine(Path.GetTempPath(), "agribot_assistant_scene_" + Guid.NewGuid().ToString("N"));
            string simulation = RosSimulationLauncher.ReadManagedStatus();
            Directory.CreateDirectory(directory);
            try
            {
                Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", directory);
                foreach (string file in new[] { "world.sdf", "map.yaml", "waypoints.yaml" })
                    File.WriteAllText(Path.Combine(directory, file), "fixture");
                string manifest = Path.Combine(directory, "scene.json");
                File.WriteAllText(manifest, "{\"schema_version\":1,\"world\":\"world.sdf\",\"map\":\"map.yaml\",\"waypoints\":\"waypoints.yaml\",\"spawn\":[0,0,0]}");
                var scene = new FarmScenario { id = "scan_assistant", name = "助手测试园区", imported = true,
                    source = "LCC2", tasks = new[] { "扫描场景" }, spawn = new[] { 0f, 0f, 0f }, sceneManifest = manifest };
                File.WriteAllText(Path.Combine(directory, "catalog.json"), JsonUtility.ToJson(new Catalog { scenarios = new[] { scene } }));
                FarmScenarioCatalog.ResetSelection(); AssistantConversation.Reset();
                yield return SceneManager.LoadSceneAsync("scene1"); yield return null;
                Find("Car0_Img").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.8f);
                FarmScenarioCatalog.SelectedId = scene.id; AssistantConversation.SceneImported(scene);
                using var process = new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo("/usr/bin/python3")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    Arguments = "-m agribot_farm_sim.natural_recommendation --workspace-src " + NaturalRecommendationClient.WorkspaceSource +
                        " --candidate-id Car0|scan_assistant|none",
                }};
                process.StartInfo.Environment["PYTHONPATH"] = Path.Combine(NaturalRecommendationClient.WorkspaceSource, "agribot_farm_sim") + ":" +
                    Path.Combine(NaturalRecommendationClient.WorkspaceSource, "agribot_vehicle_config_tools");
                process.Start(); var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                while (!process.HasExited) yield return null;
                Assert.That(process.ExitCode, Is.Zero, stdout.Result + stderr.Result);
                var item = JsonUtility.FromJson<NaturalRecommendationResponse>(stdout.Result).recommendations.Single();
                Assert.That(item.sceneImported, Is.True);
                Assert.That(item.sceneConfigSha256, Is.Not.Empty);
                var verified = NaturalRecommendationClient.Revalidate(item, CancellationToken.None);
                while (!verified.IsCompleted) yield return null;
                Assert.That(verified.IsFaulted, Is.False, verified.Exception?.GetBaseException().Message);
                var panel = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .First(value => value.GetType().Name == "LastPanel");
                panel.GetType().GetMethod("ApplyRecommendation").Invoke(panel, new object[] { verified.Result });
                Assert.That(FarmScenarioCatalog.Selected.name, Is.EqualTo(scene.name));
                Assert.That(Find("Scenario_" + scene.id).transform.Find("SceneTasks").GetComponent<Text>().text,
                    Does.StartWith("用户导入"));
                Assert.That(Find("Scenario_" + scene.id).activeInHierarchy, Is.False, "Algorithm tab is open after applying the plan");
                Assert.That(AssistantConversation.State.algorithm, Is.EqualTo(item.algorithm));
                Assert.That(RosSimulationLauncher.ReadManagedStatus(), Is.EqualTo(simulation));
                File.AppendAllText(Path.Combine(directory, "world.sdf"), "changed");
                var stale = NaturalRecommendationClient.Revalidate(item, CancellationToken.None);
                while (!stale.IsCompleted) yield return null;
                Assert.That(stale.IsFaulted, Is.True, "Changed scene must invalidate the old plan");
                Assert.That(RosSimulationLauncher.ReadManagedStatus(), Is.EqualTo(simulation));
            }
            finally
            {
                Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", previous);
                FarmScenarioCatalog.ResetSelection(); AssistantConversation.Reset();
                Directory.Delete(directory, true);
            }
        }

        [UnityTest] public IEnumerator DeleteButtonConfirmsBlocksActiveSimulationAndClearsCacheAndSelection()
        {
            string previous = Environment.GetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT");
            string previousScript = Environment.GetEnvironmentVariable("AGRIBOT_ROS_SYNC_SCRIPT");
            string previousSource = Environment.GetEnvironmentVariable("AGRIBOT_WORKSPACE_SRC");
            string workspaceSource = NaturalRecommendationClient.WorkspaceSource;
            string directory = Path.Combine(Path.GetTempPath(), "agribot_scene_delete_" + Guid.NewGuid().ToString("N"));
            string cache = Path.Combine(directory, "lcc2_v2_0123456789abcdef01234567");
            string script = Path.Combine(directory, "status.sh");
            Directory.CreateDirectory(cache);
            try
            {
                Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", directory);
                Environment.SetEnvironmentVariable("AGRIBOT_WORKSPACE_SRC", workspaceSource);
                Environment.SetEnvironmentVariable("AGRIBOT_ROS_SYNC_SCRIPT", script);
                File.WriteAllText(script, "echo stopped\n");
                string manifest = Path.Combine(cache, "scene.json"); File.WriteAllText(manifest, "{}");
                var scene = new FarmScenario { id = "scan_delete", name = "待删除测试园区", imported = true,
                    tasks = new[] { "扫描场景" }, spawn = new[] { 0f, 0f, 0f }, sceneManifest = manifest };
                File.WriteAllText(Path.Combine(directory, "catalog.json"), JsonUtility.ToJson(new Catalog { scenarios = new[] { scene } }));
                FarmScenarioCatalog.ResetSelection(); AssistantConversation.Reset();
                yield return SceneManager.LoadSceneAsync("scene1"); yield return null;
                Find("Car0_Img").GetComponent<Button>().onClick.Invoke(); yield return new WaitForSeconds(.8f);
                Find("ROS_Algorithm_Selection").SetActive(true);
                Find("SceneTab").GetComponent<Button>().onClick.Invoke(); yield return null;
                var card = Find("Scenario_" + scene.id);
                var delete = card.transform.Find("DeleteScene").GetComponent<Button>();
                Assert.That(delete.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(36, 36)));
                foreach (var builtin in FarmScenarioCatalog.Scenarios.Where(s => !s.imported))
                    Assert.That(Find("Scenario_" + builtin.id).transform.Find("DeleteScene"), Is.Null);
                delete.onClick.Invoke(); yield return null;
                Assert.That(Find("SceneDeleteDialog").activeInHierarchy, Is.True);
                Assert.That(FarmScenarioCatalog.HasSelection, Is.False, "Delete must not select the parent card");
                Find("CancelSceneDelete").GetComponent<Button>().onClick.Invoke(); yield return null;
                Assert.That(File.Exists(manifest), Is.True);
                Assert.That(Find("SceneDeleteDialog").activeInHierarchy, Is.False);

                FarmScenarioCatalog.SelectedId = scene.id; AssistantConversation.SceneImported(scene);
                delete.onClick.Invoke(); File.WriteAllText(script, "echo running\n");
                Find("ConfirmSceneDelete").GetComponent<Button>().onClick.Invoke();
                float deadline = Time.realtimeSinceStartup + 15;
                while (!Find("ConfirmSceneDelete").GetComponent<Button>().interactable && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Find("DeleteBody").transform.Find("Status").GetComponent<Text>().text, Does.Contain("请先停止仿真"));
                Assert.That(File.Exists(manifest), Is.True);
                Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo(scene.id));

                File.WriteAllText(script, "echo stopped\n");
                Find("ConfirmSceneDelete").GetComponent<Button>().onClick.Invoke();
                deadline = Time.realtimeSinceStartup + 15;
                while (!Find("ConfirmSceneDelete").GetComponent<Button>().interactable && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(Find("SceneDeleteDialog").activeSelf, Is.False,
                    Find("DeleteBody").transform.Find("Status").GetComponent<Text>().text);
                Assert.That(Directory.Exists(cache), Is.False);
                Assert.That(FarmScenarioCatalog.HasSelection, Is.False);
                Assert.That(AssistantConversation.State.scene, Is.Empty);
                Assert.That(AssistantConversation.State.latestImportedSceneId, Is.Empty);
                Assert.That(FarmScenarioCatalog.Scenarios.Length, Is.EqualTo(9));
                Assert.That(Find("Launch").GetComponent<Button>().interactable, Is.False);
                Assert.That(RosSimulationLauncher.ReadManagedStatus(), Is.EqualTo("stopped"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", previous);
                Environment.SetEnvironmentVariable("AGRIBOT_ROS_SYNC_SCRIPT", previousScript);
                Environment.SetEnvironmentVariable("AGRIBOT_WORKSPACE_SRC", previousSource);
                FarmScenarioCatalog.ResetSelection(); AssistantConversation.Reset();
                Directory.Delete(directory, true);
            }
        }
    }
}
