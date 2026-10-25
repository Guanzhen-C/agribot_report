using System;
using System.Collections;
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
    public sealed class FarmGarageMountTests
    {
        [UnityTest]
        public IEnumerator EveryVehicleRestsOnGarageFloorAfterSelection()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var gaps = new System.Collections.Generic.List<float>();
            foreach (string name in Enumerable.Range(0, 10).Concat(new[] { 0, 9 }).Select(i => "Car" + i))
            {
                Click(Find(name + "_Img"));
                var car = Find(name);
                for (int sample = 0; sample < 3; sample++)
                {
                    yield return new WaitForSeconds(.12f);
                    gaps.Add(Mathf.Abs(GroundGap(car)));
                }
                yield return new WaitForSeconds(.64f);
                gaps.Add(Mathf.Abs(GroundGap(car)));
                Click(Find("Back_btn"));
                yield return new WaitForSeconds(.6f);
            }
            Assert.That(gaps.Max(), Is.LessThan(.002f), "Vehicle contact surfaces must rest on the garage floor.");
        }

        [UnityTest]
        public IEnumerator CompactToolUsesTightFramingWithoutScalingVehicle()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Click(Find("Car6_Img"));
            yield return new WaitForSeconds(1.5f);
            var car = Find("Car6");
            var farm = Component(car, "FarmGarageVehicle");
            float originalRadius = (float)farm.GetType().GetProperty("PreviewRadius").GetValue(farm);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(.8f);
            var details = Component(Find("BagPanel"), "EquipmentDetailsUI");
            Click(Find("ChiselPlow_slot"));
            yield return null;
            Click((Button)details.GetType().GetProperty("InstallButton").GetValue(details));
            yield return new WaitForSeconds(1f);
            float mountedRadius = (float)farm.GetType().GetProperty("PreviewRadius").GetValue(farm);
            float sphereRadius = RendererBounds(car).extents.magnitude * 1.1f /
                Mathf.Sin(Camera.main.fieldOfView * .5f * Mathf.Deg2Rad);
            Debug.Log($"COMPACT_MOUNT_ZOOM before={originalRadius} after={mountedRadius}");
            Assert.That(car.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(mountedRadius, Is.LessThan(sphereRadius * .95f),
                "Fit the tool in the actual frustum without excessive bounding-sphere padding.");
            Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
            yield return new WaitForSeconds(1f);
            foreach (var filter in car.GetComponentsInChildren<MeshFilter>())
                foreach (var corner in Corners(filter.sharedMesh.bounds))
                {
                    var point = Camera.main.WorldToViewportPoint(filter.transform.TransformPoint(corner));
                    Assert.That(point.z, Is.GreaterThan(0));
                    Assert.That(point.x, Is.InRange(0f, 1f));
                    Assert.That(point.y, Is.InRange(0f, 1f));
                }
        }

        [UnityTest]
        public IEnumerator InstallingArmAndRtkKeepsCurrentEquipmentCameraDistance()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            foreach (string name in new[] { "Car0", "Car3" })
            {
                Click(Find(name + "_Img"));
                // Wait for the menu-to-vehicle camera blend as well as the scale animation.
                yield return new WaitForSeconds(2.5f);
                var car = Find(name);
                var control = car.GetComponentsInChildren<Component>(true).First(item => item != null &&
                    item.GetType().Name is "cameraControl" or "OrbitCameraController");
                var field = control.GetType().GetField("targetRadius", BindingFlags.Instance | BindingFlags.NonPublic);
                float entryRadius = (float)field.GetValue(control);
                Vector3 entryPosition = Camera.main.transform.position;
                Click(Find("OpenBag_btn"));
                yield return new WaitForSeconds(.8f);
                float radius = (float)field.GetValue(control);
                Debug.Log($"EQUIPMENT_PANEL_ZOOM car={name} entry={entryRadius} open={radius}");
                Assert.That(radius, Is.EqualTo(entryRadius).Within(.01f),
                    "Opening the equipment panel must preserve the vehicle view.");
                Assert.That(Vector3.Distance(Camera.main.transform.position, entryPosition), Is.LessThan(.1f));
                var details = Component(Find("BagPanel"), "EquipmentDetailsUI");
                foreach (string tool in new[] { "Hand", "RTK" })
                {
                    Click(Find(tool + "_slot"));
                    yield return null;
                    Click((Button)details.GetType().GetProperty("InstallButton").GetValue(details));
                    yield return new WaitForSeconds(1f);
                    Debug.Log($"ARM_MOUNT_ZOOM car={name} equipment={tool} before={radius} after={field.GetValue(control)}");
                    Assert.That((float)field.GetValue(control), Is.EqualTo(radius).Within(.001f),
                        name + " must not force a distant camera preset when installing equipment.");
                    Assert.That(Mathf.Abs(GroundGap(car)), Is.LessThan(.002f));
                }
                Click(Find("Back_btn"));
                yield return new WaitForSeconds(.6f);
            }
        }

        [UnityTest]
        public IEnumerator SwitchingFromTrailerRestoresNormalVehicleView()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Click(Find("Car7_Img"));
            yield return new WaitForSeconds(1.5f);
            var car = Find("Car7");
            Vector3 position = car.transform.position;
            Quaternion rotation = car.transform.rotation;
            Vector3 scale = car.transform.localScale;
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(.8f);
            var details = Component(Find("BagPanel"), "EquipmentDetailsUI");
            foreach (string tool in new[] { "FarmTrailer", "ChiselPlow" })
            {
                Click(Find(tool + "_slot"));
                yield return null;
                Click((Button)details.GetType().GetProperty("InstallButton").GetValue(details));
                yield return new WaitForSeconds(1f);
            }
            Debug.Log($"TRAILER_SWITCH_VIEW fov={Camera.main.fieldOfView} rotation={car.transform.rotation.eulerAngles}");
            Assert.That(Camera.main.fieldOfView, Is.EqualTo(65f).Within(.01f),
                "A normal tool must not inherit the trailer's wide-angle view.");
            Assert.That(Vector3.Distance(car.transform.position, position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(car.transform.rotation, rotation), Is.LessThan(.01f));
            Assert.That(car.transform.localScale, Is.EqualTo(scale));
            Assert.That(Mathf.Abs(GroundGap(car)), Is.LessThan(.002f));
        }

        [UnityTest]
        public IEnumerator ExpandedRearMountsInstallExclusivelyExportAndStayInsideGarage()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            int verified = 0;
            var rearTools = new[] { "ChiselPlow", "DiscHarrow", "GrooveMaker", "Cultivator",
                "ChainCrusher", "InterrowMill", "GarlicPlanter", "MulchPlanter" };
            foreach (string carName in new[] { "Car1", "Car2", "Car4", "Car5", "Car6", "Car7" })
            {
                Click(Find(carName + "_Img"));
                yield return new WaitForSeconds(.8f);
                var car = Find(carName);
                var host = FarmVehicleLibrary.Presets.Single(p => p.garageName == carName);
                var variants = FarmVehicleLibrary.Presets.Where(p => p.baseVehicleId == host.id &&
                    rearTools.Contains(p.implementObjectName)).ToArray();
                var targets = variants.Select(p => car.transform.Find(p.implementObjectName)).ToArray();
                Assert.That(targets.All(t => t != null), Is.True, carName);
                Assert.That(targets.Where(t => !t.gameObject.activeSelf).All(t => t.childCount == 0), Is.True,
                    "Inactive variants must remain unloaded");
                Click(Find("OpenBag_btn"));
                yield return new WaitForSeconds(.8f);
                var details = Component(Find("BagPanel"), "EquipmentDetailsUI");
                var bag = Component(Find("BagPanel"), "EquipmentBagController");
                foreach (string sensorName in new[] { "Radar", "RTK", "Camera", "Imu" })
                {
                    var getter = bag.GetType().GetMethod("GetSlot");
                    var slot = (Component)getter.Invoke(bag, new[] {
                        Enum.Parse(getter.GetParameters()[0].ParameterType, sensorName) });
                    Click(slot.gameObject);
                    yield return null;
                    Click((Button)details.GetType().GetProperty("InstallButton").GetValue(details));
                    yield return new WaitUntil(() => !(bool)bag.GetType().GetProperty("IsSceneAnimating").GetValue(bag));
                }
                var main = Component(Find("MainPanel"), "MainPanel");
                foreach (var preset in variants)
                {
                    Click(Find(preset.implementObjectName + "_slot"));
                    yield return null;
                    var selected = details.GetType().GetProperty("CurrentEquipment").GetValue(details);
                    Assert.That(selected, Is.Not.Null, preset.id);
                    Assert.That(selected.GetType().GetProperty("EquipmentType").GetValue(selected).ToString(),
                        Is.EqualTo(preset.implementObjectName), preset.id);
                    var install = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
                    Assert.That(install.interactable, Is.True, preset.id);
                    Assert.That(install.IsInteractable(), Is.True, preset.id + " parent panel is not interactive");
                    Click(install);
                    yield return new WaitForSeconds(.25f);
                    var target = car.transform.Find(preset.implementObjectName);
                    Assert.That(target.gameObject.activeInHierarchy, Is.True, preset.id);
                    var loader = target.GetComponents<Behaviour>().Single(c => c.GetType().Name == "FarmImplementPreview");
                    Assert.That(loader.isActiveAndEnabled, Is.True, preset.id);
                    Assert.That(target.childCount, Is.GreaterThan(0), preset.id + " preview child missing");
                    Assert.That(target.GetComponentsInChildren<MeshFilter>().Length, Is.GreaterThan(0), preset.id);
                    Assert.That(targets.Count(t => t.gameObject.activeSelf), Is.EqualTo(1), preset.id);
                    var export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
                    Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(
                        Path.Combine(FarmVehicleLibrary.AssetDirectory(preset.id), "vehicle_config.json"))), preset.id);
                    Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
                    yield return new WaitForSeconds(1f);
                    var room = RendererBounds(Find("Garage1").GetComponentsInChildren<Transform>(true).Single(t => t.name == "Walls").gameObject);
                    var assembly = RendererBounds(car);
                    Assert.That(assembly.min.x, Is.GreaterThan(room.min.x), preset.id);
                    Assert.That(assembly.max.x, Is.LessThan(room.max.x), preset.id);
                    Assert.That(assembly.min.z, Is.GreaterThan(room.min.z), preset.id);
                    Assert.That(assembly.max.z, Is.LessThan(room.max.z), preset.id);
                    foreach (var filter in car.GetComponentsInChildren<MeshFilter>())
                        foreach (Vector3 corner in Corners(filter.sharedMesh.bounds))
                        {
                            var pixel = Camera.main.WorldToViewportPoint(filter.transform.TransformPoint(corner));
                            Assert.That(pixel.z, Is.GreaterThan(0), preset.id);
                            Assert.That(pixel.x, Is.InRange(0f, 1f), preset.id);
                            Assert.That(pixel.y, Is.InRange(0f, 1f), preset.id);
                        }
                    if (preset.id == "tractor_groove" || preset.id == "car5_crusher" || preset.id == "car1_harrow")
                        CaptureCamera("/tmp/cross_mount_" + preset.id + ".png");
                    Click(Find("OpenBag_btn"));
                    yield return new WaitForSeconds(.8f);
                    var uninstall = (Button)details.GetType().GetProperty("UninstallButton").GetValue(details);
                    Assert.That(uninstall.IsInteractable(), Is.True, preset.id);
                    Click(uninstall);
                    yield return null;
                    yield return null;
                    Assert.That(targets.All(t => !t.gameObject.activeSelf && t.childCount == 0), Is.True, preset.id);
                    verified++;
                }
                Click(Find("Back_btn"));
                yield return new WaitForSeconds(.5f);
            }
            Assert.That(verified, Is.EqualTo(46));
        }

        private static Vector3[] Corners(Bounds b) => (from x in new[] { b.min.x, b.max.x }
            from y in new[] { b.min.y, b.max.y } from z in new[] { b.min.z, b.max.z } select new Vector3(x, y, z)).ToArray();

        [UnityTest]
        public IEnumerator OriginalTractorsUseMetricModelsAndExportInstalledImplements()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            foreach (var entry in new[] { ("Car1", "Cultivator", "car1_cultivator", 4.8),
                ("Car2", "Cultivator", "car2_cultivator", 5.2),
                ("Car4", "ChiselPlow", "car4_chisel", 3.6),
                ("Car5", "DiscHarrow", "car5_harrow", 3.4) })
            {
                Click(Find(entry.Item1 + "_Img"));
                yield return new WaitForSeconds(1.5f);
                var car = Find(entry.Item1);
                Assert.That(car.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(Component(car, "FarmGarageVehicle"), Is.Not.Null);
                var config = FarmVehicleLibrary.Configuration("unity_"+entry.Item1.ToLowerInvariant());
                Assert.That(config.geometry.bodyExtents.frontM+config.geometry.bodyExtents.rearM,
                    Is.EqualTo(entry.Item4).Within(.001));
                Click(Find("OpenBag_btn"));
                yield return new WaitForSeconds(.8f);
                var bag = Component(Find("BagPanel"), "EquipmentBagController");
                var details = Component(Find("BagPanel"), "EquipmentDetailsUI");
                foreach (var slotName in new[] { "Radar", "RTK", "Camera", "Imu" })
                {
                    var getter = bag.GetType().GetMethod("GetSlot");
                    var slot = (Component)getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, slotName) });
                    Click(slot.gameObject);
                    yield return null;
                    Click((Button)details.GetType().GetProperty("InstallButton").GetValue(details));
                    yield return new WaitUntil(() => !(bool)bag.GetType().GetProperty("IsSceneAnimating").GetValue(bag));
                }
                var implementSlot = Find(entry.Item2+"_slot");
                Click(implementSlot);
                yield return null;
                var install = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
                Assert.That(install.interactable, Is.True);
                Click(install);
                yield return new WaitForSeconds(.5f);
                Assert.That(car.transform.Find(entry.Item2).gameObject.activeSelf, Is.True);
                var main = Component(Find("MainPanel"), "MainPanel");
                var export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
                Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(
                    Path.Combine(FarmVehicleLibrary.AssetDirectory(entry.Item3), "vehicle_config.json"))));
                Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
                yield return new WaitForSeconds(.8f);
                CaptureCamera("/tmp/metric_"+entry.Item1+"_installed.png");
                var room = RendererBounds(Find("Garage1").GetComponentsInChildren<Transform>(true).Single(t => t.name == "Walls").gameObject);
                var assembly = RendererBounds(car);
                Assert.That(assembly.min.x, Is.GreaterThan(room.min.x));
                Assert.That(assembly.max.x, Is.LessThan(room.max.x));
                Assert.That(assembly.min.z, Is.GreaterThan(room.min.z));
                Assert.That(assembly.max.z, Is.LessThan(room.max.z));
                Click(Find("Back_btn"));
                yield return new WaitForSeconds(.6f);
            }
        }

        [UnityTest]
        public IEnumerator HarvesterHeaderInstallsSeparatelyWithoutShrinkingVehicle()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var garage = Find("Garage1").transform;
            Vector3 originalGarageScale = garage.localScale;
            Click(Find("Car9_Img"));
            yield return new WaitForSeconds(2f);
            var car = Find("Car9");
            Assert.That(car.transform.localScale, Is.EqualTo(Vector3.one));
            var farm = Component(car, "FarmGarageVehicle");
            Assert.That(farm.GetType().GetProperty("PreviewRadius").GetValue(farm), Is.EqualTo(12f));
            Assert.That(garage.localScale.x, Is.GreaterThan(originalGarageScale.x));
            var header = car.transform.Find("CombineHeader");
            Assert.That(header, Is.Not.Null);
            Assert.That(header.gameObject.activeSelf, Is.False);
            Assert.That(header.name.StartsWith("Car"), Is.False);
            foreach (var filter in car.GetComponentsInChildren<MeshFilter>())
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = Camera.main.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                    Assert.That(point.z, Is.GreaterThan(0));
                    Assert.That(point.x, Is.InRange(0f, 1f));
                    Assert.That(point.y, Is.InRange(0f, 1f));
                }
            CaptureCamera("/tmp/harvester_fullsize_bare_preview.png");
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(.8f);
            var bag = Component(Find("BagPanel"), "EquipmentBagController");
            var details = Component(Find("BagPanel"), "EquipmentDetailsUI");
            foreach (var slotName in new[] { "Radar", "RTK", "Camera", "Imu" })
            {
                var getter = bag.GetType().GetMethod("GetSlot");
                var slot = (Component)getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, slotName) });
                Click(slot.gameObject);
                yield return null;
                Click((Button)details.GetType().GetProperty("InstallButton").GetValue(details));
                yield return new WaitUntil(() => !(bool)bag.GetType().GetProperty("IsSceneAnimating").GetValue(bag));
                yield return null;
            }
            var main = Component(Find("MainPanel"), "MainPanel");
            var export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
            Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(
                Path.Combine(FarmVehicleLibrary.AssetDirectory("tracked_harvester"), "vehicle_config.json"))));
            var headerSlot = Find("CombineHeader_slot");
            headerSlot.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0;
            yield return null;
            Click(headerSlot);
            yield return null;
            var install = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
            Assert.That(install.interactable, Is.True);
            Vector3 headerPosition = header.localPosition;
            Click(install);
            yield return new WaitForSeconds(.5f);
            Assert.That(header.gameObject.activeSelf, Is.True);
            Assert.That(farm.GetType().GetProperty("PreviewRadius").GetValue(farm), Is.EqualTo(18f));
            Assert.That(header.localPosition, Is.EqualTo(headerPosition));
            Assert.That(car.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(RendererBounds(header.gameObject).size.magnitude, Is.GreaterThan(12f));
            export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
            Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(
                Path.Combine(FarmVehicleLibrary.AssetDirectory("harvester_header"), "vehicle_config.json"))));
            Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
            yield return new WaitForSeconds(1.5f);
            var room = RendererBounds(garage.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Walls").gameObject);
            var assembly = RendererBounds(car);
            Assert.That(assembly.min.x, Is.GreaterThan(room.min.x));
            Assert.That(assembly.max.x, Is.LessThan(room.max.x));
            Assert.That(assembly.min.z, Is.GreaterThan(room.min.z));
            Assert.That(assembly.max.z, Is.LessThan(room.max.z));
            foreach (var filter in car.GetComponentsInChildren<MeshFilter>())
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = Camera.main.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                    Assert.That(point.z, Is.GreaterThan(0));
                    Assert.That(point.x, Is.InRange(0f, 1f));
                    Assert.That(point.y, Is.InRange(0f, 1f));
                }
            CaptureCamera("/tmp/harvester_fullsize_header_installed.png");
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(.7f);
            Click(headerSlot);
            yield return null;
            Click((Button)details.GetType().GetProperty("UninstallButton").GetValue(details));
            yield return null;
            Assert.That(header.gameObject.activeSelf, Is.False);
            Assert.That(farm.GetType().GetProperty("PreviewRadius").GetValue(farm), Is.EqualTo(12f));
            Assert.That(car.transform.localScale, Is.EqualTo(Vector3.one));
            Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
            yield return new WaitForSeconds(.6f);
            Click(Find("Back_btn"));
            yield return new WaitForSeconds(.6f);
            Assert.That(garage.localScale, Is.EqualTo(originalGarageScale));
            Click(Find("Car6_Img"));
            yield return new WaitForSeconds(1.5f);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(.8f);
            details = Component(Find("BagPanel"), "EquipmentDetailsUI");
            Click(Find("CombineHeader_slot"));
            yield return null;
            AssertUnavailableWithoutDescription(details);
        }

        [UnityTest]
        public IEnumerator NewSeedersTrailerAndHarvesterUseExistingInstallationAndExport()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            foreach (var entry in new[] { ("Car6", "GarlicPlanter", "tractor_garlic"),
                ("Car6", "MulchPlanter", "tractor_mulch"), ("Car7", "FarmTrailer", "cab_trailer"),
                ("Car9", "", "tracked_harvester") })
            {
                Click(Find(entry.Item1 + "_Img"));
                yield return new WaitForSeconds(1.5f);
                var car = Find(entry.Item1);
                Vector3 initialPosition = car.transform.position;
                Assert.That(car.activeInHierarchy, Is.True);
                Click(Find("OpenBag_btn"));
                yield return new WaitForSeconds(.8f);
                var bag = Component(Find("BagPanel"), "EquipmentBagController");
                var details = Component(Find("BagPanel"), "EquipmentDetailsUI");
                foreach (var slotName in new[] { "Radar", "RTK", "Camera", "Imu" })
                {
                    var getter = bag.GetType().GetMethod("GetSlot");
                    var slot = (Component)getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, slotName) });
                    Click(slot.gameObject);
                    yield return null;
                    var button = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
                    if (button.interactable) Click(button);
                    yield return new WaitUntil(() => !(bool)bag.GetType().GetProperty("IsSceneAnimating").GetValue(bag));
                    yield return null;
                }
                if (entry.Item2.Length != 0)
                {
                    var tool = car.transform.Find(entry.Item2);
                    Assert.That(tool, Is.Not.Null);
                    var originalPosition = tool.localPosition;
                    var slot = Find(entry.Item2 + "_slot");
                    slot.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0;
                    yield return null;
                    Click(slot);
                    yield return null;
                    var button = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
                    Assert.That(button.interactable, Is.True);
                    Click(button);
                    yield return new WaitForSeconds(.6f);
                    Assert.That(tool.gameObject.activeSelf, Is.True);
                    Assert.That(tool.localPosition, Is.EqualTo(originalPosition));
                    if (entry.Item2 == "FarmTrailer")
                        Assert.That(tool.GetComponentsInChildren<MeshFilter>().Select(t => t.name).Distinct().Count(), Is.EqualTo(5));
                }
                else
                {
                    foreach (var name in new[] { "GarlicPlanter_slot", "MulchPlanter_slot", "FarmTrailer_slot" })
                    {
                        Click(Find(name));
                        yield return null;
                        AssertUnavailableWithoutDescription(details);
                    }
                }
                var main = Component(Find("MainPanel"), "MainPanel");
                var export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
                Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(
                    Path.Combine(FarmVehicleLibrary.AssetDirectory(entry.Item3), "vehicle_config.json"))));
                Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
                yield return new WaitForSeconds(.6f);
                CaptureCamera("/tmp/" + entry.Item3 + "_installed.png");
                if (entry.Item2 == "FarmTrailer")
                {
                    var walls = Find("Garage1").GetComponentsInChildren<Transform>(true).Single(t => t.name == "Walls");
                    var room = RendererBounds(walls.gameObject);
                    var visibleRenderers = car.GetComponentsInChildren<Renderer>();
                    var bounds = visibleRenderers[0].bounds;
                    foreach (var renderer in visibleRenderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    Assert.That(bounds.min.x, Is.GreaterThan(room.min.x));
                    Assert.That(bounds.max.x, Is.LessThan(room.max.x));
                    Assert.That(bounds.min.z, Is.GreaterThan(room.min.z));
                    Assert.That(bounds.max.z, Is.LessThan(room.max.z));
                    Click(Find("OpenBag_btn"));
                    yield return new WaitForSeconds(.6f);
                    Click(Find("FarmTrailer_slot"));
                    yield return null;
                    Click((Button)details.GetType().GetProperty("UninstallButton").GetValue(details));
                    yield return null;
                    Assert.That(car.transform.position, Is.EqualTo(initialPosition));
                    Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
                    yield return new WaitForSeconds(.6f);
                }
                Click(Find("Back_btn"));
                yield return new WaitForSeconds(.6f);
            }
        }

        [UnityTest]
        public IEnumerator MowerUsesExistingGarageSensorInstallationAndExport()
        {
            Assert.That(FarmVehicleLibrary.IsInstalled("ride_on_mower"), Is.True);
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Click(Find("Car8_Img"));
            yield return new WaitForSeconds(2f);
            var car = Find("Car8");
            Assert.That(car.activeInHierarchy, Is.True);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(1f);
            var bag = Component(Find("BagPanel"), "EquipmentBagController");
            var details = Component(Find("BagPanel"), "EquipmentDetailsUI");
            foreach (var slotName in new[] { "Radar", "RTK", "Camera", "Imu" })
            {
                var getter = bag.GetType().GetMethod("GetSlot");
                var slot = (Component)getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, slotName) });
                Click(slot.gameObject);
                yield return null;
                var install = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
                Assert.That(install.interactable, Is.True, slotName);
                Click(install);
                yield return new WaitForSeconds(.8f);
            }
            foreach (string name in new[] { "RADAR", "RTK", "CarCamera", "Imu_box" })
                Assert.That(car.transform.Find(name).gameObject.activeSelf, Is.True, name);
            foreach (string toolSlot in new[] { "Hand_slot", "ChiselPlow_slot", "InterrowMill_slot" })
            {
                Click(Find(toolSlot));
                yield return null;
                AssertUnavailableWithoutDescription(details);
            }
            var main = Component(Find("MainPanel"), "MainPanel");
            var export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
            Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(
                Path.Combine(FarmVehicleLibrary.AssetDirectory("ride_on_mower"), "vehicle_config.json"))));
            StringAssert.Contains("steeringAssembly", File.ReadAllText(export.JsonPath));
            StringAssert.Contains("\"vehicleType\": \"ackermann\"", File.ReadAllText(export.JsonPath));
            var tool = car.transform.Find("FrontCutter");
            Assert.That(tool, Is.Not.Null);
            Assert.That(tool.gameObject.activeSelf, Is.False);
            Vector3 toolPosition = tool.localPosition;
            Click(Find("FrontCutter_slot"));
            yield return null;
            var installCutter = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
            Assert.That(installCutter.interactable, Is.True);
            Click(installCutter);
            yield return new WaitForSeconds(.8f);
            Assert.That(tool.gameObject.activeSelf, Is.True);
            Assert.That(tool.localPosition, Is.EqualTo(toolPosition));
            export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
            Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(
                Path.Combine(FarmVehicleLibrary.AssetDirectory("mower_front_cutter"), "vehicle_config.json"))));
            Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
            yield return new WaitForSeconds(1f);
            var screen = Camera.main.WorldToViewportPoint(RendererBounds(car.transform.Find("car8").gameObject).center);
            Assert.That(screen.z, Is.GreaterThan(0));
            Assert.That(screen.x, Is.InRange(.1f, .9f));
            Assert.That(screen.y, Is.InRange(.1f, .9f));
            foreach (var filter in car.GetComponentsInChildren<MeshFilter>())
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = Camera.main.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                    Assert.That(point.z, Is.GreaterThan(0));
                    Assert.That(point.x, Is.InRange(0f, 1f));
                    Assert.That(point.y, Is.InRange(0f, 1f));
                }
            CaptureCamera("/tmp/ride_on_mower_front_cutter_installed.png");
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(.7f);
            Click(Find("FrontCutter_slot"));
            yield return null;
            Click((Button)details.GetType().GetProperty("UninstallButton").GetValue(details));
            yield return null;
            Assert.That(tool.gameObject.activeSelf, Is.False);
            export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
            Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(
                Path.Combine(FarmVehicleLibrary.AssetDirectory("ride_on_mower"), "vehicle_config.json"))));
        }

        [UnityTest]
        public IEnumerator ImplementListShowsScrollbarAndLastRowCanBeClicked()
        {
            if (!FarmVehicleLibrary.IsInstalled("compact_tractor")) Assert.Ignore("Local CAD assets not installed.");
            foreach (var scene in new[] { "scene1", "scene2" })
            {
                yield return SceneManager.LoadSceneAsync(scene);
                yield return null;
                if (scene == "scene1")
                {
                    Click(Find("Car6_Img"));
                    yield return new WaitForSeconds(1f);
                    Click(Find("OpenBag_btn"));
                }
                else
                {
                    Component(Find("LastPanel"), "LastPanel").GetType().GetMethod("OpenLastPanel")
                        .Invoke(Component(Find("LastPanel"), "LastPanel"), null);
                    yield return new WaitForSeconds(.8f);
                    Click(Find("OpeBag_btn"));
                }
                yield return new WaitForSeconds(1f);
                var lastSlot = Find("CombineHeader_slot");
                var scroll = lastSlot.GetComponentInParent<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
                Assert.That(scroll.verticalScrollbar.gameObject.activeInHierarchy, Is.True, scene);
                Assert.That(scroll.verticalScrollbar.targetGraphic.isActiveAndEnabled, Is.True, scene);
                Assert.That(scroll.verticalScrollbar.targetGraphic.color.a, Is.GreaterThan(.1f));
                Assert.That(scroll.verticalScrollbar.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(12f));
                foreach (var name in new[] { "Hand_slot", "ChiselPlow_slot", "DiscHarrow_slot", "GrooveMaker_slot", "Cultivator_slot", "ChainCrusher_slot", "InterrowMill_slot", "FrontCutter_slot", "GarlicPlanter_slot", "MulchPlanter_slot", "FarmTrailer_slot", "CombineHeader_slot" })
                    Assert.That(Find(name), Is.Not.Null, name);

                scroll.verticalNormalizedPosition = 0;
                yield return null;
                Canvas.ForceUpdateCanvases();
                var rect = lastSlot.GetComponent<RectTransform>();
                var canvas = scroll.GetComponentInParent<Canvas>();
                var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var bar = scroll.verticalScrollbar.GetComponent<RectTransform>();
                var barCenter = RectTransformUtility.WorldToScreenPoint(eventCamera, bar.TransformPoint(bar.rect.center));
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)scroll.transform,
                    barCenter, eventCamera), Is.True, "Scrollbar must remain inside the panel clipping area");
                var center = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center));
                Assert.That(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, center, eventCamera), Is.True);
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = center }, hits);
                Assert.That(hits.Any(hit => hit.gameObject.transform.IsChildOf(lastSlot.transform)), Is.True,
                    "The last implement must be reachable by a real pointer, not just ExecuteEvents");
                Click(lastSlot);
            }
        }

        [UnityTest]
        public IEnumerator CabTractorInstallsSensorsExportsAndSelectsNewScene()
        {
            if (!FarmVehicleLibrary.IsInstalled("cab_tractor")) Assert.Ignore("Local CAD assets not installed.");
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return new WaitForSeconds(.2f);
            Click(Find("Car7_Img"));
            yield return new WaitForSeconds(2f);
            var car = Find("Car7");
            var body = car.transform.Find("car7");
            Assert.That(car.activeInHierarchy, Is.True);
            var bodyBounds = RendererBounds(body.gameObject);
            var viewport = Camera.main.WorldToViewportPoint(bodyBounds.center);
            Assert.That(viewport.z, Is.GreaterThan(0));
            Assert.That(viewport.x, Is.InRange(.1f, .9f));
            Assert.That(viewport.y, Is.InRange(.1f, .9f));
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(2f);
            var bag = Component(Find("BagPanel"), "EquipmentBagController");
            var details = Component(Find("BagPanel"), "EquipmentDetailsUI");
            foreach (var slotName in new[] { "Radar", "RTK", "Camera", "Imu" })
            {
                var getter = bag.GetType().GetMethod("GetSlot");
                var slot = (Component)getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, slotName) });
                Click(slot.gameObject);
                yield return null;
                var install = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
                Assert.That(install.interactable, Is.True, slotName);
                Click(install);
                yield return new WaitForSeconds(.8f);
            }
            var main = Component(Find("MainPanel"), "MainPanel");
            var export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
            Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(
                Path.Combine(FarmVehicleLibrary.AssetDirectory("cab_tractor"), "vehicle_config.json"))));
            foreach (var sensor in FarmVehicleLibrary.Configuration("cab_tractor").sensors.Where(s => s.id != "rtk_secondary"))
            {
                string name = sensor.id switch { "imu" => "Imu_box", "lidar" => "RADAR", "right_camera" => "CarCamera", _ => "RTK" };
                var group = car.transform.Find(name);
                Assert.That(group.gameObject.activeSelf, Is.True, name);
                var pose = RosFluConverter.RelativePose(car.transform, group);
                for (int axis = 0; axis < 3; axis++)
                    Assert.That(pose.xyz[axis], Is.EqualTo(sensor.pose.xyz[axis]).Within(.00001));
            }
            if (FarmVehicleLibrary.IsInstalled("cab_groove"))
            {
                var tools = new[] { car.transform.Find("GrooveMaker"), car.transform.Find("Cultivator"), car.transform.Find("ChainCrusher"), car.transform.Find("InterrowMill") };
                Assert.That(tools.All(tool => tool != null), Is.True);
                var positions = tools.Select(tool => tool.localPosition).ToArray();
                foreach (var item in new[] { ("GrooveMaker", "cab_groove", tools[0]),
                    ("Cultivator", "cab_cultivator", tools[1]), ("ChainCrusher", "cab_crusher", tools[2]),
                    ("InterrowMill", "cab_interrow", tools[3]),
                    ("GrooveMaker", "cab_groove", tools[0]) })
                {
                    Click(Find(item.Item1 + "_slot"));
                    yield return null;
                    var install = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
                    Assert.That(install.interactable, Is.True);
                    Click(install);
                    yield return new WaitForSeconds(.6f);
                    for (int index = 0; index < tools.Length; index++)
                    {
                        Assert.That(tools[index].gameObject.activeSelf, Is.EqualTo(tools[index] == item.Item3));
                        Assert.That(tools[index].localPosition, Is.EqualTo(positions[index]));
                    }
                    export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
                    Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(
                        Path.Combine(FarmVehicleLibrary.AssetDirectory(item.Item2), "vehicle_config.json"))));
                    CaptureCamera("/tmp/" + item.Item2 + "_installed.png");
                }
                Click((Button)details.GetType().GetProperty("UninstallButton").GetValue(details));
                yield return null;
                Assert.That(tools.Any(tool => tool.gameObject.activeSelf), Is.False);
            }
            foreach (var slotName in new[] { "Hand_slot", "FrontCutter_slot", "CombineHeader_slot" })
            {
                Click(Find(slotName));
                yield return null;
                AssertUnavailableWithoutDescription(details);
            }
            Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
            yield return new WaitForSeconds(1f);
            Debug.Log($"CAB_CAMERA pose={Camera.main.transform.position} fov={Camera.main.fieldOfView} bounds={bodyBounds}");
            CaptureCamera("/tmp/cab_tractor_garage.png");
            var projectedMin = Vector3.one * float.PositiveInfinity;
            var projectedMax = Vector3.one * float.NegativeInfinity;
            foreach (var filter in body.GetComponentsInChildren<MeshFilter>())
            {
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var screen = Camera.main.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                    projectedMin = Vector3.Min(projectedMin, screen);
                    projectedMax = Vector3.Max(projectedMax, screen);
                }
            }
            Assert.That(projectedMin.z, Is.GreaterThan(0));
            Assert.That(projectedMin.x, Is.GreaterThanOrEqualTo(0));
            Assert.That(projectedMin.y, Is.GreaterThanOrEqualTo(0));
            Assert.That(projectedMax.x, Is.LessThanOrEqualTo(1));
            Assert.That(projectedMax.y, Is.LessThanOrEqualTo(1));
            Click(Find("Output_btn"));
            yield return null;
            var scroll = Find("SceneViewport").GetComponent<ScrollRect>();
            Assert.That(!scroll.horizontal && scroll.vertical, Is.True);
            var grid = scroll.content.GetComponent<GridLayoutGroup>();
            Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(grid.constraintCount, Is.EqualTo(3));
            var buttons = scroll.content.GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Scenario_", StringComparison.Ordinal)).ToArray();
            Assert.That(buttons.Length, Is.EqualTo(FarmScenarioCatalog.Scenarios.Length));
            Canvas.ForceUpdateCanvases();
            var rectangles = buttons.Select(button =>
            {
                var corners = new Vector3[4];
                button.GetComponent<RectTransform>().GetWorldCorners(corners);
                return new Rect(corners[0], corners[2] - corners[0]);
            }).ToArray();
            for (int a = 0; a < rectangles.Length; a++)
                for (int b = a + 1; b < rectangles.Length; b++)
                    Assert.That(rectangles[a].Overlaps(rectangles[b]), Is.False, "Scene cards overlap");
            scroll.verticalNormalizedPosition = 0;
            yield return null;
            Click(Find("Scenario_vineyard"));
            Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo("vineyard"));
            Click(Find("Scenario_vegetable_field"));
            Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo("vegetable_field"));
            CaptureCamera("/tmp/farm_scenario_selection.png");
            FarmScenarioCatalog.SelectedId = "orchard";
        }

        private static void CaptureCamera(string path)
        {
            var camera = Camera.main;
            var target = new RenderTexture(1280, 720, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(image);
            }
        }

        [UnityTest]
        public IEnumerator NewVehicleCameraRemainsInsideGarage()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var walls = RendererBounds(Find("Walls"));
            var floor = RendererBounds(Find("Floor"));
            var ceiling = RendererBounds(Find("Ceiling"));
            Debug.Log($"GARAGE_CAMERA_BOUNDS walls={walls} floor={floor} ceiling={ceiling}");
            Click(Find("Car6_Img"));
            yield return new WaitForSeconds(2f);
            var control = Component(Find("Car6"), "cameraControl");
            var target = Find("target6").transform;
            Debug.Log($"GARAGE_CAMERA_TARGET {target.position} camera={Camera.main.transform.position}");
            var inside = new Bounds();
            inside.SetMinMax(new Vector3(walls.min.x + .3f, floor.max.y + .3f, walls.min.z + .3f),
                new Vector3(walls.max.x - .3f, ceiling.min.y - .3f, walls.max.z - .3f));
            foreach (float vertical in new[] { 1f, 35f, 70f })
                for (int horizontal = -180; horizontal < 180; horizontal += 45)
                {
                    control.GetType().GetMethod("FixCamera").Invoke(control, new object[] { 1000f, (float)horizontal, vertical });
                    yield return RemainInside(inside, 1.8f, $"yaw={horizontal}, pitch={vertical}");
                }
            control.GetType().GetMethod("FixCamera").Invoke(control, new object[] { 0f, -45f, 25f });
            yield return RemainInside(inside, 1.8f, "minimum zoom");
            Assert.That(Vector3.Distance(Camera.main.transform.position, target.position), Is.InRange(2.3f, 2.8f));
            Click(Find("OpenBag_btn"));
            yield return RemainInside(inside, 1.8f, "equipment view");
            Click(Find("BagPanel").transform.Find("Header/CloseButton").gameObject);
            yield return RemainInside(inside, 1f, "close equipment view");
            Assert.That(control.GetType().GetProperty("IsInputLocked").GetValue(control), Is.False);
            Click(Find("Back_btn"));
            yield return new WaitForSeconds(1f);
            Click(Find("Car5_Img"));
            yield return new WaitForSeconds(1f);
            var originalControl = Component(Find("Car5"), "cameraControl");
            Assert.That(originalControl.GetComponents<Component>().Any(c => c.GetType().Name == "CinemachineConfiner3D"), Is.True);
            foreach (var setting in new[] { ("minRadius", 3f), ("maxRadius", 14f) })
                Assert.That(originalControl.GetType().GetField(setting.Item1, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(originalControl), Is.EqualTo(setting.Item2), "Metric Car5 uses the confined metre-scale camera");
            Click(Find("Back_btn"));
            yield return new WaitForSeconds(1f);
            Click(Find("Car6_Img"));
            yield return new WaitForSeconds(2f);
            yield return RemainInside(inside, 1f, "reselect vehicle");
            Assert.That(Resources.FindObjectsOfTypeAll<Transform>().Count(t =>
                t.gameObject.scene == SceneManager.GetActiveScene() && t.name == "FarmPreviewCameraBounds"), Is.EqualTo(InstalledVehicleCount));
            if (!Application.isBatchMode)
            {
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot("/tmp/farm_camera_confined.png");
                yield return null;
            }
        }

        private static IEnumerator RemainInside(Bounds inside, float seconds, string operation)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Assert.That(inside.Contains(Camera.main.transform.position), Is.True,
                    $"Camera escaped garage during {operation}: {Camera.main.transform.position}, allowed={inside}");
            }
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [UnityTest]
        public IEnumerator ExistingGarageInstallsFixedSensorsAndExclusiveImplements()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return new WaitForSeconds(.2f);
            Assert.That(Find("FarmMachinePanel"), Is.Null, "Do not add a separate farm-machine screen");
            var scroll = Find("GarageScroll").GetComponent<ScrollRect>();
            Assert.That(scroll.content.GetComponentsInChildren<Button>().Length, Is.EqualTo(10),
                "Metric replacements reuse Car1/2/4/5 cards rather than creating duplicates");
            scroll.verticalNormalizedPosition = 0;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var vehicleLabel = Find("Car6_Img").GetComponentInChildren<Text>();
            Assert.That(vehicleLabel.cachedTextGenerator.vertexCount, Is.GreaterThan(4),
                "The new vehicle name must render inside the existing garage card");
            Click(Find("Car6_Img"));
            yield return new WaitForSeconds(1f);
            var car = Find("Car6");
            Assert.That(car.activeInHierarchy, Is.True);
            yield return new WaitForSeconds(2f);
            Assert.That(Vector3.Distance(Camera.main.transform.position, car.transform.position), Is.GreaterThan(4f),
                "The inherited camera orbit scale must not place the camera inside the new vehicle");
            for (int i = 1; i <= 5; i++) Assert.That(Find("Car" + i).activeInHierarchy, Is.False);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(2f);
            var vehicleInView = Camera.main.WorldToViewportPoint(car.transform.TransformPoint(new Vector3(0, .4f, .5f)));
            Assert.That(vehicleInView.z, Is.GreaterThan(0));
            Assert.That(vehicleInView.x, Is.InRange(.1f, .8f));
            Assert.That(vehicleInView.y, Is.InRange(.1f, .9f));
            var bag = Component(Find("BagPanel"), "EquipmentBagController");
            var details = Component(Find("BagPanel"), "EquipmentDetailsUI");
            foreach (var slotName in new[] { "Radar", "RTK", "Camera", "Imu" })
            {
                var getter = bag.GetType().GetMethod("GetSlot");
                var slot = (Component)getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, slotName) });
                Click(slot.gameObject);
                yield return null;
                Click((Button)details.GetType().GetProperty("InstallButton").GetValue(details));
                yield return new WaitForSeconds(1f);
            }
            var main = Component(Find("MainPanel"), "MainPanel");
            var export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
            Assert.That(JsonUtility.FromJson<VehicleConfigurationData>(File.ReadAllText(export.JsonPath)).vehicleId, Is.EqualTo("compact_tractor"));
            foreach (var sensor in FarmVehicleLibrary.Configuration("compact_tractor").sensors.Where(s => s.id != "rtk_secondary"))
            {
                string name = sensor.id switch { "imu" => "Imu_box", "lidar" => "RADAR", "right_camera" => "CarCamera", _ => "RTK" };
                var pose = RosFluConverter.RelativePose(car.transform, car.transform.Find(name));
                for (int axis = 0; axis < 3; axis++) Assert.That(pose.xyz[axis], Is.EqualTo(sensor.pose.xyz[axis]).Within(.00001));
            }
            var chisel = car.transform.Find("ChiselPlow");
            var harrow = car.transform.Find("DiscHarrow");
            var tools = new[] { chisel, harrow };
            var positions = tools.Select(tool => tool.localPosition).ToArray();
            foreach (var item in new[] { ("ChiselPlow", "tractor_chisel", chisel),
                ("DiscHarrow", "tractor_harrow", harrow),
                ("ChiselPlow", "tractor_chisel", chisel) })
            {
                Click(Find(item.Item1 + "_slot"));
                yield return null;
                Click((Button)details.GetType().GetProperty("InstallButton").GetValue(details));
                yield return new WaitForSeconds(.6f);
                Assert.That(item.Item3.gameObject.activeSelf, Is.True);
                for (int index = 0; index < tools.Length; index++)
                {
                    Assert.That(tools[index].gameObject.activeSelf, Is.EqualTo(tools[index] == item.Item3));
                    Assert.That(tools[index].localPosition, Is.EqualTo(positions[index]));
                }
                export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
                Assert.That(File.ReadAllBytes(export.JsonPath), Is.EqualTo(File.ReadAllBytes(Path.Combine(FarmVehicleLibrary.AssetDirectory(item.Item2), "vehicle_config.json"))));
            }
            Click((Button)details.GetType().GetProperty("UninstallButton").GetValue(details));
            yield return null;
            Assert.That(tools.Any(tool => tool.gameObject.activeSelf), Is.False);
            Click(Find("Hand_slot"));
            yield return null;
            AssertUnavailableWithoutDescription(details);
            Click(Find("ChainCrusher_slot"));
            yield return null;
            AssertUnavailableWithoutDescription(details);
            Click(Find("InterrowMill_slot"));
            yield return null;
            AssertUnavailableWithoutDescription(details);
            Click(Find("Back_btn"));
            yield return new WaitForSeconds(1f);
            Click(Find("Car6_Img"));
            yield return new WaitForSeconds(1f);
            Assert.That(tools.Any(tool => tool.gameObject.activeSelf), Is.False);
        }

        [UnityTest]
        public IEnumerator OriginalCarUsesSharedEquipmentPanelStyleAndCanReopen()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Click(Find("Car3_Img"));
            yield return new WaitForSeconds(.8f);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(1f);
            var reference = Find("BagPanel");
            var referenceRect = reference.GetComponent<RectTransform>();
            Vector2 position = referenceRect.anchoredPosition, size = referenceRect.sizeDelta;
            string title = reference.transform.Find("Header/Text (Legacy)").GetComponent<Text>().text;
            var referenceDetails = Component(reference, "EquipmentDetailsUI");
            var referenceInstall = (Button)referenceDetails.GetType().GetProperty("InstallButton").GetValue(referenceDetails);
            var installSprite = referenceInstall.GetComponent<Image>().sprite;
            var installColour = referenceInstall.GetComponent<Image>().color;

            Click(reference.transform.Find("Header/CloseButton").gameObject);
            yield return new WaitForSeconds(.8f);
            Click(Find("Back_btn"));
            yield return new WaitForSeconds(.8f);
            Click(Find("Car0_Img"));
            yield return new WaitForSeconds(.8f);
            var panel = Find("BagPanel");
            for (int opening = 0; opening < 2; opening++)
            {
                Click(Find("OpenBag_btn"));
                yield return new WaitForSeconds(1f);
                Assert.That(panel.activeInHierarchy, Is.True);
                Assert.That(Find("Car0").activeInHierarchy, Is.True);
                Assert.That(SceneManager.GetSceneByName("scene2").isLoaded, Is.False);
                Assert.That(panel.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(position));
                Assert.That(panel.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(size));
                Assert.That(panel.transform.Find("Header/Text (Legacy)").GetComponent<Text>().text, Is.EqualTo(title));
                Assert.That(panel.GetComponent<CanvasGroup>().interactable, Is.True);
                var details = Component(panel, "EquipmentDetailsUI");
                var install = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
                Assert.That(install.name, Is.EqualTo("Button (Legacy)"));
                Assert.That(install.GetComponent<Image>().sprite, Is.EqualTo(installSprite));
                Assert.That(install.GetComponent<Image>().color, Is.EqualTo(installColour));
                Assert.That(install, Is.SameAs(referenceInstall), "Car0 must use the same shared equipment controls");
                Assert.That(details.transform.Find("ActionButtons"), Is.Null,
                    "Use the authored skinned controls, not fallback buttons");
                Click(Find("ChiselPlow_slot"));
                yield return null;
                AssertUnavailableWithoutDescription(details);
                if (opening == 0 && !Application.isBatchMode)
                {
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot("/tmp/ackermann_equipment_panel_unified.png");
                    yield return new WaitForSeconds(.2f);
                }
                Click(panel.transform.Find("Header/CloseButton").gameObject);
                yield return new WaitForSeconds(.6f);
                Assert.That(panel.activeSelf, Is.False);
                Assert.That(Find("Car0").activeInHierarchy, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator OriginalCarDisablesIncompatibleImplementsWithoutDescription()
        {
            yield return SceneManager.LoadSceneAsync("scene2");
            yield return null;
            Component(Find("LastPanel"), "LastPanel").GetType().GetMethod("OpenLastPanel")
                .Invoke(Component(Find("LastPanel"), "LastPanel"), null);
            yield return new WaitForSeconds(.8f);
            Click(Find("OpeBag_btn"));
            yield return new WaitForSeconds(1f);
            var details = Component(Find("BagPanel"), "EquipmentDetailsUI_copy");
            foreach (var name in new[] { "ChiselPlow_slot", "DiscHarrow_slot", "GrooveMaker_slot", "Cultivator_slot", "ChainCrusher_slot", "InterrowMill_slot", "FrontCutter_slot" })
            {
                Click(Find(name));
                yield return null;
                AssertUnavailableWithoutDescription(details);
            }
        }

        private static void AssertUnavailableWithoutDescription(Component details)
        {
            var button = (Button)details.GetType().GetProperty("InstallButton").GetValue(details);
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            Assert.That(button.interactable, Is.False);
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            Click(button);
            Assert.That(clicks, Is.Zero, "An unavailable implement must not accept install clicks");
            var description = (Component)details.GetType().GetField("equipmentDescription",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(details);
            Assert.That(description.gameObject.activeSelf, Is.False);
            Assert.That(description.GetType().GetProperty("text").GetValue(description), Is.EqualTo(string.Empty));
        }

        private static float GroundGap(GameObject car)
        {
            float floor = Find("Garage1").GetComponentsInChildren<Transform>(true)
                .Single(t => t.name == "Floor").GetComponentsInChildren<Renderer>(true).Max(r => r.bounds.max.y);
            var baseRoot = car.GetComponents<Component>().Any(c => c != null && c.GetType().Name == "FarmGarageVehicle")
                ? car.transform.Find(car.name.ToLowerInvariant()) : null;
            var body = baseRoot != null ? baseRoot : car.transform;
            float bottom = body.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled &&
                !r.GetComponentsInParent<Transform>(true).Any(t =>
                    t.name is "RADAR" or "RTK" or "CarCamera" or "Imu_box" or "Hand")).Min(r => r.bounds.min.y);
            Debug.Log($"GARAGE_GROUND car={car.name} floor={floor:F6} bottom={bottom:F6} gap={bottom-floor:F6}");
            return bottom - floor;
        }

        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(t => t.gameObject.scene == SceneManager.GetActiveScene() && t.name == name)?.gameObject;
        private static int InstalledVehicleCount => FarmVehicleLibrary.Presets.Count(p =>
            !string.IsNullOrEmpty(p.garageName) && FarmVehicleLibrary.IsInstalled(p.id));
        private static Component Component(GameObject root, string type) => root.GetComponentsInChildren<Component>(true)
            .First(c => c != null && c.GetType().Name == type);
        private static object Invoke(Component target, string name) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Click(Button button) => Click(button.gameObject);
        private static void Click(GameObject target)
        {
            Assert.That(target, Is.Not.Null);
            Assert.That(target.activeInHierarchy, Is.True, target.name);
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler);
        }
    }
}
