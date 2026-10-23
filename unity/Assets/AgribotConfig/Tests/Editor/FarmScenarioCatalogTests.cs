using System;
using System.Linq;
using NUnit.Framework;

namespace Agribot.VehicleConfiguration.Tests
{
    public class FarmScenarioCatalogTests
    {
        [Test]
        public void CatalogHasDistinctSelectableScenesAndRejectsUnknownIds()
        {
            string original = FarmScenarioCatalog.SelectedId;
            try
            {
                Assert.That(FarmScenarioCatalog.Scenarios.Where(item => !item.imported).Select(item => item.id),
                    Is.EquivalentTo(new[] { "orchard", "arable_field", "greenhouse", "farmyard", "tomato_greenhouse", "strawberry_field", "vineyard", "vegetable_field", "wide_headland" }));
                foreach (var scene in FarmScenarioCatalog.Scenarios)
                {
                    FarmScenarioCatalog.SelectedId = scene.id;
                    Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo(scene.id));
                    Assert.That(scene.tasks, Is.Not.Empty);
                    Assert.That(scene.spawn, Has.Length.EqualTo(3));
                }
                Assert.Throws<ArgumentException>(() => FarmScenarioCatalog.SelectedId = "unknown");
            }
            finally
            {
                if (string.IsNullOrEmpty(original)) FarmScenarioCatalog.ResetSelection();
                else FarmScenarioCatalog.SelectedId = original;
            }
        }

        [Test]
        public void NewAppIgnoresOldScenarioPreferenceAndDoesNotSaveSelection()
        {
            const string preference = "Agribot.RosSimulation.Scenario";
            bool hadPreference = UnityEngine.PlayerPrefs.HasKey(preference);
            string previous = UnityEngine.PlayerPrefs.GetString(preference, "");
            string selected = FarmScenarioCatalog.SelectedId;
            try
            {
                UnityEngine.PlayerPrefs.SetString(preference, "wide_headland");
                FarmScenarioCatalog.ResetSelection();
                Assert.That(FarmScenarioCatalog.HasSelection, Is.False);
                Assert.That(FarmScenarioCatalog.SelectedId, Is.Empty);
                FarmScenarioCatalog.SelectedId = "vineyard";
                Assert.That(FarmScenarioCatalog.HasSelection, Is.True);
                Assert.That(UnityEngine.PlayerPrefs.GetString(preference), Is.EqualTo("wide_headland"));
                FarmScenarioCatalog.ResetSelection();
                Assert.That(FarmScenarioCatalog.SelectedId, Is.Empty);
            }
            finally
            {
                if (hadPreference) UnityEngine.PlayerPrefs.SetString(preference, previous);
                else UnityEngine.PlayerPrefs.DeleteKey(preference);
                if (string.IsNullOrEmpty(selected)) FarmScenarioCatalog.ResetSelection();
                else FarmScenarioCatalog.SelectedId = selected;
            }
        }
    }
}
