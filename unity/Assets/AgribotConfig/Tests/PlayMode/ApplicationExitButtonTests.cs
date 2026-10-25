using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class ApplicationExitButtonTests
    {
        [UnityTest]
        public IEnumerator ExitControlPersistsOnceAndOnlyInterceptsItsOwnCorner()
        {
            foreach (string scene in new[] { "scene1", "scene2", "scene1" })
            {
                yield return SceneManager.LoadSceneAsync(scene);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Where(button => button.name == "ExitApplication").ToArray();
                Assert.That(buttons.Length, Is.EqualTo(1));
                var exit = buttons.Single();
                Assert.That(exit.IsInteractable(), Is.True);
                Assert.That(exit.GetComponentInChildren<Text>().text, Is.EqualTo("\u00d7"));
                Assert.That(exit.GetComponentInChildren<Text>().cachedTextGenerator.characterCountVisible, Is.EqualTo(1),
                    "The close glyph must actually render, not merely exist as a text value.");
                var bounds = Bounds(exit.GetComponent<RectTransform>());
                Assert.That(bounds.size, Is.EqualTo(new Vector2(36, 36)));
                Assert.That(Screen.width - bounds.xMax, Is.EqualTo(14).Within(.1f));
                Assert.That(Screen.height - bounds.yMax, Is.EqualTo(14).Within(.1f));
                var pointer = new PointerEventData(EventSystem.current) { position = bounds.center };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits.First().gameObject, Is.EqualTo(exit.gameObject));
                var tooltip = exit.transform.Find("ExitTooltip").gameObject;
                Assert.That(tooltip.activeSelf, Is.False);
                ExecuteEvents.Execute(exit.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                Assert.That(tooltip.activeSelf, Is.True);
                ExecuteEvents.Execute(exit.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                Assert.That(tooltip.activeSelf, Is.False);
                pointer.position = new Vector2(Screen.width * .5f, Screen.height * .5f);
                hits.Clear();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits.Any(hit => hit.gameObject == exit.gameObject), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ExitControlDoesNotOverlapToolbarEquipmentOrAssistantButtons()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var exit = Find("ExitApplication").GetComponent<RectTransform>();
            foreach (string car in new[] { "Car0", "Car3", "Car9" })
            {
                Click(car + "_Img");
                yield return new WaitForSeconds(.8f);
                Canvas.ForceUpdateCanvases();
                foreach (string name in new[] { "Back_btn", "Output_btn", "Stop_NewVehicle_Simulation",
                             "Export_NewVehicle_Physical", "Explode_NewVehicle_btn", "OpenBag_btn" })
                    Assert.That(Bounds(exit).Overlaps(Bounds(Find(name).GetComponent<RectTransform>())), Is.False, name);
                Click("OpenBag_btn");
                yield return new WaitForSeconds(.8f);
                var close = Find("BagPanel").transform.Find("Header/CloseButton").GetComponent<Button>();
                Debug.Log($"EXIT_BUTTON_LAYOUT car={car} screen={Screen.width}x{Screen.height} exit={Bounds(exit)} panelClose={Bounds(close.GetComponent<RectTransform>())}");
                Assert.That(Bounds(exit).Overlaps(Bounds(close.GetComponent<RectTransform>())), Is.False);
                close.onClick.Invoke();
                yield return new WaitForSeconds(.5f);
                Click("Back_btn");
                yield return new WaitForSeconds(.6f);
            }
            Click("GlobalAssistant");
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (string name in new[] { "CloseRecommendation", "NewConversation" })
                Assert.That(Bounds(exit).Overlaps(Bounds(Find(name).GetComponent<RectTransform>())), Is.False, name);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Bounds(exit).center }, hits);
            Assert.That(hits.First().gameObject, Is.EqualTo(exit.gameObject));
            Click("CloseRecommendation");
        }

        private static GameObject Find(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .First(item => item.gameObject.scene.IsValid() && item.name == name).gameObject;
        private static void Click(string name) => Find(name).GetComponent<Button>().onClick.Invoke();
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
