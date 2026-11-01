using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class FuxiFarmProfileTests
    {
        [Serializable] private sealed class Catalog { public string schemaVersion = "1.0"; public FarmScenario[] scenarios; }
        private string directory, previousRoot;

        [SetUp] public void Setup()
        {
            previousRoot = Environment.GetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT");
            directory = Path.Combine(Path.GetTempPath(), "agribot_fuxi_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            foreach (string file in new[] { "scene.json", "profile.json" })
                File.WriteAllText(Path.Combine(directory, file), "{}");
            File.WriteAllText(Path.Combine(directory, "vehicle_config.json"), "{\"vehicleId\":\"unity_car3\"}");
            var scene = new FarmScenario { id = "scan_fuxi_test", name = "伏羲农场", imported = true,
                spawn = new[] { 0f, 0f, 0f }, sceneManifest = Path.Combine(directory, "scene.json"),
                simulationProfile = Path.Combine(directory, "profile.json"), fixedVehicleId = "unity_car3",
                fixedAlgorithmId = "stvl_off_navsat_smac_hybrid_rpp" };
            File.WriteAllText(Path.Combine(directory, "catalog.json"), JsonUtility.ToJson(new Catalog { scenarios = new[] { scene } }));
            Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", directory);
            FarmScenarioCatalog.ResetSelection();
            FarmScenarioCatalog.SelectedId = scene.id;
        }

        [TearDown] public void Cleanup()
        {
            Environment.SetEnvironmentVariable("AGRIBOT_SCANNED_SCENE_ROOT", previousRoot);
            FarmScenarioCatalog.ResetSelection();
            Directory.Delete(directory, true);
        }

        private ProcessStartInfo Build(string algorithm = "navsat_rpp")
        {
            return (ProcessStartInfo)typeof(RosSimulationLauncher).GetMethod("BuildStartInfo", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { "/tmp/simulation.sh", directory, RosSimulationLauncher.FindAlgorithm(algorithm) });
        }

        [Test] public void SelectedFarmStartsFixedRouteWithoutManualGoals()
        {
            Assert.That(FarmScenarioCatalog.Selected.name, Is.EqualTo("伏羲农场"));
            Assert.That(FarmScenarioCatalog.Selected.HasFixedRoute, Is.True);
            string arguments = Build().Arguments;
            Assert.That(arguments, Does.Contain(" --simulation-profile "));
            Assert.That(arguments, Does.Contain(" --run-waypoints true"));
            Assert.That(arguments, Does.Contain(" --controller \"rpp\""));
            Assert.That(arguments, Does.Contain(" --localization \"navsat\""));
        }

        [Test] public void OtherControllerDoesNotMasqueradeAsTheAcceptedProfile()
        {
            string arguments = Build("navsat_mppi").Arguments;
            Assert.That(arguments, Does.Contain(" --simulation-profile "));
            Assert.That(arguments, Does.Contain(" --run-waypoints true"));
            Assert.That(arguments, Does.Contain(" --controller \"mppi\""));
        }

        [Test] public void OtherVehicleIsRejectedInsteadOfBeingSilentlyReplaced()
        {
            File.WriteAllText(Path.Combine(directory, "vehicle_config.json"), "{\"vehicleId\":\"unity_car0\"}");
            var error = Assert.Throws<TargetInvocationException>(() => Build());
            Assert.That(error.InnerException.Message, Does.Contain("Car3"));
        }

        [Test] public void MissingProfileCannotFallBackToManualNavigation()
        {
            File.Delete(Path.Combine(directory, "profile.json"));
            var error = Assert.Throws<TargetInvocationException>(() => Build());
            Assert.That(error.InnerException, Is.TypeOf<FileNotFoundException>());
        }
    }
}
