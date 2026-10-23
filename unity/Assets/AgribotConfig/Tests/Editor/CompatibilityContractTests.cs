using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class CompatibilityContractTests
    {
        [Serializable] private sealed class Binding
        {
            public string car, preset, implement, unityType;
        }
        [Serializable] private sealed class Components
        {
            public string[] perception, vision, localization, planner, controller;
        }
        [Serializable] private sealed class Catalog
        {
            public int schemaVersion;
            public Binding[] bindings;
            public Components components;
        }

        private static Catalog Load()
        {
            string workspace = Environment.GetEnvironmentVariable("AGRIBOT_ROS_WORKSPACE") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "agribot_ws/ros2_ws");
            string path = Path.Combine(workspace, "src/agribot_farm_sim/config/compatibility.json");
            Assert.That(File.Exists(path), Is.True, "Missing shared ROS/Unity compatibility contract: " + path);
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(path));
            Assert.That(catalog.schemaVersion, Is.EqualTo(1));
            return catalog;
        }

        [Test]
        public void EveryCarAndImplementButtonMatchesTheCompatibilityContract()
        {
            var catalog = Load();
            var sceneAssembly = Assembly.Load("Assembly-CSharp");
            var equipment = sceneAssembly.GetType("AgriculturalMachinery.BagSystem.EquipmentType", true);
            var ui = sceneAssembly.GetType("AgriculturalMachinery.BagSystem.FarmEquipmentUi", true);
            var mounted = ui.GetMethod("IsMountedImplement", BindingFlags.Public | BindingFlags.Static);
            var unavailable = ui.GetMethod("UnavailableReason", BindingFlags.Public | BindingFlags.Static);
            var types = Enum.GetValues(equipment).Cast<object>()
                .Where(t => t.ToString() == "Hand" || (bool)mounted.Invoke(null, new[] { t })).ToArray();
            Assert.That(types.Length, Is.EqualTo(12));
            for (int index = 0; index < 10; index++)
            {
                string car = "Car" + index;
                foreach (var type in types)
                {
                    bool declared = catalog.bindings.Any(b => b.car == car && b.unityType == type.ToString());
                    Assert.That(unavailable.Invoke(null, new object[] { type, car }) == null,
                        Is.EqualTo(declared), car + "/" + type);
                }
            }
        }

        [Test]
        public void EveryFarmPresetMatchesTheMountingContract()
        {
            var catalog = Load();
            foreach (var preset in FarmVehicleLibrary.Presets)
                Assert.That(catalog.bindings.Count(b => b.preset == preset.id && b.implement == preset.implementId),
                    Is.EqualTo(1), preset.id);
        }

        [Test]
        public void AllAlgorithmOptionsMatchTheRosCompatibilityContract()
        {
            var parts = Load().components;
            Assert.That(RosSimulationLauncher.Perceptions.Select(p => p.Id), Is.EqualTo(parts.perception));
            Assert.That(RosSimulationLauncher.VisualPerceptions.Select(p => p.Id), Is.EqualTo(parts.vision));
            Assert.That(RosSimulationLauncher.Localizations.Select(p => p.Id), Is.EqualTo(parts.localization));
            Assert.That(RosSimulationLauncher.Planners.Select(p => p.Id), Is.EqualTo(parts.planner));
            Assert.That(RosSimulationLauncher.Controllers.Select(p => p.Id), Is.EqualTo(parts.controller));
            Assert.That(RosSimulationLauncher.CombinationCount, Is.EqualTo(480));
        }
    }
}
