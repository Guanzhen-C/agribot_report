using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Editor
{
    public static class AckermannEquipmentPanelStyle
    {
        [MenuItem("农机配置/统一阿克曼装备面板样式")]
        public static void Synchronize()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("请先退出运行模式。");
            var referenceScene = EditorSceneManager.OpenScene("Assets/Scenes/scene1.unity");
            var reference = FindPanel(referenceScene);
            var targetScene = EditorSceneManager.OpenScene("Assets/Scenes/scene2.unity", OpenSceneMode.Additive);
            var target = FindPanel(targetScene);
            var sourceRect = (RectTransform)reference.transform;
            var targetRect = (RectTransform)target.transform;
            targetRect.anchorMin = sourceRect.anchorMin;
            targetRect.anchorMax = sourceRect.anchorMax;
            targetRect.pivot = sourceRect.pivot;
            targetRect.sizeDelta = sourceRect.sizeDelta;
            targetRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
            targetRect.localScale = sourceRect.localScale;
            target.transform.Find("Header/Text (Legacy)").GetComponent<Text>().text =
                reference.transform.Find("Header/Text (Legacy)").GetComponent<Text>().text;
            target.transform.Find("Header/CloseButton").gameObject.SetActive(true);

            var sourceDetails = reference.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(c => c != null && c.GetType().Name == "EquipmentDetailsUI");
            var targetDetails = target.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(c => c != null && c.GetType().Name == "EquipmentDetailsUI_copy");
            var sourceSettings = new SerializedObject(sourceDetails);
            var targetSettings = new SerializedObject(targetDetails);
            foreach (string property in new[] { "installButton", "uninstallButton" })
            {
                var sourceButton = (Button)sourceSettings.FindProperty(property).objectReferenceValue;
                var path = AnimationUtility.CalculateTransformPath(sourceButton.transform, reference.transform);
                targetSettings.FindProperty(property).objectReferenceValue = target.transform.Find(path).GetComponent<Button>();
            }
            targetSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene);
            Debug.Log("ACKERMANN_EQUIPMENT_STYLE_SYNCHRONIZED");
        }

        public static void Inspect()
        {
            var report = new StringBuilder();
            foreach (var name in new[] { "scene1", "scene2" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                var panel = FindPanel(scene);
                report.AppendLine(name);
                foreach (var item in panel.GetComponentsInChildren<RectTransform>(true))
                {
                    report.AppendLine($"{AnimationUtility.CalculateTransformPath(item, panel.transform)} active={item.gameObject.activeSelf} pos={item.anchoredPosition} size={item.sizeDelta} scale={item.localScale} anchor={item.anchorMin}/{item.anchorMax}");
                    foreach (var component in item.GetComponents<Component>())
                    {
                        if (component is Image image) report.AppendLine($"  Image {(image.sprite != null ? image.sprite.name : "none")} {AssetDatabase.GetAssetPath(image.sprite)}");
                        else if (component is Text text) report.AppendLine($"  Text {text.text}");
                        else if (component is TMP_Text tmp) report.AppendLine($"  TMP {tmp.text}");
                        else if (component != null && !(component is RectTransform) && !(component is CanvasRenderer)) report.AppendLine("  " + component.GetType().Name);
                    }
                }
            }
            File.WriteAllText("/tmp/agribot_equipment_panel_styles.txt", report.ToString());
        }

        private static GameObject FindPanel(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Single(item => item.name == "BagPanel").gameObject;
    }
}
