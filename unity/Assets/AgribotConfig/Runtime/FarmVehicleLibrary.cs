using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    [Serializable] public sealed class FarmVehiclePreset
    {
        public string id, displayName, implementId, implementName;
        public string baseVehicleId, garageName, implementObjectName;
        public string[] tasks;
        public string verification;
    }

    public static class FarmVehicleLibrary
    {
        [Serializable] private sealed class Catalog { public int schemaVersion; public FarmVehiclePreset[] presets; }
        [Serializable] public sealed class FileRecord { public string name, sha256; }
        [Serializable] public sealed class AssetManifest
        {
            public int schemaVersion, meshGroups, triangles;
            public string vehicleId, previewSha256, sourceConfigSha256;
            public float[] minimumRos, maximumRos;
            public FileRecord[] files;
        }

        private static FarmVehiclePreset[] presets;
        public static FarmVehiclePreset[] Presets => presets ??= LoadCatalog();
        public static string AssetDirectory(string id)
        {
            RequirePreset(id);
            return Path.Combine(Application.streamingAssetsPath, "LocalFarmExports", id);
        }

        public static bool HasAssetFiles(string id) =>
            File.Exists(Path.Combine(AssetDirectory(id), "asset_manifest.json")) &&
            File.Exists(Path.Combine(AssetDirectory(id), "vehicle_config.json"));

        public static bool IsInstalled(string id) => HasAssetFiles(id) &&
            Resources.Load<TextAsset>("LocalFarmAssets/" + id + "/preview") != null;

        public static VehicleConfigurationData Configuration(string id) =>
            JsonUtility.FromJson<VehicleConfigurationData>(File.ReadAllText(Path.Combine(AssetDirectory(id), "vehicle_config.json")));

        public static AssetManifest Manifest(string id)
        {
            return ReadManifest(AssetDirectory(id), id);
        }

        internal static AssetManifest ReadManifest(string directory, string id)
        {
            var manifest = JsonUtility.FromJson<AssetManifest>(File.ReadAllText(Path.Combine(directory, "asset_manifest.json")));
            if (manifest.schemaVersion != 1 || manifest.vehicleId != id || manifest.files == null ||
                !manifest.files.Any(item => item.name == "vehicle_config.json"))
                throw new InvalidDataException("农机资源清单无效：" + id);
            foreach (var item in manifest.files)
                if (Path.GetFileName(item.name) != item.name || string.IsNullOrEmpty(item.name) ||
                    !(item.name == "vehicle_config.json" || item.name.EndsWith(".obj") || item.name.EndsWith(".mtl") ||
                      item.name.EndsWith(".png") || item.name.EndsWith(".jpg") || item.name.EndsWith(".jpeg")))
                    throw new InvalidDataException("农机资源路径无效。");
            return manifest;
        }

        public static VehicleConfigurationExportResult Export(string id, string destinationRoot = null)
        {
            var manifest = Manifest(id);
            string source = AssetDirectory(id);
            var config = Configuration(id);
            if (config.vehicleId != id || config.metadata.calibrationState != "unity_nominal_simulation_only")
                throw new InvalidDataException("农机名义仿真配置与资源不一致。");
            foreach (var item in manifest.files)
                if (Digest(File.ReadAllBytes(Path.Combine(source, item.name))) != item.sha256)
                    throw new InvalidDataException("农机资源校验失败：" + item.name);
            string destination = Path.Combine(destinationRoot ?? Path.Combine(Application.persistentDataPath, "ConfigExports"), id);
            Directory.CreateDirectory(destination);
            // Copy the canonical JSON verbatim: JsonUtility does not preserve
            // unknown extension fields such as per-wheel dynamics and implements.
            foreach (var item in manifest.files)
                File.Copy(Path.Combine(source, item.name), Path.Combine(destination, item.name), true);
            return new VehicleConfigurationExportResult(Path.Combine(destination, "vehicle_config.json"),
                Path.Combine(destination, "vehicle_visual.obj"), manifest.meshGroups);
        }

        public static GameObject CreatePreview(string id, Func<string, bool> includeLink = null)
        {
            return LoadPreview(Manifest(id), "LocalFarmAssets/" + id + "/preview", includeLink);
        }

        internal static GameObject LoadPreview(AssetManifest manifest, string resourcePath, Func<string, bool> includeLink = null)
        {
            string id = manifest.vehicleId;
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null || Digest(asset.bytes) != manifest.previewSha256)
                throw new InvalidDataException("农机预览资源缺失或损坏：" + id);
            var root = new GameObject(id + "_preview");
            var owner = root.AddComponent<FarmPreviewAssets>();
            try
            {
                using var reader = new BinaryReader(new MemoryStream(asset.bytes));
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "AFM1")
                    throw new InvalidDataException("农机预览格式错误。");
                int groups = checked((int)reader.ReadUInt32());
                if (groups != manifest.meshGroups || groups > 500) throw new InvalidDataException("预览分组数错误。");
                for (int group = 0; group < groups; group++)
                {
                    int length = checked((int)reader.ReadUInt32());
                    if (length > 256) throw new InvalidDataException("预览名称错误。");
                    string name = Encoding.UTF8.GetString(reader.ReadBytes(length));
                    Color color = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    int count = checked((int)reader.ReadUInt32());
                    if (count <= 0 || count > 300000 || reader.BaseStream.Length - reader.BaseStream.Position < count * 36L)
                        throw new InvalidDataException("预览网格数据错误。");
                    if (includeLink != null && !includeLink(name))
                    {
                        reader.BaseStream.Seek(count * 36L, SeekOrigin.Current);
                        continue;
                    }
                    var vertices = new Vector3[count * 3];
                    var indices = new int[count * 3];
                    for (int face = 0; face < count; face++)
                    {
                        for (int corner = 0; corner < 3; corner++)
                            vertices[face * 3 + corner] = RosFluConverter.RosToUnity(
                                new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
                        indices[face * 3] = face * 3;
                        indices[face * 3 + 1] = face * 3 + 2;
                        indices[face * 3 + 2] = face * 3 + 1;
                    }
                    var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.vertices = vertices; mesh.triangles = indices;
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    string shaderName = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null
                        ? "Standard" : "Universal Render Pipeline/Lit";
                    var shader = Shader.Find(shaderName);
                    if (shader == null) throw new InvalidOperationException("缺少农机预览着色器：" + shaderName);
                    var material = new Material(shader) { color = color };
                    material.SetFloat("_Smoothness", .25f);
                    var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                    part.transform.SetParent(root.transform, false);
                    part.GetComponent<MeshFilter>().sharedMesh = mesh;
                    part.GetComponent<MeshRenderer>().sharedMaterial = material;
                    owner.Register(mesh, material);
                }
                if (reader.BaseStream.Position != reader.BaseStream.Length)
                    throw new InvalidDataException("预览文件包含多余数据。");
                return root;
            }
            catch
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
            finally { Resources.UnloadAsset(asset); }
        }

        public static string Digest(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static void RequirePreset(string id)
        {
            if (!Presets.Any(preset => preset.id == id)) throw new ArgumentException("未知农机组合：" + id);
        }

        private static FarmVehiclePreset[] LoadCatalog()
        {
            var asset = Resources.Load<TextAsset>("farm_vehicle_presets");
            if (asset == null) throw new InvalidOperationException("缺少农机资源目录。");
            var catalog = JsonUtility.FromJson<Catalog>(asset.text);
            if (catalog.schemaVersion != 1 || catalog.presets == null || catalog.presets.Length == 0 ||
                catalog.presets.Select(item => item.id).Distinct().Count() != catalog.presets.Length)
                throw new InvalidDataException("农机资源目录格式错误。");
            return catalog.presets;
        }
    }

    public sealed class FarmPreviewAssets : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<UnityEngine.Object> owned = new();
        public void Register(Mesh mesh, Material material) { owned.Add(mesh); owned.Add(material); }
        private void OnDestroy()
        {
            foreach (var item in owned)
                if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
    }
}
