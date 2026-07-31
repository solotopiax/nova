/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ProjectGuard.cs
 * author:    taoye
 * created:   2026/7/15
 * descrip:   Nova 项目规范守卫入口
 ***************************************************************/

using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class ProjectGuard
        {
            public static NovaGuardReport ValidateQuick()
            {
                return Validate(NovaGuardProfile.Quick, EditorUserBuildSettings.activeBuildTarget);
            }

            public static NovaGuardReport ValidatePlay()
            {
                return Validate(NovaGuardProfile.Play, EditorUserBuildSettings.activeBuildTarget);
            }

            public static NovaGuardReport ValidateBuild(BuildTarget target)
            {
                return Validate(NovaGuardProfile.Build, target);
            }

            public static NovaGuardReport ValidateRelease(BuildTarget target)
            {
                return Validate(NovaGuardProfile.Release, target);
            }

            private static NovaGuardReport Validate(NovaGuardProfile profile, BuildTarget target)
            {
                var report = new NovaGuardReport();
                GuardScope scope = CreateScope(profile, target);
                ResetLastConfigSource();
                ValidateScenes(scope.ScenePaths, scope.RequireEntryScene, scope.UseSavedScenes, true, report);
                ValidateResources(scope.ManagedRoots, report);
                return report;
            }
        }
    }
}
