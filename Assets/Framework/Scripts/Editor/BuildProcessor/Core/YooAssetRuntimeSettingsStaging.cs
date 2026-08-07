/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  YooAssetRuntimeSettingsStaging.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   YooAsset 运行时 Settings 构建期唯一副本暂存服务
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 管理正式 Player 构建期间唯一存在的 Resources/YooAssetSettings.asset 临时副本。
    /// </summary>
    [InitializeOnLoad]
    internal static class YooAssetRuntimeSettingsStaging
    {
        private const string c_HybridClrTemporaryBuildSegment = "/HybridCLRData/StrippedAOTDllsTempProj/";
        private const string c_MarkerRelativePath = "Library/Nova/YooAssetRuntimeSettingsStaging.json";
        private const string c_RuntimeSettingsFileName = "YooAssetSettings.asset";
        private static bool s_BuildObserved;

        /// <summary>
        /// 构建期临时副本的持久化所有权记录；字段名保持可读 JSON 形式。
        /// </summary>
        [Serializable]
        private sealed class OwnershipMarker
        {
            public string sourceAssetPath;
            public string targetAssetPath;
            public string expectedContentHash;
            public string resourcesDirectory;
            public bool createdResourcesDirectory;
        }

        /// <summary>
        /// 注册构建结束监控、域重载和退出恢复入口；marker 是跨域恢复的唯一真相源。
        /// </summary>
        static YooAssetRuntimeSettingsStaging()
        {
            EditorApplication.update -= MonitorBuildCompletion;
            EditorApplication.update += MonitorBuildCompletion;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupBeforeAssemblyReload;
            EditorApplication.quitting -= CleanupBeforeEditorQuit;
            EditorApplication.quitting += CleanupBeforeEditorQuit;
            EditorApplication.delayCall += RecoverAfterEditorLoad;
        }

        /// <summary>
        /// 为正式 Player 构建解析当前 ConfigMaster 并创建唯一运行时副本。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        internal static void StageForBuild(BuildReport report)
        {
            string outputPath = report?.summary.outputPath;
            if (ShouldSkipBuild(outputPath))
            {
                return;
            }

            EnsureStaleStagingRecovered();
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master == null)
            {
                throw new InvalidOperationException("未找到当前激活的 ConfigMasterSO，无法生成 YooAsset 运行时 Settings。");
            }

            string masterPath = AssetDatabase.GetAssetPath(master);
            string demoRoot = ResolveDemoRoot(masterPath);
            string sourcePath = ResolveYooAssetSettingsPath(master);
            if (string.IsNullOrEmpty(sourcePath) || AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
            {
                throw new InvalidOperationException(
                    $"当前 ConfigMaster 解析出的 YooAssetSettings 不存在：{sourcePath}");
            }

            StageExplicit(sourcePath, demoRoot, IsConsumerLayout(masterPath));
            s_BuildObserved = BuildPipeline.isBuildingPlayer;
        }

        /// <summary>
        /// 使用显式源和 Demo 根目录创建临时副本；供构建入口与隔离测试共用。
        /// </summary>
        /// <param name="sourceAssetPath">权威 Editor YooAssetSettings 路径。</param>
        /// <param name="demoRoot">当前 Demo 根目录。</param>
        /// <param name="consumerLayout">是否使用消费态 Resources 选择规则。</param>
        /// <returns>生成的临时运行时资产路径。</returns>
        internal static string StageExplicit(string sourceAssetPath, string demoRoot, bool consumerLayout)
        {
            EnsureStaleStagingRecovered();
            string source = NormalizeAssetPath(sourceAssetPath);
            if (AssetDatabase.LoadMainAssetAtPath(source) == null)
            {
                throw new InvalidOperationException($"YooAssetSettings 权威源不存在：{source}");
            }

            string[] existingRuntimeSettings = FindRuntimeSettingsAssetPaths();
            string[] resourcesDirectories = FindResourcesDirectories(demoRoot);
            string targetDirectory = ResolveTargetResourcesDirectory(demoRoot, consumerLayout, resourcesDirectories);
            string targetAssetPath = $"{targetDirectory}/{c_RuntimeSettingsFileName}";
            ValidateNoRuntimeSettingsConflicts(targetAssetPath, existingRuntimeSettings);

            bool createdDirectory = !AssetDatabase.IsValidFolder(targetDirectory);
            string expectedHash = ComputeFileHash(source);
            OwnershipMarker marker = new OwnershipMarker
            {
                sourceAssetPath = source,
                targetAssetPath = targetAssetPath,
                expectedContentHash = expectedHash,
                resourcesDirectory = targetDirectory,
                createdResourcesDirectory = createdDirectory,
            };
            WriteMarker(marker);

            try
            {
                if (createdDirectory)
                {
                    string parent = NormalizeAssetPath(IOPath.GetDirectoryName(targetDirectory));
                    string folderName = IOPath.GetFileName(targetDirectory);
                    string guid = AssetDatabase.CreateFolder(parent, folderName);
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new IOException($"创建 Resources 目录失败：{targetDirectory}");
                    }
                }

                if (!AssetDatabase.CopyAsset(source, targetAssetPath))
                {
                    throw new IOException($"复制 YooAssetSettings 失败：{source} -> {targetAssetPath}");
                }
                AssetDatabase.ImportAsset(targetAssetPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.SaveAssets();
                string actualHash = ComputeFileHash(targetAssetPath);
                if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                {
                    throw new IOException("YooAssetSettings 临时副本内容与权威源不一致。");
                }
                return targetAssetPath;
            }
            catch
            {
                CleanupOwnedStaging(true);
                throw;
            }
        }

        /// <summary>
        /// 校验构建前工程中不存在任何常驻运行时 YooAssetSettings，禁止覆盖用户文件。
        /// </summary>
        /// <param name="targetAssetPath">本轮计划创建的目标路径，仅用于错误信息。</param>
        /// <param name="runtimeSettingsAssetPaths">已发现的 Resources/YooAssetSettings.asset 路径。</param>
        internal static void ValidateNoRuntimeSettingsConflicts(
            string targetAssetPath,
            string[] runtimeSettingsAssetPaths)
        {
            string[] conflicts = runtimeSettingsAssetPaths ?? Array.Empty<string>();
            if (conflicts.Length == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"构建前发现常驻 Resources/YooAssetSettings.asset，已拒绝覆盖。Target={targetAssetPath}, " +
                $"Conflicts={string.Join(", ", conflicts.OrderBy(path => path, StringComparer.Ordinal))}");
        }

        /// <summary>
        /// 删除 marker 证明归本工具所有且内容未变化的临时副本。
        /// </summary>
        /// <param name="logErrors">清理失败时是否输出错误日志。</param>
        /// <returns>已清理或无需清理返回 true；内容漂移或清理异常返回 false。</returns>
        internal static bool CleanupOwnedStaging(bool logErrors)
        {
            string markerPath = GetMarkerFilePath();
            if (!File.Exists(markerPath))
            {
                return true;
            }

            try
            {
                OwnershipMarker marker = ReadMarker(markerPath);
                if (marker == null || string.IsNullOrEmpty(marker.targetAssetPath))
                {
                    throw new InvalidDataException("YooAssetSettings staging marker 内容无效。");
                }

                if (File.Exists(AssetPathToAbsolutePath(marker.targetAssetPath)))
                {
                    string actualHash = ComputeFileHash(marker.targetAssetPath);
                    if (!string.Equals(actualHash, marker.expectedContentHash, StringComparison.Ordinal))
                    {
                        if (logErrors)
                        {
                            Debug.LogError(
                                $"[YooAssetSettingsStaging] 临时副本内容已变化，已保留文件并停止清理：{marker.targetAssetPath}");
                        }
                        return false;
                    }

                    if (!AssetDatabase.DeleteAsset(marker.targetAssetPath))
                    {
                        throw new IOException($"删除 YooAssetSettings 临时副本失败：{marker.targetAssetPath}");
                    }
                }

                if (marker.createdResourcesDirectory
                    && AssetDatabase.IsValidFolder(marker.resourcesDirectory)
                    && IsAssetDirectoryEmpty(marker.resourcesDirectory))
                {
                    AssetDatabase.DeleteAsset(marker.resourcesDirectory);
                }

                File.Delete(markerPath);
                s_BuildObserved = false;
                return true;
            }
            catch (Exception exception)
            {
                if (logErrors)
                {
                    Debug.LogError($"[YooAssetSettingsStaging] 清理失败：{exception.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// 获取持久化 ownership marker 的绝对路径。
        /// </summary>
        internal static string GetMarkerFilePath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("无法解析 Unity 工程根目录。");
            }
            return IOPath.Combine(projectRoot, c_MarkerRelativePath);
        }

        /// <summary>
        /// 根据 ConfigMaster 约定布局解析当前 Demo 根目录。
        /// ConfigMaster 必须位于 DemoRoot/Editor/ConfigMaster.asset。
        /// </summary>
        /// <param name="configMasterPath">ConfigMaster 的 Unity AssetPath。</param>
        /// <returns>当前 Demo 根目录的 Unity AssetPath。</returns>
        internal static string ResolveDemoRoot(string configMasterPath)
        {
            string normalized = NormalizeAssetPath(configMasterPath);
            string editorDirectory = NormalizeAssetPath(IOPath.GetDirectoryName(normalized));
            if (string.IsNullOrEmpty(editorDirectory)
                || !string.Equals(IOPath.GetFileName(editorDirectory), "Editor", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"ConfigMaster 不符合 DemoRoot/Editor/ConfigMaster.asset 布局：{configMasterPath}");
            }

            string demoRoot = NormalizeAssetPath(IOPath.GetDirectoryName(editorDirectory));
            if (string.IsNullOrEmpty(demoRoot) || !demoRoot.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"无法从 ConfigMaster 解析 Demo 根目录：{configMasterPath}");
            }
            return demoRoot;
        }

        /// <summary>
        /// 按当前 ConfigMaster 的 Platform / Channel / DevelopMode 三维坐标解析权威 Settings 路径。
        /// </summary>
        /// <param name="master">当前激活的 ConfigMaster。</param>
        /// <returns>规范化后的 YooAssetSettings 项目相对路径。</returns>
        internal static string ResolveYooAssetSettingsPath(ConfigMasterSO master)
        {
            if (master == null)
            {
                throw new ArgumentNullException(nameof(master));
            }

            EditorUtil.Config.DimensionalResolver.YooAssetResult resolved =
                EditorUtil.Config.DimensionalResolver.ResolveYooAsset(
                    master,
                    master.CurrentPlatform,
                    master.CurrentChannel,
                    master.CurrentDevelopMode);
            return NormalizeAssetPath(resolved.YooAssetSettingsPath);
        }

        /// <summary>
        /// 解析运行时 Settings 的目标 Resources 目录。
        /// 开发态固定使用 Demo 根目录；消费态仅在当前 Demo 内按相对深度和路径稳定选择。
        /// </summary>
        /// <param name="demoRoot">当前 Demo 根目录。</param>
        /// <param name="consumerLayout">是否为 UPM Sample 消费态布局。</param>
        /// <param name="existingResourcesDirectories">工程中已存在的 Resources 目录。</param>
        /// <returns>应暂存 YooAssetSettings 的 Resources 目录。</returns>
        internal static string ResolveTargetResourcesDirectory(
            string demoRoot,
            bool consumerLayout,
            string[] existingResourcesDirectories)
        {
            string normalizedRoot = NormalizeAssetPath(demoRoot).TrimEnd('/');
            if (!consumerLayout)
            {
                return $"{normalizedRoot}/Resources";
            }

            IEnumerable<string> candidates = existingResourcesDirectories ?? Array.Empty<string>();
            string selected = candidates
                .Select(NormalizeAssetPath)
                .Where(path => path.StartsWith($"{normalizedRoot}/", StringComparison.Ordinal)
                               && string.Equals(IOPath.GetFileName(path), "Resources", StringComparison.Ordinal))
                .OrderBy(path => GetRelativeDepth(normalizedRoot, path))
                .ThenBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            return string.IsNullOrEmpty(selected) ? $"{normalizedRoot}/Resources" : selected;
        }

        /// <summary>
        /// 判断构建是否为 HybridCLR 生成裁剪 AOT DLL 使用的内部临时 Player 构建。
        /// </summary>
        /// <param name="outputPath">BuildReport 输出路径。</param>
        /// <returns>临时构建返回 true，正式 Player 构建返回 false。</returns>
        internal static bool ShouldSkipBuild(string outputPath)
        {
            string normalized = NormalizeAssetPath(outputPath);
            return normalized.IndexOf(c_HybridClrTemporaryBuildSegment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Nova 自有 BuildPlayer finally 调用的同步清理兜底。
        /// </summary>
        internal static void CleanupAfterBuild()
        {
            CleanupOwnedStaging(true);
        }

        /// <summary>
        /// Editor PlayMode 在 YooAsset SubsystemRegistration 清空静态缓存后重新注入权威配置。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InjectForEditorPlayMode()
        {
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master == null)
            {
                Debug.LogWarning("[YooAssetSettingsStaging] 未找到当前 ConfigMaster，跳过 Editor PlayMode 注入。");
                return;
            }

            EditorUtil.Config.YooAssetInjector.InjectByPath(ResolveYooAssetSettingsPath(master));
        }

        /// <summary>
        /// 根据 ConfigMaster 路径深度判断是否为 UPM Sample 消费态嵌套布局。
        /// </summary>
        private static bool IsConsumerLayout(string configMasterPath)
        {
            string normalized = NormalizeAssetPath(configMasterPath);
            const string prefix = "Assets/Samples/";
            int editorIndex = normalized.LastIndexOf("/Editor/", StringComparison.Ordinal);
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal) || editorIndex < prefix.Length)
            {
                return false;
            }
            string relativeRoot = normalized.Substring(prefix.Length, editorIndex - prefix.Length);
            return relativeRoot.Contains("/");
        }

        /// <summary>
        /// 查找当前 Demo 根目录内所有 Resources 目录并转换为 Unity AssetPath。
        /// </summary>
        private static string[] FindResourcesDirectories(string demoRoot)
        {
            string normalizedRoot = NormalizeAssetPath(demoRoot);
            string absoluteRoot = AssetPathToAbsolutePath(normalizedRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                return Array.Empty<string>();
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return Directory.GetDirectories(absoluteRoot, "Resources", SearchOption.AllDirectories)
                .Select(path => NormalizeAssetPath(IOPath.GetRelativePath(projectRoot, path)))
                .ToArray();
        }

        /// <summary>
        /// 查找所有会被 Resources.Load("YooAssetSettings") 命中的常驻资产。
        /// </summary>
        private static string[] FindRuntimeSettingsAssetPaths()
        {
            return AssetDatabase.FindAssets("t:YooAssetSettings")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(NormalizeAssetPath)
                .Where(path => path.EndsWith($"/Resources/{c_RuntimeSettingsFileName}", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 暂存开始前恢复上次异常退出留下的、仍可证明所有权的副本。
        /// </summary>
        private static void EnsureStaleStagingRecovered()
        {
            if (!File.Exists(GetMarkerFilePath()))
            {
                return;
            }
            if (!CleanupOwnedStaging(true))
            {
                throw new InvalidOperationException(
                    "检测到无法安全恢复的 YooAssetSettings 临时副本，请处理 Console 指向的冲突文件后重试。");
            }
        }

        /// <summary>
        /// Editor 加载完成后恢复上次崩溃或强制退出留下的临时副本。
        /// </summary>
        private static void RecoverAfterEditorLoad()
        {
            if (!BuildPipeline.isBuildingPlayer)
            {
                CleanupOwnedStaging(true);
            }
        }

        /// <summary>
        /// 监控所有 BuildPipeline 入口，在构建退出后补偿可能未执行的 postprocess。
        /// </summary>
        private static void MonitorBuildCompletion()
        {
            if (!File.Exists(GetMarkerFilePath()))
            {
                s_BuildObserved = false;
                return;
            }
            if (BuildPipeline.isBuildingPlayer)
            {
                s_BuildObserved = true;
                return;
            }
            if (s_BuildObserved)
            {
                CleanupOwnedStaging(true);
            }
        }

        /// <summary>
        /// 域重载前尝试清理，失败时保留 marker 供下次加载恢复。
        /// </summary>
        private static void CleanupBeforeAssemblyReload()
        {
            CleanupOwnedStaging(true);
        }

        /// <summary>
        /// Editor 正常退出前尝试清理，进程崩溃场景由 marker 在下次启动恢复。
        /// </summary>
        private static void CleanupBeforeEditorQuit()
        {
            CleanupOwnedStaging(true);
        }

        /// <summary>
        /// 将 ownership marker 原子写入 Library，保证副本出现前已有恢复凭据。
        /// </summary>
        private static void WriteMarker(OwnershipMarker marker)
        {
            string markerPath = GetMarkerFilePath();
            string directory = IOPath.GetDirectoryName(markerPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string temporaryPath = markerPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(marker, true), new UTF8Encoding(false));
            if (File.Exists(markerPath))
            {
                File.Replace(temporaryPath, markerPath, null);
            }
            else
            {
                File.Move(temporaryPath, markerPath);
            }
        }

        /// <summary>
        /// 读取 ownership marker；JSON 解析失败时由调用方按不安全残留处理。
        /// </summary>
        private static OwnershipMarker ReadMarker(string markerPath)
        {
            return JsonUtility.FromJson<OwnershipMarker>(File.ReadAllText(markerPath));
        }

        /// <summary>
        /// 计算资产正文 SHA-256，用于清理前确认文件仍为本工具创建的副本。
        /// </summary>
        private static string ComputeFileHash(string assetPath)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(File.ReadAllBytes(AssetPathToAbsolutePath(assetPath)));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// 判断 Unity 资产目录是否不包含任何实际文件或子目录。
        /// </summary>
        private static bool IsAssetDirectoryEmpty(string assetDirectory)
        {
            string absolute = AssetPathToAbsolutePath(assetDirectory);
            return Directory.Exists(absolute) && !Directory.EnumerateFileSystemEntries(absolute).Any();
        }

        /// <summary>
        /// 将 Unity AssetPath 转为不依赖进程当前目录的绝对文件系统路径。
        /// </summary>
        private static string AssetPathToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("无法解析 Unity 工程根目录。");
            }
            return IOPath.GetFullPath(IOPath.Combine(projectRoot, NormalizeAssetPath(assetPath)));
        }

        /// <summary>
        /// 计算目标目录相对于 Demo 根目录的层级，用于消费态由浅到深选择。
        /// </summary>
        private static int GetRelativeDepth(string demoRoot, string path)
        {
            string relative = path.Substring(demoRoot.Length).Trim('/');
            return relative.Length == 0 ? 0 : relative.Count(character => character == '/') + 1;
        }

        /// <summary>
        /// 将文件系统或 Unity 路径统一为正斜杠形式。
        /// </summary>
        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }
    }
}
