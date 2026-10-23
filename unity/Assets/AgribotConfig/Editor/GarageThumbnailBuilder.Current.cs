using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agribot.VehicleConfiguration.Editor
{
    public static partial class GarageThumbnailBuilder
    {
        private const string Folder = "Assets/AgribotConfig/Resources/LocalGarageThumbnails/";
        private static string CarId(string id) => id.StartsWith("Car", StringComparison.Ordinal) ? id :
            FarmVehicleLibrary.Presets.FirstOrDefault(p => p.id == id && !string.IsNullOrEmpty(p.garageName))?.garageName;

        public static string CurrentImageSource(string id)
        {
            string car = CarId(id);
            if (car == null) return null;
            string source = car == "Car0" ? "Assets/Scenes/scene2.unity" :
                car == "Car3" ? "Assets/AgribotConfig/Resources/LocalVehicleAppearances/unity_car3/preview.bytes" :
                "Assets/AgribotConfig/Resources/LocalFarmAssets/" + FarmVehicleLibrary.Presets.First(p => p.garageName == car).id + "/preview.bytes";
            return "framing-v2:" + FarmVehicleLibrary.Digest(File.ReadAllBytes(source));
        }

        private static bool CurrentImageReady(string id)
        {
            string source = CurrentImageSource(id);
            return source != null && File.Exists(Folder + id + ".bytes") && File.Exists(Folder + id + ".bytes.source.txt") &&
                File.ReadAllText(Folder + id + ".bytes.source.txt").Trim() == source;
        }

        public static void ValidateCurrentVehicleImages()
        {
            for (int i = 0; i < 10; i++)
                if (!CurrentImageReady("Car" + i)) throw new InvalidOperationException("请先从当前车模刷新车库预览：Car" + i);
        }

        [MenuItem("农机配置/从当前车模重绘全部车库预览")]
        public static void RenderCurrentVehicleImages()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式。");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Directory.CreateDirectory(Folder);
                EditorSceneManager.OpenScene("Assets/Scenes/scene2.unity");
                var car0 = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Single(t => t.name == "Car0").gameObject;
                var clone = UnityEngine.Object.Instantiate(car0);
                foreach (var part in clone.GetComponentsInChildren<Transform>(true))
                    if (new[] { "RADAR", "RTK", "CarCamera", "Imu_box", "Hand" }.Contains(part.name)) part.gameObject.SetActive(false);
                foreach (var canvas in clone.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
                clone.SetActive(true);
                Render(clone, "Car0");
                UnityEngine.Object.DestroyImmediate(clone);
                for (int i = 1; i < 10; i++)
                {
                    string car = "Car" + i;
                    string preset = i == 3 ? null : FarmVehicleLibrary.Presets.First(p => p.garageName == car).id;
                    var preview = i == 3 ? SimulationAppearanceLibrary.CreateBodyPreview("unity_car3") :
                        FarmVehicleLibrary.CreatePreview(preset, link => !FarmVehicleLibrary.Configuration(preset).sensors
                            .Any(s => s.frameId == link || s.measurementFrameId == link));
                    Render(preview, car);
                    if (preset != null)
                    {
                        File.Copy(Folder + car + ".bytes", Folder + preset + ".bytes", true);
                        File.WriteAllText(Folder + preset + ".bytes.source.txt", CurrentImageSource(preset));
                    }
                    UnityEngine.Object.DestroyImmediate(preview);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ValidateCurrentVehicleImages();
                Debug.Log("已从当前模型生成全部10辆车的同源预览。");
            }
            finally
            {
                if (setup.Length > 0 && setup.Any(s => s.isActive && s.isLoaded))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void Render(GameObject preview, string id)
        {
            foreach (var t in preview.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            foreach (var canvas in preview.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            var renderers = preview.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("车型没有可绘制网格：" + id);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var cameraObject = new GameObject("ThumbnailCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>(); camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.orthographic = true; camera.aspect = 1.6f;
            camera.nearClipPlane = .01f; camera.farClipPlane = bounds.size.magnitude * 20 + 100;
            camera.transform.position = bounds.center + new Vector3(1.4f, .85f, 1.8f).normalized * (bounds.size.magnitude * 3 + 5);
            camera.transform.LookAt(bounds.center);
            float size = 0;
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                var local = camera.transform.InverseTransformPoint(corner);
                size = Mathf.Max(size, Mathf.Abs(local.y), Mathf.Abs(local.x) / camera.aspect);
            }
            camera.orthographicSize = size * 1.06f;
            var sceneLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled).ToArray();
            foreach (var sceneLight in sceneLights) sceneLight.enabled = false;
            var ambientMode = RenderSettings.ambientMode;
            var ambientColor = RenderSettings.ambientLight;
            float reflections = RenderSettings.reflectionIntensity;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.3f, .3f, .3f);
            RenderSettings.reflectionIntensity = .3f;
            var light = new GameObject("ThumbnailKey", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 1.2f; light.GetComponent<Light>().cullingMask = 1 << 31;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            var fill = new GameObject("ThumbnailFill", typeof(Light));
            fill.GetComponent<Light>().type = LightType.Directional; fill.GetComponent<Light>().intensity = .55f;
            fill.GetComponent<Light>().cullingMask = 1 << 31; fill.transform.rotation = Quaternion.Euler(20, 140, 0);
            var texture = new Texture2D(1024, 640, TextureFormat.RGBA32, false);
            var rt = new RenderTexture(1024, 640, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1024, 640), 0, 0); texture.Apply();
                File.WriteAllBytes(Folder + id + ".bytes", texture.EncodeToPNG());
                File.WriteAllText(Folder + id + ".bytes.source.txt", CurrentImageSource(id));
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                foreach (var sceneLight in sceneLights) if (sceneLight != null) sceneLight.enabled = true;
                RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambientColor;
                RenderSettings.reflectionIntensity = reflections;
                UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(light); UnityEngine.Object.DestroyImmediate(fill);
            }
        }
    }
}
