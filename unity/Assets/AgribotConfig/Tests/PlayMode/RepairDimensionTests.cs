using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class RepairDimensionTests
    {
        private static object Invoke(Component target, string name, params object[] args) => target.GetType()
            .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
        private static T Field<T>(Component target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
        private static float Property(Component target, string name) => (float)target.GetType().GetProperty(name).GetValue(target);
        private static Component Component(GameObject root, string name) => root.GetComponentsInChildren<MonoBehaviour>(true)
            .Single(item => item != null && item.GetType().Name == name);
        private static Component Slot(GameObject root, int id) => root.GetComponentsInChildren<MonoBehaviour>(true)
            .Single(item => item != null && item.GetType().Name == "slotBtn" && Field<int>(item, "slotId") == id);

        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void AssertInputLayout(InputField input, Button confirm, string title)
        {
            Canvas.ForceUpdateCanvases();
            var window = (RectTransform)input.transform.parent;
            var unit = window.Find("xiahuaxian").GetComponent<Text>();
            var heading = window.GetComponentsInChildren<Text>().Single(label => label.text == title);
            var inputBounds = Bounds(input.GetComponent<RectTransform>());
            var unitBounds = Bounds(unit.rectTransform);
            var buttonBounds = Bounds(confirm.GetComponent<RectTransform>());
            Assert.That(unit.text, Is.EqualTo("cm"));
            Assert.That(unit.resizeTextForBestFit, Is.False);
            Assert.That(confirm.GetComponentInChildren<Text>().text, Is.EqualTo("确认"));
            Assert.That(inputBounds.Overlaps(unitBounds), Is.False);
            Assert.That(unitBounds.Overlaps(buttonBounds), Is.False);
            Assert.That(Bounds(heading.rectTransform).Overlaps(inputBounds), Is.False);
            foreach (var element in new[] { inputBounds, unitBounds, buttonBounds, Bounds(heading.rectTransform) })
            {
                Assert.That(Bounds(window).Contains(element.min), Is.True);
                Assert.That(Bounds(window).Contains(element.max), Is.True);
            }
            Assert.That(input.textComponent.resizeTextForBestFit, Is.False);
            Assert.That(input.textComponent.fontSize, Is.EqualTo(22));
            Assert.That(window.GetComponentsInChildren<Text>().Any(label => label.text.Contains("点击确认")), Is.False);
        }

        private static IEnumerator Capture(string name)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.validation/repair_dimensions_20261117"));
            Directory.CreateDirectory(directory);
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RepairUsesCorrectTermsAndCentimetersWithoutChangingMeterGeometry()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            GameObject.Find("Car0_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(1f);
            var car = GameObject.Find("Car0");
            var host = Component(car, "Car0GarageVehicle");
            var change = Component(car, "CarChange");
            float originalWheelbase = Property(change, "Cur_wheelDis");
            Invoke(host, "OpenRepair");
            yield return new WaitForSeconds(.4f);
            var repair = Component(car, "FixPanel").transform;
            var notes = repair.GetComponentsInChildren<Text>(true).Select(label => label.text).ToArray();
            Assert.That(notes, Does.Contain("调整左右轮距"));
            Assert.That(notes, Does.Contain("调整前后轴距"));
            Assert.That(notes.Any(value => value.Contains("轮胎轮毂长度") || value.Contains("前后轮轮距")), Is.False);
            yield return Capture("repair-menu");

            Invoke(Slot(car, 1), "OnClick");
            Invoke(Slot(car, 1), "OnClick");
            yield return new WaitForSeconds(.7f);
            var track = Component(car, "AxisPanel");
            var trackInput = Field<InputField>(track, "axisDistanceInput");
            Assert.That(track.gameObject.activeInHierarchy, Is.True);
            AssertInputLayout(trackInput, Field<Button>(track, "sureButton"), "左右轮距");
            trackInput.text = "65";
            Field<Button>(track, "sureButton").onClick.Invoke();
            yield return new WaitForSeconds(.4f);
            Assert.That(Property(change, "CurWheelTrack"), Is.EqualTo(.65f).Within(.00001f));
            Assert.That(Property(change, "Cur_wheelDis"), Is.EqualTo(originalWheelbase).Within(.00001f));
            Assert.That(Field<Text>(track, "axisDistanceValueText").text, Is.EqualTo("左右轮距：65 cm"));
            Assert.That(Field<Button>(track, "minAxisButton").GetComponentInChildren<Text>().text, Is.EqualTo("最小轮距：59 cm"));
            Assert.That(track.GetComponentsInChildren<Text>().Any(label => label.text.Contains("轴距")), Is.False);
            yield return Capture("track-65cm");
            trackInput.SetTextWithoutNotify("50");
            trackInput.onSubmit.Invoke("50");
            Assert.That(Property(change, "CurWheelTrack"), Is.EqualTo(.65f).Within(.00001f));
            Invoke(track, "HideImmediately");

            Invoke(host, "OpenRepair");
            yield return new WaitForSeconds(.3f);
            Invoke(Slot(car, 2), "OnClick");
            Invoke(Slot(car, 2), "OnClick");
            yield return new WaitForSeconds(.7f);
            var wheelbase = Component(car, "WheelDistancePanel");
            var input = Field<InputField>(wheelbase, "distanceInput");
            AssertInputLayout(input, Field<Button>(wheelbase, "Sure_btn"), "前后轴距");
            Assert.That(input, Is.Not.SameAs(trackInput), "Each panel must bind its own input");
            input.text = "55";
            Field<Button>(wheelbase, "Sure_btn").onClick.Invoke();
            yield return new WaitForSeconds(.4f);
            Assert.That(Property(change, "Cur_wheelDis"), Is.EqualTo(.55f).Within(.00001f));
            Assert.That(Property(change, "CurWheelTrack"), Is.EqualTo(.65f).Within(.00001f));
            Assert.That(Field<Text>(wheelbase, "wheelDistanceValueText").text, Is.EqualTo("前后轴距：55 cm"));
            Assert.That(Field<Button>(wheelbase, "minDistanceButton").GetComponentInChildren<Text>().text, Is.EqualTo("最小轴距：42 cm"));
            Assert.That(wheelbase.GetComponentsInChildren<Text>().Any(label => label.text.Contains("0.42") || label.text.Contains(" m")), Is.False);
            yield return Capture("wheelbase-55cm");

            input.SetTextWithoutNotify("0.55");
            Field<Button>(wheelbase, "Sure_btn").onClick.Invoke();
            Assert.That(Property(change, "Cur_wheelDis"), Is.EqualTo(.55f).Within(.00001f));
            input.SetTextWithoutNotify("58.5");
            input.onSubmit.Invoke("58.5");
            yield return new WaitForSeconds(.4f);
            Assert.That(Property(change, "Cur_wheelDis"), Is.EqualTo(.585f).Within(.00001f));
            Field<Button>(wheelbase, "maxDistanceButton").onClick.Invoke();
            yield return new WaitForSeconds(.4f);
            Assert.That(input.text, Is.EqualTo("60"));
            var authoring = (VehicleConfigurationAuthoring)Invoke(host, "BuildConfiguration");
            var configuration = authoring.BuildConfiguration();
            Assert.That(configuration.geometry.wheelbaseM, Is.EqualTo(.60).Within(.001));
            Assert.That(configuration.geometry.frontTrackM, Is.EqualTo(.65).Within(.001));
            Assert.That(configuration.geometry.rearTrackM, Is.EqualTo(.65).Within(.001));
            Invoke(host, "CloseRepair");
        }
    }
}
