using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public class AuthoredManipulatorClickTests
    {
        [UnityTest]
        public IEnumerator NewHandEquipmentInstallsMovesAndUninstallsOnBothCars()
        {
            foreach (string name in new[] { "Car0", "Car3" })
            {
                yield return SceneManager.LoadSceneAsync("scene1");
                yield return null;
                Click(Find(name + "_Img"));
                yield return new WaitForSeconds(.8f);
                var car = Find(name);
                Click(Find("OpenBag_btn"));
                yield return new WaitForSeconds(.8f);
                var bag = Find("BagPanel");
                var controller = bag.GetComponents<MonoBehaviour>().First(c => c.GetType().Name == "EquipmentBagController");
                var getter = controller.GetType().GetMethod("GetEquipment");
                object equipment = getter.Invoke(controller, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, "Hand") });
                Assert.That(equipment, Is.Not.Null);
                Click(Find("Hand_slot"));
                yield return null;
                var details = bag.GetComponentsInChildren<MonoBehaviour>(true).First(c => c.GetType().Name == "EquipmentDetailsUI");
                Click(((Button)details.GetType().GetProperty("InstallButton").GetValue(details)).gameObject);
                Assert.That(controller.GetType().GetMethod("IsInstalled").Invoke(controller, new[] { equipment }), Is.True);
                yield return new WaitForSeconds(1f);
                var hand = car.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hand");
                var preview = hand.Find("ROS_manipulator_preview");
                Assert.That(hand.gameObject.activeInHierarchy, Is.True);
                Assert.That(preview, Is.Not.Null);
                var wrist = preview.GetComponentsInChildren<Transform>().First(t => t.name == "manipulator_link6");
                var finger = preview.GetComponentsInChildren<Transform>().First(t => t.name == "manipulator_finger1");
                Quaternion rotation = wrist.localRotation;
                Vector3 position = finger.localPosition;
                yield return new WaitForSeconds(3f);
                Assert.That(Quaternion.Angle(rotation, wrist.localRotation), Is.GreaterThan(5f), name);
                Assert.That(Vector3.Distance(position, finger.localPosition), Is.GreaterThan(0.001f), name);
                Click(((Button)details.GetType().GetProperty("UninstallButton").GetValue(details)).gameObject);
                Assert.That(controller.GetType().GetMethod("IsInstalled").Invoke(controller, new[] { equipment }), Is.False);
                yield return new WaitForSeconds(0.6f);
                Assert.That(hand.gameObject.activeInHierarchy, Is.False);
            }
        }
        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(t => t.gameObject.scene == SceneManager.GetActiveScene() && t.name == name).gameObject;

        private static void Click(GameObject target)
        {
            Assert.That(target.activeInHierarchy, Is.True, target.name);
            var button = target.GetComponent<Button>();
            if (button != null) Assert.That(button.IsInteractable(), Is.True, target.name);
            ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }
    }
}
