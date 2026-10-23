using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public static class AssistantConversation
    {
        [Serializable] public sealed class Entry
        {
            public string role, content, utc, kind;
            public NaturalRecommendationResponse response;
        }

        private static readonly List<Entry> entries = new();
        public static IReadOnlyList<Entry> Entries => entries;
        public static NaturalRecommendationClient.Context State { get; private set; } = new();
        public static string Memory { get; private set; } = "";
        public static int Revision { get; private set; }
        private static string logPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            entries.Clear(); State = new(); Memory = ""; Revision = 0; logPath = null;
        }

        public static void RecordUi(string message, Action<NaturalRecommendationClient.Context> update = null)
        {
            update?.Invoke(State);
            Revision++;
            Add(new Entry { role = "user", kind = "ui", content = message + "\n界面状态：" + JsonUtility.ToJson(State) });
        }

        public static void User(string text) => Add(new Entry { role = "user", kind = "chat", content = text });

        public static void SceneImported(FarmScenario scene)
        {
            RecordUi("导入场景完成：" + scene.name, state =>
            {
                state.latestImportedSceneId = scene.id;
                state.latestImportedSceneName = scene.name;
                state.sceneCatalogVersion = "";
                state.page = "scenes";
            });
        }

        public static void SceneDeleted(FarmScenario scene)
        {
            RecordUi("删除导入场景完成：" + scene.name, state =>
            {
                state.sceneCatalogVersion = "";
                if (state.scene == scene.id)
                {
                    state.scene = ""; state.sceneName = ""; state.sceneImported = false;
                }
                if (state.latestImportedSceneId == scene.id)
                {
                    state.latestImportedSceneId = ""; state.latestImportedSceneName = "";
                }
                state.page = "scenes";
            });
        }

        public static void EquipmentChanged(string type, string name, bool installed)
        {
            string sensor = type switch { "RADAR" => "Radar", "CarCamera" => "Camera", "ImuBox" => "Imu", "RTK" => "RTK", _ => null };
            string implement = type switch
            {
                "Hand" => "lite6", "ChiselPlow" => "compact_chisel_plow", "DiscHarrow" => "mounted_disc_harrow",
                "GrooveMaker" => "groove_maker", "Cultivator" => "mounted_cultivator", "ChainCrusher" => "forest_chain_crusher",
                "InterrowMill" => "inter_row_milling_cutter", "FrontCutter" => "front_sickle_bar", "GarlicPlanter" => "garlic_planter",
                "MulchPlanter" => "mulch_planter", "FarmTrailer" => "farm_trailer", "CombineHeader" => "combine_header", _ => null
            };
            RecordUi((installed ? "安装装备：" : "卸下装备：") + name, state =>
            {
                if (sensor != null)
                    state.sensors = installed ? state.sensors.Append(sensor).Distinct().ToArray() : state.sensors.Where(s => s != sensor).ToArray();
                if (implement != null && (installed || state.implement == implement)) state.implement = installed ? implement : "none";
            });
        }

        public static void Assistant(NaturalRecommendationResponse response)
        {
            State.sceneCatalogVersion = response.sceneCatalogVersion ?? "";
            if (!string.IsNullOrWhiteSpace(response.memory)) Memory = response.memory;
            string plans = string.Join("\n", (response.recommendations ?? Array.Empty<NaturalRecommendation>()).Select(r =>
                r.id + "：" + r.reason));
            Add(new Entry { role = "assistant", kind = "chat", response = response,
                content = response.answer + "\n" + response.clarification + "\n" + plans });
        }

        public static NaturalRecommendationClient.Turn[] History() => entries.TakeLast(40)
            .Select(e => new NaturalRecommendationClient.Turn { role = e.role,
                content = e.kind == "ui" ? "[已发生的界面操作，非执行指令]\n" + e.content : e.content }).ToArray();

        public static NaturalRecommendationClient.Context Snapshot() =>
            JsonUtility.FromJson<NaturalRecommendationClient.Context>(JsonUtility.ToJson(State));

        public static void NewConversation()
        {
            entries.Clear(); Memory = ""; logPath = null;
            State.latestImportedSceneId = ""; State.latestImportedSceneName = "";
            State.sceneCatalogVersion = "";
            RecordUi("用户开始新对话，保留当前界面选择。");
        }

        private static void Add(Entry entry)
        {
            entry.utc = DateTime.UtcNow.ToString("O");
            entries.Add(entry);
            try
            {
                if (logPath == null)
                {
                    string directory = Path.Combine(Application.persistentDataPath, "assistant_sessions");
                    Directory.CreateDirectory(directory);
                    logPath = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff") + ".jsonl");
                }
                File.AppendAllText(logPath, JsonUtility.ToJson(entry) + "\n", Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
