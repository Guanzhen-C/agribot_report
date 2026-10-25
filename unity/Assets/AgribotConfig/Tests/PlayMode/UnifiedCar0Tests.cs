using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class UnifiedCar0Tests
    {
        private readonly Dictionary<string, string> savedInventory = new();

        [SetUp]
        public void PreserveInventory()
        {
            foreach (string name in new[] { "equipment-bag-state.json", "equipment-bag-state-copy.json",
                         "equipment-bag-state-scene1.json" })
            {
                string path = Path.Combine(Application.persistentDataPath, name);
                savedInventory[path] = File.Exists(path) ? File.ReadAllText(path) : null;
            }
        }

        [TearDown]
        public void RestoreInventory()
        {
            foreach (var pair in savedInventory)
                if (pair.Value == null) { if (File.Exists(pair.Key)) File.Delete(pair.Key); }
                else File.WriteAllText(pair.Key, pair.Value);
            savedInventory.Clear();
        }

        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(item => item.gameObject.scene.IsValid() && item.name == name).gameObject;
        private static Component Component(string name) => UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).First(item => item.GetType().Name == name);
        private static object Invoke(Component target, string method, params object[] args) => target.GetType()
            .GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        private static void Click(string name) => Find(name).GetComponent<Button>().onClick.Invoke();

        [UnityTest]
        public IEnumerator AllVehiclesUseTheSameGarageAndFastReturn()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var main = Find("MainPanel");
            int mainId = main.GetInstanceID();
            int handle = SceneManager.GetActiveScene().handle;
            foreach (var car in new[] { "Car0", "Car1", "Car3", "Car9", "Car0" })
            {
                Click(car + "_Img");
                yield return new WaitForSeconds(.8f);
                Assert.That(Find(car).activeInHierarchy, Is.True, car);
                Assert.That(Find("VehicleActionToolbar").GetComponentsInChildren<Button>().Length, Is.EqualTo(5));
                Assert.That(Find("Back_btn").GetComponents<MonoBehaviour>().Any(item => item.GetType().Name == "BackBtn"), Is.False);
                float started = Time.realtimeSinceStartup;
                Click("Back_btn");
                while (!main.activeInHierarchy && Time.realtimeSinceStartup - started < 2) yield return null;
                Assert.That(main.activeInHierarchy, Is.True, car);
                Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(1), car + " must only play the shared return transition");
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(handle));
                Assert.That(Find("MainPanel").GetInstanceID(), Is.EqualTo(mainId));
                Assert.That(SceneManager.GetSceneByName("scene2").isLoaded, Is.False);
                Debug.Log($"SHARED_GARAGE_RETURN {car} {Time.realtimeSinceStartup - started:F3}s");
                yield return new WaitForSeconds(.5f);
            }
        }

        [UnityTest]
        public IEnumerator RepairIsCar0OnlyAndSharedEquipmentCloseIsVisible()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Click("Car0_Img");
            yield return new WaitForSeconds(.8f);
            var car = Find("Car0");
            var host = car.GetComponents<MonoBehaviour>().Single(item => item.GetType().Name == "Car0GarageVehicle");
            var repair = car.GetComponentsInChildren<Transform>(true).Single(item => item.name == "fixPanel");
            Assert.That(repair.gameObject.activeInHierarchy, Is.False);
            Invoke(host, "RegisterBodyClick", Vector2.one, 10f);
            Assert.That(repair.gameObject.activeInHierarchy, Is.False);
            Invoke(host, "RegisterBodyClick", Vector2.one, 10.1f);
            yield return new WaitForSeconds(1.1f);
            Assert.That(repair.gameObject.activeInHierarchy, Is.True);
            Assert.That(car.GetComponentsInChildren<MonoBehaviour>(true).Any(item => item != null && item.GetType().Name == "UIHoverFade"), Is.False);
            var close = repair.Find("Header/CloseButton").GetComponent<Button>();
            Assert.That(close.GetComponentInChildren<Text>().text, Is.EqualTo("关闭"));
            close.onClick.Invoke();
            yield return new WaitForSeconds(1.1f);
            Click("OpenBag_btn");
            yield return new WaitForSeconds(.8f);
            var bag = Find("BagPanel");
            Assert.That(bag.GetComponents<MonoBehaviour>().Any(item => item.GetType().Name == "BagPanel_copy"), Is.False);
            close = bag.transform.Find("Header/CloseButton").GetComponent<Button>();
            Assert.That(close.GetComponentInChildren<Text>().text, Is.EqualTo("关闭"));
            Assert.That(close.GetComponentInChildren<Text>().color.a, Is.GreaterThan(.9f));
            Assert.That(close.GetComponentInChildren<Text>().GetComponent<RectTransform>().rect.height, Is.GreaterThan(12));
            close.onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            Assert.That(bag.activeInHierarchy, Is.False);
            Click("Back_btn");
            yield return new WaitForSeconds(.8f);
            Click("Car1_Img");
            yield return new WaitForSeconds(.8f);
            Assert.That(Find("Car1").GetComponentsInChildren<MonoBehaviour>(true)
                .Any(item => item != null && item.GetType().Name == "Car0GarageVehicle"), Is.False);
            Assert.That(repair.gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator BodyDoubleClickUsesTheSharedRenderingCamera()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Click("Car0_Img");
            yield return new WaitForSeconds(1);
            Assert.That(Camera.allCameras.Count(item => item.enabled && item.targetTexture == null), Is.EqualTo(1));
            var orbit = Component("OrbitCameraController");
            var body = Find("BodyCollider").GetComponent<Collider>();
            Vector2 point = Camera.main.WorldToScreenPoint(body.bounds.center);
            Assert.That((bool)Invoke(orbit, "IsPointerOverGraphic", point), Is.False,
                "Physics picking must not lock normal preview orbit");
            Vector2 toolbarPoint = RectTransformUtility.WorldToScreenPoint(null, Find("Output_btn").transform.position);
            Assert.That((bool)Invoke(orbit, "IsPointerOverGraphic", toolbarPoint), Is.True,
                "Toolbar and slider interactions must not rotate the preview");
            var repair = Find("Car0").GetComponentsInChildren<Transform>(true).Single(item => item.name == "fixPanel");
            var ui = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, ui);
            Debug.Log("CAR0_BODY_PICK point=" + point + " collider=" + body.enabled + " hits=" +
                string.Join(",", Physics.RaycastAll(Camera.main.ScreenPointToRay(point)).Select(hit => hit.collider.transform.name)) +
                " ui=" + string.Join(",", ui.Select(hit => hit.gameObject.name)));
            var target = ui.First(hit => hit.module is PhysicsRaycaster).gameObject;
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(target).name, Is.EqualTo("Car0"));
            var click = new PointerEventData(EventSystem.current)
                { position = point, button = PointerEventData.InputButton.Left };
            ExecuteEvents.ExecuteHierarchy(target, click, ExecuteEvents.pointerClickHandler);
            Assert.That(repair.gameObject.activeInHierarchy, Is.False);
            yield return new WaitForSeconds(.1f);
            ExecuteEvents.ExecuteHierarchy(target, click, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSeconds(1.1f);
            Assert.That(repair.gameObject.activeInHierarchy, Is.True, "Actual body picking must open repair, not only its method call");
        }

        [UnityTest]
        public IEnumerator GarageCardCanSelectCar0ThroughPointerInput()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return new WaitForSeconds(.5f);
            var card = Find("Car0_Img").GetComponent<Button>();
            Assert.That(card.IsInteractable(), Is.True);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, card.GetComponent<RectTransform>().position);
            var ui = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, ui);
            Debug.Log("CAR0_CARD_PICK point=" + point + " hits=" + string.Join(",", ui.Select(hit => hit.gameObject.name)));
            Assert.That(ui.First().gameObject, Is.EqualTo(card.gameObject));
            var pointer = new PointerEventData(EventSystem.current)
                { position = point, button = PointerEventData.InputButton.Left };
            ExecuteEvents.ExecuteHierarchy(ui.First().gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return null;
            ExecuteEvents.ExecuteHierarchy(ui.First().gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(ui.First().gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSeconds(1);
            Assert.That(Find("Car0").activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator AssistantAcceptsCommittedTextWithoutDuplicatingLegacyInput()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Invoke(Component("NaturalRecommendationPanel"), "OpenMode", "help");
            yield return null;
            var field = Find("RecommendationInput").GetComponent<TMP_InputField>();
            field.text = "";
            field.ActivateInputField();
            yield return null;
            Assert.That(field.isFocused, Is.True);
            InputSystem.QueueTextEvent(Keyboard.current, 'A');
            InputSystem.QueueTextEvent(Keyboard.current, '农');
            yield return null;
            yield return null;
            Assert.That(field.text, Is.EqualTo("A农"));
            field.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace });
            Assert.That(field.text, Is.EqualTo("A"));
            field.text = "";
            InputSystem.QueueTextEvent(Keyboard.current, 'x');
            InputSystem.Update();
            field.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.X, character = 'x' });
            yield return null;
            Assert.That(field.text, Is.EqualTo("x"), "Normal TMP text and native text must not be inserted twice");
            string clipboard = GUIUtility.systemCopyBuffer;
            try
            {
                GUIUtility.systemCopyBuffer = "中文粘贴";
                field.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.V, modifiers = EventModifiers.Control });
                Assert.That(field.text, Does.Contain("中文粘贴"));
            }
            finally { GUIUtility.systemCopyBuffer = clipboard; }
        }

        [UnityTest]
        public IEnumerator InstalledCameraIsOnlyAVisualModel()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Click("Car0_Img");
            yield return new WaitForSeconds(.8f);
            Click("OpenBag_btn");
            yield return new WaitForSeconds(.7f);
            var car = Find("Car0");
            var sensor = car.GetComponentsInChildren<Transform>(true).Single(item => item.name == "CarCamera");
            Debug.Log("CAMERA_PREVIEW_COMPONENTS " + string.Join(",", sensor.GetComponentsInChildren<Component>(true)
                .Where(item => item is Light || item is Camera || item is MonoBehaviour)
                .Select(item => item.name + ":" + item.GetType().Name)));
            var bag = Component("EquipmentBagController");
            var getter = bag.GetType().GetMethod("GetEquipment");
            var equipment = getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, "Camera") });
            Assert.That((bool)Invoke(bag, "InstallEquipment", equipment), Is.True);
            yield return new WaitForSeconds(.7f);
            Assert.That(sensor.GetComponentsInChildren<Light>(true).Any(item => item.enabled), Is.False,
                "A mounted sensor model must not change the garage lighting");
            Assert.That(sensor.GetComponentsInChildren<Camera>(true).Any(item => item.enabled), Is.False);
            Assert.That(Camera.allCameras.Count(item => item.enabled && item.targetTexture == null), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AssistantCompletesWholeChineseCommitsIncludingPartiallyHandledBatches()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Invoke(Component("NaturalRecommendationPanel"), "OpenMode", "help");
            yield return null;
            var field = Find("RecommendationInput").GetComponent<TMP_InputField>();
            field.text = "";
            field.ActivateInputField();
            yield return null;
            foreach (var phrase in new[] { "我要在苹果园使用阿克曼车", "并安装机械臂", "选择合适的定位算法" })
            {
                string before = field.text;
                foreach (char character in phrase) InputSystem.QueueTextEvent(Keyboard.current, character);
                InputSystem.Update();
                field.ProcessEvent(new Event { type = EventType.KeyDown, character = phrase[0] });
                yield return null;
                yield return null;
                Assert.That(field.text, Is.EqualTo(before + phrase), "The first TMP character must not discard the rest of the IME commit");
            }
            const string fullyHandled = "全部交给输入框";
            string existing = field.text;
            foreach (char character in fullyHandled) InputSystem.QueueTextEvent(Keyboard.current, character);
            InputSystem.Update();
            foreach (char character in fullyHandled) field.ProcessEvent(new Event { type = EventType.KeyDown, character = character });
            yield return null;
            yield return null;
            Assert.That(field.text, Is.EqualTo(existing + fullyHandled), "A fully handled IME commit must not be duplicated");

            field.stringPosition = 0;
            field.selectionStringFocusPosition = 2;
            field.ForceLabelUpdate();
            string suffix = field.text.Substring(2);
            foreach (char character in "请帮我") InputSystem.QueueTextEvent(Keyboard.current, character);
            InputSystem.Update();
            field.ProcessEvent(new Event { type = EventType.KeyDown, character = '请' });
            yield return null;
            yield return null;
            Assert.That(field.text, Is.EqualTo("请帮我" + suffix), "Replacing selected text must retain the remainder of the commit");
        }

        [UnityTest]
        public IEnumerator NativeChineseCommitsKeepWholeWordsSelectionAndUnicodeLimit()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Invoke(Component("NaturalRecommendationPanel"), "OpenMode", "help");
            yield return null;
            var field = Find("RecommendationInput").GetComponent<TMP_InputField>();
