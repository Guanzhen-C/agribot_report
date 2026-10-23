using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class ImportedSceneCatalogTests
    {
        [Serializable] private sealed class Catalog { public string schemaVersion = "1.0"; public FarmScenario[] scenarios; }
        private string directory, previous;

        [SetUp] public void Setup()
        {
            previous = Environment.GetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT");
            directory = Path.Combine(Path.GetTempPath(), "agribot_import_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", directory);
            FarmScenarioCatalog.ResetSelection();
            AssistantConversation.Reset();
        }
        [TearDown] public void Cleanup()
        {
            Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", previous);
            FarmScenarioCatalog.ResetSelection();
            Directory.Delete(directory, true);
        }
        private FarmScenario AddScene()
        {
            string manifest = Path.Combine(directory, "园区 有空格", "scene.json");
            Directory.CreateDirectory(Path.GetDirectoryName(manifest)); File.WriteAllText(manifest, "{}");
            var item = new FarmScenario { id = "scan_fixture", name = "自动化所园区", imported = true,
                tasks = new[] { "扫描场景" }, spawn = new[] { 1f, 2f, .5f }, sceneManifest = manifest };
            File.WriteAllText(Path.Combine(directory, "catalog.json"), JsonUtility.ToJson(new Catalog { scenarios = new[] { item } }));
            FarmScenarioCatalog.RefreshImportedScenes(); return item;
        }
        [Test] public void NamedImportsPersistButNoSceneIsAutomaticallySelected()
        {
            AddScene();
            Assert.That(FarmScenarioCatalog.Scenarios.Single(s => s.imported).name, Is.EqualTo("自动化所园区"));
            FarmScenarioCatalog.SelectedId = "scan_fixture";
            FarmScenarioCatalog.ResetSelection();
            Assert.That(FarmScenarioCatalog.HasSelection, Is.False);
            Assert.That(FarmScenarioCatalog.Scenarios.Any(s => s.id == "scan_fixture"), Is.True);
        }
        [Test] public void MissingImportedAssetDoesNotBreakBuiltInScenes()
        {
            var item = AddScene(); File.Delete(item.sceneManifest);
            FarmScenarioCatalog.RefreshImportedScenes();
            Assert.That(FarmScenarioCatalog.Scenarios.Count(s => !s.imported), Is.EqualTo(9));
            Assert.That(FarmScenarioCatalog.Scenarios.Any(s => s.imported), Is.False);
        }
        [Test] public void ImportedManifestIsAnExtraLaunchArgumentAndAlgorithmIsPreserved()
        {
            var item = AddScene(); FarmScenarioCatalog.SelectedId = item.id;
            var method = typeof(RosSimulationLauncher).GetMethod("BuildStartInfo", BindingFlags.NonPublic | BindingFlags.Static);
            var profile = RosSimulationLauncher.Algorithms.First();
            var start = (System.Diagnostics.ProcessStartInfo)method.Invoke(null, new object[] { "/tmp/run.sh", "/tmp/export", profile });
            Assert.That(start.Arguments, Does.Contain("--scene-manifest"));
            Assert.That(start.Arguments, Does.Contain(item.sceneManifest));
            Assert.That(start.Arguments, Does.Contain("--scenario \"orchard\""));
            Assert.That(start.Arguments, Does.Contain("--localization \"" + profile.LocalizationComponent.Id + "\""));
        }

        [Test] public void ImportedSceneIdentitySurvivesHistoryTrimmingWithoutChangingCurrentSelection()
        {
            var item = AddScene(); FarmScenarioCatalog.SelectedId = item.id;
            AssistantConversation.SceneImported(item);
            Assert.That(AssistantConversation.State.sceneName, Is.EqualTo(item.name));
            Assert.That(AssistantConversation.State.sceneImported, Is.True);
            Assert.That(AssistantConversation.History().Last().content, Does.Contain("导入场景完成"));
            FarmScenarioCatalog.SelectedId = "orchard";
            for (int i = 0; i < 50; i++) AssistantConversation.RecordUi("界面操作");
            var state = AssistantConversation.Snapshot();
            Assert.That(state.sceneImported, Is.False);
            Assert.That(state.scene, Is.EqualTo("orchard"));
            Assert.That(state.latestImportedSceneId, Is.EqualTo(item.id));
            Assert.That(state.latestImportedSceneName, Is.EqualTo(item.name));
            AssistantConversation.NewConversation();
            Assert.That(AssistantConversation.State.scene, Is.EqualTo("orchard"));
            Assert.That(AssistantConversation.State.latestImportedSceneId, Is.Empty);
        }

        [TestCase(true)] [TestCase(false)]
        public void DeletedImportClearsOnlyItsOwnSelectionAndRecentReference(bool selected)
        {
            var item = AddScene();
            FarmScenarioCatalog.SelectedId = selected ? item.id : "orchard";
            AssistantConversation.SceneImported(item);
            File.WriteAllText(Path.Combine(directory, "catalog.json"), JsonUtility.ToJson(new Catalog { scenarios = Array.Empty<FarmScenario>() }));
            FarmScenarioCatalog.ImportedSceneDeleted(item);
            Assert.That(FarmScenarioCatalog.HasSelection, Is.EqualTo(!selected));
            Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo(selected ? "" : "orchard"));
            Assert.That(AssistantConversation.State.scene, Is.EqualTo(selected ? "" : "orchard"));
            Assert.That(AssistantConversation.State.sceneImported, Is.False);
            Assert.That(AssistantConversation.State.latestImportedSceneId, Is.Empty);
            Assert.That(AssistantConversation.State.latestImportedSceneName, Is.Empty);
            Assert.That(AssistantConversation.History().Last().content, Does.Contain("删除导入场景完成"));
            FarmScenarioCatalog.ResetSelection();
            Assert.That(FarmScenarioCatalog.Scenarios.Count(), Is.EqualTo(9));
            Assert.Throws<ArgumentException>(() => FarmScenarioCatalog.ImportedSceneDeleted(FarmScenarioCatalog.Scenarios.First()));
        }

        [Test] public void SceneCatalogCacheLivesInConversationButClearsOnImportDeleteAndNewChat()
        {
            var item = AddScene();
            var response = new NaturalRecommendationResponse { answer = "已查询目录。", sceneCatalogVersion = new string('a', 64) };
            AssistantConversation.Assistant(response);
            AssistantConversation.RecordUi("更换控制器", state => state.controller = "rpp");
            Assert.That(AssistantConversation.Snapshot().sceneCatalogVersion, Is.EqualTo(response.sceneCatalogVersion));
            AssistantConversation.SceneImported(item);
            Assert.That(AssistantConversation.State.sceneCatalogVersion, Is.Empty);
            AssistantConversation.Assistant(response);
            AssistantConversation.SceneDeleted(item);
            Assert.That(AssistantConversation.State.sceneCatalogVersion, Is.Empty);
            AssistantConversation.Assistant(response);
            AssistantConversation.NewConversation();
            Assert.That(AssistantConversation.State.sceneCatalogVersion, Is.Empty);
            AssistantConversation.Assistant(response);
            AssistantConversation.Reset();
            Assert.That(AssistantConversation.State.sceneCatalogVersion, Is.Empty);
        }
    }
}
