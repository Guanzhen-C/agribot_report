using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public sealed class HilSimulationUi : MonoBehaviour
    {
        private static HilSimulationUi instance;
        private bool busy;
        private string observed;
        public static bool IsBusy => instance != null && instance.busy;
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
        public static void StartProfile(HilProfile profile, Action<string> notification)
        {
            var owner = Ensure();
            if (!owner.busy) owner.StartCoroutine(owner.Execute(false, profile, notification));
        }

        public static void Stop(Action<string> notification)
        {
            var owner = Ensure();
            if (!owner.busy) owner.StartCoroutine(owner.Execute(true, null, notification));
        }

        private IEnumerator Execute(bool stop, HilProfile profile, Action<string> notify)
        {
            busy = true;
            Changed?.Invoke();
            // All Unity API access and recipe validation stays on the main thread.
            string id = profile?.id;
            AssistantConversation.RecordUi(stop ? "请求停止HIL仿真" : "请求启动HIL仿真：" + profile.name,
                state => state.simulation = stop ? "hil_stop_requested" : "hil_launch_requested");
            var task = Task.Run(() => HilSimulationLauncher.Command(stop, id));
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted || task.IsCanceled)
            {
                string error = task.Exception?.GetBaseException().Message ?? "HIL操作已取消";
                AssistantConversation.RecordUi("HIL请求未完成：" + error);
                notify?.Invoke(error);
            }
            else notify?.Invoke(stop ? "已提交停止请求" : "正在准备HIL仿真");
            busy = false;
            Changed?.Invoke();
        }

        private IEnumerator Start()
        {
            while (true)
            {
                var status = HilSimulationLauncher.ReadStatus();
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
