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
                bool useSavedScenes, NovaGuardReport report)
            {
                if (scenePaths == null)
                    return;

                for (int i = 0; i < scenePaths.Length; i++)
                    ValidateScene(scenePaths[i], requireEntryScene && i == 0, useSavedScenes, report);
            }

            private static void ValidateScene(string rawPath, bool requireEntryScene,
                bool useSavedScene, NovaGuardReport report)
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
                        report.Add(new NovaGuardIssue("NOVA-SCENE-000", NovaGuardSeverity.Error,
                            $"无法只读检查 Scene：{exception.Message}", path));
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
                        report.Add(new NovaGuardIssue("NOVA-SCENE-001", NovaGuardSeverity.Warning,
                            "首个启用的 Build Scene 不含 Nova；请确认它是否为合法的自定义 Bootstrap。", path));
                    }

                    if (novas.Length == 0 && components.Count > 0)
                    {
                        report.Add(new NovaGuardIssue("NOVA-SCENE-002", NovaGuardSeverity.Error,
                            "Scene 中存在 FrameworkComponent，但缺少 Nova 根节点。", path));
                    }

                    if (novas.Length > 1)
                    {
                        report.Add(new NovaGuardIssue("NOVA-SCENE-003", NovaGuardSeverity.Error,
                            "Scene 中存在多个 Nova 根节点。", path));
                    }

                    foreach (Nova nova in novas)
                    {
                        if (IsCanonicalNovaInstance(nova))
                            continue;

                        report.Add(new NovaGuardIssue("NOVA-SCENE-004", NovaGuardSeverity.Error,
                            "Nova 必须是框架 canonical Nova.prefab 的 connected prefab instance。", path));
                    }
                }
                finally
                {
                    if (closePreview && scene.IsValid())
                        EditorSceneManager.ClosePreviewScene(scene);
                }
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
                ValidateScenes(scenePaths, requireFirstEntry, false, report);
                return report;
            }
        }
    }
}
