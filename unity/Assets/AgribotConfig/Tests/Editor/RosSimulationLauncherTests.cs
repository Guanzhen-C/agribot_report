using System.Collections.Generic;
using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using Agribot.VehicleConfiguration;
using NUnit.Framework;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class RosSimulationLauncherTests
    {
        [Test]
        public void StopInvalidatesAlreadyDispatchedLaunchesButNewLaunchesUseTheNewFence()
        {
            string previous = FarmScenarioCatalog.SelectedId;
            string previousScript = Environment.GetEnvironmentVariable("AGRIBOT_ROS_SYNC_SCRIPT");
            string script = Path.Combine(Path.GetTempPath(), "agribot-fence-test-"+Guid.NewGuid().ToString("N")+".sh");
            File.WriteAllText(script, "#!/bin/bash\nexit 0\n");
            FarmScenarioCatalog.SelectedId = "orchard";
            Environment.SetEnvironmentVariable("AGRIBOT_ROS_SYNC_SCRIPT", script);
            try
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Static;
                var build = typeof(RosSimulationLauncher).GetMethod("BuildStartInfo", flags);
                var first = (ProcessStartInfo)build.Invoke(null, new object[] {script, "/tmp/export",
                    RosSimulationLauncher.FindAlgorithm("navsat_mppi")});
                string path = (string)typeof(RosSimulationLauncher).GetField("LaunchFencePath", flags).GetValue(null);
                string oldToken = File.ReadAllText(path);
                Assert.That(first.Arguments, Does.Contain(" --launch-fence-file "));
                Assert.That(first.Arguments, Does.Contain(oldToken));
                RosSimulationLauncher.RunManagedCommand(false);
                string current = File.ReadAllText(path);
                Assert.That(current, Is.Not.EqualTo(oldToken));
                var next = (ProcessStartInfo)build.Invoke(null, new object[] {script, "/tmp/export",
                    RosSimulationLauncher.FindAlgorithm("navsat_mppi")});
                Assert.That(next.Arguments, Does.Contain(current));
            }
            finally
            {
                Environment.SetEnvironmentVariable("AGRIBOT_ROS_SYNC_SCRIPT", previousScript);
                if (string.IsNullOrEmpty(previous)) FarmScenarioCatalog.ResetSelection();
                else FarmScenarioCatalog.SelectedId = previous;
                File.Delete(script);
            }
        }

        [Test]
        public void GameLaunchWaitsForUserSelectedNavigationPoses()
        {
            string previous = FarmScenarioCatalog.SelectedId;
            FarmScenarioCatalog.SelectedId = "orchard";
            try
            {
                MethodInfo buildStartInfo = typeof(RosSimulationLauncher).GetMethod(
                    "BuildStartInfo", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(buildStartInfo, Is.Not.Null);
                ProcessStartInfo startInfo = (ProcessStartInfo)buildStartInfo.Invoke(
                    null, new object[]
                    {
                        "/tmp/agribot-simulation.sh", "/tmp/vehicle-export",
                        RosSimulationLauncher.FindAlgorithm("fastlivo_rtk_mppi")
                    });

                Assert.That(startInfo.Arguments, Does.Contain(" --run-waypoints false"));
                Assert.That(startInfo.Arguments, Does.Not.Contain(" --run-waypoints true"));
            }
            finally
            {
                if (string.IsNullOrEmpty(previous)) FarmScenarioCatalog.ResetSelection();
                else FarmScenarioCatalog.SelectedId = previous;
            }
        }

        [Test]
        public void MissingSceneRejectsLaunchInsteadOfUsingLastSessionOrDefault()
        {
            string previous = FarmScenarioCatalog.SelectedId;
            FarmScenarioCatalog.ResetSelection();
            try
            {
                var method = typeof(RosSimulationLauncher).GetMethod("BuildStartInfo", BindingFlags.NonPublic | BindingFlags.Static);
                var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] {
                    "/tmp/agribot-simulation.sh", "/tmp/vehicle-export",
                    RosSimulationLauncher.FindAlgorithm(RosSimulationLauncher.DefaultAlgorithmId) }));
                Assert.That(error.InnerException, Is.TypeOf<System.InvalidOperationException>());
                Assert.That(error.InnerException.Message, Does.Contain("选择作业场景"));
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous)) FarmScenarioCatalog.SelectedId = previous;
            }
        }

        [Test]
        public void GeneratesEveryIndependentComponentCombination()
        {
            Assert.That(RosSimulationLauncher.Perceptions.Count, Is.EqualTo(2));
            Assert.That(RosSimulationLauncher.VisualPerceptions.Count, Is.EqualTo(4));
            Assert.That(RosSimulationLauncher.Localizations.Count, Is.EqualTo(4));
            Assert.That(RosSimulationLauncher.Planners.Count, Is.EqualTo(5));
            Assert.That(RosSimulationLauncher.Controllers.Count, Is.EqualTo(3));
            Assert.That(RosSimulationLauncher.CombinationCount, Is.EqualTo(480));

            HashSet<string> ids = new HashSet<string>();
            foreach (RosSimulationAlgorithmProfile profile in
                     RosSimulationLauncher.Algorithms)
            {
                Assert.That(ids.Add(profile.Id), Is.True, profile.Id);
                Assert.That(
                    RosSimulationLauncher.FindAlgorithm(profile.Id).Id,
                    Is.EqualTo(profile.Id));
            }
        }

        [Test]
        public void ComposesAnArbitraryStackFromFiveIndependentSelections()
        {
            RosSimulationAlgorithmProfile profile = RosSimulationLauncher.Compose(
                "voxel", "instance_segmentation", "kiss_icp", "theta_star", "dwb");

            Assert.That(
                profile.Id,
                Is.EqualTo(
                    "voxel_instance_segmentation_kiss_icp_theta_star_dwb"));
            Assert.That(profile.Perception, Does.Contain("体素"));
            Assert.That(profile.VisualPerception, Does.Contain("实例分割"));
            Assert.That(profile.Localization, Is.EqualTo("KISS-ICP"));
            Assert.That(profile.Planner, Is.EqualTo("Theta*"));
            Assert.That(profile.Controller, Is.EqualTo("DWB"));
            Assert.That(profile.RouteMode, Is.EqualTo("连续全局路径"));
        }

        [Test]
        public void PreservesLegacyAlgorithmIdentifiers()
        {
            Assert.That(
                RosSimulationLauncher.FindAlgorithm("fastlivo_rtk_mppi").Id,
                Is.EqualTo("stvl_off_fastlivo_rtk_smac_hybrid_mppi"));
            Assert.That(
                RosSimulationLauncher.FindAlgorithm("fastlivo_rtk_direct_mppi").Id,
                Is.EqualTo("stvl_off_fastlivo_rtk_direct_mppi"));
            Assert.That(
                RosSimulationLauncher.FindAlgorithm(
                    "voxel_kiss_icp_theta_star_dwb").Id,
                Is.EqualTo("voxel_off_kiss_icp_theta_star_dwb"));
        }
    }
}
