using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class HilSensorAssetTests
    {
        [Test]
        public void AckermannAntennasUseTheUpperDeckAheadOfCargoInBothVisualsAndExport()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/scene2.unity", OpenSceneMode.Single);
            var authoring = AckermannRuntimeConfiguration.BuildOrRefresh();
            var deck = authoring.VehicleVisualRoot.GetComponentsInChildren<MeshFilter>(true)
                .Single(filter => filter.name.StartsWith("车上板"));
            var points = deck.sharedMesh.vertices.Select(vertex => RosFluConverter.UnityToRos(
                authoring.BaseLink.InverseTransformPoint(deck.transform.TransformPoint(vertex)))).ToArray();
            float rear = points.Min(point => point.x), front = points.Max(point => point.x);
            float left = points.Max(point => point.y), right = points.Min(point => point.y);
            float top = points.Max(point => point.z);
            Debug.Log($"ACKERMANN_UPPER_DECK rear={rear:R} left={left:R} right={right:R} top={top:R}");
            SensorMountLibrary.ApplyAckermann(authoring.transform, authoring.BaseLink);
            authoring = AckermannRuntimeConfiguration.BuildOrRefresh();
            var configuration = authoring.BuildConfiguration();
            foreach (var mount in SensorMountLibrary.AckermannRtk)
            {
                var sensor = configuration.sensors.Single(item => item.id == mount.id);
                Assert.That(sensor.pose.xyz[0], Is.EqualTo(.12).Within(.00002));
                Assert.That(sensor.pose.xyz[0], Is.InRange(rear+.025, front-.025));
                Assert.That(sensor.pose.xyz[1], Is.EqualTo(mount.id == "rtk_master" ? left-.025 : right+.025).Within(.00002));
                Assert.That(sensor.pose.xyz[2]-.1, Is.EqualTo(top).Within(.00005), "Short stems must rest on the upper deck");
                var antenna = authoring.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == (mount.id == "rtk_master" ? "Mushroom1" : "Mushroom2"));
                var visual = RosFluConverter.RelativePose(authoring.BaseLink, antenna);
                for (int axis = 0; axis < 3; axis++)
                    Assert.That(visual.xyz[axis], Is.EqualTo(sensor.pose.xyz[axis]).Within(.00001));
            }
        }

        [Test]
        public void ShortAntennaContainsOnlyTheCapAndItsSmallStem()
        {
            var manifest = JObject.Parse(Resources.Load<TextAsset>("HilSensors/rtk_antenna/manifest").text);
            double height = (double)manifest["maximumRos"][2]-(double)manifest["minimumRos"][2];
            Assert.That(height, Is.EqualTo(.1228).Within(.0001));
            Assert.That((int)manifest["triangles"], Is.EqualTo(3208));
        }

        [TestCaseSource(typeof(FarmVehicleLibraryTests), nameof(FarmVehicleLibraryTests.AllPresetIds))]
        public void EveryFarmPresetUsesShortStemsInBothThePreviewAndCanonicalExport(string id)
        {
            string directory = FarmVehicleLibrary.AssetDirectory(id);
            var source = JObject.Parse(File.ReadAllText(Path.Combine(directory, "vehicle_config.json")));
            var marker = source["metadata"]["simulationRtkMount"];
            Assert.That((string)marker["style"], Is.EqualTo("hil_short_stems_no_rack"));
            foreach (var sensor in source["sensors"].Where(sensor => ((string)sensor["id"]).StartsWith("rtk_")))
            {
                string name = (string)sensor["id"];
                var before = marker["originalAntennaPoses"][name];
                Assert.That((double)sensor["pose"]["xyz"][0], Is.EqualTo((double)before[0]));
                Assert.That((double)sensor["pose"]["xyz"][1], Is.EqualTo((double)before[1]));
                Assert.That((double)sensor["pose"]["xyz"][2], Is.EqualTo((double)before[2]-.4202005).Within(.00001));
            }
            var preset = FarmVehicleLibrary.Presets.Single(item => item.id == id);
            string trackMarker = "Assets/AgribotConfig/Resources/LocalFarmAssets/"+id+"_icon.png.source.txt";
            if (File.Exists(trackMarker))
                Assert.That(File.ReadAllText(trackMarker).Trim(), Is.EqualTo(FarmVehicleLibrary.Digest(
                    Resources.Load<TextAsset>("LocalFarmAssets/"+id+"/preview").bytes)));
            if (!string.IsNullOrEmpty(preset.garageName))
            {
                string thumbnails = "Assets/AgribotConfig/Resources/LocalGarageThumbnails/";
                Assert.That(File.ReadAllBytes(thumbnails+id+".bytes"),
                    Is.EqualTo(File.ReadAllBytes(thumbnails+preset.garageName+".bytes")),
                    "RTK-only asset updates must not replace the current body thumbnail with a legacy image");
            }
        }

        [TestCase("hil_sprayer")]
        [TestCase("hil_shredder")]
        public void NewToolThumbnailsUseTheSameTransparentBackgroundAsOtherImplements(string id)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(Resources.Load<TextAsset>("LocalGarageThumbnails/"+id).bytes), Is.True);
                Assert.That(texture.width, Is.EqualTo(640));
                Assert.That(texture.height, Is.EqualTo(440));
                Assert.That(texture.GetPixel(0, 0).a, Is.Zero);
                Assert.That(texture.GetPixel(texture.width-1, texture.height-1).a, Is.Zero);
                Assert.That(texture.GetPixels32().Any(pixel => pixel.a == 255), Is.True);
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
