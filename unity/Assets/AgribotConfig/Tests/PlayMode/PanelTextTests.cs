using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class PanelTextTests
    {
        [UnityTest]
        public IEnumerator EquipmentPromptIsEditableTextAndUsesUpdatedWording()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var main = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(x => x.GetType().Name == "MainPanel");
            main.GetType().GetMethod("SelectRecommendedVehicle").Invoke(main, new object[] { "Car3" });
            yield return new WaitForSeconds(1f);
            main.GetType().GetMethod("OpenRecommendedEquipment").Invoke(main, null);
            yield return new WaitForSeconds(.5f);
            var details = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.GetType().Name == "EquipmentDetailsUI");
            var empty = (GameObject)details.GetType().GetField("emptyState",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(details);
            Assert.That(empty, Is.Not.Null);
            var labels = empty.GetComponentsInChildren<Text>(true);
            Assert.That(labels.Select(x => x.text), Does.Contain("请选择装备"));
            Assert.That(empty.GetComponentsInChildren<RawImage>(true), Is.Empty);
            Assert.That(empty.GetComponentsInChildren<Image>(true).Where(x => x.enabled && x.sprite != null), Is.Empty);
            foreach (var label in labels)
            {
                Assert.That(label.text, Does.Not.Contain("请选择一件装备"));
                Assert.That(label.transform.lossyScale.x / label.transform.lossyScale.y, Is.EqualTo(1f).Within(.01f),
                    "The prompt must not stretch glyphs into a tall, narrow image-like shape");
            }
            var root = details.GetComponentInParent<Canvas>().transform;
            foreach (var label in root.GetComponentsInChildren<Text>(true))
                Assert.That(label.GetComponent<RawImage>(), Is.Null);
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(label.GetComponent<RawImage>(), Is.Null);
        }
    }
}
