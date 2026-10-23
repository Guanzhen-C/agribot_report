using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class GaragePresentationTests
    {
        [UnityTest]
        public IEnumerator SharedEquipmentIconsPreserveSensorColours()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return new WaitForSeconds(.5f);
            Click(Find("Car1_Img"));
            yield return new WaitForSeconds(1.2f);
            Click(Find("OpenBag_btn"));
            yield return new WaitForSeconds(.8f);
            yield return CheckSensorIcons("EquipmentDetailsUI");
        }

        [UnityTest]
        public IEnumerator OriginalEquipmentIconsPreserveSensorColours()
        {
            yield return SceneManager.LoadSceneAsync("scene2");
            yield return null;
            var last = Find("LastPanel").GetComponents<Component>()
                .Single(c => c != null && c.GetType().Name == "LastPanel");
            last.GetType().GetMethod("OpenLastPanel").Invoke(last, null);
            yield return new WaitForSeconds(.8f);
            Click(Find("OpeBag_btn"));
            yield return new WaitForSeconds(.8f);
            yield return CheckSensorIcons("EquipmentDetailsUI_copy");
        }

        private static IEnumerator CheckSensorIcons(string detailsType)
        {
            var bag = Find("BagPanel");
            var details = bag.GetComponentsInChildren<Component>(true)
                .Single(c => c != null && c.GetType().Name == detailsType);
            var fieldFlags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var detailsImage = (Image)details.GetType().GetField("equipmentIcon", fieldFlags).GetValue(details);
            foreach (var item in new[] { ("RADAR", "Radar.png"), ("RTK", "mushroom.png"),
                         ("CarCamera", "camera.png"), ("ImuBox", "IMUtu.png") })
            {
                var slot = bag.GetComponentsInChildren<Component>(true)
                    .Where(c => c != null && c.GetType().Name == "EquipmentSlotUI")
                    .First(c => {
                        var data = c.GetType().GetProperty("CurrentEquipment").GetValue(c);
                        return data != null && data.GetType().GetProperty("EquipmentType").GetValue(data).ToString() == item.Item1;
                    });
                var image = (Image)slot.GetType().GetField("equipmentIcon", fieldFlags).GetValue(slot);
                Assert.That(image.gameObject.activeInHierarchy, Is.True, item.Item1);
                var sprite = image.sprite;
                Click(slot.gameObject);
                yield return null;
                Assert.That(detailsImage.sprite, Is.SameAs(sprite), item.Item1 + " detail icon differs from slot");
                var data = slot.GetType().GetProperty("CurrentEquipment").GetValue(slot);
                Assert.That(data.GetType().GetProperty("Icon").GetValue(data), Is.SameAs(sprite),
                    item.Item1 + " allocated a second texture on repeated access");
                var png = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Assert.That(png.LoadImage(File.ReadAllBytes("Assets/equips/" + item.Item2)), Is.True);
                var expected = ReadTexture(png);
                var actual = ReadTexture(sprite.texture);
                try
                {
                    var a = expected.GetPixels32();
                    var b = actual.GetPixels32();
                    Assert.That(b.Length, Is.EqualTo(a.Length), item.Item1);
                    long error = 0;
                    for (int i = 0; i < a.Length; i++)
                        error += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
                    double meanError = (double)error / (a.Length * 3);
                    Debug.Log($"SENSOR_ICON_COLOUR {detailsType}/{item.Item1} meanError={meanError:F4}");
                    Assert.That(meanError, Is.LessThan(2.0), item.Item1 + " differs from the original PNG");
                    foreach (var visibleImage in new[] { image, detailsImage })
                    {
                        Assert.That(visibleImage.color, Is.EqualTo(Color.white));
                        Assert.That(visibleImage.canvasRenderer.GetColor(), Is.EqualTo(Color.white));
                    }
                }
                finally
                {
                    UnityEngine.Object.Destroy(actual);
                    UnityEngine.Object.Destroy(expected);
                    UnityEngine.Object.Destroy(png);
                }
            }
        }

        [UnityTest]
        public IEnumerator ReturningFromLargeVehicleRestoresOverviewCamera()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return new WaitForSeconds(.5f);
            var camera = Camera.main;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            float fov = camera.fieldOfView;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Click(Find("Car9_Img"));
                yield return new WaitForSeconds(1.2f);
                if (attempt > 0)
                {
                    Click(Find("OpenBag_btn"));
                    yield return new WaitForSeconds(.7f);
                    if (attempt == 2)
                    {
                        var slot = Find("CombineHeader_slot");
                        slot.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0;
                        yield return null;
                        Click(slot);
                        yield return null;
                        var details = Find("BagPanel").GetComponentsInChildren<Component>(true)
                            .Single(c => c != null && c.GetType().Name == "EquipmentDetailsUI");
                        Click(((Button)details.GetType().GetProperty("InstallButton").GetValue(details)).gameObject);
                        yield return new WaitForSeconds(.5f);
                    }
                }
                Click(Find("Back_btn"));
                yield return new WaitForSeconds(1f);
                Assert.That(Find("MainPanel").activeInHierarchy, Is.True);
                Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThan(.01f),
                    $"Overview position changed on return {attempt}: {camera.transform.position}, expected {position}");
                Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(.01f));
                Assert.That(camera.fieldOfView, Is.EqualTo(fov).Within(.01f));
            }
        }

        [UnityTest]
        public IEnumerator GarageCardsPreserveSourceColoursInEveryButtonState()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return new WaitForSeconds(.5f);
            var colourErrors = new System.Collections.Generic.List<string>();
            foreach (string id in new[] { "Car0", "Car1", "Car2", "Car3", "Car4", "Car5", "Car6", "Car7", "Car8", "Car9" })
            {
                var button = Find(id + "_Img").GetComponent<Button>();
                var image = button.transform.Find("VehiclePreview").GetComponent<Image>();
                var source = Resources.Load<TextAsset>(GarageThumbnailLibrary.ResourceFolder + "/" + id);
                Assert.That(source, Is.Not.Null);
                var png = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Assert.That(png.LoadImage(source.bytes), Is.True);
                var expected = ReadTexture(png);
                var texture = ReadTexture(image.sprite.texture);
                var a = expected.GetPixels32();
                var b = texture.GetPixels32();
                Assert.That(a.Length, Is.EqualTo(b.Length), id);
                long error = 0, samples = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i].a < 250 || b[i].a < 250) continue;
                    error += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
                    samples += 3;
                }
                Assert.That(samples, Is.GreaterThan(100));
                Debug.Log($"GARAGE_COLOUR {id} imported={image.sprite.texture.format}/{image.sprite.texture.graphicsFormat} " +
                    $"png={png.format}/{png.graphicsFormat} error={(double)error/samples:F3}");
                File.WriteAllBytes("/tmp/garage_imported_" + id + ".png", texture.EncodeToPNG());
                File.WriteAllBytes("/tmp/garage_expected_" + id + ".png", expected.EncodeToPNG());
                if ((double)error/samples >= 2.0) colourErrors.Add(id + ": " + (double)error/samples);
                var sprite = image.sprite;
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSeconds(.15f);
                Assert.That(image.overrideSprite, Is.EqualTo(sprite), id + " hover changed the model image");
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSeconds(.15f);
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                EventSystem.current.SetSelectedGameObject(button.gameObject);
                yield return new WaitForSeconds(.15f);
                Assert.That(image.overrideSprite, Is.EqualTo(sprite), id + " selection changed the model image");
                Assert.That(image.color, Is.EqualTo(Color.white));
                Assert.That(image.canvasRenderer.GetColor(), Is.EqualTo(Color.white));
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                EventSystem.current.SetSelectedGameObject(null);
                UnityEngine.Object.Destroy(texture);
                UnityEngine.Object.Destroy(expected);
                UnityEngine.Object.Destroy(png);
            }
            Assert.That(colourErrors, Is.Empty, "Imported textures differ from their source PNGs");
        }

        [UnityTest]
        public IEnumerator EveryVehicleCanReturnToTheOriginalGarageOverview()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return new WaitForSeconds(.5f);
            var camera = Camera.main;
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            float fov = camera.fieldOfView;
            foreach (int number in new[] { 9, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
            {
                Click(Find("Car" + number + "_Img"));
                yield return new WaitForSeconds(1.2f);
                Assert.That(Find("Car" + number).activeInHierarchy, Is.True);
                Capture("/tmp/garage_vehicle_Car" + number + ".png");
                Click(Find("Back_btn"));
                yield return new WaitForSeconds(.9f);
                Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThan(.01f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(.01f));
                Assert.That(camera.fieldOfView, Is.EqualTo(fov).Within(.01f));
            }
            Click(Find("Car0_Img"));
            yield return new WaitForSeconds(1.2f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("scene1"));
            Assert.That(Find("Car0").activeInHierarchy, Is.True);
            Capture("/tmp/garage_vehicle_Car0.png");
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return new WaitForSeconds(.5f);
            Assert.That(Vector3.Distance(Camera.main.transform.position, position), Is.LessThan(.01f));
        }

        private static void Capture(string path)
        {
            var camera = Camera.main;
            var rt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            var texture = ReadTexture(rt);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = previous;
            RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(texture);
        }

        private static Texture2D ReadTexture(Texture source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0,0,source.width,source.height),0,0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(t => t.gameObject.scene == SceneManager.GetActiveScene() && t.name == name).gameObject;

        private static void Click(GameObject target) => ExecuteEvents.Execute(target,
            new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
    }
}
