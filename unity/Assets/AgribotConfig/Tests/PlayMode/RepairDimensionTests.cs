using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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
            Assert.That(heading.preferredHeight, Is.LessThanOrEqualTo(heading.rectTransform.rect.height),
                "The CJK heading must fit its line box without being vertically clipped");
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
        public IEnumerator SceneSelectionIsVisibleImmediatelyAndSurvivesFocusAndReentry()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            GameObject.Find("Car0_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            GameObject.Find("Output_btn").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var first = GameObject.Find("Scenario_orchard").GetComponent<Button>();
            var second = GameObject.Find("Scenario_vineyard").GetComponent<Button>();
            var accent = new Color(.75f, .88f, .27f);
            first.onClick.Invoke();
            Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo("orchard"));
            Assert.That(first.colors.normalColor, Is.EqualTo(accent));
            Assert.That(first.colors.highlightedColor, Is.EqualTo(accent));
            Assert.That(first.colors.selectedColor, Is.EqualTo(accent));
            EventSystem.current.SetSelectedGameObject(second.gameObject);
            first.OnPointerExit(new PointerEventData(EventSystem.current));
            yield return new WaitForSeconds(.2f);
            Assert.That(first.targetGraphic.canvasRenderer.GetColor(), Is.EqualTo(accent));
            second.onClick.Invoke();
            Assert.That(FarmScenarioCatalog.SelectedId, Is.EqualTo("vineyard"));
            Assert.That(second.colors.normalColor, Is.EqualTo(accent));
            Assert.That(second.GetComponent<Outline>().enabled, Is.True);
            Assert.That(first.colors.normalColor, Is.Not.EqualTo(accent));
            Assert.That(first.GetComponent<Outline>().enabled, Is.False);
            Assert.That(second.transform.Find("SceneName").GetComponent<Text>().color,
                Is.EqualTo(new Color(.055f, .065f, .075f)));
            yield return Capture("scene-selection-immediate");
            var selector = GameObject.Find("VehicleSimulationSelector");
            var panel = Component(selector, "LastPanel");
            var dialog = Field<GameObject>(panel, "algorithmDialog");
            var footer = dialog.transform.Find("SimulationSetup/Footer");
            Assert.That(footer.Find("SetupNext"), Is.Null);
            Assert.That(footer.Find("Cancel"), Is.Null);
            Assert.That(footer.GetComponentsInChildren<Text>().Any(label => label.text == "选择算法" || label.text == "取消"), Is.False);
            var setup = dialog.transform.Find("SimulationSetup");
            setup.Find("AlgorithmTab").GetComponent<Button>().onClick.Invoke();
            Assert.That(setup.Find("AlgorithmPage").gameObject.activeSelf, Is.True);
            setup.Find("SceneTab").GetComponent<Button>().onClick.Invoke();
            Assert.That(setup.Find("ScenePage").gameObject.activeSelf, Is.True);
            Assert.That(second.colors.normalColor, Is.EqualTo(accent));
            setup.Find("Close").GetComponent<Button>().onClick.Invoke();
            Assert.That(dialog.activeSelf, Is.False);
            GameObject.Find("Output_btn").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(second.colors.normalColor, Is.EqualTo(accent));
            Assert.That(first.GetComponent<Outline>().enabled, Is.False);
            Invoke(panel, "CloseSetup");
        }

        [UnityTest]
        public IEnumerator RepairModulesUseOneSelectionStyleAndDoNotReopenFromBodyClicks()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            GameObject.Find("Car0_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            var car = GameObject.Find("Car0");
            var host = Component(car, "Car0GarageVehicle");
            var repair = Component(car, "FixPanel");
            Invoke(host, "RegisterBodyClick", Vector2.one, 10f);
            Invoke(host, "RegisterBodyClick", Vector2.one, 10.1f);
            yield return new WaitForSeconds(.3f);
            var initial = repair.transform.Find("note_none");
            var initialHint = initial.GetComponentInChildren<Text>();
            Assert.That(initial.gameObject.activeInHierarchy, Is.True);
            Assert.That(initialHint, Is.Not.Null);
            Assert.That(initialHint.enabled, Is.True);
            Assert.That(initialHint.text, Is.EqualTo("点击上方卡片查看说明\n再次点击同一卡片进入对应功能"));
            Canvas.ForceUpdateCanvases();
            Assert.That(initialHint.preferredHeight, Is.LessThanOrEqualTo(initialHint.rectTransform.rect.height));
            ColorBlock? selectedStyle = null;
            for (int id = 1; id <= 5; id++)
            {
                var slot = Slot(car, id);
                Invoke(slot, "OnClick");
                var button = slot.GetComponent<Button>();
                if (selectedStyle.HasValue) Assert.That(button.colors, Is.EqualTo(selectedStyle.Value));
                selectedStyle = button.colors;
                Assert.That(button.GetComponent<Outline>().enabled, Is.True);
                Assert.That(Field<GameObject>(slot, "selected_img").activeSelf, Is.False);
                for (int other = 1; other <= 5; other++)
                    Assert.That(Slot(car, other).GetComponent<Outline>().enabled, Is.EqualTo(other == id));
                var detail = repair.transform.Cast<Transform>().Single(item => item.name.StartsWith("note_" + id));
                var description = detail.GetComponentsInChildren<Text>(true).Single(label => label.name == "content");
                Assert.That(description.text, Does.Contain("再次点击上方卡片"));
                if (id <= 3)
                {
                    Assert.That(description.text, Does.Contain("（cm）\n再次点击上方卡片进入调整"));
                    Canvas.ForceUpdateCanvases();
                    Assert.That(description.preferredHeight, Is.LessThanOrEqualTo(description.rectTransform.rect.height),
                        "Both the dimension description and the action hint must fit without clipping");
                }
            }
            Invoke(Slot(car, 3), "OnClick");
            Assert.That(Field<int>(Slot(car, 3), "clickStep"), Is.EqualTo(1));
            yield return Capture("repair-selected-radius");
            Invoke(Slot(car, 3), "OnClick");
            Assert.That(Field<int>(Slot(car, 3), "clickStep"), Is.Zero);
            Assert.That(repair.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            yield return new WaitForSecondsRealtime(Field<float>(repair, "fadeOutDuration") + .15f);
            var radius = Component(car, "bottonPanel");
            var input = Field<InputField>(radius, "txt_input");
            Assert.That(repair.gameObject.activeInHierarchy, Is.False,
                "Repair alpha=" + repair.GetComponent<CanvasGroup>().alpha + " radius active=" + radius.gameObject.activeInHierarchy);
            Assert.That(radius.gameObject.activeInHierarchy, Is.True);
            AssertInputLayout(input, Field<Button>(radius, "Sure_btn"), "车轮半径");

            // Double-clicking the visible body must not replace the active adjustment page.
            Invoke(host, "RegisterBodyClick", Vector2.one, 11f);
            Invoke(host, "RegisterBodyClick", Vector2.one, 11.1f);
            yield return new WaitForSeconds(.3f);
            Assert.That(repair.gameObject.activeInHierarchy, Is.False);
            Assert.That(radius.gameObject.activeInHierarchy, Is.True);
            var change = Component(car, "CarChange");
            input.text = "14";
            Field<Button>(radius, "Sure_btn").onClick.Invoke();
            Assert.That(Property(change, "CurWheelRadius"), Is.EqualTo(.14f).Within(.00001f));
            input.SetTextWithoutNotify("15.5");
            input.onSubmit.Invoke("15.5");
            Assert.That(Property(change, "CurWheelRadius"), Is.EqualTo(.155f).Within(.00001f));
            input.SetTextWithoutNotify("99");
            input.onSubmit.Invoke("99");
            Assert.That(Property(change, "CurWheelRadius"), Is.EqualTo(.155f).Within(.00001f));
            Field<Button>(radius, "MinRadius_btn").onClick.Invoke();
            Assert.That(input.text, Is.EqualTo("7"));
            Field<Button>(radius, "NormalRadius_btn").onClick.Invoke();
            Assert.That(input.text, Is.EqualTo("12.75"));
            yield return Capture("radius-input-unified");
            Invoke(host, "CloseRepairAnimated");
            Invoke(host, "RegisterBodyClick", Vector2.one, 12f);
            Invoke(host, "RegisterBodyClick", Vector2.one, 12.1f);
            yield return new WaitForSeconds(.4f);
            Assert.That(repair.gameObject.activeInHierarchy, Is.False);
            Invoke(host, "RegisterBodyClick", Vector2.one, 13f);
            Invoke(host, "RegisterBodyClick", Vector2.one, 13.1f);
            yield return new WaitForSeconds(.3f);
            Assert.That(repair.gameObject.activeInHierarchy, Is.True);
            Invoke(host, "CloseRepair");
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
