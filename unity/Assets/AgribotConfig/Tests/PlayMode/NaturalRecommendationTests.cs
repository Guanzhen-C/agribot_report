using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class NaturalRecommendationTests
    {
        private static Component Component(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.GetType().Name == name);
        private static object Invoke(Component component, string method, params object[] args) =>
            component.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(component, args);
        private static NaturalRecommendation Item(string id, string car, string preset, string tool, string scene, string algorithm)
        {
            string hash = null;
            if (car != "Car0" && car != "Car3")
            {
                using var sha = SHA256.Create();
                hash = BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(System.IO.Path.Combine(
                    FarmVehicleLibrary.AssetDirectory(preset), "vehicle_config.json")))).Replace("-", "").ToLowerInvariant();
            }
            return new() { id = id, car = car, vehicleName = car, preset = preset, implement = tool,
                implementName = tool, scene = scene, sceneName = scene, algorithm = algorithm,
                vehicleConfigSha256 = hash,
                verification = "用户分组确认，未逐项重跑", evidence = "manual_confirmations.json",
                reason = "按用户指定场景、车型与机具匹配，使用已经确认的定位、规划和控制方案。" };
        }

        [UnityTest]
        public IEnumerator RecommendationsApplyDifferentVehiclesToolsAndAlgorithmsWithoutLaunching()
        {
            string originalStatus = RosSimulationLauncher.ReadManagedStatus();
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var panel = Component("NaturalRecommendationPanel");
            Invoke(panel, "Open");
            Assert.That(GameObject.Find("RecommendationInput").GetComponent<TMPro.TMP_InputField>().characterLimit, Is.EqualTo(2000));
            var input = GameObject.Find("RecommendationInput").GetComponent<TMPro.TMP_InputField>();
            input.ActivateInputField();
            yield return null;
            yield return null;
            Assert.That(input.isFocused, Is.True);
            Assert.That(input.textViewport.rect.width, Is.GreaterThan(200));
            input.text = "葡萄园使用履带车";
            input.textComponent.ForceMeshUpdate();
            Assert.That(input.textComponent.textInfo.characterCount, Is.GreaterThan(0));
            foreach (var item in new[] {
                Item("Car3|vineyard|lite6", "Car3", "unity_car3", "lite6", "vineyard", "stvl_off_navsat_smac_hybrid_mppi"),
                Item("Car1|arable_field|compact_chisel_plow", "Car1", "car1_chisel", "compact_chisel_plow", "arable_field", "stvl_off_navsat_smac_hybrid_mppi"),
                Item("Car9|wide_headland|combine_header", "Car9", "harvester_header", "combine_header", "wide_headland", "stvl_off_navsat_smac_hybrid_rpp") })
            {
                Invoke(panel, "Open");
                Invoke(panel, "Present", new NaturalRecommendationResponse { ok = true, recommendations = new[] { item } });
                yield return null;
                yield return null;
                var apply = GameObject.Find("ApplyRecommendation").GetComponent<Button>();
                Assert.That(apply.interactable, Is.True);
                var card = apply.transform.parent as RectTransform;
                Assert.That(card.rect.height, Is.GreaterThan(130));
                if (item.car == "Car3")
                {
                    ScreenCapture.CaptureScreenshot("/tmp/agribot_llm_recommendation.png");
                    yield return null;
                }
                yield return (IEnumerator)Invoke(panel, item.car == "Car3" ? "ApplyConfiguration" : "ValidateAndApply", item);
                Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo(item.scene));
                Assert.That(GameObject.Find("ROS_Algorithm_Selection"), Is.Not.Null);
                Assert.That(GameObject.Find("RecommendationStatus")?.GetComponent<Text>().text,
                    Is.Not.EqualTo("无法安装推荐装备：Hand"));
                var main = Component("MainPanel");
                var export = (VehicleConfigurationExportResult)Invoke(main, "ExportSelectedVehicle");
                var json = System.IO.File.ReadAllText(export.JsonPath);
                Assert.That(json, Does.Contain(item.preset));
                Assert.That(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Any(b => b.name == "Launch"), Is.True);
                Assert.That(RosSimulationLauncher.ReadManagedStatus(), Is.EqualTo(originalStatus));
                GameObject.Find("ROS_Algorithm_Selection").SetActive(false);
                if (item.car == "Car3")
                {
                    item.action = "algorithm";
                    Invoke(panel, "Open");
                    FarmScenarioCatalog.SelectedId = "arable_field";
                    yield return (IEnumerator)Invoke(panel, "ValidateAndApply", item);
                    Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo("arable_field"));
                    Assert.That(GameObject.Find("RecommendationStatus").GetComponent<Text>().text, Does.Contain("已变化"));
                    FarmScenarioCatalog.SelectedId = item.scene;
                    item.action = "configuration";
                }
            }
        }

        [UnityTest]
        public IEnumerator Car0ConfigurationCanAlsoBeApplied()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var panel = Component("NaturalRecommendationPanel");
            Invoke(panel, "Open");
            var item = Item("Car0|greenhouse|lite6", "Car0", "ackermann_current", "lite6", "greenhouse", "stvl_off_fastlivo_rtk_smac_hybrid_mppi");
            yield return (IEnumerator)Invoke(panel, "ApplyConfiguration", item);
            Assert.That(GameObject.Find("ROS_Algorithm_Selection"), Is.Not.Null);
            var export = AckermannRuntimeConfiguration.BuildOrRefresh();
            Assert.That(export, Is.Not.Null);
            Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo("greenhouse"));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("scene1"));
        }

        [UnityTest]
        public IEnumerator LocalRevalidationRejectsUnconfirmedCandidateAndMissingKeyIsNotNeeded()
        {
            var item = Item("Car7|farmyard|mounted_cultivator", "Car7", "cab_cultivator", "mounted_cultivator", "farmyard", "stvl_off_navsat_smac_hybrid_mppi");
            var task = NaturalRecommendationClient.Revalidate(item, CancellationToken.None);
            while (!task.IsCompleted) yield return null;
            Assert.That(task.IsFaulted, Is.True);
            Assert.That(task.Exception.GetBaseException().Message, Does.Contain("已变化"));
            var valid = Item("Car9|wide_headland|combine_header", "Car9", "harvester_header", "combine_header", "wide_headland", "stvl_off_navsat_smac_hybrid_rpp");
            task = NaturalRecommendationClient.Revalidate(valid, CancellationToken.None);
            while (!task.IsCompleted) yield return null;
            Assert.That(task.IsFaulted, Is.False, task.Exception?.GetBaseException().Message);
            Assert.That(task.Result.vehicleName, Does.Contain("联合收割机"));
            Assert.That(task.Result.sceneName, Does.Contain("宽地头"));
        }
    }
}
