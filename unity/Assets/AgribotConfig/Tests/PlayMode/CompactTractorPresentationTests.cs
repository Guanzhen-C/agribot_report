using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class CompactTractorPresentationTests
    {
        [UnityTest]
        public IEnumerator Car6UsesCompleteTwoSidedPreviewThroughoutCameraOrbitAndReturn()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return new WaitForSeconds(.5f);
            var overviewPosition = Camera.main.transform.position;
            var overviewRotation = Camera.main.transform.rotation;
            Click(Find("Car6_Img"));
            yield return new WaitForSeconds(1.2f);
            var car = Find("Car6");
            Assert.That(car.activeInHierarchy, Is.True);
            var body = car.transform.Find("car6");
            Assert.That(body, Is.Not.Null);
            var meshes = body.GetComponentsInChildren<MeshFilter>();
            Assert.That(meshes.Length, Is.GreaterThan(0));
            foreach (var renderer in body.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Agribot/CAD Preview Two Sided"));
                Assert.That(renderer.sharedMaterial.GetFloat("_Cull"), Is.Zero);
            }
            var orbit = car.GetComponentsInChildren<Component>(true)
                .Single(component => component != null && component.GetType().Name == "cameraControl");
            string diagnostics = Environment.GetEnvironmentVariable("AGRIBOT_CAR6_DIAGNOSTICS");
            foreach (float angle in new[] { -45f, 45f, 135f, -135f })
            {
                orbit.GetType().GetMethod("FixCamera").Invoke(orbit, new object[] { 5.2f, angle, 20f });
                yield return new WaitForSeconds(.7f);
                Assert.That(body.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(meshes.Length));
                var minimum = Vector3.positiveInfinity;
                var maximum = Vector3.negativeInfinity;
                foreach (var filter in meshes)
                    foreach (var vertex in filter.sharedMesh.vertices)
                    {
                        var point = Camera.main.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                        minimum = Vector3.Min(minimum, point);
                        maximum = Vector3.Max(maximum, point);
                    }
                Assert.That(minimum.z, Is.GreaterThan(Camera.main.nearClipPlane));
                Assert.That(minimum.x, Is.GreaterThan(.01f), "Left side clipped at " + angle);
                Assert.That(minimum.y, Is.GreaterThan(.01f), "Wheels clipped at " + angle);
                Assert.That(maximum.x, Is.LessThan(.99f), "Right side clipped at " + angle);
                Assert.That(maximum.y, Is.LessThan(.9f), "Model overlaps toolbar at " + angle);
                if (!string.IsNullOrEmpty(diagnostics)) Capture(Path.Combine(diagnostics, $"car6_garage_{angle}.png"));
            }
            Click(Find("Back_btn"));
            yield return new WaitForSeconds(1f);
            Assert.That(Find("MainPanel").activeInHierarchy, Is.True);
            Assert.That(Vector3.Distance(Camera.main.transform.position, overviewPosition), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(Camera.main.transform.rotation, overviewRotation), Is.LessThan(.01f));
        }

        private static void Capture(string path)
        {
            var camera = Camera.main;
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            var active = RenderTexture.active;
            var previous = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(transform => transform.gameObject.scene == SceneManager.GetActiveScene() && transform.name == name).gameObject;

        private static void Click(GameObject target) => ExecuteEvents.Execute(target,
            new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
    }
}
