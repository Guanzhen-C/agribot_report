using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    [Serializable] public sealed class HilRecordingStatus
    {
        public string phase, message, run_id, campaign;
    }

    public static class HilRecordingLauncher
    {
        public static string RecordsPath => Path.Combine(HilSimulationLauncher.Repository, ".local", "hil", "recordings", "campaigns");

        public static HilRecordingStatus ReadStatus(string run)
        {
            if (string.IsNullOrEmpty(run) || !Regex.IsMatch(run, "^[A-Za-z0-9_]{1,80}$"))
                return new HilRecordingStatus();
            var path = Path.Combine(HilSimulationLauncher.Repository, ".local", "hil", "recordings", "runs", run, "status.json");
            try
            {
                return File.Exists(path) ? JsonUtility.FromJson<HilRecordingStatus>(File.ReadAllText(path)) : new HilRecordingStatus();
            }
            catch (IOException) { return new HilRecordingStatus { message = "正在读取自动记录状态" }; }
            catch (ArgumentException) { return new HilRecordingStatus { message = "自动记录状态暂不可读" }; }
        }

        // This process can only attach passive readers to the existing run.
        // No start/stop/flash or ROS control action is exposed by this interface.
        public static string Attach(string repository, string run, string profile)
        {
            if (string.IsNullOrEmpty(run) || !Regex.IsMatch(run, "^[A-Za-z0-9_]{1,80}$"))
                throw new ArgumentException("HIL未返回有效运行编号，无法绑定自动记录。");
            var script = Path.Combine(repository, "tools", "hil_recording", "service.py");
            if (!File.Exists(script)) throw new FileNotFoundException("自动记录组件尚未安装。", script);
            var info = new ProcessStartInfo("/usr/bin/python3")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true, WorkingDirectory = repository
            };
            foreach (var value in new[] { script, "attach", "--run", run, "--profile", profile }) info.ArgumentList.Add(value);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("无法启动自动记录组件。");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                process.Kill();
                throw new TimeoutException("自动记录接口响应超时；HIL任务未被中断，请查看测试记录目录。");
            }
            if (process.ExitCode != 0) throw new InvalidOperationException(error.GetAwaiter().GetResult().Trim());
            return output.GetAwaiter().GetResult().Trim();
        }

        public static void OpenRecords()
        {
            Directory.CreateDirectory(RecordsPath);
            Application.OpenURL(new Uri(RecordsPath + Path.DirectorySeparatorChar).AbsoluteUri);
        }
    }
}
