using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class GarageThumbnailTests
    {
        [Test]
        public void RuntimePngBytesMatchTheOriginalVehicleAndImplementImages()
        {
            var sources = new Dictionary<string, string>();
            sources.Add("Equipment_RADAR", "Assets/equips/Radar.png");
            sources.Add("Equipment_RTK", "Assets/equips/mushroom.png");
            sources.Add("Equipment_CarCamera", "Assets/equips/camera.png");
            sources.Add("Equipment_ImuBox", "Assets/equips/IMUtu.png");
            foreach (var preset in FarmVehicleLibrary.Presets.Where(p => FarmVehicleLibrary.IsInstalled(p.id)))
            {
                if (!string.IsNullOrEmpty(preset.garageName)) continue;
                string id = preset.implementId == "none" ? preset.id : preset.implementId;
                sources[id] = preset.id.StartsWith("unity_car", StringComparison.Ordinal)
                    ? "Assets/Resources/Sprites/" + preset.garageName + ".png"
                    : "Assets/AgribotConfig/Resources/LocalFarmAssets/" + id + "_icon.png";
            }
            foreach (var entry in sources)
            {
                var asset = Resources.Load<TextAsset>(GarageThumbnailLibrary.ResourceFolder + "/" + entry.Key);
                Assert.That(asset, Is.Not.Null, entry.Key);
                Assert.That(asset.bytes, Is.EqualTo(File.ReadAllBytes(entry.Value)), entry.Key);
            }
        }

        [Test]
        public void AllVehicleIconsAreBoundToTheCurrentDetailModels()
        {
            for (int i = 0; i < 10; i++)
            {
                string id = "Car" + i;
                Assert.That(File.ReadAllText("Assets/AgribotConfig/Resources/LocalGarageThumbnails/" + id + ".bytes.source.txt").Trim(),
                    Is.EqualTo(global::Agribot.VehicleConfiguration.Editor.GarageThumbnailBuilder.CurrentImageSource(id)), id);
                var image = GarageThumbnailLibrary.LoadTexture(id);
                Assert.That(image.width, Is.EqualTo(1024));
                Assert.That(image.height, Is.EqualTo(640));
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        [TestCase("../Car0")]
        [TestCase("Car0/icon")]
        [TestCase("")]
        public void InvalidThumbnailPathsAreRejected(string id) =>
            Assert.Throws<ArgumentException>(() => GarageThumbnailLibrary.LoadTexture(id));

        [Test]
        public void MissingThumbnailIsReported() =>
            Assert.Throws<FileNotFoundException>(() => GarageThumbnailLibrary.LoadTexture("missing_thumbnail"));
    }
}
