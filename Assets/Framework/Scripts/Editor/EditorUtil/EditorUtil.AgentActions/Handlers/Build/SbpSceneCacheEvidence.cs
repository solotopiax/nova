/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SbpSceneCacheEvidence.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   正规 SBP 本次场景缓存命中的只读证据
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    internal static class SbpSceneCacheEvidence
    {
        /// <summary>本域唯一身份在程序集重载后改变，与 MCP 连接、窗口激活或 Unity 进程 PID 无关。</summary>
        internal static readonly string Domain = Guid.NewGuid().ToString("N");

        [Serializable]
        internal sealed class Receipt
        {
            public string domain;
            public string outputPath;
            public string packageName;
            public string version;
            public long startedUtcTicks;
            public long closedUtcTicks;
            public GenerateActionCommon.Artifact trace;
            public GenerateActionCommon.Artifact report;
            public string[] sceneGuids;
            public string sha256;
        }

        /// <summary>仅从本次输出目录读取正式报告及完整 trace；文件必须在当前作用域内写出，场景必须属于冻结依赖闭包。</summary>
        /// <param name="outputPath">本次构建独占的正式输出目录。</param>
        /// <param name="packageName">冻结 Package 名称。</param>
        /// <param name="version">冻结 Package 版本。</param>
        /// <param name="startedUtcTicks">本次构建观察窗开始的 UTC ticks。</param>
        /// <param name="writes">已冻结的正式写集和场景依赖闭包。</param>
        /// <param name="token">只读解析和文件哈希的取消令牌。</param>
        /// <returns>仅在本次缓存零回调证据完整时返回自洽回执，否则抛异常。</returns>
        internal static Receipt Capture(string outputPath, string packageName, string version, long startedUtcTicks,
            BuildActionCommon.BundleWriteSnapshot writes, CancellationToken token)
        {
            VerifyPipelineIdentity(token);
            string tracePath = Path.Combine(outputPath, "buildlogtep.json");
            string reportPath = Path.Combine(outputPath, YooAssetConfiguration.GetBuildReportFileName(packageName, version));
            long closed = DateTime.UtcNow.Ticks;
            foreach (string path in new[] { tracePath, reportPath })
            {
                token.ThrowIfCancellationRequested();
                if (!File.Exists(path) || new FileInfo(path).Length > 16 * 1024 * 1024 ||
                    File.GetLastWriteTimeUtc(path).Ticks < startedUtcTicks || File.GetLastWriteTimeUtc(path).Ticks > closed)
                    throw new InvalidOperationException("场景缓存证据不是本次有界输出文件：" + path);
            }
            var report = BuildReport.Deserialize(File.ReadAllText(reportPath));
            ReportSummary summary = report?.Summary;
            if (summary == null || summary.BuildTarget != BuildTarget.Android || summary.UnityVersion != Application.unityVersion ||
                summary.BuildPackageName != packageName || summary.BuildPackageVersion != version ||
                summary.BuildPipeline != "ScriptableBuildPipeline" || summary.BuildBundleType != (int)EBundleType.AssetBundle ||
                report.AssetInfos == null || report.BundleInfos == null || summary.AllBundleTotalCount != report.BundleInfos.Count)
                throw new InvalidOperationException("本次场景缓存报告与冻结 Android、版本或正式管线不一致。");
            var scenes = report.AssetInfos.Where(asset => asset.AssetPath.EndsWith(".unity", StringComparison.Ordinal)).ToArray();
            if (scenes.Any(scene => !writes.preservedSceneDependencyPaths.Contains(scene.AssetPath, StringComparer.Ordinal) ||
                AssetDatabase.AssetPathToGUID(scene.AssetPath) != scene.AssetGuid))
                throw new InvalidOperationException("本次报告场景不在冻结依赖闭包或 GUID 已漂移。");
            string[] guids = scenes.Select(scene => scene.AssetGuid).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            ValidateTrace(File.ReadAllText(tracePath), guids, Application.unityVersion, report.BundleInfos.Count);
            var result = new Receipt { domain = Domain, outputPath = outputPath, packageName = packageName, version = version,
                startedUtcTicks = startedUtcTicks, closedUtcTicks = closed, sceneGuids = guids,
                trace = GenerateActionCommon.CaptureFile(tracePath, token), report = GenerateActionCommon.CaptureFile(reportPath, token) };
            result.sha256 = Hash(result);
            return result;
        }

        /// <summary>核验同域自洽证据和两份文件原字节；Verify 仅重读，不重建或补造缓存记录。</summary>
        /// <param name="receipt">当前同域的本次缓存证据。</param>
        /// <param name="token">只读文件核验取消令牌；缺失或漂移时抛异常。</param>
        internal static void Verify(Receipt receipt, CancellationToken token)
        {
            VerifyPipelineIdentity(token);
            if (receipt == null || receipt.domain != Domain || receipt.startedUtcTicks <= 0 || receipt.closedUtcTicks < receipt.startedUtcTicks ||
                receipt.trace == null || receipt.report == null || receipt.sceneGuids == null || receipt.sha256 != Hash(receipt) ||
                receipt.trace.path != Path.Combine(receipt.outputPath, "buildlogtep.json") ||
                receipt.report.path != Path.Combine(receipt.outputPath, YooAssetConfiguration.GetBuildReportFileName(receipt.packageName, receipt.version)) ||
                !GenerateActionCommon.TryVerifyArtifacts(new[] { receipt.trace, receipt.report }, token, out _, out _))
                throw new InvalidOperationException("本次缓存证据缺失、域已变化或文件字节已漂移。");
            var report = BuildReport.Deserialize(File.ReadAllText(receipt.report.path));
            ValidateTrace(File.ReadAllText(receipt.trace.path), receipt.sceneGuids, report.Summary.UnityVersion, report.BundleInfos.Count);
        }

        /// <summary>要求完整场景依赖命中、全部写任务计数自洽且没有执行任何场景写命令；仅 cached 单行或文件存在均不足。</summary>
        /// <param name="json">本次 SBP 输出的完整 trace 原文。</param>
        /// <param name="sceneGuids">正式报告中的完整唯一场景 GUID 集合。</param>
        /// <param name="unityVersion">本次报告绑定的引擎版本。</param>
        /// <param name="bundleCount">本次正式报告 Bundle 总数；不一致或证据不完整时抛异常。</param>
        internal static void ValidateTrace(string json, string[] sceneGuids, string unityVersion, int bundleCount)
        {
            JObject root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            var rows = root["traceEvents"] as JArray;
            if ((string)root["UnityVersion"] != unityVersion || (string)root["com.unity.scriptablebuildpipeline"] != "2.6.1" || rows == null ||
                sceneGuids == null || sceneGuids.Any(value => !Regex.IsMatch(value ?? string.Empty, "^[0-9a-f]{32}$")) ||
                sceneGuids.Distinct(StringComparer.Ordinal).Count() != sceneGuids.Length || bundleCount <= 0 ||
                rows.Any(row => row.Type != JTokenType.Object || row["name"]?.Type != JTokenType.String || (string)row["ph"] != "X" ||
                    !NonnegativeFiniteNumber(row["ts"]) || !NonnegativeFiniteNumber(row["dur"]) || row["tid"]?.Type != JTokenType.Integer))
                throw new InvalidOperationException("SBP trace 不是已审完整版本、时间范围或唯一场景集合。");
            JToken[] dependencies = rows.Where(row => (string)row["name"] == "Calculate Scene Dependencies").ToArray();
            string[] cached = dependencies.Select(row => (string)row["args"]?["0"]).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            JToken[] writers = rows.Where(row => ((string)row["name"] ?? string.Empty).StartsWith("Writing ", StringComparison.Ordinal)).ToArray();
            JToken[] calculations = rows.Where(row => (string)row["name"] == "CalculateSceneDependencyData").ToArray();
            JToken[] writeTasks = rows.Where(row => (string)row["name"] == "WriteSerializedFiles").ToArray();
            if (calculations.Length != 1 || writeTasks.Length != 1 ||
                !cached.SequenceEqual(sceneGuids.OrderBy(value => value, StringComparer.Ordinal).Select(value => value + " (cached)")) ||
                dependencies.Any(row => !Inside(row, calculations[0])) || writers.Any(row => !Inside(row, writeTasks[0]) ||
                    (string)row["name"] != "Writing AssetBundleWriteOperation"))
                throw new InvalidOperationException("SBP 场景并非全部命中缓存，或实际执行了场景/未知写命令。");
            JToken[] operations = rows.Where(row => (string)row["name"] == "RunCachedOperation" && Inside(row, writeTasks[0])).ToArray();
            Match counts = operations.Length == 1 ? Regex.Match((string)operations[0]["args"]?["0"] ?? string.Empty,
                "^Total Entries: ([0-9]+), Processed: ([0-9]+), Cached: ([0-9]+)$") : Match.Empty;
            if (!counts.Success || !int.TryParse(counts.Groups[1].Value, out int total) || !int.TryParse(counts.Groups[2].Value, out int processed) ||
                !int.TryParse(counts.Groups[3].Value, out int hit) || total != bundleCount || processed != writers.Length || hit != bundleCount - writers.Length ||
                rows.Count(row => (string)row["name"] == "Hashing PreloadInfo" && Inside(row, writeTasks[0])) != sceneGuids.Length ||
                rows.Count(row => (string)row["name"] == "PostWritingCallback") != 1)
                throw new InvalidOperationException("SBP 完整写任务、场景命令或收尾计数不能证明本次无场景回调。");
        }

        /// <summary>比较同一线程的完整 trace 区间，排除另一任务或未闭合时段的相似日志。</summary>
        /// <param name="child">待验证的已闭合子步骤。</param>
        /// <param name="parent">必须包含该步骤的完整父任务。</param>
        /// <returns>线程相同且子时段完整处于父时段内时为 true。</returns>
        private static bool Inside(JToken child, JToken parent)
        {
            return (int?)child["tid"] == (int?)parent["tid"] && (double)child["ts"] >= (double)parent["ts"] &&
                (double)child["ts"] + (double)child["dur"] <= (double)parent["ts"] + (double)parent["dur"] + 1;
        }

        /// <summary>trace 时刻与时长必须是有限非负数，不把字符串转换、NaN 或 Infinity 当成有效时段。</summary>
        /// <param name="value">trace 中的原始数值 token。</param>
        /// <returns>仅接受有限、非负的整数或浮点数。</returns>
        private static bool NonnegativeFiniteNumber(JToken value)
        {
            if (value == null || value.Type != JTokenType.Integer && value.Type != JTokenType.Float) return false;
            double number = (double)value;
            return !double.IsNaN(number) && !double.IsInfinity(number) && number >= 0;
        }

        /// <summary>摘要覆盖全部类型化证据而排除自身摘要字段，便于只读检查回执自洽。</summary>
        /// <param name="receipt">需要计算摘要的缓存证据。</param>
        /// <returns>全部固定字段的 SHA-256。</returns>
        private static string Hash(Receipt receipt)
        {
            return GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new { receipt.domain, receipt.outputPath, receipt.packageName,
                receipt.version, receipt.startedUtcTicks, receipt.closedUtcTicks, receipt.trace, receipt.report, receipt.sceneGuids }));
        }

        /// <summary>缓存无回调推断只适用于已精读的实际 SBP DLL；不能仅按包名或日志版本接纳其他实现。</summary>
        /// <param name="token">只读 DLL 文件哈希的取消令牌；身份不符时抛异常。</param>
        private static void VerifyPipelineIdentity(CancellationToken token)
        {
            if (GenerateActionCommon.ComputeFileHash(typeof(UnityEditor.Build.Pipeline.Tasks.WriteSerializedFiles).Assembly.Location, token) !=
                "4e1f7c6e6a8e23c862a765219a7da93fa2a846883d8d2b79d61920c6e4015eaf")
                throw new InvalidOperationException("场景缓存证据的实际 SBP DLL 已偏离精读版本。");
        }
    }
}
