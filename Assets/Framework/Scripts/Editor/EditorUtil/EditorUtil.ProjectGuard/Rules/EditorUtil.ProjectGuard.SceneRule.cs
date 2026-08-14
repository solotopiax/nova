/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ProjectGuard.SceneRule.cs
 * author:    taoye
 * created:   2026/7/15
 * descrip:   Nova 项目规范守卫场景规则
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class ProjectGuard
        {
            private const string c_CanonicalNovaPrefabGuid = "045d894d6a90aa04f9d2e0820d04deb4";

            private static void ValidateScenes(string[] scenePaths, bool requireEntryScene,
                bool useSavedScenes, bool validateConfig, NovaGuardReport report)
            {
                if (scenePaths == null)
                    return;

                for (int i = 0; i < scenePaths.Length; i++)
                    ValidateScene(scenePaths[i], requireEntryScene && i == 0, useSavedScenes,
                        validateConfig, report);
            }

            /// <summary>
            /// 在不保存场景的前提下检查 Nova 根节点、框架组件和启动配置。
            /// </summary>
            /// <param name="rawPath">待检查场景路径。</param>
            /// <param name="requireEntryScene">是否按启动场景规则检查。</param>
            /// <param name="useSavedScene">是否强制以只读 Preview Scene 检查。</param>
            /// <param name="validateConfig">是否检查场景中的启动配置。</param>
            /// <param name="report">问题收集报告。</param>
            private static void ValidateScene(string rawPath, bool requireEntryScene,
                bool useSavedScene, bool validateConfig, NovaGuardReport report)
            {
                string path = NormalizePath(rawPath);
                if (string.IsNullOrEmpty(path))
                    return;

                Scene scene = useSavedScene ? default : SceneManager.GetSceneByPath(path);
                bool closePreview = useSavedScene || !scene.IsValid() || !scene.isLoaded;
                if (closePreview)
                {
                    try
                    {
                        scene = EditorSceneManager.OpenPreviewScene(path);
                    }
                    catch (Exception exception)
                    {
                        AddSceneIssue(report, "NOVA-SCENE-000", NovaGuardSeverity.Error,
                            "无法检查这个场景。",
                            "请确认场景文件未损坏，且可在 Unity 中正常打开后再检查。",
                            $"无法只读检查 Scene：{exception.Message}", path);
                        return;
                    }
                }

                try
                {
                    var components = new List<FrameworkComponent>();
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        components.AddRange(root.GetComponentsInChildren<FrameworkComponent>(true)
                            .Where(component => component.gameObject.activeInHierarchy));
                    }

                    Nova[] novas = components.OfType<Nova>().ToArray();
                    if (requireEntryScene && novas.Length == 0)
                    {
                        AddSceneIssue(report, "NOVA-SCENE-001", NovaGuardSeverity.Warning,
                            "启动场景中没有 Nova。",
                            "请确认启动流程是否会加载 Nova；只有该场景应直接承载 Nova 时，才添加 Nova.prefab。",
                            "首个启用的 Build Scene 不含 Nova。", path);
                    }

                    if (novas.Length == 0 && components.Count > 0)
                    {
                        AddSceneIssue(report, "NOVA-SCENE-002", NovaGuardSeverity.Error,
                            "场景已挂载 Nova 框架组件，但缺少 Nova 根节点。",
                            "请添加或恢复 Nova.prefab。",
                            "Scene 中存在 FrameworkComponent，但缺少 Nova 根节点。", path);
                    }

                    if (novas.Length > 1)
                    {
                        AddSceneIssue(report, "NOVA-SCENE-003", NovaGuardSeverity.Error,
                            "场景中有多个 Nova 根节点。",
                            "每个启动场景只能保留一个 Nova，请删除多余节点。",
                            "Scene 中存在多个 Nova 根节点。", path);
                    }

                    foreach (Nova nova in novas)
                    {
                        if (IsCanonicalNovaInstance(nova))
                            continue;

                        AddSceneIssue(report, "NOVA-SCENE-004", NovaGuardSeverity.Error,
                            "当前 Nova 不是框架提供的 Nova.prefab 实例。",
                            "请删除后，从框架资源重新添加 Nova.prefab。",
                            "Nova 必须是框架 canonical Nova.prefab 的 connected prefab instance。", path);
                    }

                    if (validateConfig)
                    {
                        foreach (ConfigComponent configComponent in components.OfType<ConfigComponent>())
                        {
                            ValidateConfigComponent(configComponent, path, report);
                        }
                    }
                }
                finally
                {
                    if (closePreview && scene.IsValid())
                        EditorSceneManager.ClosePreviewScene(scene);
                }
            }

            /// <summary>
            /// 添加场景检查问题，前两行用于窗口展示，技术细节保留给 Console 与 Editor.log。
            /// </summary>
            /// <param name="report">问题收集报告。</param>
            /// <param name="ruleId">规则编号。</param>
            /// <param name="severity">问题严重性。</param>
            /// <param name="summary">项目成员可直接理解的问题摘要。</param>
            /// <param name="action">可直接执行的处理方式。</param>
            /// <param name="technicalDetails">仅供诊断使用的技术细节。</param>
            /// <param name="path">关联场景路径。</param>
            private static void AddSceneIssue(NovaGuardReport report, string ruleId, NovaGuardSeverity severity,
                string summary, string action, string technicalDetails, string path)
            {
                string message = $"{summary}\n处理方式：{action}";
                if (!string.IsNullOrWhiteSpace(technicalDetails))
                    message += $"\n技术信息：{technicalDetails}";

                report.Add(new NovaGuardIssue(ruleId, severity, message, path));
            }

            private static bool IsCanonicalNovaInstance(Nova nova)
            {
                string canonicalPath = NormalizePath(AssetDatabase.GUIDToAssetPath(c_CanonicalNovaPrefabGuid));
                if (string.IsNullOrEmpty(canonicalPath))
                    return false;

                GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(nova.gameObject);
                if (instanceRoot == null || PrefabUtility.GetPrefabInstanceStatus(instanceRoot) != PrefabInstanceStatus.Connected)
                    return false;

                GameObject sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                return sourceRoot != null && string.Equals(
                    NormalizePath(AssetDatabase.GetAssetPath(sourceRoot)), canonicalPath,
                    StringComparison.OrdinalIgnoreCase);
            }

            private static NovaGuardReport ValidateScenePathsForDiagnostics(
                string[] scenePaths, bool requireFirstEntry)
            {
                var report = new NovaGuardReport();
                ValidateScenes(scenePaths, requireFirstEntry, false, false, report);
                return report;
            }
        }
    }
}
