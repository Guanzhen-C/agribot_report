using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Editor
{
    [CustomEditor(typeof(VehicleConfigurationAuthoring))]
    public sealed class VehicleConfigurationAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            VehicleConfigurationAuthoring authoring =
                (VehicleConfigurationAuthoring)target;
            EditorGUILayout.Space();

            if (GUILayout.Button("校验当前阿克曼配置", GUILayout.Height(28f)))
                ShowValidation(authoring);
            if (GUILayout.Button("导出 ROS 配置包", GUILayout.Height(32f)))
                Export(authoring);
        }

        private static void ShowValidation(VehicleConfigurationAuthoring authoring)
        {
            bool valid = authoring.ValidateConfiguration(
                out List<string> errors,
                out List<string> warnings);
            string message = valid ? "配置校验通过。" : string.Join("\n", errors);
            if (warnings.Count > 0)
                message += "\n\n警告：\n" + string.Join("\n", warnings);
            EditorUtility.DisplayDialog(valid ? "校验通过" : "校验失败", message, "确定");
        }

        private static void Export(VehicleConfigurationAuthoring authoring)
        {
            if (!authoring.ValidateConfiguration(
                    out List<string> errors,
                    out List<string> warnings))
            {
                EditorUtility.DisplayDialog("无法导出", string.Join("\n", errors), "确定");
                return;
            }
            string jsonPath = authoring.ExportJson();
            int meshCount = 0;
            if (authoring.ExportVisualMesh)
            {
                meshCount = ObjMeshExporter.Export(
                    authoring.VehicleVisualRoot,
                    authoring.BaseLink,
                    authoring.Sensors,
                    Path.Combine(Path.GetDirectoryName(jsonPath), "vehicle_visual.obj"));
            }
            string warningText = warnings.Count > 0
                ? "\n警告：\n" + string.Join("\n", warnings)
                : string.Empty;
            EditorUtility.DisplayDialog(
                "导出完成",
                $"配置：{jsonPath}\n模型网格：{meshCount} 个{warningText}",
                "确定");
        }
    }
}
