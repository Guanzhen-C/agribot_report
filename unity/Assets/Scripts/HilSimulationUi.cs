using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public sealed partial class HilSimulationUi : MonoBehaviour
    {
        private static HilSimulationUi instance;
        private bool busy;
        private bool stopRequested, lastCommandSucceeded;
        private string observed;
        public static bool IsBusy => instance != null && (instance.busy || instance.stopRequested);
        public static bool IsStopPending => instance != null && instance.stopRequested;
        public static event Action Changed;

        private static HilSimulationUi Ensure()
        {
            if (instance != null) return instance;
            var root = new GameObject("HIL_Simulation_Handoff");
            instance = root.AddComponent<HilSimulationUi>();
            DontDestroyOnLoad(root);
            return instance;
        }

        public static void Observe() => Ensure();
        public static void StartProfile(HilProfile profile, string algorithm, string[] sensors, Action<string> notification)
        {
            HilSimulationLauncher.ValidateSelection(profile?.id, algorithm, sensors);
            var owner = Ensure();
            if (!owner.busy && !owner.stopRequested) owner.StartCoroutine(owner.Execute(false, profile, notification, algorithm, sensors));
        }

        public static void Stop(Action<string> notification)
        {
            var owner = Ensure();
            if (owner.stopRequested) return;
            owner.stopRequested = true;
            Changed?.Invoke();
            owner.StartCoroutine(owner.StopWhenReady(notification));
        }

        private IEnumerator StopWhenReady(Action<string> notify)
        {
            try
            {
                while (busy) yield return null;
                yield return Execute(true, null, notify);
                if (!lastCommandSucceeded) yield break;
                float deadline = Time.realtimeSinceStartup + 360;
                while (true)
                {
                    var status = HilSimulationLauncher.ReadStatus();
                    if (status.phase == "stopped" && !status.session_alive)
                    { notify?.Invoke("HIL仿真已停止"); break; }
                    if (status.phase == "failed")
                    { notify?.Invoke("停止HIL未完成：" + status.message); break; }
                    if (Time.realtimeSinceStartup >= deadline)
                    { notify?.Invoke("尚未确认HIL完全停止，请查看运行日志；未强行中断烧录。"); break; }
                    yield return new WaitForSecondsRealtime(.2f);
                }
            }
            finally
            {
                stopRequested = false;
                Changed?.Invoke();
            }
        }

        private IEnumerator Execute(bool stop, HilProfile profile, Action<string> notify, string algorithm = null, string[] sensors = null)
        {
            busy = true;
            lastCommandSucceeded = false;
            Changed?.Invoke();
            // All Unity API access and recipe validation stays on the main thread.
            string id = profile?.id;
            AssistantConversation.RecordUi(stop ? "请求停止HIL仿真" : "请求启动HIL仿真：" + profile.name,
                state => state.simulation = stop ? "hil_stop_requested" : "hil_launch_requested");
            var task = Task.Run(() => HilSimulationLauncher.Command(stop, id, algorithm, sensors));
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted || task.IsCanceled)
            {
                string error = task.Exception?.GetBaseException().Message ?? "HIL操作已取消";
                AssistantConversation.RecordUi("HIL请求未完成：" + error);
                notify?.Invoke(error);
            }
            else
            {
                lastCommandSucceeded = true;
                notify?.Invoke(stop ? "正在停止HIL仿真；烧录期间会等待校验完成" : "正在准备HIL仿真");
            }
            busy = false;
            Changed?.Invoke();
        }

        private IEnumerator Start()
        {
            while (true)
            {
                var status = HilSimulationLauncher.ReadStatus();
                RefreshFlashProgress(status);
                string key = status.run_id + ":" + status.phase;
                if (key != observed)
                {
                    observed = key;
                    if (!string.IsNullOrEmpty(status.run_id))
                        AssistantConversation.RecordUi("HIL状态：" + status.message,
                            state => { state.simulation = "hil_" + status.phase;
                                state.hilProfile = status.profile; state.hilStatus = status.phase; });
                    Changed?.Invoke();
                }
                yield return new WaitForSecondsRealtime(.5f);
            }
        }
    }
}
