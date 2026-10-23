using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class SceneBindingsTests
    {
        [Test]
        public void Car0SceneAndResourcesUseConsistentNames()
        {
            var garage = EditorSceneManager.OpenScene("Assets/Scenes/scene1.unity", OpenSceneMode.Single);
            var cards = garage.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true));
            Assert.That(cards.Count(item => item.name == "Car0_Img"), Is.EqualTo(1));

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/scene2.unity", OpenSceneMode.Single);
            var car = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(item => item.name == "Car0");
            Assert.That(car.GetComponents<MonoBehaviour>().Any(component =>
                component != null && component.GetType().Name == "CarChange"), Is.True);
            Assert.That(Resources.Load<GameObject>("models/Car0"), Is.Not.Null);
            Assert.That(Resources.Load<Sprite>("Sprites/Car0"), Is.Not.Null);
            Assert.That(Resources.Load<Sprite>("Sprites/Car0_HL"), Is.Not.Null);
            Assert.That(AckermannRuntimeConfiguration.BuildOrRefresh().VehicleId, Is.EqualTo("ackermann_current"));
        }

        [Test]
        public void AckermannConfigurationPanelsHaveValidScripts()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/scene2.unity", OpenSceneMode.Single);
            var transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (string name in new[] { "LastPanel", "BombPanel" })
            {
                var panel = transforms.Single(item => item.name == name).gameObject;
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(panel), Is.Zero, name);
                Assert.That(panel.GetComponents<MonoBehaviour>().Any(component =>
                    component != null && component.GetType().Name == name), Is.True, name);
            }
        }

    }
}
