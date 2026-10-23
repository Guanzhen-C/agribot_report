using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    [Serializable] public sealed class SceneImportProgress
    {
        public string stage, error;
        public float progress;
        public bool ok;
        public FarmScenario scene;
    }

    public static class ScannedSceneImporter
    {
        [Serializable] private sealed class Request { public string operation, id, archive, name; }
        [DllImport("libc", SetLastError = true)] private static extern int kill(int pid, int signal);

        public static Task<string> PickArchive(CancellationToken token) => Task.Run(() =>
        {
            if (!File.Exists("/usr/bin/zenity"))
                throw new InvalidOperationException("缺少文件选择器 zenity，请先安装。");
            using var process = new Process { StartInfo = new ProcessStartInfo("/usr/bin/zenity")
            {
                Arguments = "--file-selection --title=\"选择LCC2场景压缩包\" --file-filter=\"LCC2 ZIP | *.zip *.ZIP\"",
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            }};
            process.Start();
            using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch { } });
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            error.GetAwaiter().GetResult();
            token.ThrowIfCancellationRequested();
            return process.ExitCode == 0 ? output.GetAwaiter().GetResult().TrimEnd('\r', '\n') : null;
        }, token);

        public static Task<FarmScenario> Import(string archive, string name,
            IProgress<SceneImportProgress> progress, CancellationToken token) =>
            Execute(new Request { operation = "import", archive = archive, name = name }, progress, token);

        public static Task<FarmScenario> Delete(string id, CancellationToken token) =>
            Execute(new Request { operation = "delete", id = id }, null, token);

        private static Task<FarmScenario> Execute(Request request,
            IProgress<SceneImportProgress> progress, CancellationToken token) => Task.Run(() =>
        {
            if (request.operation == "delete" && RosSimulationLauncher.ReadManagedStatus() != "stopped")
                throw new InvalidOperationException("请先停止仿真，再删除导入场景。");
            string source = NaturalRecommendationClient.WorkspaceSource;
            if (!File.Exists(Path.Combine(source, "agribot_k2_sim/agribot_k2_sim/import_scene.py")))
                throw new InvalidOperationException("未找到本机场景导入程序，请同步 ROS 工作区。");
            var start = new ProcessStartInfo("/usr/bin/setsid")
            {
                Arguments = "/usr/bin/python3 -m agribot_k2_sim.import_scene --request-stdin --library " + Quote(FarmScenarioCatalog.ImportedLibraryDirectory),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = source,
            };
            start.Environment["PYTHONPATH"] = Path.Combine(source, "agribot_k2_sim");
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            start.Environment["OPENBLAS_NUM_THREADS"] = "1";
            using var process = new Process { StartInfo = start };
            process.Start();
            using var registration = token.Register(() =>
            {
                try { if (!process.HasExited) kill(-process.Id, 15); } catch (InvalidOperationException) { }
            });
            var stderr = process.StandardError.ReadToEndAsync();
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
            process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
            process.StandardInput.Close();
            SceneImportProgress last = null;
            string line;
            while ((line = process.StandardOutput.ReadLine()) != null)
            {
                if (!line.StartsWith("{", StringComparison.Ordinal)) continue;
                try { last = JsonUtility.FromJson<SceneImportProgress>(line); }
                catch (Exception) { continue; }
                if (last != null) progress?.Report(last);
            }
            process.WaitForExit();
            stderr.GetAwaiter().GetResult();
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || last == null || !last.ok || last.scene == null)
                throw new InvalidOperationException(last?.error ?? "场景操作未完成，请检查本机依赖。");
            return last.scene;
        }, token);

        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
