/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GenerateActionCommon.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   Generate Action 的只读冻结、路径与 SHA-256 验证辅助
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    internal static class GenerateActionCommon
    {
        [Serializable]
        internal sealed class Artifact
        {
            public string path;
            public string kind;
            public string sha256;
            public int fileCount;
        }

        [Serializable]
        internal sealed class ActiveMasterFile
        {
            public string configMasterGuid;
            public string configMasterPathHint;
        }

        internal static string ProjectRoot =>
            Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        internal static bool TryValidateGuid(string value, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length != 32 ||
                value.Any(character => !Uri.IsHexDigit(character)))
            {
                error = "masterGuid 必须是 32 位十六进制 Unity Asset GUID。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 按区分大小写的枚举名称解析运行坐标，拒绝 Enum.TryParse 默认接受的数字字符串。
        /// </summary>
        internal static bool TryParseCoordinate(
            string platformValue,
            string channelValue,
            string modeValue,
            out PlatformType platform,
            out ChannelType channel,
            out DevelopMode mode,
            out string error)
        {
            platform = PlatformType.None;
            channel = ChannelType.None;
            mode = DevelopMode.Debug;
            error = null;
            if (!IsExactEnumName(typeof(PlatformType), platformValue) ||
                !Enum.TryParse(platformValue, false, out platform) ||
                !Enum.IsDefined(typeof(PlatformType), platform) || platform == PlatformType.None)
            {
                error = "platform 必须是非 None 的 PlatformType 精确枚举名。";
                return false;
            }
            if (!IsExactEnumName(typeof(ChannelType), channelValue) ||
                !Enum.TryParse(channelValue, false, out channel) ||
                !Enum.IsDefined(typeof(ChannelType), channel) || channel == ChannelType.None)
            {
                error = "channel 必须是非 None 的 ChannelType 精确枚举名。";
                return false;
            }
            if (!IsExactEnumName(typeof(DevelopMode), modeValue) ||
                !Enum.TryParse(modeValue, false, out mode) || !Enum.IsDefined(typeof(DevelopMode), mode))
            {
                error = "developMode 必须是 DevelopMode 精确枚举名。";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 按区分大小写的枚举名称解析 BuildTarget，拒绝数字、别名组合与非规范大小写。
        /// </summary>
        internal static bool TryParseActiveBuildTarget(string value, out BuildTarget target, out string error)
        {
            target = BuildTarget.NoTarget;
            error = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || !IsExactEnumName(typeof(BuildTarget), value) ||
                !Enum.TryParse(value, false, out target) || !Enum.IsDefined(typeof(BuildTarget), target) ||
                target == BuildTarget.NoTarget)
            {
                error = "activeBuildTarget 必须是非 NoTarget 的 Unity BuildTarget 精确枚举名。";
                return false;
            }
            return true;
        }

        internal static ConfigMasterSO ResolveMaster(string masterGuid, out string assetPath)
        {
            assetPath = AssetDatabase.GUIDToAssetPath(masterGuid);
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(assetPath);
        }

        /// <summary>
        /// 只读核对 WorkspaceActive 文件。这里不调用 WorkspaceActive.Get()，避免 Plan 因 pathHint 修复或
        /// sample scene 推断而产生写入。
        /// </summary>
        internal static bool TryValidateActiveMasterBinding(
            string masterGuid,
            string masterAssetPath,
            out string error)
        {
            error = null;
            string globalsPath = Path.Combine(ProjectRoot, "ProjectSettings/Nova/Globals.json");
            if (!File.Exists(globalsPath))
            {
                error = "ProjectSettings/Nova/Globals.json 不存在；无法只读证明请求的 ConfigMaster 是当前激活 Master。";
                return false;
            }

            ActiveMasterFile active;
            try
            {
                active = JsonUtility.FromJson<ActiveMasterFile>(File.ReadAllText(globalsPath));
            }
            catch (Exception exception)
            {
                error = "Globals.json 无法解析：" + exception.Message;
                return false;
            }

            if (active == null || !string.Equals(active.configMasterGuid, masterGuid, StringComparison.OrdinalIgnoreCase))
            {
                error = $"请求 masterGuid={masterGuid} 与 Globals.json 激活 GUID={active?.configMasterGuid ?? "<empty>"} 不一致。";
                return false;
            }
            if (!string.Equals(NormalizeAssetPath(active.configMasterPathHint), NormalizeAssetPath(masterAssetPath), StringComparison.Ordinal))
            {
                error = "Globals.json 的 ConfigMaster pathHint 已漂移；请先由 ConfigWindow/场景路由修复后重新计划。";
                return false;
            }

            string scenePath = NormalizeAssetPath(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            string sceneSample = GetSampleRoot(scenePath);
            string masterSample = GetSampleRoot(NormalizeAssetPath(masterAssetPath));
            if (!string.IsNullOrEmpty(sceneSample) && !string.Equals(sceneSample, masterSample, StringComparison.Ordinal))
            {
                error = "当前 Sample 场景与 Globals.json 指向不同 Sample；WorkspaceActive 会重推断并写回，Plan 因此拒绝。";
                return false;
            }
            return true;
        }

        internal static bool CoordinateMatches(
            ConfigMasterSO master,
            PlatformType platform,
            ChannelType channel,
            DevelopMode mode)
        {
            return master != null && master.CurrentPlatform == platform && master.CurrentChannel == channel &&
                   master.CurrentDevelopMode == mode;
        }

        internal static bool TryResolveAssetSavePath(string value, out string assetPath, out string absolutePath, out string error)
        {
            assetPath = NormalizeAssetPath(value?.Trim());
            absolutePath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(assetPath) || assetPath.Length > 1024 || assetPath.IndexOf('\0') >= 0 ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                assetPath.Split('/').Any(part => part == "." || part == ".." || part.Length == 0))
            {
                error = "savePath 必须是 Assets/ 下无导航片段、以 .asset 结尾的规范资产路径。";
                return false;
            }
            return TryResolveProjectPath(assetPath, "savePath", out absolutePath, out error);
        }

        /// <summary>
        /// 将输入规范化为项目内绝对路径，并拒绝路径上任何已存在的符号链接或重解析点。
        /// 对尚未生成的输出，只检查到最近的已存在祖先，防止后续 IO 穿透项目边界。
        /// </summary>
        internal static bool TryResolveProjectPath(string value, string field, out string absolutePath, out string error)
        {
            absolutePath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.IndexOf('\0') >= 0)
            {
                error = field + " 必须是长度不超过 2048 的非空项目内路径。";
                return false;
            }
            try
            {
                absolutePath = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(ProjectRoot, value));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                error = field + " 无法规范化：" + exception.Message;
                return false;
            }
            if (!IsWithinProject(absolutePath))
            {
                error = field + " 必须位于当前项目根目录内。";
                return false;
            }
            if (!TryRejectExistingReparsePoints(absolutePath, field, out error))
            {
                absolutePath = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 判断规范绝对路径是否处于当前项目根内；Windows 使用不区分大小写的路径比较。
        /// </summary>
        internal static bool IsWithinProject(string path)
        {
            string full = Path.GetFullPath(path);
            return string.Equals(full, ProjectRoot, PathComparison) ||
                   full.StartsWith(ProjectRoot + Path.DirectorySeparatorChar, PathComparison);
        }

        internal static string ToProjectRelative(string absolutePath)
        {
            string full = Path.GetFullPath(absolutePath);
            if (!IsWithinProject(full)) return full.Replace('\\', '/');
            return full.Substring(ProjectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
        }

        internal static string ComputeFileHash(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buffer = new byte[1024 * 1024];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sha256.TransformBlock(buffer, 0, count, null, 0);
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToHex(sha256.Hash);
            }
        }

        internal static string ComputeTextHash(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty)));
            }
        }

        /// <summary>
        /// 捕获单个项目内文件的稳定摘要；读取前重新执行项目边界与重解析点检查。
        /// </summary>
        internal static Artifact CaptureFile(string path, CancellationToken cancellationToken)
        {
            if (!TryResolveProjectPath(path, "产物路径", out string full, out string error))
            {
                throw new InvalidOperationException(error);
            }
            if (!File.Exists(full)) throw new FileNotFoundException("精确产物不存在。", full);
            return new Artifact { path = full, kind = "file", sha256 = ComputeFileHash(full, cancellationToken), fileCount = 1 };
        }

        /// <summary>
        /// 捕获项目内目录摘要；递归枚举时拒绝目录树中的任意符号链接或重解析点。
        /// </summary>
        internal static Artifact CaptureDirectory(string path, string searchPattern, CancellationToken cancellationToken)
        {
            if (!TryResolveProjectPath(path, "产物目录", out string root, out string error))
            {
                throw new InvalidOperationException(error);
            }
            root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("精确产物目录不存在：" + root);
            string[] files = EnumerateFilesWithoutReparsePoints(
                    root, string.IsNullOrEmpty(searchPattern) ? "*" : searchPattern, cancellationToken)
                .OrderBy(file => file, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) throw new FileNotFoundException("精确产物目录为空：" + root);

            using (SHA256 sha256 = SHA256.Create())
            {
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace('\\', '/');
                    string entry = relative + "\n" + new FileInfo(file).Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                                   ComputeFileHash(file, cancellationToken) + "\n";
                    byte[] bytes = Encoding.UTF8.GetBytes(entry);
                    sha256.TransformBlock(bytes, 0, bytes.Length, null, 0);
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return new Artifact { path = root, kind = "directory", sha256 = ToHex(sha256.Hash), fileCount = files.Length };
            }
        }

        internal static bool TryVerifyArtifacts(
            IEnumerable<Artifact> expected,
            CancellationToken cancellationToken,
            out Artifact[] actual,
            out string error)
        {
            var captured = new List<Artifact>();
            error = null;
            Artifact[] expectedArtifacts = expected?.ToArray() ?? Array.Empty<Artifact>();
            if (expectedArtifacts.Length == 0)
            {
                actual = Array.Empty<Artifact>();
                error = "RecoveryPayload 尚无执行后产物 Hash；只允许报告 partial，不会据此假定 Execute 已发生。";
                return false;
            }
            try
            {
                foreach (Artifact artifact in expectedArtifacts)
                {
                    if (artifact == null || string.IsNullOrWhiteSpace(artifact.path) || string.IsNullOrWhiteSpace(artifact.sha256))
                    {
                        error = "Receipt 尚无完整产物 Hash；不会据此重放 Execute。";
                        actual = captured.ToArray();
                        return false;
                    }
                    if (artifact.kind != "file" && artifact.kind != "directory")
                    {
                        error = "Receipt 包含未知产物类型：" + artifact.kind;
                        actual = captured.ToArray();
                        return false;
                    }
                    Artifact current = artifact.kind == "directory"
                        ? CaptureDirectory(artifact.path, "*", cancellationToken)
                        : CaptureFile(artifact.path, cancellationToken);
                    captured.Add(current);
                    if (!string.Equals(current.kind, artifact.kind, StringComparison.Ordinal) ||
                        !string.Equals(current.sha256, artifact.sha256, StringComparison.OrdinalIgnoreCase) ||
                        current.fileCount != artifact.fileCount)
                    {
                        error = "产物已漂移：" + artifact.path;
                        actual = captured.ToArray();
                        return false;
                    }
                }
                actual = captured.ToArray();
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                error = exception.Message;
                actual = captured.ToArray();
                return false;
            }
        }

        private static string NormalizeAssetPath(string value)
        {
            return string.IsNullOrEmpty(value) ? value : value.Replace('\\', '/');
        }

        private static string GetSampleRoot(string path)
        {
            const string prefix = "Assets/Samples/";
            if (string.IsNullOrEmpty(path) || !path.StartsWith(prefix, StringComparison.Ordinal)) return string.Empty;
            int slash = path.IndexOf('/', prefix.Length);
            return slash < 0 ? path.Substring(prefix.Length) : path.Substring(prefix.Length, slash - prefix.Length);
        }

        private static string ToHex(byte[] bytes)
        {
            return bytes == null ? null : string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static StringComparison PathComparison =>
            Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        /// <summary>
        /// 判断输入是否恰好是枚举声明名称，不接受 Enum.TryParse 可解析的数字字符串。
        /// </summary>
        private static bool IsExactEnumName(Type enumType, string value)
        {
            return !string.IsNullOrEmpty(value) && Enum.GetNames(enumType).Contains(value, StringComparer.Ordinal);
        }

        /// <summary>
        /// 从项目根逐段检查到目标路径，拒绝所有已存在的符号链接、junction 与其他重解析点。
        /// </summary>
        private static bool TryRejectExistingReparsePoints(string absolutePath, string field, out string error)
        {
            error = null;
            string relative = absolutePath.Substring(ProjectRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (relative.Length == 0) return true;

            string current = ProjectRoot;
            foreach (string part in relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                try
                {
                    FileAttributes attributes = File.GetAttributes(current);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        error = field + " 的已存在路径段是符号链接或重解析点：" + ToProjectRelative(current);
                        return false;
                    }
                }
                catch (Exception exception) when (exception is FileNotFoundException || exception is DirectoryNotFoundException)
                {
                    // 输出尚未生成时，更深层路径也不可能存在，到此即完成祖先检查。
                    break;
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                                   exception is ArgumentException || exception is NotSupportedException)
                {
                    error = field + " 无法检查已存在路径段：" + exception.Message;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 在不跟随重解析点的前提下递归枚举文件，确保目录摘要不会读取项目边界外内容。
        /// </summary>
        private static IEnumerable<string> EnumerateFilesWithoutReparsePoints(
            string root,
            string searchPattern,
            CancellationToken cancellationToken)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string current = pending.Pop();
                foreach (string file in Directory.GetFiles(current, searchPattern, SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException("产物目录包含符号链接或重解析点：" + ToProjectRelative(file));
                    }
                    yield return file;
                }
                foreach (string directory in Directory.GetDirectories(current, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException("产物目录包含符号链接或重解析点：" + ToProjectRelative(directory));
                    }
                    pending.Push(directory);
                }
            }
        }
    }
}
