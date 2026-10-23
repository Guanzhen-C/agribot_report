using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class GarageNotificationTests
    {
        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(item => item.gameObject.scene.IsValid() && item.name == name).gameObject;

        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator NoticesWrapReplaceAndDismissWithoutCoveringControls()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Find("Car4_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            var main = Find("MainPanel").GetComponents<MonoBehaviour>().Single(item => item.GetType().Name == "MainPanel");
            var show = main.GetType().GetMethod("ShowSimulationMessage", BindingFlags.NonPublic | BindingFlags.Instance);
            show.Invoke(main, new object[] { "ROS 配置导出失败：请先安装雷达、RTK、相机和 IMU，等待安装动画结束。" });
            yield return new WaitForSeconds(.3f);
            var notice = Find("GarageNotification");
            var bounds = Bounds(notice.GetComponent<RectTransform>());
            foreach (string name in new[] { "Back_btn", "Output_btn", "Explode_NewVehicle_btn", "OpenBag_btn", "GlobalAssistant" })
                Assert.That(bounds.Overlaps(Bounds(Find(name).GetComponent<RectTransform>())), Is.False, name);
            var message = notice.transform.Find("MessageViewport/Message").GetComponent<TMP_Text>();
            Assert.That(message.enableAutoSizing, Is.False);
            Assert.That(message.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
            Assert.That(message.transform.lossyScale.x, Is.EqualTo(message.transform.lossyScale.y).Within(.001f));
            var close = notice.transform.Find("CloseNotification").GetComponent<Button>();
            Assert.That(Bounds(close.GetComponent<RectTransform>()).Overlaps(Bounds(message.rectTransform)), Is.False);
            show.Invoke(main, new object[] { "请检查配置后重试。" });
            yield return null;
            Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(item => item.GetType().Name == "GarageNotification"), Is.EqualTo(1));
            Assert.That(message.text, Is.EqualTo("请检查配置后重试。"));
            close.onClick.Invoke();
            yield return new WaitForSeconds(.3f);
            Assert.That(notice.activeInHierarchy, Is.False);

            show.Invoke(main, new object[] { string.Join("\n", Enumerable.Repeat("长错误信息和配置路径不会撑破提示框。", 30)) });
            yield return new WaitForSeconds(.3f);
            var scroll = notice.GetComponent<ScrollRect>();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(notice.GetComponent<RectTransform>().rect.height, Is.LessThanOrEqualTo(260));
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.001));
            close.onClick.Invoke();
            yield return new WaitForSeconds(.3f);
        }

        [UnityTest]
        public IEnumerator Car4AndCar5HaveFourBlackTyresAndLightHubs()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            foreach (var vehicle in new[] { "Car4", "Car5" })
            {
                Find(vehicle + "_Img").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.8f);
                var car = Find(vehicle);
                foreach (string wheel in new[] { "front_left", "front_right", "rear_left", "rear_right" })
                {
                    var renderers = car.GetComponentsInChildren<MeshRenderer>()
                        .Where(item => item.name == wheel + "_wheel_link").ToArray();
                    Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(2), vehicle + " " + wheel);
                    var tyres = renderers.Where(item => item.sharedMaterial.color.maxColorComponent < .15f).ToArray();
                    Assert.That(tyres.Length, Is.EqualTo(1), vehicle + " " + wheel + " black tyre mesh");
                    Assert.That(tyres[0].GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(1000));
                    Assert.That(renderers.Any(item => item.sharedMaterial.color.r > .7f && item.sharedMaterial.color.g > .6f),
                        Is.True, vehicle + " " + wheel + " hub must keep its separate light material");
                }
                var camera = Camera.main;
                var texture = new RenderTexture(1280, 720, 24);
                var previous = camera.targetTexture;
                camera.targetTexture = texture;
                camera.Render();
                var previousActive = RenderTexture.active;
                RenderTexture.active = texture;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                System.IO.File.WriteAllBytes("/tmp/agribot_black_tyres_" + vehicle + ".png", image.EncodeToPNG());
                RenderTexture.active = previousActive;
                camera.targetTexture = previous;
                Object.Destroy(image);
                texture.Release();
                Object.Destroy(texture);
                Find("Back_btn").GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSeconds(.8f);
            }
        }
    }
}
