/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ProjectGuard.Scope.cs
 * author:    taoye
 * created:   2026/7/15
 * descrip:   Nova 项目规范守卫检查范围
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class ProjectGuard
        {
            private sealed class GuardScope
            {
                public GuardScope(string[] scenePaths, string[] managedRoots, bool requireEntryScene,
                    bool useSavedScenes)
                {
                    ScenePaths = scenePaths;
                    ManagedRoots = managedRoots;
                    RequireEntryScene = requireEntryScene;
                    UseSavedScenes = useSavedScenes;
                }

                public string[] ScenePaths { get; }
                public string[] ManagedRoots { get; }
                public bool RequireEntryScene { get; }
                public bool UseSavedScenes { get; }
            }

            private static GuardScope CreateScope(NovaGuardProfile profile, BuildTarget target)
            {
                _ = target;
                bool isBuild = profile == NovaGuardProfile.Build || profile == NovaGuardProfile.Release;
                string[] scenePaths = isBuild
                    ? EditorBuildSettings.scenes.Where(scene => scene.enabled && !string.IsNullOrEmpty(scene.path))
                        .Select(scene => NormalizePath(scene.path)).ToArray()
                    : GetActiveScenePath();

                string[] roots = scenePaths
                    .Select(path => NormalizePath(Path.GetDirectoryName(path)))
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new GuardScope(scenePaths, roots, isBuild, isBuild);
            }

            private static string[] GetActiveScenePath()
            {
                Scene scene = SceneManager.GetActiveScene();
                return scene.IsValid() && !string.IsNullOrEmpty(scene.path)
                    ? new[] { NormalizePath(scene.path) }
                    : Array.Empty<string>();
            }

            private static string NormalizePath(string path)
            {
                return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
            }

            private static bool IsUnderRoot(string assetPath, IEnumerable<string> managedRoots)
            {
                foreach (string rawRoot in managedRoots ?? Array.Empty<string>())
                {
                    string root = NormalizePath(rawRoot);
                    if (string.IsNullOrEmpty(root))
                        continue;
                    if (assetPath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                        assetPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }
        }
    }
}
