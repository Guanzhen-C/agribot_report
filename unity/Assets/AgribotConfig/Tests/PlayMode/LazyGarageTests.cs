using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class LazyGarageTests
    {
        [UnityTest]
        public IEnumerator GarageCardsDoNotConstructUnselectedFarmMeshes()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var vehicles = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Where(item => item.GetType().Name == "FarmGarageVehicle").ToArray();
            Assert.That(vehicles.Length, Is.EqualTo(8));
            foreach (var vehicle in vehicles)
            {
                Assert.That((bool)vehicle.GetType().GetProperty("IsLoaded").GetValue(vehicle), Is.False);
                Assert.That(vehicle.GetComponentsInChildren<MeshFilter>(true), Is.Empty);
            }
            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            buttons.Single(item => item.name == "Car4_Img").onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            var tractor = vehicles.Single(item => item.name == "Car4");
            Assert.That((bool)tractor.GetType().GetProperty("IsLoaded").GetValue(tractor), Is.True);
            Assert.That(tractor.GetComponentsInChildren<MeshFilter>(true).Length, Is.GreaterThan(10));
            foreach (var other in vehicles.Where(item => item != tractor))
                Assert.That((bool)other.GetType().GetProperty("IsLoaded").GetValue(other), Is.False);
            Debug.Log($"AGRIBOT_LAZY_GARAGE_TEST seconds={Time.realtimeSinceStartup:F3} loaded=1 total=8");
        }
    }
}
