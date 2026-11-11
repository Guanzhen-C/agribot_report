using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    [Serializable] public sealed class HilProfile
    {
        public string id, car, implement, scene, name, firmware, algorithm, route, runtime, transmission;
        public string unityType;
        public int domain, gazeboPort;
    }

    [Serializable] public sealed class HilRunStatus
    {
        public string phase, profile, message, run_id;
        public bool session_alive;
        public bool Active => !string.IsNullOrEmpty(phase) && phase != "stopped" && phase != "completed" && phase != "failed";
    }

    public static class HilSimulationLauncher
    {
        [Serializable] private sealed class Catalog { public int schemaVersion; public HilProfile[] profiles; }
        private static HilProfile[] profiles;
        public static HilProfile[] Profiles
        {
            get
            {
                if (profiles != null) return profiles;
                var resource = Resources.Load<TextAsset>("hil_profiles");
                if (resource == null) throw new InvalidOperationException("缺少HIL方案目录。");
                var catalog = JsonUtility.FromJson<Catalog>(resource.text);
                if (catalog.schemaVersion != 1 || catalog.profiles == null) throw new InvalidDataException("HIL方案目录无效。");
                return profiles = catalog.profiles;
            }
        }

        public static HilProfile Match(string car, string implement, string scene, string transmission = "cvt_auto")
        {
            return Profiles.FirstOrDefault(item => item.car == car && item.implement == implement &&
                item.scene == scene && (item.car != "Car4" || item.transmission == transmission));
        }

        public static string Repository => Environment.GetEnvironmentVariable("AGRIBOT_REPORT_ROOT") ??
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string StatusPath => Path.Combine(Repository, ".local", "hil", "state.json");
        public static bool HasSession
        {
            get { var status = ReadStatus(); return status.session_alive || status.Active; }
        }

        public static HilRunStatus ReadStatus()
        {
            try
            {
                return File.Exists(StatusPath) ? JsonUtility.FromJson<HilRunStatus>(File.ReadAllText(StatusPath)) :
                    new HilRunStatus { phase = "stopped" };
            }
            catch (IOException) { return new HilRunStatus { phase = "unknown", message = "正在读取HIL状态" }; }
            catch (ArgumentException) { return new HilRunStatus { phase = "unknown", message = "正在读取HIL状态" }; }
        }

        public static string Command(bool stop, string profile = null, string algorithm = null)
        {
            if (Application.platform != RuntimePlatform.LinuxEditor && Application.platform != RuntimePlatform.LinuxPlayer)
                throw new PlatformNotSupportedException("硬件在环启动需要Ubuntu 22.04仿真工作站。");
            if (!stop && !Profiles.Any(item => item.id == profile)) throw new ArgumentException("不支持的HIL方案。");
            var script = Path.Combine(Repository, "tools", "hil", "cli.py");
            if (!File.Exists(script)) throw new FileNotFoundException("HIL启动程序尚未安装。", script);
            var info = new ProcessStartInfo("/usr/bin/python3")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true, WorkingDirectory = Repository
            };
            info.ArgumentList.Add(script);
            info.ArgumentList.Add(stop ? "stop" : "start");
            if (!stop)
            {
                info.ArgumentList.Add("--profile"); info.ArgumentList.Add(profile);
                if (!string.IsNullOrWhiteSpace(algorithm))
                { info.ArgumentList.Add("--algorithm"); info.ArgumentList.Add(algorithm); }
            }
            using var process = Process.Start(info) ?? throw new InvalidOperationException("无法创建HIL启动进程。");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                process.Kill();
                throw new TimeoutException("HIL启动接口响应超时，请查看运行状态。");
            }
            if (process.ExitCode != 0) throw new InvalidOperationException(error.GetAwaiter().GetResult().Trim());
            return output.GetAwaiter().GetResult().Trim();
        }
    }
}
