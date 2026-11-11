using System.Linq;
using NUnit.Framework;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class HilCatalogTests
    {
        [Test] public void OnlyReviewedVehiclesAndScenesCanStartHil()
        {
            Assert.That(HilSimulationLauncher.Profiles.Length, Is.EqualTo(5));
            Assert.That(HilSimulationLauncher.Match("Car9", "combine_header", "orchard"), Is.Null);
            Assert.That(HilSimulationLauncher.Match("Car3", "hil_sprayer", "vineyard"), Is.Null);
            Assert.That(HilSimulationLauncher.Match("Car0", "none", "orchard"), Is.Null);
            Assert.That(HilSimulationLauncher.Match("Car0", "lite6", "orchard").id, Is.EqualTo("ackermann_apple"));
            Assert.That(HilSimulationLauncher.Match("Car3", "hil_sprayer", "orchard").id, Is.EqualTo("tracked_sprayer"));
        }

        [Test] public void TractorModesShareOneFirmwareAndKeepTheVerifiedRoute()
        {
            var cvt = HilSimulationLauncher.Match("Car4", "mounted_disc_harrow", "wide_headland");
            var powershift = HilSimulationLauncher.Match("Car4", "mounted_disc_harrow", "wide_headland", "powershift_auto");
            Assert.That(cvt.transmission, Is.EqualTo("cvt_auto"));
            Assert.That(powershift.transmission, Is.EqualTo("powershift_auto"));
            Assert.That(cvt.firmware, Is.EqualTo(powershift.firmware));
            Assert.That(cvt.runtime, Is.EqualTo(powershift.runtime));
            Assert.That(HilSimulationLauncher.Profiles.All(profile => profile.domain == 73 || profile.domain == 74), Is.True);
        }

        [Test] public void TerminalStatesReleaseTheUiButUnknownStateDoesNot()
        {
            foreach (var phase in new[] { "stopped", "completed", "failed" })
                Assert.That(new HilRunStatus { phase = phase }.Active, Is.False);
            foreach (var phase in new[] { "checking", "flashing", "waiting_ready", "running", "unknown" })
                Assert.That(new HilRunStatus { phase = phase }.Active, Is.True);
        }
    }
}
