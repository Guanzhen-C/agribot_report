using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    [Serializable]
    public sealed class FarmScenario
    {
        public string id;
        public string name;
        public string[] tasks;
        public string source;
        public float[] spawn;
        public bool imported;
        public string sceneManifest;
        public string previewFile;
    }

    public static class FarmScenarioCatalog
    {
        [Serializable]
        private sealed class Catalog
        {
            public string schemaVersion;
            public FarmScenario[] scenarios;
        }

        private static FarmScenario[] scenarios;
        private static string selectedId = "";
        public static FarmScenario[] Scenarios => scenarios ??= Load();
        public static bool HasSelection => !string.IsNullOrEmpty(selectedId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetSelection() { selectedId = ""; scenarios = null; }
        public static string ImportedLibraryDirectory => Path.GetFullPath(
            Environment.GetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT") ?? Path.Combine(
                NaturalRecommendationClient.WorkspaceSource, "..", "..", ".local", "scanned_scenes"));
        public static FarmScenario Selected => Scenarios.FirstOrDefault(item => item.id == selectedId);
        public static void RefreshImportedScenes() => scenarios = null;

        public static void ImportedSceneDeleted(FarmScenario scene)
        {
            if (scene == null || !scene.imported) throw new ArgumentException("只能删除导入场景。");
            scenarios = null;
            if (selectedId == scene.id) selectedId = "";
            AssistantConversation.SceneDeleted(scene);
        }

        public static string SelectedId
        {
            get => selectedId;
            set
            {
                if (!Scenarios.Any(item => item.id == value))
                    throw new ArgumentException("未知作业场景：" + value);
                selectedId = value;
                if (AssistantConversation.State.scene != value)
                    AssistantConversation.RecordUi("选择场景：" + Scenarios.First(s => s.id == value).name,
                        state =>
                        {
                            state.scene = value; state.sceneName = Selected.name;
                            state.sceneImported = Selected.imported;
                        });
            }
        }

        private static FarmScenario[] Load()
        {
            var resource = Resources.Load<TextAsset>("farm_scenarios");
            if (resource == null)
                throw new InvalidOperationException("缺少农机场景目录。");
            var catalog = JsonUtility.FromJson<Catalog>(resource.text);
            if (catalog.schemaVersion != "1.0" || catalog.scenarios == null || catalog.scenarios.Length == 0)
                throw new InvalidOperationException("农机场景目录格式无效。");
            string importedPath = Path.Combine(ImportedLibraryDirectory, "catalog.json");
            if (!File.Exists(importedPath)) return catalog.scenarios;
            try
            {
                var local = JsonUtility.FromJson<Catalog>(File.ReadAllText(importedPath));
                if (local?.schemaVersion != "1.0" || local.scenarios == null) return catalog.scenarios;
                var known = catalog.scenarios.Select(item => item.id).ToHashSet();
                var imported = local.scenarios.Where(item => item != null && item.imported &&
                    !string.IsNullOrWhiteSpace(item.id) && item.id.StartsWith("scan_", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(item.name) && item.spawn?.Length == 3 &&
                    File.Exists(item.sceneManifest) && known.Add(item.id));
                return catalog.scenarios.Concat(imported).ToArray();
            }
            catch (Exception)
            {
                Debug.LogWarning("本机导入场景目录暂不可读，内置场景仍可使用。");
                return catalog.scenarios;
            }
        }
    }
}
