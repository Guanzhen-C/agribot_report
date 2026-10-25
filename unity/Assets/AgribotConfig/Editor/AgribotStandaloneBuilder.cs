using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Agribot.VehicleConfiguration.Editor
{
    public static class AgribotStandaloneBuilder
    {
        private const string OutputEnvironmentVariable = "AGRIBOT_STANDALONE_OUTPUT";
        private const string OutputDirectoryName = "AgriculturalCar_Interior_Linux";
        private const string ExecutableName = "AgribotConfigurator.x86_64";
        private const string LauncherName = "start_agribot_configurator.sh";
        private const string DesktopFileName = "agribot-configurator.desktop";

        [MenuItem("农机配置/构建 Linux 可执行程序 _F8")]
        public static void BuildFromMenu()
        {
            try
            {
                string executablePath = BuildLinuxStandalone();
                EditorUtility.RevealInFinder(executablePath);
                EditorUtility.DisplayDialog(
                    "构建完成",
                    $"程序已导出到：\n{executablePath}\n\n桌面快捷方式：农机配置平台",
                    "确定");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("构建失败", exception.Message, "确定");
                throw;
            }
        }

        // Entry point for Unity's -executeMethod option.
        public static void BuildFromCommandLine()
        {
            try
            {
                string executablePath = BuildLinuxStandalone();
                Debug.Log($"AGRIBOT_STANDALONE_BUILD_SUCCESS={executablePath}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(2);
            }
        }

        private static string BuildLinuxStandalone()
        {
            Car0GarageIntegration.Prepare();
            GarageThumbnailBuilder.ValidateTrackVehicleImages();
            GarageThumbnailBuilder.ValidateCurrentVehicleImages();
            GarageThumbnailBuilder.PrepareRuntimeImages();
            // Git can move these scripts without updating Unity's cached asset paths.
            foreach (string scriptPath in new[] { "Assets/Scripts/LastPanel.cs", "Assets/Scripts/BombPanel.cs" })
            {
                AssetDatabase.ImportAsset(scriptPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script == null || script.GetClass() == null)
                    throw new InvalidOperationException($"配置面板脚本未正确导入：{scriptPath}");
            }
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && scene.path == "Assets/Scenes/scene1.unity")
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("Build Settings 中没有启用的场景。");

            if (!Application.isBatchMode && !EditorSceneManager.SaveOpenScenes())
                throw new InvalidOperationException("无法保存当前打开的场景。");
            AssetDatabase.SaveAssets();

            string outputDirectory = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDirectory);
            string executablePath = Path.Combine(outputDirectory, ExecutableName);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = executablePath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.CleanBuildCache
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Linux 程序构建失败：{report.summary.result}，" +
                    $"错误 {report.summary.totalErrors} 个，警告 {report.summary.totalWarnings} 个。");
            }

            BuildLinuxImeBridge(outputDirectory);
            WriteLauncher(outputDirectory, executablePath);
            if (Environment.GetEnvironmentVariable("AGRIBOT_SKIP_DESKTOP_INSTALL") != "1")
                InstallDesktopEntry(outputDirectory);
            Debug.Log(
                $"Linux 程序构建完成：{executablePath}，" +
                $"大小 {report.summary.totalSize / (1024d * 1024d):F1} MiB，" +
                $"耗时 {report.summary.totalTime.TotalSeconds:F1} 秒。");
            return executablePath;
        }

        private static string ResolveOutputDirectory()
        {
            string configured = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured))
                return Path.GetFullPath(configured);

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Builds", OutputDirectoryName);
        }

        private static void WriteLauncher(string outputDirectory, string executablePath)
        {
            string launcherPath = Path.Combine(outputDirectory, LauncherName);
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string relativeRoot = Path.GetRelativePath(outputDirectory, repository).Replace("'", "'\"'\"'");
            string script =
                "#!/usr/bin/env bash\n" +
                "set -euo pipefail\n" +
                "HERE=\"$(cd \"$(dirname \"$0\")\" && pwd)\"\n" +
                $"ROOT=\"${{AGRIBOT_REPORT_ROOT:-$(cd \"$HERE\"/'{relativeRoot}' && pwd)}}\"\n" +
                "if [[ ! -f \"$ROOT/scripts/env.sh\" ]]; then echo '请从完整agribot_report仓库启动。' >&2; exit 2; fi\n" +
                "source \"$ROOT/scripts/env.sh\"\n" +
                "export XDG_CONFIG_HOME=\"$AGRIBOT_REPORT_CONFIG_HOME\"\n" +
                "cd \"$HERE\"\n" +
                "export LD_PRELOAD=\"$PWD/libagribot_linux_ime.so${LD_PRELOAD:+:$LD_PRELOAD}\"\n" +
                $"exec \"$HERE/{ExecutableName}\" -screen-fullscreen 1 \"$@\"\n";
            File.WriteAllText(launcherPath, script);
            RunCommand("/bin/chmod", $"+x \"{launcherPath}\"");
        }

        private static void BuildLinuxImeBridge(string outputDirectory)
        {
            string source = Path.GetFullPath("Tools/LinuxIme/agribot_linux_ime.c");
            string library = Path.Combine(outputDirectory, "libagribot_linux_ime.so");
            RunCommand("/usr/bin/gcc", "-O2 -Wall -Wextra -Werror -fPIC -shared -pthread " +
                "-Wl,-soname,libagribot_linux_ime.so -o \"" + library + "\" \"" + source + "\" -ldl");
        }

        private static void InstallDesktopEntry(string outputDirectory)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string launcherPath = Path.Combine(outputDirectory, LauncherName);
            string desktopDirectory = ResolveDesktopDirectory(home);
            string applicationDirectory = Path.Combine(home, ".local", "share", "applications");
            Directory.CreateDirectory(desktopDirectory);
            Directory.CreateDirectory(applicationDirectory);

            string desktopEntry =
                "[Desktop Entry]\n" +
                "Version=1.0\n" +
                "Type=Application\n" +
                "Name=农机配置平台\n" +
                "Comment=配置车辆、传感器和 ROS 仿真算法\n" +
                $"Exec={launcherPath}\n" +
                $"Path={outputDirectory}\n" +
                "Icon=applications-engineering\n" +
                "Terminal=false\n" +
                "StartupNotify=true\n" +
                "Categories=Development;Science;\n";

            string applicationPath = Path.Combine(applicationDirectory, DesktopFileName);
            string desktopPath = Path.Combine(desktopDirectory, "农机配置平台.desktop");
            File.WriteAllText(applicationPath, desktopEntry);
            File.WriteAllText(desktopPath, desktopEntry);
            RunCommand("/bin/chmod", $"+x \"{applicationPath}\" \"{desktopPath}\"");
            TryRunCommand("/usr/bin/gio", $"set \"{desktopPath}\" metadata::trusted true");
            TryRunCommand("/usr/bin/update-desktop-database", $"\"{applicationDirectory}\"");
        }

        private static string ResolveDesktopDirectory(string home)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = "/usr/bin/xdg-user-dir",
                    Arguments = "DESKTOP",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using Process process = Process.Start(info);
                string path = process?.StandardOutput.ReadToEnd().Trim();
                process?.WaitForExit();
                if (!string.IsNullOrWhiteSpace(path))
                    return path;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"读取桌面目录失败，将使用默认目录：{exception.Message}");
            }

            return Path.Combine(home, "Desktop");
        }

        private static void RunCommand(string fileName, string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using Process process = Process.Start(info);
            if (process == null)
                throw new InvalidOperationException($"无法启动命令：{fileName}");
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"命令执行失败：{fileName} {arguments}");
        }

        private static void TryRunCommand(string fileName, string arguments)
        {
            try
            {
                RunCommand(fileName, arguments);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"可选桌面集成命令执行失败：{exception.Message}");
            }
        }
    }
}
