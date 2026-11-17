using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration.Tests
{
    public sealed class SimulationPageAndStopTests
    {
        private string root, previousRoot, previousScript;

        [SetUp] public void IsolateCommands()
        {
            previousRoot = Environment.GetEnvironmentVariable("AGRIBOT_REPORT_ROOT");
            previousScript = Environment.GetEnvironmentVariable("AGRIBOT_ROS_SYNC_SCRIPT");
            root = Path.Combine(Path.GetTempPath(), "agribot-page-stop-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("AGRIBOT_REPORT_ROOT", root);
            string script = Path.Combine(root, "fake-managed.sh");
            File.WriteAllText(script, "#!/bin/bash\nprintf '%s\\n' \"$*\" >> \""+root+"/managed-commands\"\n");
            Environment.SetEnvironmentVariable("AGRIBOT_ROS_SYNC_SCRIPT", script);
        }

        [TearDown] public void RestoreCommands()
        {
            Environment.SetEnvironmentVariable("AGRIBOT_REPORT_ROOT", previousRoot);
            Environment.SetEnvironmentVariable("AGRIBOT_ROS_SYNC_SCRIPT", previousScript);
            Directory.Delete(root, true);
        }

        [UnityTearDown] public IEnumerator FinishFakeCommandsBeforeRestoringEnvironment()
        {
            var hil = Ui("HilSimulationUi");
            var managed = Ui("RosSimulationHandoffUi");
            float deadline = Time.realtimeSinceStartup+20;
            while ((Flag(hil, "IsBusy") || Flag(managed, "IsBusy")) && Time.realtimeSinceStartup<deadline)
                yield return null;
            foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(item => item.GetType() == hil || item.GetType() == managed))
                UnityEngine.Object.Destroy(component.gameObject);
            yield return null;
        }

        private static GameObject Find(string name) => UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).First(item => item.name == name).gameObject;
        private static Type Ui(string name) => AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())
            .Single(type => type.FullName == "Agribot.VehicleConfiguration."+name);
        private static bool Flag(Type type, string name) => (bool)type.GetProperty(name).GetValue(null);
        private static IEnumerator BusyDraft() { yield return new WaitForSecondsRealtime(.4f); }
        private static Rect Bounds(GameObject item)
        {
            var corners = new Vector3[4]; item.GetComponent<RectTransform>().GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        [UnityTest]
        public IEnumerator SetupReplacesVehicleControlsAndReturnsWithoutLosingEquipment()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Find("Car0_Img").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.8f);
            Find("OpenBag_btn").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(.5f);
            var bag = Find("BagPanel").GetComponentsInChildren<MonoBehaviour>(true)
                .Single(item => item.GetType().Name == "EquipmentBagController");
            var getter = bag.GetType().GetMethod("GetEquipment");
            var equipment = getter.Invoke(bag, new[] { Enum.Parse(getter.GetParameters()[0].ParameterType, "RTK") });
            bag.GetType().GetMethod("SetInstallationState").Invoke(bag, new[] { equipment, (object)true });
            yield return new WaitForSeconds(.8f);
            var toolbar = Find("VehicleActionToolbar");
            var launch = toolbar.GetComponentsInChildren<Button>(true).Single(button => button.name == "Output_btn");
            launch.onClick.Invoke();
            yield return new WaitForSeconds(.4f);
            var page = Find("ROS_Algorithm_Selection");
            Assert.That(page.activeInHierarchy, Is.True);
            Assert.That(page.GetComponent<Image>().color.a, Is.EqualTo(1));
            foreach (var group in page.GetComponentsInParent<CanvasGroup>())
                Assert.That(group.alpha, Is.EqualTo(1), group.name+" must not fade the setup page");
            Assert.That(toolbar.activeInHierarchy, Is.False);
            Assert.That(Find("Back_btn").activeInHierarchy, Is.False);
            Assert.That(Find("BagPanel").activeInHierarchy, Is.False);
            Assert.That((bool)bag.GetType().GetMethod("IsInstalled").Invoke(bag, new[] { equipment }), Is.True);
            var panel = page.transform.Find("SimulationSetup").GetComponent<RectTransform>();
            Assert.That(panel.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(panel.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(page.GetComponentsInChildren<Button>(true).Single(button => button.name == "SetupStop").gameObject.activeInHierarchy, Is.True);
            Canvas.ForceUpdateCanvases();
            foreach (string name in new[] { "Close", "Launch", "LaunchHil" })
            {
                var button = page.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
                foreach (string global in new[] { "GlobalAssistant", "ExitApplication" })
                    Assert.That(Bounds(button.gameObject).Overlaps(Bounds(Find(global))), Is.False, name+" / "+global);
            }
            yield return HilUiTests.Capture("simulation_setup_page");
            page.GetComponentsInChildren<Button>(true).Single(button => button.name == "Close").onClick.Invoke();
            yield return null;
            Assert.That(page.activeInHierarchy, Is.False);
            Assert.That(toolbar.activeInHierarchy, Is.True);
            Assert.That(Find("Back_btn").activeInHierarchy, Is.True);
            Assert.That((bool)bag.GetType().GetMethod("IsInstalled").Invoke(bag, new[] { equipment }), Is.True);
            yield return HilUiTests.Capture("vehicle_page_restored");
        }

        [UnityTest]
        public IEnumerator OneManagedStopClickIsRetainedWhileAnotherOperationIsBusy()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var type = Ui("RosSimulationHandoffUi");
            type.GetMethod("RequestDraftExport").Invoke(null, new object[] { BusyDraft() });
            Assert.That(Flag(type, "IsBusy"), Is.True);
            type.GetMethod("RequestStop").Invoke(null, new object[] { null });
            type.GetMethod("RequestStop").Invoke(null, new object[] { null });
            Assert.That(Flag(type, "IsStopPending"), Is.True);
            float deadline = Time.realtimeSinceStartup+5;
            while (Flag(type, "IsBusy") && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(Flag(type, "IsBusy"), Is.False);
            Assert.That(File.ReadAllLines(Path.Combine(root, "managed-commands")), Is.EqualTo(new[] { "--stop" }));
        }

        [UnityTest]
        public IEnumerator OneHilStopClickDuringStartWaitsAndSubmitsExactlyOnce()
        {
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            Directory.CreateDirectory(Path.Combine(root, "tools/hil"));
            Directory.CreateDirectory(Path.Combine(root, ".local/hil"));
            File.WriteAllText(Path.Combine(root, "tools/hil/cli.py"),
                "import json,pathlib,sys,time\nr=pathlib.Path(__file__).resolve().parents[2]\na=sys.argv[1]\n"+
                "with (r/'commands').open('a') as f:f.write(a+'\\n')\n"+
                "if a=='start':time.sleep(.4)\n"+
                "s={'phase':'running' if a=='start' else 'stopped','session_alive':a=='start','run_id':'fixture'}\n"+
                "(r/'.local/hil/state.json').write_text(json.dumps(s))\nprint(json.dumps(s))\n");
            var type = Ui("HilSimulationUi");
            var profile = HilSimulationLauncher.Profiles.First();
            type.GetMethod("StartProfile").Invoke(null, new object[] { profile, profile.algorithm,
                new[] { "Radar", "RTK", "Camera", "Imu" }, null });
            Assert.That(Flag(type, "IsBusy"), Is.True);
            type.GetMethod("Stop").Invoke(null, new object[] { null });
            type.GetMethod("Stop").Invoke(null, new object[] { null });
            Assert.That(Flag(type, "IsStopPending"), Is.True);
            float deadline=Time.realtimeSinceStartup+8;
            while (Flag(type, "IsBusy") && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(Flag(type, "IsBusy"), Is.False);
            Assert.That(File.ReadAllLines(Path.Combine(root, "commands")), Is.EqualTo(new[] { "start", "stop" }));
            Assert.That(HilSimulationLauncher.ReadStatus().phase, Is.EqualTo("stopped"));
        }
    }
}
