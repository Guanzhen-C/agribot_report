using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class SensorInstallationClickTests
    {
        private static readonly string[] Slots = { "Radar", "RTK", "Camera", "Imu" };
        private static readonly string[] Targets = { "RADAR", "RTK", "CarCamera", "Imu_box" };
        private readonly Dictionary<string, string> savedState = new();
        private string previousExportRoot;
        private string draftTestRoot;

        [SetUp]
        public void PreserveInventory()
        {
            previousExportRoot = Environment.GetEnvironmentVariable("AGRIBOT_PHYSICAL_EXPORT_ROOT");
            draftTestRoot = Path.Combine(Path.GetTempPath(), "agribot-ui-draft-" + Guid.NewGuid());
            Environment.SetEnvironmentVariable("AGRIBOT_PHYSICAL_EXPORT_ROOT", draftTestRoot);
            foreach (string name in new[] { "equipment-bag-state.json", "equipment-bag-state-copy.json",
                         "equipment-bag-state-scene1.json" })
            {
                string path = Path.Combine(Application.persistentDataPath, name);
                savedState[path] = File.Exists(path) ? File.ReadAllText(path) : null;
            }
        }

        [TearDown]
        public void RestoreInventory()
        {
            Environment.SetEnvironmentVariable("AGRIBOT_PHYSICAL_EXPORT_ROOT", previousExportRoot);
            if (Directory.Exists(draftTestRoot)) Directory.Delete(draftTestRoot, true);
            foreach (var pair in savedState)
                if (pair.Value != null) File.WriteAllText(pair.Key, pair.Value);
                else if (File.Exists(pair.Key)) File.Delete(pair.Key);
            savedState.Clear();
        }

        [UnityTest] public IEnumerator Car1SensorClicks() { yield return CheckNewVehicle("Car1"); }
        [UnityTest] public IEnumerator Car2SensorClicks() { yield return CheckNewVehicle("Car2"); }
        [UnityTest] public IEnumerator Car3SensorClicks() { yield return CheckNewVehicle("Car3"); }
        [UnityTest] public IEnumerator Car4SensorClicks() { yield return CheckNewVehicle("Car4"); }
        [UnityTest] public IEnumerator Car5SensorClicks() { yield return CheckNewVehicle("Car5"); }

        private IEnumerator CheckNewVehicle(string carName)
        {
            yield return SceneManager.LoadSceneAsync("scene1", LoadSceneMode.Single);
            yield return null;
            Click(Find(carName + "_Img"));
            yield return new WaitForSeconds(1f);
            var car = Find(carName);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(1f);
            var bag = NamedComponent(Find("BagPanel"), "EquipmentBagController");
            var details = NamedComponent(Find("BagPanel"), "EquipmentDetailsUI");
            Assert.That((bool)Property(bag, "IsInitialized"), Is.True);
            yield return CheckFourSensors(bag, details, car.transform, true);

            var main = NamedComponent(Find("MainPanel"), "MainPanel");
            var bundle = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
            var configured = JsonUtility.FromJson<VehicleConfigurationData>(File.ReadAllText(bundle.JsonPath));
            Assert.That(configured.sensors.Length, Is.EqualTo(5));

            Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
            yield return new WaitForSeconds(0.7f);
            Click(Find("Export_NewVehicle_Physical"));
            string draft = Path.Combine(draftTestRoot, "unity_" + carName.ToLowerInvariant() + "_physical_draft");
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!File.Exists(Path.Combine(draft, "manifest.json")) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(File.Exists(Path.Combine(draft, "manifest.json")), Is.True, "UI did not export " + carName);
            var exported = JsonUtility.FromJson<VehicleConfigurationData>(File.ReadAllText(Path.Combine(draft, "vehicle_config.json")));
            Assert.That(exported.vehicleId, Is.EqualTo(configured.vehicleId));
            Assert.That(exported.vehicleType, Is.EqualTo(carName == "Car3" ? "tracked" : "ackermann"));
            foreach (string file in new[] { "sensor_mounts.yaml", "nav2_geometry.yaml", "vehicle_kinematics.yaml", "fastlivo_extrinsics.yaml" })
                Assert.That(File.Exists(Path.Combine(draft, file)), Is.True, file);
            Assert.That(Directory.Exists(Path.Combine(draft, "firmware")), Is.False);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(0.8f);
            Assert.That(Slots.All(slot => Installed(bag, Equipment(bag, slot))), Is.True,
                carName + " lost installation state when reopening the bag");
            Click(Find("Back_btn"));
            yield return new WaitForSeconds(1f);
            Click(Find(carName + "_Img"));
            yield return new WaitForSeconds(1f);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(0.8f);
            Assert.That(Slots.All(slot => !Installed(bag, Equipment(bag, slot))), Is.True);
            Assert.That(Targets.All(name => !Child(car.transform, name).gameObject.activeSelf), Is.True);
            var exception = Assert.Throws<TargetInvocationException>(() => Invoke(main, "ExportSelectedVehicle"));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [UnityTest]
        public IEnumerator OriginalCarSensorClicks()
        {
            yield return SceneManager.LoadSceneAsync("scene1", LoadSceneMode.Single);
            yield return null;
            Click(Find("Car0_Img"));
            yield return new WaitForSeconds(0.8f);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(1f);
            var bag = NamedComponent(Find("BagPanel"), "EquipmentBagController");
            var details = NamedComponent(Find("BagPanel"), "EquipmentDetailsUI");
            yield return CheckFourSensors(bag, details, Find("Car0").transform, false);
            Assert.That(SceneManager.GetSceneByName("scene2").isLoaded, Is.False);
            Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
            yield return new WaitForSeconds(0.8f);
            string runtimeFile = Path.Combine(draftTestRoot, "ackermann_physical_runtime.json");
            var handoff = NamedComponent(Find("ROS_Simulation_Handoff"), "RosSimulationHandoffUi");
            handoff.GetType().GetMethod("SimulationStarted").Invoke(null, new object[] { runtimeFile });
            Click(Find("Stop_NewVehicle_Simulation"));
            float deadline = Time.realtimeSinceStartup + 15f;
            var export = Find("Export_NewVehicle_Physical").GetComponent<Button>();
            while (!export.interactable && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(export.interactable, Is.True, "Original export did not become available after stop");
            Click(export.gameObject);
            deadline = Time.realtimeSinceStartup + 15f;
            while (!File.Exists(runtimeFile) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(File.Exists(runtimeFile), Is.True, "Car0 UI export failed");
            string deployment = Path.Combine(draftTestRoot, "ackermann_physical_runtime_deployment");
            Assert.That(File.Exists(Path.Combine(deployment, "manifest.json")), Is.True);
            foreach (string name in new[] { "hpm6750_ackermann.bin", "hpm6750_ackermann.elf" })
                Assert.That(File.Exists(Path.Combine(deployment, "firmware", name)), Is.True, name);
        }

        [UnityTest]
        public IEnumerator SwitchingVehiclesDoesNotInstallOnPreviousVehicle()
        {
            yield return SceneManager.LoadSceneAsync("scene1", LoadSceneMode.Single);
            yield return null;
            for (int i = 1; i <= 5; i++)
            {
                Click(Find($"Car{i}_Img"));
                yield return new WaitForSeconds(1f);
                Click(Find("OpenBag_btn"));
                yield return new WaitForSeconds(0.8f);
                var bag = NamedComponent(Find("BagPanel"), "EquipmentBagController");
                var details = NamedComponent(Find("BagPanel"), "EquipmentDetailsUI");
                foreach (string slotName in Slots)
                {
                    Click(Slot(bag, slotName));
                    yield return null;
                    Click(((Button)Property(details, "InstallButton")).gameObject);
                    yield return new WaitForSeconds(0.9f);
                }
                for (int other = 1; other <= 5; other++)
                    if (other != i)
                        Assert.That(Find($"Car{other}").activeInHierarchy, Is.False);
                Click(Find("Back_btn"));
                yield return new WaitForSeconds(1f);
            }
        }

        [UnityTest]
        public IEnumerator EmptyInventorySaveStillOffersFourSensors()
        {
            string empty = "{\"version\":1,\"inventoryInitialized\":true,\"ownedItemIds\":[],\"installedItemIds\":[]}";
            foreach (string name in new[] { "equipment-bag-state-scene1.json", "equipment-bag-state-copy.json" })
                File.WriteAllText(Path.Combine(Application.persistentDataPath, name), empty);
            yield return SceneManager.LoadSceneAsync("scene1", LoadSceneMode.Single);
            yield return null;
            Click(Find("Car3_Img"));
            yield return new WaitForSeconds(1f);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(0.8f);
            AssertCatalog(NamedComponent(Find("BagPanel"), "EquipmentBagController"));
            yield return SceneManager.LoadSceneAsync("scene2", LoadSceneMode.Single);
            yield return null;
            var last = NamedComponent(Find("LastPanel"), "LastPanel");
            Invoke(last, "OpenLastPanel");
            yield return new WaitForSeconds(0.8f);
            Click(Find("OpeBag_btn"));
            yield return new WaitForSeconds(0.8f);
            AssertCatalog(NamedComponent(Find("BagPanel"), "EquipmentBagController_copy"));
        }

        [UnityTest]
        public IEnumerator OriginalAndNewScenesKeepCatalogAcrossRepeatedSwitches()
        {
            yield return SceneManager.LoadSceneAsync("scene1", LoadSceneMode.Single);
            yield return null;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Click(Find($"Car{cycle + 2}_Img"));
                yield return new WaitForSeconds(1f);
                Click(Find("OpenBag_btn"));
                yield return new WaitForSeconds(0.8f);
                AssertCatalog(NamedComponent(Find("BagPanel"), "EquipmentBagController"));
                Click(Find("Back_btn"));
                yield return new WaitForSeconds(1f);
                Click(Find("Car0_Img"));
                yield return new WaitForSeconds(1f);
                Assert.That(SceneManager.GetSceneByName("scene2").isLoaded, Is.False);
                Click(Find("OpenBag_btn"));
                yield return new WaitForSeconds(0.8f);
                AssertCatalog(NamedComponent(Find("BagPanel"), "EquipmentBagController"));
                Click(Find("Back_btn"));
                yield return new WaitForSeconds(1f);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("scene1"));
            }
        }

        private static void AssertCatalog(Component bag)
        {
            foreach (string slot in Slots)
            {
                object equipment = Equipment(bag, slot);
                Assert.That(equipment, Is.Not.Null, slot + " missing after scene reload");
                Assert.That((bool)Invoke(bag, "IsOwned", equipment), Is.True);
            }
        }

        private static IEnumerator CheckFourSensors(Component bag, Component details, Transform car, bool animatedHide)
        {
            Assert.That(Resources.FindObjectsOfTypeAll<Transform>().Any(t =>
                t.gameObject.scene.IsValid() && (t.name == "Edit_Mount_Button" || t.name == "Mount_Editor")), Is.False);
            for (int i = 0; i < Slots.Length; i++)
            {
                object equipment = Equipment(bag, Slots[i]);
                Assert.That(equipment, Is.Not.Null, car.name + "/" + Slots[i]);
                Transform target = Child(car, Targets[i]);
                Vector3 originalScale = target.localScale;
                Vector3 originalPosition = target.localPosition;
                Quaternion originalRotation = target.localRotation;
                Assert.That(target.gameObject.activeSelf, Is.False, car.name + "/" + Targets[i]);
                Click(Slot(bag, Slots[i]));
                yield return null;
                Assert.That(Property(details, "CurrentEquipment"), Is.SameAs(equipment));
                var install = (Button)Property(details, "InstallButton");
                var uninstall = (Button)Property(details, "UninstallButton");
                Click(install.gameObject);
                yield return new WaitForSeconds(1f);
                AssertInstalled(bag, equipment, target, originalScale, originalPosition, originalRotation);
                Assert.That(uninstall.IsActive() && uninstall.IsInteractable(), Is.True);
                Assert.That(install.IsActive(), Is.False);

                // A real second click cannot operate the now-hidden install button.
                ExecuteEvents.Execute(install.gameObject, new PointerEventData(EventSystem.current),
                    ExecuteEvents.pointerClickHandler);
                Click(uninstall.gameObject);
                Assert.That(Installed(bag, equipment), Is.False);
                if (animatedHide) yield return new WaitForSeconds(0.1f);
                else yield return null;
                Click(install.gameObject);
                yield return new WaitForSeconds(1f);
                AssertInstalled(bag, equipment, target, originalScale, originalPosition, originalRotation);

                Click(uninstall.gameObject);
                yield return new WaitForSeconds(0.5f);
                Assert.That(target.gameObject.activeSelf, Is.False);
                Assert.That(Installed(bag, equipment), Is.False);
                Click(install.gameObject);
                yield return new WaitForSeconds(1f);
                AssertInstalled(bag, equipment, target, originalScale, originalPosition, originalRotation);
            }
        }

        private static void AssertInstalled(Component bag, object equipment, Transform target,
            Vector3 scale, Vector3 position, Quaternion rotation)
        {
            Assert.That(Installed(bag, equipment), Is.True, target.name);
            Assert.That(target.gameObject.activeInHierarchy, Is.True, target.name + " is hidden after installation");
            Assert.That(Vector3.Distance(target.localScale, scale), Is.LessThan(0.0001f), target.name + " scale");
            Assert.That(Vector3.Distance(target.localPosition, position), Is.LessThan(0.0001f), target.name + " position");
            Assert.That(Quaternion.Angle(target.localRotation, rotation), Is.LessThan(0.01f), target.name + " rotation");
        }

        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(t => t.gameObject.scene.IsValid() && t.name == name).gameObject;
        private static Transform Child(Transform root, string name) => root.GetComponentsInChildren<Transform>(true)
            .First(t => t.name.Trim() == name);
        private static Component NamedComponent(GameObject root, string name) => root.GetComponentsInChildren<MonoBehaviour>(true)
            .First(component => component != null && component.GetType().Name == name);
        private static object Property(Component owner, string name) => owner.GetType().GetProperty(name).GetValue(owner);
        private static object Invoke(Component owner, string name, params object[] arguments) => owner.GetType()
            .GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, arguments);
        private static object EnumArgument(Component bag, string method, string slot) => Enum.Parse(
            bag.GetType().GetMethod(method).GetParameters()[0].ParameterType, slot);
        private static object Equipment(Component bag, string slot) => Invoke(bag, "GetEquipment", EnumArgument(bag, "GetEquipment", slot));
        private static GameObject Slot(Component bag, string slot) => ((Component)Invoke(bag, "GetSlot", EnumArgument(bag, "GetSlot", slot))).gameObject;
        private static bool Installed(Component bag, object equipment) => (bool)Invoke(bag, "IsInstalled", equipment);

        // Use the scene's GraphicRaycasters and pointer handlers, not onClick.Invoke.
        private static void Click(GameObject target)
        {
            Assert.That(target.activeInHierarchy, Is.True, target.name + " inactive");
            var button = target.GetComponent<Button>();
            if (button != null) Assert.That(button.IsInteractable(), Is.True, target.name + " disabled");
            Canvas.ForceUpdateCanvases();
            var rect = target.GetComponent<RectTransform>();
            var canvas = target.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), target.name + " has no UI raycast hit");
            var hit = hits[0].gameObject;
            Assert.That(hit.transform.IsChildOf(target.transform), Is.True,
                target.name + " is obscured by " + PathOf(hit.transform) + " at " + pointer.position +
                " (canvas " + canvas.name + ", " + canvas.renderMode + ")" +
                "; target rect=" + rect.rect + " scale=" + rect.lossyScale +
                "; hits=" + string.Join(";", hits.Take(5).Select(item => PathOf(item.gameObject.transform) +
                    " rect=" + (item.gameObject.transform is RectTransform uiRect ? uiRect.rect.ToString() : "n/a") +
                    " position=" + item.gameObject.transform.position)));
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static string PathOf(Transform value) => value.parent == null ? value.name : PathOf(value.parent) + "/" + value.name;
    }
}
