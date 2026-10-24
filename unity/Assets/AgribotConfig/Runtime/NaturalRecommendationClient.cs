using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    [Serializable]
    public sealed class NaturalRecommendation
    {
        [Serializable] public sealed class Geometry { public float lengthM, minTurningRadiusM; }
        public string id, car, vehicleName, preset, implement, implementName;
        public string scene, sceneName, algorithm, vehicleConfigSha256;
        public string sceneConfigSha256;
        public bool sceneImported;
        public string verification, evidence, reason, action, guidance;
        public bool dynamicsReview;
        public Geometry geometry;
    }

    [Serializable]
    public sealed class NaturalRecommendationResponse
    {
        public bool ok;
        public bool sceneToolUsed;
        public bool sceneCatalogReused;
        public int modelRequestCount;
        public string sceneCatalogVersion;
        public string error, clarification, limitation, answer, memory;
        public NaturalRecommendation[] recommendations;
    }

    public static class NaturalRecommendationClient
    {
        [Serializable] public sealed class Context
        {
            public string car = "", scene = "", implement = "none", algorithm = "", page = "garage", simulation = "unknown";
            public string sceneName = "", latestImportedSceneId = "", latestImportedSceneName = "";
            public string sceneCatalogVersion = "";
            public bool sceneImported;
            public string perception = "", vision = "", localization = "", planner = "", controller = "";
            public string[] sensors = Array.Empty<string>();
            public float wheelbaseM, wheelTrackM, wheelRadiusM;
        }
        [Serializable] public sealed class Turn { public string role, content; }
        [Serializable] private sealed class Request { public string text, mode, memory; public Context context; public Turn[] history; }

        public static string WorkspaceSource
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("AGRIBOT_WORKSPACE_SRC");
                if (!string.IsNullOrWhiteSpace(path)) return Path.GetFullPath(path);
                string script = Environment.GetEnvironmentVariable("AGRIBOT_ROS_SYNC_SCRIPT");
                if (!string.IsNullOrWhiteSpace(script))
                    return Directory.GetParent(Path.GetFullPath(script))?.Parent?.Parent?.FullName;
                for (var directory = new DirectoryInfo(Application.dataPath); directory != null; directory = directory.Parent)
                {
                    string candidate = Path.Combine(directory.FullName, "ros2_ws", "src");
                    if (File.Exists(Path.Combine(directory.FullName, "scripts", "env.sh")) && Directory.Exists(candidate))
                        return candidate;
                }
                throw new InvalidOperationException("未找到本软件的ROS工作区，请从完整仓库启动或设置AGRIBOT_WORKSPACE_SRC。");
            }
        }

        public static Task<NaturalRecommendationResponse> Query(string text, CancellationToken token,
            string mode = "assistant", Context context = null, Turn[] history = null, string memory = "") =>
            Run(JsonUtility.ToJson(new Request { text = text, mode = mode, context = context, history = history, memory = memory }), null, token);

        public static async Task<NaturalRecommendation> Revalidate(NaturalRecommendation item, CancellationToken token)
        {
            var response = await Run("{}", item.id, token);
            if (response.recommendations == null || response.recommendations.Length != 1 ||
                response.recommendations[0].vehicleConfigSha256 != item.vehicleConfigSha256 ||
                (response.recommendations[0].sceneConfigSha256 ?? "") != (item.sceneConfigSha256 ?? "") ||
                response.recommendations[0].algorithm != item.algorithm || response.recommendations[0].preset != item.preset)
                throw new InvalidOperationException("场景或车辆配置已变化，请重新获取推荐。");
            response.recommendations[0].reason = item.reason;
            response.recommendations[0].action = item.action;
            return response.recommendations[0];
        }

        private static async Task<NaturalRecommendationResponse> Run(string input, string candidate, CancellationToken token)
        {
            string src = WorkspaceSource;
            if (!File.Exists(Path.Combine(src, "agribot_farm_sim", "agribot_farm_sim", "natural_recommendation.py")))
                throw new InvalidOperationException("未找到本机推荐服务，请同步 ROS 工作区代码。");
            var start = new ProcessStartInfo("/usr/bin/python3")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = src,
                Arguments = "-m agribot_farm_sim.natural_recommendation --workspace-src " + Quote(src) +
                    (candidate == null ? "" : " --candidate-id " + Quote(candidate))
            };
            start.Environment["PYTHONPATH"] = Path.Combine(src, "agribot_farm_sim") + ":" +
                Path.Combine(src, "agribot_vehicle_config_tools");
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            using var process = new Process { StartInfo = start };
            process.Start();
            using var cancellation = timeout.Token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
            });
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            byte[] requestBytes = Encoding.UTF8.GetBytes(input);
            await process.StandardInput.BaseStream.WriteAsync(requestBytes, 0, requestBytes.Length);
            await process.StandardInput.BaseStream.FlushAsync();
            process.StandardInput.Close();
            await Task.Run(() => process.WaitForExit());
            string json = await stdout;
            await stderr; // Never surface traces or HTTP headers to the UI.
            if (timeout.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
                throw new TimeoutException("推荐请求超时，请检查网络后重试。");
            }
            NaturalRecommendationResponse response;
            try { response = JsonUtility.FromJson<NaturalRecommendationResponse>(json); }
            catch (Exception) { throw new InvalidOperationException("推荐服务返回内容无效。"); }
            if (response == null || !response.ok || process.ExitCode != 0)
                throw new InvalidOperationException(response?.error ?? "推荐服务未正常完成。");
            return response;
        }

        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
