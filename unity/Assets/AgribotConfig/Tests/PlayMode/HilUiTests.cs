using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class HilUiTests
    {
        private static GameObject Find(string name) => UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).First(item => item.name == name).gameObject;

        [UnityTest]
        public IEnumerator TrackedToolsUseOriginalPosesAndSwitchExclusively()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Find("Car3_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(1f);
            var car = Find("Car3");
            Vector3 scale = car.transform.localScale;
            Find("OpenBag_btn").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            var bag = Find("BagPanel").GetComponentsInChildren<MonoBehaviour>(true)
                .First(item => item.GetType().Name == "EquipmentBagController");
            var getter = bag.GetType().GetMethod("GetEquipment");
            var install = bag.GetType().GetMethod("SetInstallationState");
            foreach (var item in new[] { ("HilSprayer", "hil_sprayer"), ("HilShredder", "hil_shredder") })
            {
                var data = getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, item.Item1) });
                Assert.That(data, Is.Not.Null);
                Assert.That((bool)install.Invoke(bag, new[] { data, (object)true }), Is.True);
                yield return null;
                var mount = car.transform.Find("ROS_base_link/"+item.Item1);
                Assert.That(mount, Is.Not.Null);
                Assert.That(mount.gameObject.activeSelf, Is.True);
                Assert.That(mount.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(mount.GetComponentsInChildren<MeshFilter>().Length, Is.GreaterThan(0));
                Assert.That(AssistantConversation.State.implement, Is.EqualTo(item.Item2));
                Assert.That(car.transform.localScale, Is.EqualTo(scale));
            }
            Assert.That(car.transform.Find("ROS_base_link/HilSprayer").gameObject.activeSelf, Is.False);
            Find("BagPanel").transform.Find("Header/CloseButton").GetComponent<Button>().onClick.Invoke();
            Find("Start_Hil_Simulation").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var normal = Find("Launch").GetComponent<RectTransform>();
            var hil = Find("LaunchHil").GetComponent<RectTransform>();
            Assert.That(normal.anchorMax.x, Is.LessThan(hil.anchorMin.x));
            Assert.That(hil.GetComponent<Button>().interactable, Is.True);
            Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo("orchard"));
        }
    }
}
