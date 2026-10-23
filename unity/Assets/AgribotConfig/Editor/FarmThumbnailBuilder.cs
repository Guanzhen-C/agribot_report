using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Editor
{
    public static class FarmThumbnailBuilder
    {
        [MenuItem("农机配置/刷新履带车型缩略图")]
        public static void BuildTrackVehicleImages()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            var light = new GameObject("Preview light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            foreach (string id in new[] { "unity_car3", "tracked_harvester" })
            {
                string folder = id == "unity_car3" ? "LocalVehicleAppearances" : "LocalFarmAssets";
                string assetPath = "Assets/AgribotConfig/Resources/" + folder + "/" + id;
                string path = id == "unity_car3" ? assetPath + "/icon.png" : assetPath + "_icon.png";
                var model = id == "unity_car3" ? SimulationAppearanceLibrary.CreateBodyPreview(id) :
                    FarmVehicleLibrary.CreatePreview(id, name => !name.Contains("imu") && !name.Contains("lidar") &&
                        !name.Contains("camera") && !name.Contains("rtk"));
                RenderModel(model, path);
                Object.DestroyImmediate(model);
                string preview = assetPath + "/preview.bytes";
                File.WriteAllText(path + ".source.txt", FarmVehicleLibrary.Digest(File.ReadAllBytes(preview)) + "\n");
            }
            AssetDatabase.Refresh();
            GarageThumbnailBuilder.PrepareRuntimeImages();
            Debug.Log("TRACK_VEHICLE_THUMBNAILS_READY");
        }

        private static void RenderModel(GameObject model, string path)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var bounds = renderers.First().bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var camera = new GameObject("Preview camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = bounds.size.magnitude * .42f;
            camera.transform.position = bounds.center + new Vector3(-4, 2.5f, 4).normalized * bounds.size.magnitude * 2;
            camera.transform.LookAt(bounds.center);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.85f, .86f, .86f, 0);
            var target = new RenderTexture(640, 440, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var texture = new Texture2D(640, 440, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 640, 440), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(camera.gameObject); target.Release(); Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
            }
        }

        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .65f, .65f);
            var light = new GameObject("Preview light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            foreach (var preset in FarmVehicleLibrary.Presets.Where(p => FarmVehicleLibrary.IsInstalled(p.id)))
            {
                bool bareVehicle = preset.implementId == "none";
                string iconId = bareVehicle ? preset.id : preset.implementId;
                var model = FarmVehicleLibrary.CreatePreview(preset.id,
                    name => bareVehicle ? !name.Contains("imu") && !name.Contains("lidar") && !name.Contains("camera") && !name.Contains("rtk") :
                        name == preset.implementId + "_link" || name.StartsWith(preset.implementId + "_link_"));
                var renderers = model.GetComponentsInChildren<Renderer>();
                var bounds = renderers.First().bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var camera = new GameObject("Preview camera").AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = bounds.size.magnitude * .42f;
                camera.transform.position = bounds.center + new Vector3(-4, 2.5f, 4).normalized * bounds.size.magnitude * 2;
                camera.transform.LookAt(bounds.center);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.85f, .86f, .86f, 0);
                var target = new RenderTexture(640, 440, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.Render();
                var old = RenderTexture.active; RenderTexture.active = target;
                var texture = new Texture2D(640, 440, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, 640, 440), 0, 0); texture.Apply();
                File.WriteAllBytes("Assets/AgribotConfig/Resources/LocalFarmAssets/" + iconId + "_icon.png", texture.EncodeToPNG());
                RenderTexture.active = old;
                Object.DestroyImmediate(camera.gameObject); target.Release(); Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture); Object.DestroyImmediate(model);
            }
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles("Assets/AgribotConfig/Resources/LocalFarmAssets", "*_icon.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            Debug.Log("FARM_THUMBNAILS_READY");
            GarageThumbnailBuilder.PrepareRuntimeImages();
        }
    }
}
