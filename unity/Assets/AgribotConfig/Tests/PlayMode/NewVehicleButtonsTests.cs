using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public class NewVehicleButtonsTests
    {
        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(t => t.gameObject.scene.IsValid() && t.name == name).gameObject;

        [UnityTest]
        public IEnumerator EachNewVehicleShowsExportAndStopControls()
        {
            yield return SceneManager.LoadSceneAsync("scene1", LoadSceneMode.Single);
            yield return null;
            for (int i = 1; i <= 5; i++)
            {
                Find($"Car{i}_Img").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(0.8f);
                Assert.That(Find($"Car{i}").activeInHierarchy, Is.True);
                var output = Find("Output_btn");
                Assert.That(output.activeInHierarchy, Is.True);
                Assert.That(output.GetComponentInChildren<Text>().text, Is.EqualTo("启动仿真"));
                Assert.That(Find("Explode_NewVehicle_btn").activeInHierarchy, Is.True);
                Assert.That(Find("Stop_NewVehicle_Simulation").activeInHierarchy, Is.True);
                Assert.That(Find("Export_NewVehicle_Physical").activeInHierarchy, Is.True);
                Assert.That(Find("Stop_NewVehicle_Simulation").GetComponentInChildren<Text>().text,
                    Is.EqualTo("停止仿真"));
                var actions = new[] { "Explode_NewVehicle_btn", "Output_btn",
                    "Stop_NewVehicle_Simulation", "Export_NewVehicle_Physical", "OpenBag_btn" }
                    .Select(name => Find(name).GetComponent<Button>()).ToArray();
                Canvas.ForceUpdateCanvases();
                Assert.That(actions.All(button => button.transform.parent == actions[0].transform.parent), Is.True);
                Assert.That(actions.Take(4).All(button => button.image.sprite == output.GetComponent<Button>().image.sprite), Is.True);
                var backBounds = WorldRect(Find("Back_btn").GetComponent<RectTransform>());
                for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
                {
                    var bounds = WorldRect(actions[actionIndex].GetComponent<RectTransform>());
                    Assert.That(bounds.Overlaps(backBounds), Is.False, actions[actionIndex].name);
                    for (int previous = 0; previous < actionIndex; previous++)
                        Assert.That(bounds.Overlaps(WorldRect(actions[previous].GetComponent<RectTransform>())), Is.False,
                            actions[previous].name + " / " + actions[actionIndex].name);
                }
                Find("Explode_NewVehicle_btn").GetComponent<Button>().onClick.Invoke();
                var slider = Find("ExplodedViewSlider").GetComponent<Slider>();
                Assert.That(slider.gameObject.activeInHierarchy, Is.True);
                var car = Find($"Car{i}");
                var originalPositions = car.GetComponentsInChildren<Transform>(true)
                    .ToDictionary(part => part, part => part.localPosition);
                slider.value = 1f;
                Assert.That(originalPositions.Any(pair =>
                    Vector3.Distance(pair.Key.localPosition, pair.Value) > 0.01f), Is.True);
                Find("CloseExplodedView").GetComponent<Button>().onClick.Invoke();
                Assert.That(originalPositions.All(pair =>
                    Vector3.Distance(pair.Key.localPosition, pair.Value) < 0.0001f), Is.True);
                Find("OpenBag_btn").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(0.6f);
                var bag = Find("BagPanel").GetComponentsInChildren<MonoBehaviour>(true)
                    .First(component => component.GetType().Name == "EquipmentBagController");
                var getter = bag.GetType().GetMethod("GetEquipment");
                var slotType = getter.GetParameters()[0].ParameterType;
                foreach (string slot in new[] { "Radar", "RTK", "Camera", "Imu" })
                    Assert.That(getter.Invoke(bag, new[] { System.Enum.Parse(slotType, slot) }), Is.Not.Null, slot);
                output.GetComponent<Button>().onClick.Invoke();
                Assert.That(Find("ROS_Algorithm_Selection").activeInHierarchy, Is.True);
                Find("ROS_Algorithm_Selection").SetActive(false);
                Find("Back_btn").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(0.8f);
                Assert.That(output.activeInHierarchy, Is.False);
            }
            Find("Car0_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(1f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("scene1"));
            Assert.That(Find("Car0").activeInHierarchy, Is.True);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
