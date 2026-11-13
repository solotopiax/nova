/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIBindingsActionCommon.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   UI 控件绑定 Action 的严格标识与文件快照边界
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>只提供传输输入与快照保护，不复制 UIView 绑定或源码生成业务。</summary>
    internal static class UIBindingsActionCommon
    {
        /// <summary>冻结实际文件存在性与完整字节摘要；缺失和空文件分别记录。</summary>
        [Serializable]
        internal sealed class FileState
        {
            public string path;
            public bool exists;
            public string sha256;
        }

        /// <summary>只接受准确的 Assets 相对路径；归属、普通 Prefab 和草稿由领域入口核验。</summary>
        internal static bool IsPrefabPath(string path) => IsAssetPath(path) &&
            path.Length <= 512 && path.EndsWith(".prefab", StringComparison.Ordinal);

        /// <summary>拒绝绝对路径、非规范分隔符、导航段与控制字符，不自动修正调用方路径。</summary>
        internal static bool IsAssetPath(string path) => !string.IsNullOrEmpty(path) &&
            path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Contains('\\') &&
            !path.Any(char.IsControl) && path.Split('/').All(part => part.Length > 0 && part != "." && part != "..");

        /// <summary>只解析当前协议的规范 GlobalObjectId，不以运行整数或节点名代替持久身份。</summary>
        internal static bool IsObjectId(string value) => !string.IsNullOrEmpty(value) && value.Length <= 512 &&
            GlobalObjectId.TryParse(value, out GlobalObjectId id) && id.ToString() == value;

        /// <summary>逐文件重新核对项目边界及符号链接，冻结实际存在性与完整 SHA256。</summary>
        internal static FileState[] CaptureFiles(string[] paths, CancellationToken cancellationToken)
        {
            if (paths == null || paths.Any(path => !IsAssetPath(path))) throw new InvalidOperationException();
            return paths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).Select(path =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!GenerateActionCommon.TryResolveProjectPath(path, "UI 绑定文件", out string absolute, out _) ||
                    Directory.Exists(absolute)) throw new InvalidOperationException();
                bool exists = File.Exists(absolute);
                return new FileState
                {
                    path = path, exists = exists,
                    sha256 = exists ? GenerateActionCommon.ComputeFileHash(absolute, cancellationToken) : null,
                };
            }).ToArray();
        }

        /// <summary>只读重算原精确文件集合，不将文件消失、路径移动或新增文件解释成原状态。</summary>
        internal static bool MatchesFiles(FileState[] expected, CancellationToken cancellationToken)
        {
            if (expected == null || expected.Any(file => file == null) ||
                expected.Select(file => file.path).Distinct(StringComparer.Ordinal).Count() != expected.Length) return false;
            FileState[] actual = CaptureFiles(expected.Select(file => file.path).ToArray(), cancellationToken);
            return expected.OrderBy(file => file.path, StringComparer.Ordinal).Zip(actual, (before, after) =>
                before.path == after.path && before.exists == after.exists && before.sha256 == after.sha256).All(value => value);
        }

        /// <summary>回执拒绝重复或未知字段、类型注入与超过边界的内容；解析本身不会调用领域操作。</summary>
        internal static T ParseReceipt<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json) || System.Text.Encoding.UTF8.GetByteCount(json) > 256 * 1024)
                throw new InvalidOperationException();
            JObject root = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            return root.ToObject<T>(JsonSerializer.Create(new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None, MaxDepth = 32,
            })) ?? throw new InvalidOperationException();
        }

        /// <summary>重新检查当前 Editor 的真实稳定状态，编译失败或 Play 期间不进入写入和验收。</summary>
        internal static bool IsStableEditor() => !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            !EditorApplication.isPlayingOrWillChangePlaymode && !EditorUtility.scriptCompilationFailed;
    }
}
