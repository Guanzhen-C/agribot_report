using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class HandoffButtonsTests
    {
        [UnityTest]
        public IEnumerator FollowConfigurationPanelWithoutOverlapping()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            int[] controls = null;
            foreach (string car in new[] { "Car0", "Car1", "Car3", "Car9" })
            {
                Find(car + "_Img").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.8f);
                var buttons = new[] { "Explode_NewVehicle_btn", "Output_btn", "Stop_NewVehicle_Simulation",
                    "Export_NewVehicle_Physical", "OpenBag_btn" }.Select(name => Find(name).GetComponent<Button>()).ToArray();
                if (controls == null) controls = buttons.Select(button => button.GetInstanceID()).ToArray();
                Assert.That(buttons.Select(button => button.GetInstanceID()).ToArray(), Is.EqualTo(controls),
                    "All cars must use the same controls, not separate copies");
                Canvas.ForceUpdateCanvases();
                var back = Bounds(Find("Back_btn").GetComponent<RectTransform>());
                for (int index = 0; index < buttons.Length; index++)
                {
                    var button = buttons[index];
                    Assert.That(button.gameObject.activeInHierarchy, Is.True);
                    Assert.That(button.image.sprite, Is.Null);
                    var bounds = Bounds(button.GetComponent<RectTransform>());
                    Assert.That(bounds.Overlaps(back), Is.False, button.name);
                    for (int previous = 0; previous < index; previous++)
                        Assert.That(bounds.Overlaps(Bounds(buttons[previous].GetComponent<RectTransform>())),
                            Is.False, buttons[previous].name + " / " + button.name);
                }
                Assert.That(buttons[1].image.color, Is.EqualTo(new Color(.75f, .88f, .27f)));
                Assert.That(buttons[1].GetComponentInChildren<Text>().text, Is.EqualTo("启动仿真"));
                Find("Back_btn").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.8f);
                Assert.That(buttons.All(button => !button.gameObject.activeInHierarchy), Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ToolbarWrapsWithoutOverlappingButtonsOrSidePanels()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Find("Car0_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            var toolbar = Find("VehicleActionToolbar");
            var layout = toolbar.GetComponents<MonoBehaviour>().Single(item => item.GetType().Name == "VehicleActionToolbarLayout");
            layout.enabled = false;
            var controls = toolbar.GetComponentsInChildren<Button>().Select(item => item.GetComponent<RectTransform>()).ToArray();
            var car = Find("Car0");
            var repair = car.GetComponents<MonoBehaviour>().Single(item => item.GetType().Name == "Car0GarageVehicle");
            repair.GetType().GetMethod("OpenRepair").Invoke(repair, null);
            yield return new WaitForSeconds(.3f);
            var panel = car.GetComponentsInChildren<Transform>(true).Single(item => item.name == "fixPanel").GetComponent<RectTransform>();
            foreach (float width in new[] { 640f, 800f, 1024f, 1280f, 1920f })
            {
                layout.GetType().GetMethod("Arrange").Invoke(layout, new object[] { width });
                LayoutRebuilder.ForceRebuildLayoutImmediate(toolbar.GetComponent<RectTransform>());
                layout.GetType().GetMethod("PositionSidePanel").Invoke(null, new object[] { panel });
                Canvas.ForceUpdateCanvases();
                var back = Bounds(Find("Back_btn").GetComponent<RectTransform>());
                var side = Bounds(panel);
                for (int index = 0; index < controls.Length; index++)
                {
                    var bounds = Bounds(controls[index]);
                    Assert.That(bounds.Overlaps(back), Is.False, $"width={width} {controls[index].name} / Back");
                    Assert.That(bounds.Overlaps(side), Is.False, $"width={width} {controls[index].name} / repair");
                    for (int previous = 0; previous < index; previous++)
                        Assert.That(bounds.Overlaps(Bounds(controls[previous])), Is.False,
                            $"width={width} {controls[index].name} / {controls[previous].name}");
                }
            }
            layout.enabled = true;
            var escape = car.GetComponentsInChildren<Transform>(true).Single(item => item.name == "Esc_btn");
            Assert.That(Bounds(escape.GetComponent<RectTransform>()).Overlaps(
                Bounds(Find("Back_btn").GetComponent<RectTransform>())), Is.False);
        }

        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(item => item.gameObject.scene.IsValid() && item.name == name).gameObject;
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
