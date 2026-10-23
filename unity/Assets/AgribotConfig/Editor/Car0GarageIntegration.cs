using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor.Events;

namespace Agribot.VehicleConfiguration.Editor
{
    public static class Car0GarageIntegration
    {
        public const string PrefabPath = "Assets/AgribotConfig/Resources/LocalGarageVehicles/Car0.prefab";

        public static void Prepare()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/scene2.unity");
            try
            {
                var objects = scene.GetRootGameObjects();
                var vehicle = objects.Single(item => item.name == "VehicleRoot");
                var visual = vehicle.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Car0");
                var camera = objects.Single(item => item.name == "FreeLook Camera");
                var canvas = objects.Single(item => item.name == "FixPanelCanvas");
                if (canvas.GetComponent<CanvasGroup>() == null) canvas.AddComponent<CanvasGroup>();
                var root = new GameObject("Car0");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.SetActive(false);
                root.transform.position = vehicle.transform.position;
                visual.name = "Car0Visual";
                foreach (var item in new[] { vehicle, camera, canvas }) item.transform.SetParent(root.transform, true);
                Object.DestroyImmediate(canvas.transform.Find("Back_btn").gameObject);
                var geometry = canvas.transform.Find("WheelChangePanel");
                foreach (var name in new[] { "LastPanel", "BagPanel", "BombPanel" })
                {
                    var obsolete = geometry.Find(name);
                    if (obsolete != null) Object.DestroyImmediate(obsolete.gameObject);
                }
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component != null && component.GetType().Name == "UIHoverFade") Object.DestroyImmediate(component);
                foreach (var worldCanvas in visual.GetComponentsInChildren<Canvas>(true))
                {
                    worldCanvas.enabled = false;
                    var raycaster = worldCanvas.GetComponent<GraphicRaycaster>();
                    if (raycaster != null) raycaster.enabled = false;
                }
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                    for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
                        if (button.onClick.GetPersistentTarget(index) == null)
                            UnityEventTools.RemovePersistentListener(button.onClick, index);
                var body = vehicle.GetComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                // Keep Cinemachine, but render all previews through the shared main camera.
                foreach (var renderCamera in root.GetComponentsInChildren<Camera>(true))
                    Object.DestroyImmediate(renderCamera);
                foreach (var importedLight in visual.GetComponentsInChildren<Light>(true))
                {
                    foreach (var data in importedLight.GetComponents<MonoBehaviour>())
                        if (data != null && data.GetType().Name == "UniversalAdditionalLightData")
                            Object.DestroyImmediate(data);
                    Object.DestroyImmediate(importedLight);
                }
                var type = System.Type.GetType("Car0GarageVehicle, Assembly-CSharp", true);
                var adapter = root.AddComponent(type);
                var settings = new SerializedObject(adapter);
                settings.FindProperty("visualRoot").objectReferenceValue = visual.gameObject;
                settings.FindProperty("configurationRoot").objectReferenceValue = vehicle.transform;
                settings.FindProperty("repairCanvas").objectReferenceValue = canvas.GetComponent<Canvas>();
                settings.FindProperty("repairPanel").objectReferenceValue = canvas.transform.Find("fixPanel")
                    .GetComponents<MonoBehaviour>().Single(item => item.GetType().Name == "FixPanel");
                settings.FindProperty("geometryPanel").objectReferenceValue = geometry.GetComponents<MonoBehaviour>()
                    .Single(item => item.GetType().Name == "WheelChangePanel");
                var orbit = camera.GetComponents<MonoBehaviour>()
                    .Single(item => item.GetType().Name == "OrbitCameraController");
                settings.FindProperty("orbitCamera").objectReferenceValue = orbit;
                // Only graphic raycasts block orbit; body picking must remain usable.
                var orbitSettings = new SerializedObject(orbit);
                orbitSettings.FindProperty("blockInputOverUI").boolValue = true;
                orbitSettings.ApplyModifiedPropertiesWithoutUndo();
                settings.FindProperty("carChange").objectReferenceValue = visual.GetComponents<MonoBehaviour>()
                    .Single(item => item.GetType().Name == "CarChange");
                settings.ApplyModifiedPropertiesWithoutUndo();
                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new System.IO.IOException("无法生成 Car0 共用车库组件。");
                Debug.Log("CAR0_SHARED_GARAGE_PREFAB_READY");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