#if UNITY_EDITOR
            var bundledFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKsc-Regular.otf");
            Assert.That(bundledFont, Is.Not.Null);
            field.textComponent.font = TMP_FontAsset.CreateFontAsset(bundledFont);
#endif
            var bridge = Component("DesktopTextInputBridge");
            field.ActivateInputField();
            yield return null;
            field.text = "";
            foreach (string phrase in new[] { "你好世界", "请帮我选择阿克曼车", "并安装机械臂。" })
            {
                string before = field.text;
                Invoke(bridge, "ApplyNativeCommit", phrase);
                yield return null;
                Assert.That(field.text, Is.EqualTo(before + phrase));
            }
            field.text = "前面旧文字后面";
            field.selectionStringAnchorPosition = 2;
            field.selectionStringFocusPosition = 5;
            Invoke(bridge, "ApplyNativeCommit", "新的中文内容");
            Assert.That(field.text, Is.EqualTo("前面新的中文内容后面"));
            Assert.That(field.stringPosition, Is.EqualTo(8));
            field.text = "";
            field.characterLimit = 3;
            Invoke(bridge, "ApplyNativeCommit", "你\ud83d\ude9c好");
            Assert.That(field.text, Is.EqualTo("你\ud83d\ude9c"));
            field.text = "";
            field.characterLimit = 2;
            Invoke(bridge, "ApplyNativeCommit", "你\ud83d\ude9c好");
            Assert.That(field.text, Is.EqualTo("你"), "A UTF-16 character limit must not split a surrogate pair");
            field.characterLimit = 2000;
            field.text = "";
            Invoke(bridge, "ApplyNativeCommit", "第一行\r\n第二行");
            Assert.That(field.text, Is.EqualTo("第一行\n第二行"));
            Canvas.ForceUpdateCanvases();
            field.textComponent.ForceMeshUpdate();
            Assert.That(field.textComponent.textInfo.lineCount, Is.EqualTo(2));
            var secondLine = field.textComponent.textInfo.characterInfo[4];
            Assert.That(secondLine.isVisible, Is.True);
            var bottom = field.textViewport.InverseTransformPoint(field.textComponent.transform.TransformPoint(secondLine.bottomLeft));
            Assert.That(bottom.y, Is.GreaterThan(field.textViewport.rect.yMin), "The second line must render inside the input viewport");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AssistantEnterSendsShiftEnterAddsNewlineAndImeEnterDoesNotSend()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Invoke(Component("NaturalRecommendationPanel"), "OpenMode", "help");
            yield return null;
            var field = Find("RecommendationInput").GetComponent<TMP_InputField>();
            Assert.That(field.GetType().Name, Is.EqualTo("AssistantInputField"));
            int submissions = 0;
            string submitted = null;
            field.onSubmit.RemoveAllListeners();
            field.onSubmit.AddListener(value => { submissions++; submitted = value; });
            field.ActivateInputField();
            yield return null;
            field.text = "第一行";
            field.stringPosition = field.text.Length;
            Invoke(field, "ProcessChatKey", new Event { type = EventType.KeyDown, keyCode = KeyCode.Return,
                character = '\r', modifiers = EventModifiers.Shift });
            field.ForceLabelUpdate();
            Assert.That(submissions, Is.Zero);
            Assert.That(field.text, Is.EqualTo("第一行\n"));
            Assert.That(field.isFocused, Is.True);
            yield return null;
            InputSystem.QueueTextEvent(Keyboard.current, '\n');
            InputSystem.Update();
            Invoke(field, "ProcessChatKey", new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, character = '\r' });
            Assert.That(submissions, Is.EqualTo(1));
            Assert.That(submitted, Is.EqualTo("第一行\n"));
            yield return null;
            Assert.That(field.text, Is.EqualTo(submitted), "Enter must not append a newline before submission");
            Invoke(Component("DesktopTextInputBridge"), "ApplyNativeCommit", "你好");
            Invoke(field, "ProcessChatKey", new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, character = '\r' });
            Assert.That(submissions, Is.EqualTo(1), "Enter confirming a Chinese candidate must not send");
            yield return null;
            Invoke(field, "ProcessChatKey", new Event { type = EventType.KeyDown, keyCode = KeyCode.KeypadEnter, character = '\n' });
            Assert.That(submissions, Is.EqualTo(2));
            Assert.That(submitted, Does.EndWith("你好"));
            field.characterLimit = field.text.Length;
            Invoke(Component("DesktopTextInputBridge"), "ApplyNativeCommit", "超过字数上限");
            Invoke(field, "ProcessChatKey", new Event { type = EventType.KeyDown, keyCode = KeyCode.Return, character = '\r' });
            Assert.That(submissions, Is.EqualTo(2), "Confirming an IME candidate at the character limit must not send either");
            field.characterLimit = 2000;
            field.onSubmit.RemoveAllListeners();
        }

        [UnityTest]
        public IEnumerator RepairKeepsPreviewScaleAndRestoresViewOnGracefulClose()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Click("Car0_Img");
            yield return new WaitForSeconds(3f);
            var host = Component("Car0GarageVehicle");
            var orbit = Component("OrbitCameraController");
            object before = Invoke(orbit, "CapturePreviewView");
            var camera = Camera.main;
            float bodySize = ProjectedBodySize(camera);
            Invoke(host, "OpenRepair");
            yield return new WaitForSeconds(.4f);
            AssertSameView(before, Invoke(orbit, "CapturePreviewView"));
            Assert.That(ProjectedBodySize(camera), Is.EqualTo(bodySize).Within(bodySize * .03f),
                "Repair entry must not zoom the preview");

            Invoke(orbit, "FixCamera", 12f, -35f, 40f);
            yield return new WaitForSeconds(.5f);
            var repair = Find("Car0").GetComponentsInChildren<Transform>(true).Single(item => item.name == "fixPanel");
            repair.Find("Header/CloseButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.1f);
            var group = repair.GetComponentInParent<Canvas>().GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.InRange(.01f, .99f), "Repair close must fade instead of snapping");
            yield return new WaitForSeconds(.3f);
            Assert.That(repair.gameObject.activeInHierarchy, Is.False);
            AssertSameView(before, Invoke(orbit, "CapturePreviewView"));
            Assert.That((bool)orbit.GetType().GetField("isViewLocked", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(orbit), Is.False);
            Invoke(host, "OpenRepair");
            yield return new WaitForSeconds(.2f);
            Click("Back_btn");
            yield return new WaitForSeconds(.8f);
            Click("Car0_Img");
            yield return new WaitForSeconds(.8f);
            Assert.That(repair.gameObject.activeInHierarchy, Is.False, "Returning to the garage must not leave a repair overlay");
        }

        [UnityTest]
        public IEnumerator ExplodedViewsUseFinePartsAndRestoreEveryVehicleExactly()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            for (int index = 0; index < 10; index++)
            {
                string name = "Car" + index;
                Click(name + "_Img");
                yield return new WaitForSeconds(2.5f);
                var vehicle = Find(name);
                // Cinemachine may follow the expanded assembly; it is not a physical part.
                var positions = vehicle.GetComponentsInChildren<Transform>(true).Where(item =>
                    !item.GetComponents<MonoBehaviour>().Any(component =>
                        component != null && component.GetType().Name == "CinemachineCamera"))
                    .ToDictionary(item => item, item => item.localPosition);
                var heights = vehicle.GetComponentsInChildren<MeshRenderer>().Where(item => item.enabled)
                    .ToDictionary(item => item, item => item.bounds.min.y);
                Vector3 vehiclePosition = vehicle.transform.position;
                Click("Explode_NewVehicle_btn");
                var view = Component("SceneVehicleExplodedView");
                int count = (int)view.GetType().GetProperty("PartCount").GetValue(view);
                int expected;
                if (index == 0)
                {
                    var collector = view.GetType().Assembly.GetType("BombPanel").GetMethod("GetVisualPartRoots");
                    expected = ((IEnumerable<Transform>)collector.Invoke(null, new object[] { vehicle.transform }))
                        .Count(item => item.gameObject.activeInHierarchy);
                    Assert.That(count, Is.GreaterThan(10), "Car0 must use its original detailed parts, not just body and axles");
                }
                else expected = vehicle.GetComponentsInChildren<Renderer>()
                    .Where(item => item is MeshRenderer || item is SkinnedMeshRenderer)
                    .Select(item => item.transform).Distinct().Count();
                Assert.That(count, Is.EqualTo(expected), name);
                var slider = Find("ExplodedViewSlider").GetComponent<Slider>();
                foreach (float amount in new[] { .3f, 1f, .6f, 1f })
                {
                    slider.value = amount;
                    yield return new WaitForSeconds(.12f);
                    Assert.That(Vector3.Distance(vehicle.transform.position, vehiclePosition), Is.LessThan(.00001f),
                        name + " must not move the vehicle root while exploding its parts");
                    foreach (var item in heights)
                        Assert.That(item.Key.bounds.min.y, Is.GreaterThanOrEqualTo(item.Value - .001f),
                            $"{name}/{item.Key.name} moved down toward or below the garage floor at slider={amount}");
                }
                Assert.That(positions.Count(pair => Vector3.Distance(pair.Key.localPosition, pair.Value) > .001f), Is.EqualTo(count),
                    name + ": " + string.Join(",", positions.Where(pair =>
                        Vector3.Distance(pair.Key.localPosition, pair.Value) > .001f).Select(pair => pair.Key.name)));
                Click("CloseExplodedView");
                Assert.That(positions.All(pair => Vector3.Distance(pair.Key.localPosition, pair.Value) < .00001f), Is.True,
                    name + " must restore every part exactly");
                Debug.Log($"FINE_EXPLODED_VIEW {name} parts={count}");
                Click("Back_btn");
                yield return new WaitForSeconds(.8f);
            }
        }

        private static float ProjectedBodySize(Camera camera)
        {
            var bounds = Find("BodyCollider").GetComponent<Collider>().bounds;
            var a = camera.WorldToViewportPoint(bounds.center - camera.transform.right * bounds.extents.magnitude);
            var b = camera.WorldToViewportPoint(bounds.center + camera.transform.right * bounds.extents.magnitude);
            return Vector2.Distance(a, b);
        }

        private static void AssertSameView(object expected, object actual)
        {
            foreach (var field in expected.GetType().GetFields())
                if (field.FieldType == typeof(float))
                    Assert.That((float)field.GetValue(actual), Is.EqualTo((float)field.GetValue(expected)).Within(.001f), field.Name);
                else Assert.That(field.GetValue(actual), Is.EqualTo(field.GetValue(expected)), field.Name);
        }

        [UnityTest]
        public IEnumerator SharedCar0PreservesCalibrationAndExportsTheCurrentAssembly()
        {
            yield return SceneManager.LoadSceneAsync("scene2");
            yield return null;
            var baseline = AckermannRuntimeConfiguration.BuildOrRefresh().BuildConfiguration();
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Click("Car0_Img");
            yield return new WaitForSeconds(.8f);
            Click("OpenBag_btn");
            yield return new WaitForSeconds(.7f);
            var bag = Component("EquipmentBagController");
            var getter = bag.GetType().GetMethod("GetEquipment");
            var slot = getter.GetParameters()[0].ParameterType;
            foreach (string name in new[] { "Radar", "Camera", "Imu", "RTK", "Hand" })
            {
                var equipment = getter.Invoke(bag, new[] { Enum.Parse(slot, name) });
                Assert.That((bool)Invoke(bag, "InstallEquipment", equipment), Is.True, name);
            }
            float deadline = Time.realtimeSinceStartup + 12;
            while ((bool)bag.GetType().GetProperty("IsSceneAnimating").GetValue(bag) && Time.realtimeSinceStartup < deadline)
                yield return null;
            var host = Component("Car0GarageVehicle");
            var authoring = (VehicleConfigurationAuthoring)Invoke(host, "BuildConfiguration");
            var configuration = authoring.BuildConfiguration();
            Assert.That(configuration.vehicleId, Is.EqualTo("ackermann_current"));
            Assert.That(configuration.geometry.wheelbaseM, Is.EqualTo(baseline.geometry.wheelbaseM).Within(.0001));
            Assert.That(configuration.geometry.frontTrackM, Is.EqualTo(baseline.geometry.frontTrackM).Within(.0001));
            Assert.That(configuration.geometry.wheelRadiusM, Is.EqualTo(baseline.geometry.wheelRadiusM).Within(.0001));
            Assert.That(configuration.sensors.Length, Is.EqualTo(5));
            foreach (var sensor in configuration.sensors)
            {
                var expected = baseline.sensors.Single(item => item.id == sensor.id);
                for (int axis = 0; axis < 3; axis++)
                {
                    Assert.That(sensor.pose.xyz[axis], Is.EqualTo(expected.pose.xyz[axis]).Within(.00001), sensor.id);
                    Assert.That(sensor.pose.rpy[axis], Is.EqualTo(expected.pose.rpy[axis]).Within(.00001), sensor.id);
                }
            }
            Assert.That(configuration.manipulators.Length, Is.EqualTo(1));
            string output = Path.Combine(Path.GetTempPath(), "agribot-car0-shared-" + Guid.NewGuid());
            try
            {
                typeof(VehicleConfigurationAuthoring).GetField("exportDirectory", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(authoring, output);
                var result = authoring.ExportConfigurationBundle();
                Assert.That(File.Exists(result.JsonPath), Is.True);
                Assert.That(result.ExportedMeshCount, Is.GreaterThan(10));
                Assert.That(File.Exists(Path.Combine(output, "vehicle_visual.obj")), Is.True);
                Assert.That(File.Exists(Path.Combine(output, "sensor_lidar_visual.obj")), Is.True);
                Click("Output_btn");
                Assert.That(Find("ROS_Algorithm_Selection").activeInHierarchy, Is.True);
                Find("ROS_Algorithm_Selection").SetActive(false);
            }
            finally { if (Directory.Exists(output)) Directory.Delete(output, true); }
        }
    }
}
