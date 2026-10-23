using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Agribot.VehicleConfiguration.Editor
{
    public static class VehicleMeshImportUtility
    {
        private const string MainScene = "Assets/Scenes/scene2.unity";
        private const string VisualRootName = "Car0";

        [MenuItem("农机配置/启用车体网格运行时读取")]
        public static void EnableCurrentVehicleMeshesFromMenu()
        {
            try
            {
                int discovered;
                int changed = EnableCurrentVehicleMeshes(out discovered);
                EditorUtility.DisplayDialog(
                    "车体网格已准备",
                    $"引用模型：{discovered} 个\n本次启用 Read/Write：{changed} 个",
                    "确定");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("准备失败", exception.Message, "确定");
            }
        }

        // Batch-mode entry point for integration tests and automated exports.
        public static void EnableAndExportCurrentVehicleBatch()
        {
            try
            {
                int discovered;
                int changed = EnableCurrentVehicleMeshes(out discovered);
                EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
                VehicleConfigurationAuthoring authoring =
                    AckermannRuntimeConfiguration.BuildOrRefresh();
                string meshPath = Path.Combine(
                    Path.GetTempPath(),
                    "agribot_vehicle_visual_validation.obj");
                int meshCount = VehicleMeshExporter.Export(
                    authoring.VehicleVisualRoot,
                    authoring.BaseLink,
                    authoring.Sensors,
                    meshPath);
                Debug.Log(
                    $"AGRIBOT_CURRENT_EXPORT_OK={meshPath};" +
                    $"assets={discovered};changed={changed};" +
                    $"meshes={meshCount}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(2);
            }
        }

        private static int EnableCurrentVehicleMeshes(out int discovered)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "请先退出 Play 模式，再准备车体网格。");
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScene)
                scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
            Transform visualRoot = FindSceneTransform(scene, VisualRootName);
            if (visualRoot == null)
                throw new InvalidOperationException($"场景中没有找到 {VisualRootName}。");

            HashSet<string> modelPaths = new HashSet<string>();
            foreach (MeshFilter filter in
                     visualRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                string assetPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (!string.IsNullOrWhiteSpace(assetPath))
                    modelPaths.Add(assetPath);
            }

            discovered = modelPaths.Count;
            int changed = 0;
            foreach (string assetPath in modelPaths)
            {
                ModelImporter importer =
                    AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null || importer.isReadable)
                    continue;
                importer.isReadable = true;
                importer.SaveAndReimport();
                changed++;
            }
            AssetDatabase.SaveAssets();
            return changed;
        }

        private static Transform FindSceneTransform(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform candidate in
                         root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == name)
                        return candidate;
                }
            }
            return null;
        }

    }
}
