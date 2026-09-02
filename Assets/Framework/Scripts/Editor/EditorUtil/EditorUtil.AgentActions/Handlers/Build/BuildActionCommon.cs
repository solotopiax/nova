/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BuildActionCommon.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   Nova Project Build Action 的冻结计划与产物只读验证辅助
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
using YooAsset;
using YooAsset.Editor;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    internal static class BuildActionCommon
    {
        internal const string NoEncryptionPolicy = "none";

        [Serializable]
        internal sealed class ArtifactReceipt
        {
            public bool finalized;
            public string target;
            public string outputPath;
            public string outputKind;
            public string outputHashSha256;
            public string manifestPath;
            public string manifestHashSha256;
            public string bundledOutputPath;
            public string bundledOutputHashSha256;
            public string packageName;
            public string packageVersion;
            public string[] scenes;
            public bool? developmentBuild;
            public string buildMode;
            public string buildReportResult;
        }

        [Serializable]
        internal sealed class BundleInputSnapshot
        {
            public string masterGuid;
            public string masterPath;
            public string masterHashSha256;
            public string platform;
            public string channel;
            public string developMode;
            public string yooAssetSettingsPath;
            public string yooAssetSettingsGuid;
            public string yooAssetSettingsHashSha256;
            public string yooFolderName;
            public string packageFilePrefix;
            public string bundleCollectorPath;
            public string bundleCollectorGuid;
            public string bundleCollectorHashSha256;
            public string packageName;
        }

        [Serializable]
        internal sealed class PlayerSettingsSnapshot
        {
            public string target;
            public string standaloneSubtarget;
            public bool developmentBuild;
            public string projectSettingsHashSha256;
            public string companyName;
            public string productName;
            public string bundleVersion;
        }

        internal static string ProjectRoot =>
            Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath);

        internal static bool TryResolveActiveTarget(string value, out BuildTarget target, out string error)
        {
            target = BuildTarget.NoTarget;
            error = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 ||
                !Enum.GetNames(typeof(BuildTarget)).Contains(value, StringComparer.Ordinal) ||
                !Enum.TryParse(value, false, out target) || target == BuildTarget.NoTarget)
            {
                error = "target 必须是 Unity BuildTarget 的精确枚举值，且不能为 NoTarget。";
                return false;
            }

            if (target != EditorUserBuildSettings.activeBuildTarget)
            {
                error = $"target={target} 与当前 activeBuildTarget={EditorUserBuildSettings.activeBuildTarget} 不一致；Build Action 不会切换活动平台。";
                return false;
            }
            return true;
        }

        internal static bool TryValidateName(string field, string value, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            {
                error = field + " 必须是 1-128 个字符的非空值。";
                return false;
            }
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsLetterOrDigit(character) && character != '.' && character != '_' && character != '-')
                {
                    error = field + " 只能包含字母、数字、点、下划线和连字符。";
                    return false;
                }
            }
            if (value == "." || value == "..")
            {
                error = field + " 不能为路径导航片段。";
                return false;
            }
            return true;
        }

        internal static bool TryResolveOutputPath(string value, out string outputPath, out string error)
        {
            outputPath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.IndexOf('\0') >= 0)
            {
                error = "outputPath 必须是长度不超过 2048 的非空文件或目录路径。";
                return false;
            }
            if (value.StartsWith("~", StringComparison.Ordinal))
            {
                error = "outputPath 不支持 ~ 展开，请提供项目相对路径或绝对路径。";
                return false;
            }
            try
            {
                outputPath = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(ProjectRoot, value));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                error = "outputPath 无法规范化：" + exception.Message;
                return false;
            }
            if (string.Equals(outputPath, Path.GetPathRoot(outputPath), StringComparison.Ordinal))
            {
                error = "outputPath 不能是文件系统根目录。";
                return false;
            }
            return true;
        }

        internal static bool IsSupportedPlayerTarget(BuildTarget target)
        {
            return target == BuildTarget.StandaloneOSX ||
                   target == BuildTarget.StandaloneWindows ||
                   target == BuildTarget.StandaloneWindows64 ||
                   target == BuildTarget.StandaloneLinux64;
        }

        internal static PlayerSettingsSnapshot CapturePlayerSettings(
            BuildTarget target,
            bool developmentBuild,
            CancellationToken cancellationToken)
        {
            string projectSettingsPath = Path.Combine(ProjectRoot, "ProjectSettings/ProjectSettings.asset");
            return new PlayerSettingsSnapshot
            {
                target = target.ToString(),
                standaloneSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget.ToString(),
                developmentBuild = developmentBuild,
                projectSettingsHashSha256 = File.Exists(projectSettingsPath)
                    ? ComputeFileHash(projectSettingsPath, cancellationToken)
                    : null,
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                bundleVersion = PlayerSettings.bundleVersion,
            };
        }

        internal static bool PlayerSettingsEqual(PlayerSettingsSnapshot expected, PlayerSettingsSnapshot actual)
        {
            return expected != null && actual != null &&
                   string.Equals(Util.Json.Serialize(expected), Util.Json.Serialize(actual), StringComparison.Ordinal);
        }

        internal static bool TryCaptureBundleInputs(
            string packageName,
            CancellationToken cancellationToken,
            out BundleInputSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = null;
            if (!EditorUtil.Config.WorkspaceActive.TryGetPersistedConfigMaster(
                    out ConfigMasterSO master, out string masterGuid, out string masterPath, out error))
            {
                return false;
            }
            if (master.CurrentPlatform == PlatformType.None)
            {
                error = "激活 ConfigMaster 当前 Platform 不能为 None。";
                return false;
            }

            EditorUtil.Config.DimensionalResolver.YooAssetResult resolved =
                EditorUtil.Config.DimensionalResolver.ResolveYooAsset(
                    master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
            string settingsPath = NormalizeAssetPath(resolved.YooAssetSettingsPath);
            string collectorPath = NormalizeAssetPath(resolved.BundleCollectorSettingPath);
            YooAssetSettings settings = string.IsNullOrEmpty(settingsPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<YooAssetSettings>(settingsPath);
            BundleCollectorSetting collector = string.IsNullOrEmpty(collectorPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<BundleCollectorSetting>(collectorPath);
            if (settings == null || collector == null)
            {
                error = "当前坐标必须显式指向可加载的 YooAssetSettings 与 BundleCollectorSetting。";
                return false;
            }
            int packageCount = collector.Packages?.Count(item => item != null && item.PackageName == packageName) ?? 0;
            if (packageCount != 1)
            {
                error = packageCount == 0
                    ? "BundleCollectorSetting 未配置目标 Package：" + packageName
                    : "BundleCollectorSetting 中目标 Package 名称不唯一：" + packageName;
                return false;
            }
            BundleCollectorPackage package = collector.GetPackage(packageName);
            if (!TryValidateBundlePackageStructure(package, out string packageError))
            {
                error = "目标 Package 的 Collector 配置未通过只读结构校验：" + packageError;
                return false;
            }

            snapshot = new BundleInputSnapshot
            {
                masterGuid = masterGuid,
                masterPath = masterPath,
                masterHashSha256 = ComputeAssetHash(master, masterPath, cancellationToken),
                platform = master.CurrentPlatform.ToString(),
                channel = master.CurrentChannel.ToString(),
                developMode = master.CurrentDevelopMode.ToString(),
                yooAssetSettingsPath = settingsPath,
                yooAssetSettingsGuid = AssetDatabase.AssetPathToGUID(settingsPath),
                yooAssetSettingsHashSha256 = ComputeAssetHash(settings, settingsPath, cancellationToken),
                yooFolderName = settings.YooFolderName,
                packageFilePrefix = settings.PackageFilePrefix,
                bundleCollectorPath = collectorPath,
                bundleCollectorGuid = AssetDatabase.AssetPathToGUID(collectorPath),
                bundleCollectorHashSha256 = ComputeAssetHash(collector, collectorPath, cancellationToken),
                packageName = packageName,
            };
            return true;
        }

        private static bool TryValidateBundlePackageStructure(
            BundleCollectorPackage package,
            out string error)
        {
            error = null;
            if (package == null)
            {
                error = "Package 为空。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(package.IgnoreRuleName) ||
                !BundleCollectorSettingData.HasAssetIgnoreRuleName(package.IgnoreRuleName))
            {
                error = "IgnoreRuleName 无效：" + package.IgnoreRuleName;
                return false;
            }
            if (package.Groups == null)
            {
                error = "Groups 为空。";
                return false;
            }

            foreach (BundleCollectorGroup group in package.Groups)
            {
                if (group == null)
                {
                    error = "Groups 包含空项。";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(group.ActiveRuleName) ||
                    !BundleCollectorSettingData.HasGroupActiveRuleName(group.ActiveRuleName))
                {
                    error = $"Group '{group.GroupName}' 的 ActiveRuleName 无效：{group.ActiveRuleName}";
                    return false;
                }
                if (group.Collectors == null)
                {
                    error = $"Group '{group.GroupName}' 的 Collectors 为空。";
                    return false;
                }

                foreach (BundleCollector item in group.Collectors)
                {
                    if (item == null)
                    {
                        error = $"Group '{group.GroupName}' 的 Collectors 包含空项。";
                        return false;
                    }
                    if (string.IsNullOrWhiteSpace(item.CollectPath) ||
                        string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(item.CollectPath)))
                    {
                        error = "Collector 路径无效：" + item.CollectPath;
                        return false;
                    }
                    if (item.CollectorType == ECollectorType.None ||
                        !BundleCollectorSettingData.HasBundlePackRuleName(item.PackRuleName) ||
                        !BundleCollectorSettingData.HasAssetFilterRuleName(item.FilterRuleName) ||
                        !BundleCollectorSettingData.HasAddressRuleName(item.AddressRuleName))
                    {
                        error = "Collector 规则无效：" + item.CollectPath;
                        return false;
                    }
                }
            }
            return true;
        }

        internal static bool BundleInputsEqual(BundleInputSnapshot expected, BundleInputSnapshot actual)
        {
            return expected != null && actual != null &&
                   string.Equals(Util.Json.Serialize(expected), Util.Json.Serialize(actual), StringComparison.Ordinal);
        }

        internal static bool TryValidateActiveBundleRuntime(
            BundleInputSnapshot expected,
            string packageVersion,
            out string error)
        {
            error = null;
            if (expected == null)
            {
                error = "Bundle 冻结输入为空。";
                return false;
            }
            BundleCollectorSetting activeCollector;
            try
            {
                activeCollector = BundleCollectorSettingData.Setting;
            }
            catch (Exception exception)
            {
                error = "当前 YooAsset Collector 无法加载：" + exception.Message;
                return false;
            }
            if (activeCollector == null ||
                !string.Equals(AssetDatabase.GetAssetPath(activeCollector), expected.bundleCollectorPath, StringComparison.Ordinal))
            {
                error = "YooAsset 当前 Collector 缓存未指向冻结的 BundleCollectorSetting。";
                return false;
            }

            string expectedManifestName = string.IsNullOrEmpty(expected.packageFilePrefix)
                ? $"{expected.packageName}_{packageVersion}.bytes"
                : $"{expected.packageFilePrefix}_{expected.packageName}_{packageVersion}.bytes";
            if (!string.Equals(YooAssetConfiguration.GetYooFolderName(), expected.yooFolderName, StringComparison.Ordinal) ||
                !string.Equals(
                    YooAssetConfiguration.GetManifestBinaryFileName(expected.packageName, packageVersion),
                    expectedManifestName,
                    StringComparison.Ordinal))
            {
                error = "YooAssetConfiguration 当前缓存未指向冻结的 YooAssetSettings。";
                return false;
            }
            return true;
        }

        internal static bool TryResolveEncryptionPolicy(string value, out string error)
        {
            if (value == NoEncryptionPolicy)
            {
                error = null;
                return true;
            }
            error = "encryptionPolicy 首版仅允许逻辑值 none；不接受 C# class/type name。";
            return false;
        }

        internal static bool TryResolveCompression(string value, out ECompressOption result, out string error)
        {
            switch (value)
            {
                case "uncompressed": result = ECompressOption.Uncompressed; error = null; return true;
                case "lzma": result = ECompressOption.LZMA; error = null; return true;
                case "lz4": result = ECompressOption.LZ4; error = null; return true;
                default: result = default; error = "compression 仅允许 uncompressed、lzma 或 lz4。"; return false;
            }
        }

        internal static bool TryResolveFileNameStyle(string value, out EFileNameStyle result, out string error)
        {
            switch (value)
            {
                case "hash-name": result = EFileNameStyle.HashName; error = null; return true;
                case "bundle-name": result = EFileNameStyle.BundleName; error = null; return true;
                case "bundle-name-hash-name": result = EFileNameStyle.BundleName_HashName; error = null; return true;
                default: result = default; error = "fileNameStyle 仅允许 hash-name、bundle-name 或 bundle-name-hash-name。"; return false;
            }
        }

        internal static bool TryResolveBundledCopyOption(
            string value,
            out EBundledCopyOption result,
            out string error)
        {
            switch (value)
            {
                case "none": result = EBundledCopyOption.None; error = null; return true;
                case "clear-and-copy-all": result = EBundledCopyOption.ClearAndCopyAll; error = null; return true;
                case "clear-and-copy-by-tags": result = EBundledCopyOption.ClearAndCopyByTags; error = null; return true;
                case "only-copy-all": result = EBundledCopyOption.OnlyCopyAll; error = null; return true;
                case "only-copy-by-tags": result = EBundledCopyOption.OnlyCopyByTags; error = null; return true;
                default:
                    result = default;
                    error = "bundledCopyOption 不是允许的逻辑值。";
                    return false;
            }
        }

        internal static bool RequiresTags(EBundledCopyOption option)
        {
            return option == EBundledCopyOption.ClearAndCopyByTags || option == EBundledCopyOption.OnlyCopyByTags;
        }

        internal static string ResolveBundleOutput(BuildTarget target, string packageName, string version)
        {
            return Path.GetFullPath(Path.Combine(
                BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                target.ToString(),
                packageName,
                version));
        }

        internal static string ResolveBundleManifest(string outputPath, string packageName, string version)
        {
            return Path.Combine(outputPath, YooAssetConfiguration.GetManifestBinaryFileName(packageName, version));
        }

        internal static string ResolveBundleManifest(
            string outputPath,
            string packageName,
            string version,
            string packageFilePrefix)
        {
            string fileName = string.IsNullOrEmpty(packageFilePrefix)
                ? $"{packageName}_{version}.bytes"
                : $"{packageFilePrefix}_{packageName}_{version}.bytes";
            return Path.Combine(outputPath, fileName);
        }

        internal static string ResolveStreamingAssetsRoot()
        {
            string path = BundleBuilderHelper.GetStreamingAssetsRoot();
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path));
        }

        internal static string ResolveBundledPackageRoot(string packageName)
        {
            return Path.Combine(ResolveStreamingAssetsRoot(), packageName);
        }

        internal static ArtifactReceipt CaptureArtifact(
            ArtifactReceipt expected,
            string buildReportResult,
            CancellationToken cancellationToken)
        {
            if (expected == null || string.IsNullOrWhiteSpace(expected.outputPath))
            {
                throw new InvalidOperationException("Build Receipt 缺少 outputPath。");
            }

            var receipt = new ArtifactReceipt
            {
                finalized = true,
                target = expected.target,
                outputPath = Path.GetFullPath(expected.outputPath),
                manifestPath = string.IsNullOrWhiteSpace(expected.manifestPath)
                    ? null
                    : Path.GetFullPath(expected.manifestPath),
                bundledOutputPath = string.IsNullOrWhiteSpace(expected.bundledOutputPath)
                    ? null
                    : Path.GetFullPath(expected.bundledOutputPath),
                packageName = expected.packageName,
                packageVersion = expected.packageVersion,
                scenes = expected.scenes == null ? null : (string[])expected.scenes.Clone(),
                developmentBuild = expected.developmentBuild,
                buildMode = expected.buildMode,
                buildReportResult = buildReportResult,
            };

            if (File.Exists(receipt.outputPath))
            {
                receipt.outputKind = "file";
                receipt.outputHashSha256 = ComputeFileHash(receipt.outputPath, cancellationToken);
            }
            else if (Directory.Exists(receipt.outputPath))
            {
                receipt.outputKind = "directory";
                receipt.outputHashSha256 = ComputeDirectoryHash(receipt.outputPath, cancellationToken);
            }
            else
            {
                throw new FileNotFoundException("构建产物不存在。", receipt.outputPath);
            }

            if (!string.IsNullOrWhiteSpace(receipt.manifestPath))
            {
                if (!File.Exists(receipt.manifestPath))
                {
                    throw new FileNotFoundException("构建 manifest 不存在。", receipt.manifestPath);
                }
                receipt.manifestHashSha256 = ComputeFileHash(receipt.manifestPath, cancellationToken);
            }
            if (!string.IsNullOrWhiteSpace(receipt.bundledOutputPath))
            {
                if (!Directory.Exists(receipt.bundledOutputPath))
                {
                    throw new DirectoryNotFoundException("BundledCopy 目标目录不存在：" + receipt.bundledOutputPath);
                }
                receipt.bundledOutputHashSha256 = ComputeDirectoryHash(receipt.bundledOutputPath, cancellationToken);
            }
            return receipt;
        }

        internal static AgentActionResult VerifyArtifact(
            string receiptJson,
            string actionLabel,
            CancellationToken cancellationToken)
        {
            ArtifactReceipt expected;
            try
            {
                expected = Util.Json.Deserialize<ArtifactReceipt>(receiptJson);
            }
            catch (Exception exception)
            {
                return AgentActionResult.Create(null, "blocked", actionLabel + " Receipt 无法解析：" + exception.Message);
            }

            try
            {
                if (expected == null || !expected.finalized)
                {
                    return AgentActionResult.Create(null, "partial", actionLabel + " Recovery Receipt 尚未包含执行后产物证据。" );
                }
                if (!string.Equals(expected.buildReportResult, "Succeeded", StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(expected.outputKind) ||
                    string.IsNullOrWhiteSpace(expected.outputHashSha256) ||
                    !string.IsNullOrWhiteSpace(expected.manifestPath) && string.IsNullOrWhiteSpace(expected.manifestHashSha256) ||
                    !string.IsNullOrWhiteSpace(expected.bundledOutputPath) && string.IsNullOrWhiteSpace(expected.bundledOutputHashSha256))
                {
                    return AgentActionResult.Create(null, "partial", actionLabel + " Receipt 缺少完整的执行后 Hash 或 Succeeded 证据。" );
                }
                ArtifactReceipt actual = CaptureArtifact(expected, expected?.buildReportResult, cancellationToken);
                if (!string.IsNullOrWhiteSpace(expected.outputKind) && expected.outputKind != actual.outputKind)
                {
                    return AgentActionResult.Create(null, "partial", actionLabel + " 产物类型已变化。" );
                }
                if (!RequiredHashMatches(expected.outputHashSha256, actual.outputHashSha256) ||
                    !OptionalHashMatches(expected.manifestHashSha256, actual.manifestHashSha256) ||
                    !OptionalHashMatches(expected.bundledOutputHashSha256, actual.bundledOutputHashSha256))
                {
                    AgentActionResult changed = AgentActionResult.Create(null, "partial", actionLabel + " 产物或 manifest Hash 与 Receipt 不一致。");
                    changed.DataJson = Util.Json.Serialize(actual);
                    changed.Artifacts.Add(actual.outputPath);
                    return changed;
                }

                AgentActionResult success = AgentActionResult.Create(null, "success", actionLabel + " 产物路径、类型、manifest 与 SHA-256 已只读核对。");
                success.DataJson = Util.Json.Serialize(actual);
                success.EvidenceKinds = AgentActionEvidence.Artifact;
                success.Artifacts.Add(actual.outputPath);
                if (!string.IsNullOrWhiteSpace(actual.manifestPath)) success.Artifacts.Add(actual.manifestPath);
                if (!string.IsNullOrWhiteSpace(actual.bundledOutputPath)) success.Artifacts.Add(actual.bundledOutputPath);
                success.Evidence.Add("Verify 仅只读检查文件/目录存在性、manifest 与 SHA-256；不会重放构建。" );
                if (!string.IsNullOrWhiteSpace(actual.buildReportResult))
                {
                    success.Evidence.Add("BuildReport 只证明 Unity 构建完成，不证明 Player 已启动或业务运行正确。" );
                }
                else
                {
                    success.Warnings.Add("RecoveryPayload 没有 BuildReport 结果；当前只证明期望路径上的产物状态满足约束。" );
                }
                return success;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return AgentActionResult.Create(null, "partial", actionLabel + " 产物验证未通过：" + exception.Message);
            }
        }

        internal static bool SceneSnapshotsEqual(string[] expected, string[] actual)
        {
            return (expected ?? Array.Empty<string>()).SequenceEqual(actual ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        internal static string[] GetEnabledBuildScenes()
        {
            return EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        }

        private static bool RequiredHashMatches(string expected, string actual)
        {
            return !string.IsNullOrWhiteSpace(expected) && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        private static bool OptionalHashMatches(string expected, string actual)
        {
            return string.IsNullOrWhiteSpace(expected)
                ? string.IsNullOrWhiteSpace(actual)
                : string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeAssetHash(UnityEngine.Object asset, string assetPath, CancellationToken cancellationToken)
        {
            string absolutePath = Path.Combine(ProjectRoot, NormalizeAssetPath(assetPath));
            string fileHash = File.Exists(absolutePath) ? ComputeFileHash(absolutePath, cancellationToken) : string.Empty;
            string serialized = asset == null ? string.Empty : EditorJsonUtility.ToJson(asset, false);
            using (SHA256 sha256 = SHA256.Create())
            {
                return ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(fileHash + "\n" + serialized)));
            }
        }

        private static string NormalizeAssetPath(string value)
        {
            return string.IsNullOrEmpty(value) ? value : value.Replace('\\', '/');
        }

        private static string ComputeFileHash(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] buffer = new byte[1024 * 1024];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sha256.AppendData(buffer, 0, read);
                }
                return ToHex(sha256.GetHashAndReset());
            }
        }

        private static string ComputeDirectoryHash(string directory, CancellationToken cancellationToken)
        {
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            using (IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                foreach (string file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    string entry = relative + "\n" + new FileInfo(file).Length.ToString(CultureInfo.InvariantCulture) + "\n" +
                                   ComputeFileHash(file, cancellationToken) + "\n";
                    byte[] bytes = Encoding.UTF8.GetBytes(entry);
                    sha256.AppendData(bytes);
                }
                return ToHex(sha256.GetHashAndReset());
            }
        }

        private static string ToHex(byte[] bytes)
        {
            return bytes == null ? null : string.Concat(bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
