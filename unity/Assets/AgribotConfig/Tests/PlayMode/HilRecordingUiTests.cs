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
    public sealed class HilRecordingUiTests
    {
        private string previous, root;

        [SetUp]
        public void IsolateAllProcessesFromTheBench()
        {
            previous = Environment.GetEnvironmentVariable("AGRIBOT_REPORT_ROOT");
            root = Path.Combine(Path.GetTempPath(), "agribot-record-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("AGRIBOT_REPORT_ROOT", root);
        }

        [TearDown]
        public void RestoreEnvironment()
        {
            Environment.SetEnvironmentVariable("AGRIBOT_REPORT_ROOT", previous);
            Directory.Delete(root, true);
        }

        private static Type UiType => AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
            .Single(t => t.FullName == "Agribot.VehicleConfiguration.HilSimulationUi");

        [UnityTest]
        public IEnumerator FiveExistingStartsAttachTheirOwnPassiveRecordingWithoutExtraUserActions()
        {
            Directory.CreateDirectory(Path.Combine(root, "tools", "hil"));
            Directory.CreateDirectory(Path.Combine(root, "tools", "hil_recording"));
            File.WriteAllText(Path.Combine(root, "tools", "hil", "cli.py"),
                "import json,sys\np=sys.argv[sys.argv.index('--profile')+1]\n" +
                "print(json.dumps(dict(run_id='fixture_'+p,profile=p,phase='queued')))\n");
            File.WriteAllText(Path.Combine(root, "tools", "hil_recording", "service.py"),
                "import json,pathlib,sys\nassert sys.argv[1]=='attach'\n" +
                "p=sys.argv[sys.argv.index('--profile')+1]\nr=sys.argv[sys.argv.index('--run')+1]\n" +
                "assert r=='fixture_'+p\npathlib.Path('attached_'+p+'.json').write_text(json.dumps(dict(run=r,profile=p)))\n" +
                "print(json.dumps(dict(phase='queued')))\n");
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            foreach (var profile in HilSimulationLauncher.Profiles)
            {
                string notification = "";
                UiType.GetMethod("StartProfile").Invoke(null, new object[] { profile, profile.algorithm,
                    new[] { "Radar", "RTK", "Camera", "Imu" }, (Action<string>)(s => notification = s) });
                float deadline = Time.realtimeSinceStartup + 10;
                while ((bool)UiType.GetProperty("IsBusy").GetValue(null) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That((bool)UiType.GetProperty("IsBusy").GetValue(null), Is.False);
                Assert.That(File.Exists(Path.Combine(root, "attached_" + profile.id + ".json")), Is.True, profile.id);
                Assert.That(notification, Does.Contain("已启用本次工况自动记录"));
            }
        }

        [UnityTest]
        public IEnumerator RecordStatusIsReadableAndTheExistingSetupHasANonOverlappingRecordsLink()
        {
            Assert.That(HilRecordingLauncher.ReadStatus("../escape").phase, Is.Null);
            var folder = Path.Combine(root, ".local", "hil", "recordings", "runs", "fixture_run");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "status.json"),
                "{\"phase\":\"waiting_stop\",\"message\":\"观察已完成，请停止仿真\"}");
            Assert.That(HilRecordingLauncher.ReadStatus("fixture_run").phase, Is.EqualTo("waiting_stop"));
            yield return SceneManager.LoadSceneAsync("scene1");
            yield return null;
            var transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var records = (RectTransform)transforms.Single(t => t.name == "HilRecords");
            var close = (RectTransform)records.parent.Find("Close");
            var title = (RectTransform)records.parent.Find("SetupBrand");
            Assert.That(title.anchorMax.x, Is.LessThan(records.anchorMin.x));
            Assert.That(records.anchorMax.x, Is.LessThan(close.anchorMin.x));
            Assert.That(records.GetComponent<Button>(), Is.Not.Null);
            Assert.That(HilRecordingLauncher.RecordsPath, Does.StartWith(root));
        }
    }
}
