using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public enum RosSimulationComponentKind
    {
        Perception,
        VisualPerception,
        Localization,
        Planner,
        Controller
    }

    public sealed class RosSimulationComponentOption
    {
        public RosSimulationComponentKind Kind { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public string ShortName { get; }
        public string Description { get; }
        public string InterfaceContract { get; }

        public RosSimulationComponentOption(
            RosSimulationComponentKind kind,
            string id,
            string displayName,
            string shortName,
            string description,
            string interfaceContract)
        {
            Kind = kind;
            Id = id;
            DisplayName = displayName;
            ShortName = shortName;
            Description = description;
            InterfaceContract = interfaceContract;
        }
    }

    public sealed class RosSimulationAlgorithmProfile
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string Perception { get; }
        public string VisualPerception { get; }
        public string Localization { get; }
        public string Planner { get; }
        public string Controller { get; }
        public string RouteMode { get; }
        public RosSimulationComponentOption PerceptionComponent { get; }
        public RosSimulationComponentOption VisualPerceptionComponent { get; }
        public RosSimulationComponentOption LocalizationComponent { get; }
        public RosSimulationComponentOption PlannerComponent { get; }
        public RosSimulationComponentOption ControllerComponent { get; }

        public RosSimulationAlgorithmProfile(
            RosSimulationComponentOption perception,
            RosSimulationComponentOption visualPerception,
            RosSimulationComponentOption localization,
            RosSimulationComponentOption planner,
            RosSimulationComponentOption controller)
        {
            PerceptionComponent = perception;
            VisualPerceptionComponent = visualPerception;
            LocalizationComponent = localization;
            PlannerComponent = planner;
            ControllerComponent = controller;
            Id =
                $"{perception.Id}_{visualPerception.Id}_{localization.Id}_" +
                $"{planner.Id}_{controller.Id}";
            DisplayName =
                $"{visualPerception.ShortName} · {localization.DisplayName} · " +
                $"{planner.DisplayName} · {controller.DisplayName}";
            Description =
                "几何感知、视觉感知、定位、规划和控制通过统一 ROS 接口连接；" +
                "视觉输出独立展示，不写入代价地图或运动控制链。";
            Perception = perception.DisplayName;
            VisualPerception = visualPerception.DisplayName;
            Localization = localization.DisplayName;
            Planner = planner.DisplayName;
            Controller = controller.DisplayName;
            RouteMode = planner.Id == "direct" ? "必经点直线路径" : "连续全局路径";
        }
    }

    public sealed class RosSimulationLaunchResult
    {
        public int ProcessId { get; }
        public string ScriptPath { get; }
        public string ExportDirectory { get; }
        public RosSimulationAlgorithmProfile Algorithm { get; }

        public RosSimulationLaunchResult(
            int processId,
            string scriptPath,
            string exportDirectory,
            RosSimulationAlgorithmProfile algorithm)
        {
            ProcessId = processId;
            ScriptPath = scriptPath;
            ExportDirectory = exportDirectory;
            Algorithm = algorithm;
        }
    }

    /// <summary>
    /// Presents independent geometry perception, visual perception, localization,
    /// planner and controller components behind stable ROS interfaces.
    /// </summary>
    public static class RosSimulationLauncher
    {
        public const string DefaultPerceptionId = "stvl";
        public const string DefaultVisualPerceptionId = "off";
        public const string DefaultLocalizationId = "fastlivo_rtk";
        public const string DefaultPlannerId = "smac_hybrid";
        public const string DefaultControllerId = "mppi";
        public const string DefaultAlgorithmId =
            "stvl_off_fastlivo_rtk_smac_hybrid_mppi";

        private static readonly RosSimulationComponentOption[] PerceptionOptions =
        {
            Component(
                RosSimulationComponentKind.Perception,
                "stvl",
                "STVL 时空体素层",
                "STVL",
                "维护带时间衰减的三维障碍体素，适合动态果园环境。",
                "输入 sensor_msgs/PointCloud2；输出 Nav2 Costmap Layer。"),
            Component(
                RosSimulationComponentKind.Perception,
                "voxel",
                "Nav2 三维体素层",
                "Voxel",
                "使用 Nav2 官方 VoxelLayer 对 C16 点云进行三维标记和清除。",
                "输入 sensor_msgs/PointCloud2；输出 Nav2 Costmap Layer。")
        };

        private static readonly RosSimulationComponentOption[]
            VisualPerceptionOptions =
        {
            Component(
                RosSimulationComponentKind.VisualPerception,
                "off",
                "关闭额外视觉感知",
                "关闭",
                "保留相机给 FAST-LIVO2 使用，但不启动额外视觉处理节点。",
                "不发布额外视觉话题。"),
            Component(
                RosSimulationComponentKind.VisualPerception,
                "object_detection",
                "AI 目标检测",
                "目标检测",
                "识别画面中的目标类别、置信度和二维边界框。",
                "输入 sensor_msgs/Image；输出标注图和 /vision/results。"),
            Component(
                RosSimulationComponentKind.VisualPerception,
                "instance_segmentation",
                "AI 实例分割",
                "实例分割",
                "区分各个目标实例并生成像素级掩膜。",
                "输入 sensor_msgs/Image；输出实例掩膜、边界框和标注图。"),
            Component(
                RosSimulationComponentKind.VisualPerception,
                "pose_estimation",
                "AI 人体姿态估计",
                "姿态估计",
                "检测人员并估计人体关键点骨架。",
                "输入 sensor_msgs/Image；输出人体关键点、边界框和标注图。")
        };

        private static readonly RosSimulationComponentOption[] LocalizationOptions =
        {
            Component(
                RosSimulationComponentKind.Localization,
                "fastlivo_rtk",
                "FAST-LIVO2 + RTK",
                "LIVO+RTK",
                "融合相机、C16、IMU 与固定解 RTK，作为真机同构定位方案。",
                "输出 nav_msgs/Odometry 和 map/odom/base_link TF。"),
            Component(
                RosSimulationComponentKind.Localization,
                "fast_lio",
                "FAST-LIO2",
                "FAST-LIO2",
                "使用 C16 与 IMU 的激光惯性里程计。",
                "输出 nav_msgs/Odometry 和 map/odom/base_link TF。"),
            Component(
                RosSimulationComponentKind.Localization,
                "navsat",
                "NavSat ESKF",
                "NavSat",
                "使用 RTK 与 IMU 的 ESKF 组合导航解。",
                "输出 nav_msgs/Odometry 和 map/odom/base_link TF。"),
            Component(
                RosSimulationComponentKind.Localization,
                "kiss_icp",
                "KISS-ICP",
                "KISS-ICP",
                "轻量级纯激光里程计，用于资源与精度对照。",
                "输出 nav_msgs/Odometry 和 map/odom/base_link TF。")
        };

        private static readonly RosSimulationComponentOption[] PlannerOptions =
        {
            Component(
                RosSimulationComponentKind.Planner,
                "smac_hybrid",
                "Smac Hybrid-A*",
                "Hybrid-A*",
                "考虑阿克曼运动学和最小转弯半径的状态空间规划。",
                "Nav2 Planner 接口：输入目标位姿，输出 nav_msgs/Path。"),
            Component(
                RosSimulationComponentKind.Planner,
                "navfn",
                "NavFn Dijkstra",
                "NavFn",
                "经典二维栅格 Dijkstra 最短路径规划。",
                "Nav2 Planner 接口：输入目标位姿，输出 nav_msgs/Path。"),
            Component(
                RosSimulationComponentKind.Planner,
                "theta_star",
                "Theta*",
                "Theta*",
                "允许任意角连线的二维栅格规划。",
                "Nav2 Planner 接口：输入目标位姿，输出 nav_msgs/Path。"),
            Component(
                RosSimulationComponentKind.Planner,
                "smac_2d",
                "Smac 2D A*",
                "Smac 2D",
                "使用 Smac 二维 A* 与内置路径平滑器。",
                "Nav2 Planner 接口：输入目标位姿，输出 nav_msgs/Path。"),
            Component(
                RosSimulationComponentKind.Planner,
                "direct",
                "必经点直连",
                "直连",
                "按顺序连接全部预设必经点，用作不经过全局搜索的基线。",
                "统一输出 nav_msgs/Path。")
        };

        private static readonly RosSimulationComponentOption[] ControllerOptions =
        {
            Component(
                RosSimulationComponentKind.Controller,
                "mppi",
                "MPPI",
                "MPPI",
                "采样式模型预测控制，结合局部代价地图实时避障。",
                "Nav2 Controller 接口：输入 nav_msgs/Path，输出 geometry_msgs/Twist。"),
            Component(
                RosSimulationComponentKind.Controller,
                "rpp",
                "Regulated Pure Pursuit",
                "RPP",
                "按曲率和障碍代价调速的纯跟踪控制器。",
                "Nav2 Controller 接口：输入 nav_msgs/Path，输出 geometry_msgs/Twist。"),
            Component(
                RosSimulationComponentKind.Controller,
                "dwb",
                "DWB",
                "DWB",
                "动态窗口轨迹采样与多评价函数控制。",
                "Nav2 Controller 接口：输入 nav_msgs/Path，输出 geometry_msgs/Twist。")
        };

        private static readonly RosSimulationAlgorithmProfile[] AlgorithmProfiles =
            BuildAlgorithmProfiles();

        private static readonly Dictionary<string, string> LegacyAlgorithmAliases =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["fastlivo_rtk_mppi"] = "stvl_off_fastlivo_rtk_smac_hybrid_mppi",
                ["fastlio_mppi"] = "stvl_off_fast_lio_smac_hybrid_mppi",
                ["navsat_mppi"] = "stvl_off_navsat_smac_hybrid_mppi",
                ["fastlivo_rtk_rpp"] = "stvl_off_fastlivo_rtk_smac_hybrid_rpp",
                ["fastlio_rpp"] = "stvl_off_fast_lio_smac_hybrid_rpp",
                ["navsat_rpp"] = "stvl_off_navsat_smac_hybrid_rpp",
                ["kiss_icp_mppi"] = "stvl_off_kiss_icp_smac_hybrid_mppi",
                ["fastlivo_rtk_navfn_mppi"] = "stvl_off_fastlivo_rtk_navfn_mppi",
                ["fastlivo_rtk_theta_mppi"] = "stvl_off_fastlivo_rtk_theta_star_mppi",
                ["fastlivo_rtk_smac2d_mppi"] = "stvl_off_fastlivo_rtk_smac_2d_mppi",
                ["fastlivo_rtk_navfn_dwb"] = "stvl_off_fastlivo_rtk_navfn_dwb",
                ["fastlivo_rtk_direct_mppi"] = "stvl_off_fastlivo_rtk_direct_mppi"
            };

        private const string ScriptEnvironmentVariable = "AGRIBOT_ROS_SYNC_SCRIPT";
        private static string DefaultScriptPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "agribot_ws/ros2_ws/src/agribot_vehicle_description/scripts/unity_sync_and_simulate.sh");

        public static IReadOnlyList<RosSimulationComponentOption> Perceptions =>
            PerceptionOptions;
        public static IReadOnlyList<RosSimulationComponentOption> VisualPerceptions =>
            VisualPerceptionOptions;
        public static IReadOnlyList<RosSimulationComponentOption> Localizations =>
            LocalizationOptions;
        public static IReadOnlyList<RosSimulationComponentOption> Planners =>
            PlannerOptions;
        public static IReadOnlyList<RosSimulationComponentOption> Controllers =>
            ControllerOptions;
        public static IReadOnlyList<RosSimulationAlgorithmProfile> Algorithms =>
            AlgorithmProfiles;
        public static int CombinationCount => AlgorithmProfiles.Length;

        public static RosSimulationAlgorithmProfile Compose(
            string perceptionId,
            string visualPerceptionId,
            string localizationId,
            string plannerId,
            string controllerId)
        {
            RosSimulationComponentOption perception = FindComponent(
                PerceptionOptions, perceptionId, "几何感知");
            RosSimulationComponentOption visualPerception = FindComponent(
                VisualPerceptionOptions, visualPerceptionId, "视觉感知");
            RosSimulationComponentOption localization = FindComponent(
                LocalizationOptions, localizationId, "定位");
            RosSimulationComponentOption planner = FindComponent(
                PlannerOptions, plannerId, "规划");
            RosSimulationComponentOption controller = FindComponent(
                ControllerOptions, controllerId, "控制");
            return new RosSimulationAlgorithmProfile(
                perception, visualPerception, localization, planner, controller);
        }

        public static RosSimulationAlgorithmProfile Compose(
            string perceptionId,
            string localizationId,
            string plannerId,
            string controllerId)
        {
            return Compose(
                perceptionId,
                DefaultVisualPerceptionId,
                localizationId,
                plannerId,
                controllerId);
        }

        public static RosSimulationAlgorithmProfile FindAlgorithm(string algorithmId)
        {
            if (LegacyAlgorithmAliases.TryGetValue(algorithmId, out string canonicalId))
                algorithmId = canonicalId;
            foreach (RosSimulationAlgorithmProfile profile in AlgorithmProfiles)
            {
                if (string.Equals(profile.Id, algorithmId, StringComparison.Ordinal))
                    return profile;
            }
            foreach (RosSimulationAlgorithmProfile profile in AlgorithmProfiles)
            {
                string previousId =
                    $"{profile.PerceptionComponent.Id}_" +
                    $"{profile.LocalizationComponent.Id}_" +
                    $"{profile.PlannerComponent.Id}_" +
                    profile.ControllerComponent.Id;
                if (profile.VisualPerceptionComponent.Id == "off" &&
                    string.Equals(previousId, algorithmId, StringComparison.Ordinal))
                    return profile;
            }
            throw new ArgumentException(
                $"未知 ROS 仿真算法组合：{algorithmId}", nameof(algorithmId));
        }

        public static RosSimulationLaunchResult Start(string jsonPath)
        {
            return Start(jsonPath, FindAlgorithm(DefaultAlgorithmId));
        }

        public static RosSimulationLaunchResult Start(
            string jsonPath,
            string algorithmId)
        {
            return Start(jsonPath, FindAlgorithm(algorithmId));
        }

        public static RosSimulationLaunchResult Start(
            string jsonPath,
            string perceptionId,
            string visualPerceptionId,
            string localizationId,
            string plannerId,
            string controllerId)
        {
            return Start(
                jsonPath,
                Compose(
                    perceptionId,
                    visualPerceptionId,
                    localizationId,
                    plannerId,
                    controllerId));
        }

        public static RosSimulationLaunchResult Start(
            string jsonPath,
            RosSimulationAlgorithmProfile algorithm)
        {
            if (Application.platform != RuntimePlatform.LinuxEditor &&
                Application.platform != RuntimePlatform.LinuxPlayer)
            {
                throw new PlatformNotSupportedException(
                    "ROS 一键仿真当前仅支持 218 Ubuntu 工作站。它不在手机端执行。");
            }
            if (string.IsNullOrWhiteSpace(jsonPath))
                throw new ArgumentException("导出的车辆配置路径为空。", nameof(jsonPath));
            if (algorithm == null)
                throw new ArgumentNullException(nameof(algorithm));

            string fullJsonPath = Path.GetFullPath(jsonPath);
            string exportDirectory = Path.GetDirectoryName(fullJsonPath);
            if (string.IsNullOrEmpty(exportDirectory) || !File.Exists(fullJsonPath))
                throw new FileNotFoundException("没有找到导出的车辆配置。", fullJsonPath);

            string scriptPath = Environment.GetEnvironmentVariable(
                ScriptEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(scriptPath))
                scriptPath = DefaultScriptPath;
            scriptPath = Path.GetFullPath(scriptPath);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException(
                    $"没有找到 ROS 同步脚本。可通过 {ScriptEnvironmentVariable} 指定路径。",
                    scriptPath);
            }

            Process process = Process.Start(BuildStartInfo(
                scriptPath, exportDirectory, algorithm));
            if (process == null)
                throw new InvalidOperationException("ROS 同步进程没有成功启动。");

            UnityEngine.Debug.Log(
                $"Unity-to-ROS 同步任务已启动：PID={process.Id}, " +
                $"algorithm={algorithm.Id}, export={exportDirectory}");
            return new RosSimulationLaunchResult(
                process.Id, scriptPath, exportDirectory, algorithm);
        }

        private static RosSimulationComponentOption Component(
            RosSimulationComponentKind kind,
            string id,
            string displayName,
            string shortName,
            string description,
            string interfaceContract)
        {
            return new RosSimulationComponentOption(
                kind, id, displayName, shortName, description, interfaceContract);
        }

        private static RosSimulationComponentOption FindComponent(
            IReadOnlyList<RosSimulationComponentOption> options,
            string id,
            string groupName)
        {
            foreach (RosSimulationComponentOption option in options)
            {
                if (string.Equals(option.Id, id, StringComparison.Ordinal))
                    return option;
            }
            throw new ArgumentException($"未知{groupName}组件：{id}");
        }

        private static RosSimulationAlgorithmProfile[] BuildAlgorithmProfiles()
        {
            List<RosSimulationAlgorithmProfile> profiles =
                new List<RosSimulationAlgorithmProfile>(480);
            foreach (RosSimulationComponentOption perception in PerceptionOptions)
            foreach (RosSimulationComponentOption visualPerception in
                     VisualPerceptionOptions)
            foreach (RosSimulationComponentOption localization in LocalizationOptions)
            foreach (RosSimulationComponentOption planner in PlannerOptions)
            foreach (RosSimulationComponentOption controller in ControllerOptions)
            {
                profiles.Add(new RosSimulationAlgorithmProfile(
                    perception,
                    visualPerception,
                    localization,
                    planner,
                    controller));
            }
            return profiles.ToArray();
        }

        private static ProcessStartInfo BuildStartInfo(
            string scriptPath,
            string exportDirectory,
            RosSimulationAlgorithmProfile algorithm)
        {
            if (!FarmScenarioCatalog.HasSelection || FarmScenarioCatalog.Selected == null)
                throw new InvalidOperationException("请先选择作业场景。");
            if (FarmScenarioCatalog.Selected.imported && !File.Exists(FarmScenarioCatalog.Selected.sceneManifest))
                throw new InvalidOperationException("导入场景文件已不存在，请重新导入或选择其他场景。");
            string arguments =
                QuoteProcessArgument(scriptPath) +
                " --export-dir " + QuoteProcessArgument(exportDirectory) +
                " --run-waypoints false" +
                " --scenario " + QuoteProcessArgument(FarmScenarioCatalog.Selected.imported ? "orchard" : FarmScenarioCatalog.SelectedId) +
                " --perception " + QuoteProcessArgument(algorithm.PerceptionComponent.Id) +
                " --vision " +
                QuoteProcessArgument(algorithm.VisualPerceptionComponent.Id) +
                " --localization " + QuoteProcessArgument(algorithm.LocalizationComponent.Id) +
                " --planner " + QuoteProcessArgument(algorithm.PlannerComponent.Id) +
                " --controller " + QuoteProcessArgument(algorithm.ControllerComponent.Id);
            if (FarmScenarioCatalog.Selected.imported)
                arguments += " --scene-manifest " + QuoteProcessArgument(FarmScenarioCatalog.Selected.sceneManifest);
            const string terminal = "/usr/bin/gnome-terminal";

            ProcessStartInfo result;
            if (File.Exists(terminal))
            {
                result = new ProcessStartInfo
                {
                    FileName = terminal,
                    Arguments = "-- /bin/bash " + arguments
                };
            }
            else
            {
                result = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = arguments,
                    CreateNoWindow = true
                };
            }

            result.UseShellExecute = false;
            result.WorkingDirectory = exportDirectory;
            return result;
        }

        private static string QuoteProcessArgument(string value)
        {
            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"") + "\"";
        }

        public static string RunManagedCommand(bool exportPhysical, string outputPath = null)
        {
            string script = Environment.GetEnvironmentVariable(ScriptEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(script)) script = DefaultScriptPath;
            if (!File.Exists(script)) throw new FileNotFoundException("没有找到 ROS 同步脚本。", script);
            string arguments = QuoteProcessArgument(script) + (exportPhysical
                ? " --export-physical " + QuoteProcessArgument(outputPath)
                : " --stop");
            using (Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }))
            {
                if (process == null) throw new InvalidOperationException("无法执行仿真管理命令。");
                var errorTask = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                string error = errorTask.GetAwaiter().GetResult();
                if (process.ExitCode != 0) throw new InvalidOperationException(error + output);
                return output;
            }
        }

        public static string ExportPhysicalDraft(string jsonPath, string outputDirectory)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("没有找到当前车辆配置。", jsonPath);
            string script = Environment.GetEnvironmentVariable(ScriptEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(script)) script = DefaultScriptPath;
            if (!File.Exists(script)) throw new FileNotFoundException("没有找到 ROS 同步脚本。", script);
            string arguments = QuoteProcessArgument(script) +
                " --export-dir " + QuoteProcessArgument(Path.GetDirectoryName(jsonPath)) +
                " --export-physical-draft " + QuoteProcessArgument(outputDirectory);
            using (Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }))
            {
                if (process == null) throw new InvalidOperationException("无法执行草案导出命令。");
                var errorTask = process.StandardError.ReadToEndAsync();
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                string error = errorTask.GetAwaiter().GetResult();
                if (process.ExitCode != 0) throw new InvalidOperationException(error + result);
                return result;
            }
        }

        public static string ReadManagedStatus()
        {
            string script = Environment.GetEnvironmentVariable(ScriptEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(script)) script = DefaultScriptPath;
            if (!File.Exists(script)) throw new FileNotFoundException("没有找到 ROS 同步脚本。", script);
            using (Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = QuoteProcessArgument(script) + " --status",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }))
            {
                if (process == null) throw new InvalidOperationException("无法读取仿真状态。");
                string result = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("读取仿真状态失败：" + process.StandardError.ReadToEnd());
                return result;
            }
        }
    }
}
