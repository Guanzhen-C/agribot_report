using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Editor
{
    public static partial class GarageThumbnailBuilder
    {
        [InitializeOnLoadMethod]
        private static void Initialize() => EditorApplication.delayCall += PrepareRuntimeImages;

        [MenuItem("农机配置/刷新缩略图资源")]
        public static void PrepareRuntimeImages()
        {
            string destination = "Assets/AgribotConfig/Resources/" + GarageThumbnailLibrary.ResourceFolder;
            Directory.CreateDirectory(destination);
            for (int i = 0; i < 6; i++)
            {
                string current = "Assets/AgribotConfig/Resources/LocalVehicleAppearances/unity_car3/icon.png";
                if (!CurrentImageReady("Car" + i))
                    Copy(i == 3 && File.Exists(current) ? current : "Assets/Resources/Sprites/Car" + i + ".png", "Car" + i, destination);
            }
            Copy("Assets/equips/Radar.png", "Equipment_RADAR", destination);
            Copy("Assets/equips/mushroom.png", "Equipment_RTK", destination);
            Copy("Assets/equips/camera.png", "Equipment_CarCamera", destination);
            Copy("Assets/equips/IMUtu.png", "Equipment_ImuBox", destination);
            foreach (var preset in FarmVehicleLibrary.Presets.Where(p => FarmVehicleLibrary.IsInstalled(p.id)))
            {
                string id = preset.implementId == "none" ? preset.id : preset.implementId;
                string source = preset.id.StartsWith("unity_car", System.StringComparison.Ordinal)
                    ? "Assets/Resources/Sprites/" + preset.garageName + ".png"
                    : "Assets/AgribotConfig/Resources/LocalFarmAssets/" + id + "_icon.png";
                if (!CurrentImageReady(id)) Copy(source, id, destination);
            }
            string workspace = NaturalRecommendationClient.WorkspaceSource;
            foreach (var scene in FarmScenarioCatalog.Scenarios)
            {
                if (File.Exists(Path.Combine(destination, "Scenario_" + scene.id + ".bytes.rendered"))) continue;
                string image = Path.Combine(workspace, "agribot_farm_sim", "assets", scene.source, "map_preview.png");
                if (File.Exists(image)) Copy(image, "Scenario_" + scene.id, destination);
            }
        }

        public static void ValidateTrackVehicleImages()
        {
            foreach (string id in new[] { "unity_car3", "tracked_harvester" })
            {
                string group = id == "unity_car3" ? "LocalVehicleAppearances" : "LocalFarmAssets";
                string directory = "Assets/AgribotConfig/Resources/" + group + "/" + id;
                string image = id == "unity_car3" ? directory + "/icon.png" : directory + "_icon.png";
                string digest = FarmVehicleLibrary.Digest(File.ReadAllBytes(directory + "/preview.bytes"));
                if (!File.Exists(image) || !File.Exists(image + ".source.txt") ||
                    File.ReadAllText(image + ".source.txt").Trim() != digest)
                    throw new System.InvalidOperationException("车型缩略图与当前模型不一致，请先刷新履带车型缩略图：" + id);
            }
        }

        private static void Copy(string source, string id, string destination)
        {
            if (!File.Exists(source)) throw new FileNotFoundException("缺少原始缩略图：" + source);
            string target = Path.Combine(destination, id + ".bytes");
            byte[] bytes = File.ReadAllBytes(source);
            if (File.Exists(target) && bytes.SequenceEqual(File.ReadAllBytes(target))) return;
            File.WriteAllBytes(target, bytes);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
    }
}
