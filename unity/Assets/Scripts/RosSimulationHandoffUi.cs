using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Agribot.VehicleConfiguration
{
    public sealed class RosSimulationHandoffUi : MonoBehaviour
    {
        private static RosSimulationHandoffUi instance;
        private Button stopButton;
        private Button exportButton;
        private Action<string> showMessage;
        private string exportPath;
        private bool busy;
        private bool stopPending;
        private bool canExport;
        public static event Action AvailabilityChanged;
        public static bool IsBusy => instance != null && (instance.busy || instance.stopPending);
        public static bool IsStopPending => instance != null && instance.stopPending;
        public static bool CanExport => instance != null && !IsBusy && instance.canExport && !HilSimulationLauncher.HasSession &&
            !string.IsNullOrEmpty(instance.exportPath);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            GameObject root = new GameObject("ROS_Simulation_Handoff");
            instance = root.AddComponent<RosSimulationHandoffUi>();
            DontDestroyOnLoad(root);
            instance.exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "unity_exports", "ackermann_physical_runtime.json");
        }

        public static void SimulationStarted(string path)
        {
            Create();
            AssistantConversation.RecordUi("仿真启动任务已提交，尚不代表导航就绪。", state => state.simulation = "launch_requested");
            instance.exportPath = path;
            instance.canExport = false;
            instance.RefreshButtons();
        }

        public static void Bind(Button stop, Button export, Action<string> notification)
        {
            Create();
            instance.RemoveListeners();
            instance.stopButton = stop;
            instance.exportButton = export;
            instance.showMessage = notification;
            stop.onClick.AddListener(instance.StopSimulation);
            if (export != null) export.onClick.AddListener(instance.ExportConfiguration);
            instance.RefreshButtons();
        }

        public static void Unbind(Button stop, Button export)
        {
            if (instance == null || instance.stopButton != stop || instance.exportButton != export) return;
            instance.RemoveListeners();
            instance.stopButton = instance.exportButton = null;
            instance.showMessage = null;
        }

        private void RemoveListeners()
        {
            if (stopButton != null) stopButton.onClick.RemoveListener(StopSimulation);
            if (exportButton != null) exportButton.onClick.RemoveListener(ExportConfiguration);
        }

        private void StopSimulation()
        {
            if (stopPending) return;
            stopPending = true;
            RefreshButtons();
            StartCoroutine(StopWhenReady());
        }

        private IEnumerator StopWhenReady()
        {
            try
            {
                while (busy) yield return null;
                if (HilSimulationLauncher.HasSession || HilSimulationUi.IsBusy)
                {
                    HilSimulationUi.Stop(showMessage);
                    while (HilSimulationUi.IsStopPending) yield return null;
                }
                else yield return Execute(false);
            }
            finally
            {
                stopPending = false;
                RefreshButtons();
            }
        }
        public static void RequestStop(Action<string> notification)
        {
            Create(); instance.showMessage = notification; instance.StopSimulation();
        }
        public static void RequestExport(Action<string> notification)
        {
            Create(); instance.showMessage = notification;
            if (!CanExport) { notification?.Invoke("请先完成仿真验证并停止仿真，再导出真机配置。"); return; }
            instance.ExportConfiguration();
        }
        public static void RequestDraftExport(IEnumerator routine)
        {
            Create();
            if (!IsBusy) instance.StartCoroutine(instance.ExecuteDraftRoutine(routine));
        }

        private IEnumerator ExecuteDraftRoutine(IEnumerator routine)
        {
            busy = true;
            RefreshButtons();
            try { yield return routine; }
            finally
            {
                busy = false;
                RefreshButtons();
            }
        }
        private void ExportConfiguration() => StartCoroutine(Execute(true));

        private void RefreshButtons()
        {
            if (stopButton != null) stopButton.interactable = !busy && !stopPending;
            if (exportButton != null) exportButton.interactable = !busy && !stopPending && canExport && !string.IsNullOrEmpty(exportPath);
            AvailabilityChanged?.Invoke();
        }

        private IEnumerator Execute(bool export)
        {
            if (busy || (export && !canExport)) yield break;
            busy = true;
            RefreshButtons();
            showMessage?.Invoke(export ? "正在导出真机配置" : "正在停止仿真");
            AssistantConversation.RecordUi(export ? "用户请求导出真机配置" : "用户请求停止仿真");
            Task<string> task = Task.Run(() => RosSimulationLauncher.RunManagedCommand(export, exportPath));
            while (!task.IsCompleted) yield return null;
            bool success = !task.IsFaulted && !task.IsCanceled;
            AssistantConversation.RecordUi(success ? (export ? "真机配置已导出，仍需人工部署" : "仿真已停止") : "停止或导出操作失败",
                state => { if (success && !export) state.simulation = "stopped"; });
            showMessage?.Invoke(success ? (export ? "已导出配置和6750固件，请人工烧录" : "仿真已停止") : "操作失败，请查看日志");
            if (success && export)
            {
                Debug.Log("真机配置已导出：" + exportPath);
                if (!Application.isBatchMode)
                    Application.OpenURL(new Uri(Path.GetDirectoryName(exportPath)).AbsoluteUri);
            }
            else if (!success) Debug.LogError(task.Exception?.GetBaseException().Message);
            busy = false;
            canExport = success;
            RefreshButtons();
        }
    }
}
