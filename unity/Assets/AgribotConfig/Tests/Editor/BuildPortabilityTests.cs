using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class BuildPortabilityTests
    {
        [Test]
        public void BuildPreparationKeepsTheVersionedCar0PrefabUnchanged()
        {
            string path = Editor.Car0GarageIntegration.PrefabPath;
            byte[] before = File.ReadAllBytes(path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Editor.Car0GarageIntegration.Prepare();
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null);
        }

        [Test]
        public void ChineseFontLoadsWithoutSceneOrOperatingSystemFontDependencies()
        {
            var font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            Assert.That(font, Is.Not.Null);
            Assert.That(font.HasCharacter('中') && font.HasCharacter('农'), Is.True);
            var selected = (Font)Type.GetType("ChineseUiFont, Assembly-CSharp", true)
                .GetProperty("LegacyFont").GetValue(null);
            Assert.That(selected, Is.SameAs(font));
            Assert.That(File.ReadAllBytes("Assets/AgribotConfig/Resources/Fonts/NotoSansCJKsc-Regular.otf"),
                Is.EqualTo(File.ReadAllBytes("Assets/Fonts/NotoSansCJKsc-Regular.otf")));
        }
    }
}
