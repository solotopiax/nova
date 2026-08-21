/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataModuleExportActionCommon.cs
 * author:    taoye
 * created:   2026/8/21
 * descrip:   Sound/Vibrate 精确导出 Action 的产物计划与验证辅助
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace NovaFramework.Editor
{
    internal static class DataModuleExportActionCommon
    {
        [Serializable]
        internal sealed class Output
        {
            public string kind;
            public string sourcePath;
            public string path;
            public GenerateActionCommon.Artifact artifact;
        }

        /// <summary>
        /// 将导出范围规范为 all、code 或 data。
        /// </summary>
        internal static bool TryNormalizeScope(string value, out string scope)
        {
            scope = value?.Trim().ToLowerInvariant();
            return scope == "all" || scope == "code" || scope == "data";
        }

        /// <summary>
        /// 判断当前范围是否包含代码产物。
        /// </summary>
        internal static bool IncludesCode(string scope) => scope == "all" || scope == "code";

        /// <summary>
        /// 判断当前范围是否包含数据产物。
        /// </summary>
        internal static bool IncludesData(string scope) => scope == "all" || scope == "data";

        /// <summary>
        /// 校验可选源文件相对路径列表的数量、内容与唯一性。
        /// </summary>
        internal static bool TryValidateSourcePaths(string[] values, out string error)
        {
            error = null;
            if (values == null || values.Length == 0) return true;
            if (values.Length > 128 || values.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 1024 || value.IndexOf('\0') >= 0))
            {
                error = "sourcePaths 最多 128 项，每项必须是长度不超过 1024 的非空路径。";
                return false;
            }
            if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            {
                error = "sourcePaths 不能重复。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 校验所有输出均位于当前 Unity 项目内。
        /// </summary>
        internal static bool TryValidateOutputPaths(IEnumerable<string> paths, out string error)
        {
            error = null;
            foreach (string path in paths.Distinct(StringComparer.Ordinal))
            {
                if (!GenerateActionCommon.TryResolveProjectPath(path, "导出路径", out _, out error)) return false;
            }
            return true;
        }

        /// <summary>
        /// 校验源文件存在且没有逃逸已冻结的源目录。
        /// </summary>
        internal static bool TryValidateSourceFile(string sourceDirectory, string relativePath, out string error)
        {
            error = null;
            if (!GenerateActionCommon.TryResolveProjectPath(sourceDirectory, "源目录", out string root, out error) ||
                !GenerateActionCommon.TryResolveProjectPath(Path.Combine(sourceDirectory, relativePath), "源文件", out string file, out error))
                return false;
            root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(file))
            {
                error = "源文件必须存在且位于冻结的源目录内：" + relativePath;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 生成排序、去重且统一分隔符的写入集。
        /// </summary>
        internal static string[] BuildWriteSet(IEnumerable<string> paths)
        {
            return paths.Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Replace('\\', '/'))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 捕获单个文件或目录产物的稳定摘要。
        /// </summary>
        internal static Output Capture(string kind, string sourcePath, string path, CancellationToken cancellationToken)
        {
            GenerateActionCommon.Artifact artifact = File.Exists(path)
                ? GenerateActionCommon.CaptureFile(path, cancellationToken)
                : GenerateActionCommon.CaptureDirectory(path, "*", cancellationToken);
            return new Output { kind = kind, sourcePath = sourcePath, path = path, artifact = artifact };
        }

        /// <summary>
        /// 只读复算 Receipt 中全部产物摘要。
        /// </summary>
        internal static bool TryVerify(Output[] outputs, CancellationToken cancellationToken,
            out GenerateActionCommon.Artifact[] actual, out string error)
        {
            if (outputs == null || outputs.Length == 0 || outputs.Any(output => output?.artifact == null))
            {
                actual = Array.Empty<GenerateActionCommon.Artifact>();
                error = "Receipt 未包含完整导出产物。";
                return false;
            }
            return GenerateActionCommon.TryVerifyArtifacts(outputs.Select(output => output.artifact), cancellationToken, out actual, out error);
        }

        /// <summary>
        /// 校验 Receipt 精确覆盖计划内的产物身份与范围。
        /// </summary>
        internal static bool HasExactCoverage(Output[] actual, IEnumerable<Output> expected)
        {
            Output[] expectedArray = expected.ToArray();
            return actual != null && actual.Length == expectedArray.Length && expectedArray.All(item =>
                actual.Any(value => value != null && value.kind == item.kind && value.sourcePath == item.sourcePath && value.path == item.path));
        }
    }
}
