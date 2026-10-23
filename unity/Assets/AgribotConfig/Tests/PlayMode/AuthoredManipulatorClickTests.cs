using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Agribot.VehicleConfiguration.Tests
{
    public class AuthoredManipulatorClickTests
    {
        [UnityTest]
        public IEnumerator NewHandEquipmentInstallsMovesAndUninstallsOnBothCars()
        {
            foreach (string name in new[] { "Car0", "Car3" })
            {
                yield return SceneManager.LoadSceneAsync(name == "Car0" ? "scene2" : "scene1");
                yield return null;
                Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>().Where(t => t.gameObject.scene.IsValid()).ToArray();
                var car = transforms.First(t => t.name == name).gameObject;
                car.SetActive(true);
                var bag = transforms.First(t => t.name == "BagPanel").gameObject;
                bag.SetActive(true);
                yield return new WaitForSeconds(0.2f);
                var controller = bag.GetComponents<MonoBehaviour>().First(c => c.GetType().Name ==
                    (name == "Car0" ? "EquipmentBagController_copy" : "EquipmentBagController"));
                var getter = controller.GetType().GetMethod("GetEquipment");
                object equipment = getter.Invoke(controller, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, "Hand") });
                Assert.That(equipment, Is.Not.Null);
                Assert.That(controller.GetType().GetMethod("InstallEquipment").Invoke(controller, new[] { equipment }), Is.True);
                yield return new WaitForSeconds(1f);
                var hand = transforms.First(t => t.name == "Hand");
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
                Assert.That(controller.GetType().GetMethod("UninstallEquipment").Invoke(controller, new[] { equipment }), Is.True);
                yield return new WaitForSeconds(0.6f);
                Assert.That(hand.gameObject.activeInHierarchy, Is.False);
            }
        }
    }
}
