/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.SceneRoute.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   Sample 场景打开时的 Editor 配置路由摘要与统一编排
 ***************************************************************/

using System;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using IOPath = System.IO.Path;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Sample 场景打开时的 Editor 配置路由编排。
        /// 统一输出场景、ConfigMaster、YooAssetSettings、PipifySettings 四个阶段，
        /// 避免多个独立 sceneOpened 监听器造成重复或不稳定的日志。
        /// </summary>
        internal static class SceneRoute
        {
            /// <summary>
            /// 域重载时注册场景打开监听；先解绑再绑定，避免重复订阅。
            /// </summary>
            [InitializeOnLoadMethod]
            private static void HookSceneOpened()
            {
                EditorSceneManager.sceneOpened -= OnSceneOpened;
                EditorSceneManager.sceneOpened += OnSceneOpened;
            }

            /// <summary>
            /// 单场景打开后按固定顺序解析并记录 Editor 配置路由。
            /// Additive 场景不切换激活配置，直接忽略。
            /// </summary>
            /// <param name="scene">新打开的场景。</param>
            /// <param name="mode">场景打开模式。</param>
            private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
            {
                if (mode != OpenSceneMode.Single || BuildPipeline.isBuildingPlayer) return;

                Log.Debug(LogTag.Editor, "[SceneRoute] 已打开场景：{0}", scene.path);
                if (!Config.WorkspaceActive.ReconcileScene(scene.path))
                {
                    Log.Warning(LogTag.Editor, "[SceneRoute] 工作区切换失败，已停止后续配置注入：{0}", scene.path);
                    return;
                }
                ConfigMasterSO master = Config.WorkspaceActive.Get();
                SynchronizeDevelopmentSampleBuildSettings(scene.path, master, IsFrameworkDevelopmentCheckout());
                LogActiveConfigMaster(master);
                Config.YooAssetInjector.Inject(master);
                Pipify.LogActiveSettings();
            }

            /// <summary>
            /// 仅在 Nova 框架源码开发仓中，把当前 Sample 的入口场景设为唯一启用的 Build Settings 场景。
            /// 消费工程、非 Sample 场景、未保存场景及无法确定入口场景时均保持原配置。
            /// </summary>
            /// <param name="openedScenePath">本次打开的 Scene 项目相对路径。</param>
            /// <param name="master">场景路由后激活的 ConfigMaster。</param>
            /// <param name="isDevelopmentCheckout">当前 Framework 是否解析自本工程 Assets/Framework。</param>
            /// <returns>实际改写 Build Settings 返回 true；无需改写返回 false。</returns>
            internal static bool SynchronizeDevelopmentSampleBuildSettings(
                string openedScenePath,
                ConfigMasterSO master,
                bool isDevelopmentCheckout)
            {
                if (!isDevelopmentCheckout || master == null || string.IsNullOrEmpty(openedScenePath)) return false;

                string normalizedScenePath = NormalizeAssetPath(openedScenePath);
                string masterPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(master));
                const string samplesPrefix = "Assets/Samples/";
                if (!normalizedScenePath.StartsWith(samplesPrefix, StringComparison.Ordinal) ||
                    !masterPath.StartsWith(samplesPrefix, StringComparison.Ordinal))
                {
                    return false;
                }

                string editorDirectory = NormalizeAssetPath(IOPath.GetDirectoryName(masterPath));
                if (!string.Equals(IOPath.GetFileName(editorDirectory), "Editor", StringComparison.Ordinal)) return false;

                string sampleRoot = NormalizeAssetPath(IOPath.GetDirectoryName(editorDirectory));
                if (string.IsNullOrEmpty(sampleRoot) ||
                    !normalizedScenePath.StartsWith(sampleRoot + "/", StringComparison.Ordinal))
                {
                    return false;
                }

                string sampleName = IOPath.GetFileName(sampleRoot);
                string entryScenePath = $"{sampleRoot}/{sampleName}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(entryScenePath) == null)
                {
                    Log.Warning(LogTag.Editor,
                        "[SceneRoute] 开发态 Sample 缺少约定入口场景，Build Settings 保持不变：{0}", entryScenePath);
                    return false;
                }

                EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
                if (current.Length == 1 && current[0].enabled &&
                    string.Equals(NormalizeAssetPath(current[0].path), entryScenePath, StringComparison.Ordinal))
                {
                    return false;
                }

                string previous = string.Join(", ", EditorBuildSettingsScene.GetActiveSceneList(current));
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(entryScenePath, true) };
                Log.Debug(LogTag.Editor,
                    "[SceneRoute] 开发态 Build Settings 已同步：{0} -> {1}", previous, entryScenePath);
                return true;
            }

            /// <summary>
            /// 判断当前 Framework 是否严格解析自本工程 Assets/Framework 源码目录。
            /// 优先核对 Unity 注册包；Assets 下本地包未注册时，再以仓库标记、包身份和 manifest 直连三项共同确认。
            /// </summary>
            /// <returns>仅 Nova 源码开发仓返回 true；无法确定时返回 false。</returns>
            private static bool IsFrameworkDevelopmentCheckout()
            {
                const string frameworkPackageName = "com.solotopia.nova.framework";
                const string developmentManifestReference = "file:../Assets/Framework";

                try
                {
                    string projectRoot = NormalizeFullPath(IOPath.Combine(Application.dataPath, ".."));
                    string developmentRoot = NormalizeFullPath(IOPath.Combine(Application.dataPath, "Framework"));
                    PackageManagerPackageInfo[] packages = PackageManagerPackageInfo.GetAllRegisteredPackages();
                    if (packages != null)
                    {
                        foreach (PackageManagerPackageInfo package in packages)
                        {
                            if (!string.Equals(package.name, frameworkPackageName, StringComparison.Ordinal)) continue;

                            return !string.IsNullOrEmpty(package.resolvedPath) &&
                                   string.Equals(
                                       NormalizeFullPath(package.resolvedPath),
                                       developmentRoot,
                                       StringComparison.OrdinalIgnoreCase);
                        }
                    }

                    // Unity 不保证把 Assets 下的本地包列入注册包；回退必须同时证明仓库、包身份和精确直连关系，
                    // 避免把消费工程中的 embedded、PackageCache 或其他 file: 引用误判为 Nova 开发仓。
                    string packageJsonPath = IOPath.Combine(developmentRoot, "package.json");
                    string novaMarkerPath = IOPath.Combine(projectRoot, ".nova", "NOVA.md");
                    string manifestPath = IOPath.Combine(projectRoot, "Packages", "manifest.json");
                    if (!System.IO.File.Exists(packageJsonPath) ||
                        !System.IO.File.Exists(novaMarkerPath) ||
                        !System.IO.File.Exists(manifestPath))
                    {
                        return false;
                    }

                    JObject packageJson = JObject.Parse(System.IO.File.ReadAllText(packageJsonPath));
                    if (!string.Equals((string)packageJson["name"], frameworkPackageName, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    JObject manifest = JObject.Parse(System.IO.File.ReadAllText(manifestPath));
                    return string.Equals(
                        (string)manifest["dependencies"]?[frameworkPackageName],
                        developmentManifestReference,
                        StringComparison.Ordinal);
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "[SceneRoute] 无法确认 Nova 开发仓身份，Build Settings 保持不变：{0}", e.Message);
                    return false;
                }
            }

            /// <summary>
            /// 规范化绝对路径，供开发仓物理包根比较使用。
            /// </summary>
            /// <param name="path">待规范化路径。</param>
            /// <returns>去除末尾分隔符并统一为正斜杠的绝对路径。</returns>
            private static string NormalizeFullPath(string path)
            {
                return IOPath.GetFullPath(path)
                    .TrimEnd(IOPath.DirectorySeparatorChar, IOPath.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
            }

            /// <summary>
            /// 规范化 Unity 项目相对路径。
            /// </summary>
            /// <param name="path">待规范化路径。</param>
            /// <returns>使用正斜杠的路径；空值返回空字符串。</returns>
            private static string NormalizeAssetPath(string path)
            {
                return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
            }

            /// <summary>
            /// 输出当前路由实际使用的 ConfigMaster；缺少资产时保持静默，沿用注入层的失败语义。
            /// </summary>
            /// <param name="master">当前激活的 ConfigMaster。</param>
            private static void LogActiveConfigMaster(ConfigMasterSO master)
            {
                if (master == null) return;

                string assetPath = AssetDatabase.GetAssetPath(master);
                if (string.IsNullOrEmpty(assetPath)) return;

                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                Log.Debug(LogTag.Editor, "[WorkspaceActive] 已激活 ConfigMaster：{0}（{1}）", assetPath, guid);
            }
        }
    }
}
