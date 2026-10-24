using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class CadPreviewSurfaceTests
    {
        [TestCase("compact_tractor")]
        [TestCase("tractor_chisel")]
        [TestCase("tractor_harrow")]
        [TestCase("tractor_garlic")]
        [TestCase("tractor_mulch")]
        [TestCase("tractor_groove")]
        [TestCase("tractor_cultivator")]
        public void Car6FamilyKeepsOriginalCadDetailSeparateFromSimulationAssets(string id)
        {
            string directory = FarmVehicleLibrary.AssetDirectory(id);
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(directory, "asset_manifest.json")));
            var detail = manifest["previewGeometry"];
            Assert.That(detail, Is.Not.Null, "The original CAD preview must not revert to simulation LOD.");
            Assert.That((string)detail["method"], Is.EqualTo("original_cad_no_decimation"));
            Assert.That((bool)detail["decimationApplied"], Is.False);
            Assert.That((int)detail["displaySourceFaces"], Is.EqualTo((int)detail["sourceFaces"]));
            Assert.That((int)detail["sourceFaces"], Is.GreaterThan(250344));
            Assert.That((double)detail["linearDeflectionMm"], Is.LessThanOrEqualTo(.25));
            Assert.That((string)detail["canonicalConfigSha256"], Is.EqualTo(
                FarmVehicleLibrary.Digest(File.ReadAllBytes(Path.Combine(directory, "vehicle_config.json")))));
            foreach (var source in manifest["files"])
                Assert.That(FarmVehicleLibrary.Digest(File.ReadAllBytes(Path.Combine(directory, (string)source["name"]))),
                    Is.EqualTo((string)source["sha256"]), "Simulation source must stay unchanged: " + source["name"]);
        }

        [TestCase("compact_tractor")]
        [TestCase("tractor_chisel")]
        public void CompactTractorRendersCadSheetsFromBothSidesWithoutChangingTheMesh(string id)
        {
            var manifest = FarmVehicleLibrary.Manifest(id);
            var preview = FarmVehicleLibrary.CreatePreview(id);
            try
            {
                var parts = preview.GetComponentsInChildren<MeshFilter>();
                Assert.That(parts.Length, Is.EqualTo(manifest.meshGroups));
                Assert.That(parts.Sum(part => part.sharedMesh.triangles.Length / 3), Is.EqualTo(manifest.triangles));
                foreach (var renderer in preview.GetComponentsInChildren<MeshRenderer>())
                {
                    var material = renderer.sharedMaterial;
                    Assert.That(material.GetFloat("_Cull"), Is.EqualTo((float)CullMode.Off));
                    Assert.That(material.doubleSidedGI, Is.True);
                    if (GraphicsSettings.currentRenderPipeline == null)
                        Assert.That(material.shader.name, Is.EqualTo("Agribot/CAD Preview Two Sided"));
                }
            }
            finally { Object.DestroyImmediate(preview); }
        }

        [Test]
        public void OtherTractorsKeepTheirExistingMaterial()
        {
            var preview = FarmVehicleLibrary.CreatePreview("cab_tractor");
            try
            {
                Assert.That(preview.GetComponentsInChildren<MeshRenderer>()
                    .All(renderer => renderer.sharedMaterial.shader.name != "Agribot/CAD Preview Two Sided"), Is.True);
            }
            finally { Object.DestroyImmediate(preview); }
        }

        [Test]
        public void CadSheetIsVisibleToCamerasOnEitherSide()
        {
            var plane = new GameObject("CadSheet", typeof(MeshFilter), typeof(MeshRenderer));
            plane.layer = 30;
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(0, .5f, 0) },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateNormals();
            plane.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Resources.Load<Shader>("Shaders/CadPreviewTwoSided")) { color = Color.white };
            plane.GetComponent<MeshRenderer>().sharedMaterial = material;
            var cameraObject = new GameObject("CadSheetCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = .75f;
            camera.aspect = 1;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 10;
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            var target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (float side in new[] { -1f, 1f })
                {
                    camera.transform.position = new Vector3(0, 0, side * 2);
                    camera.transform.LookAt(Vector3.zero);
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                    pixels.Apply();
                    Assert.That(pixels.GetPixels32().Count(pixel => pixel.a > 250), Is.GreaterThan(3000),
                        "CAD sheet disappeared when viewed from side " + side);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(plane);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }
    }
}
