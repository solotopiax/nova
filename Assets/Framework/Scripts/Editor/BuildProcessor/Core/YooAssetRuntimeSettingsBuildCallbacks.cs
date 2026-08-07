/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  YooAssetRuntimeSettingsBuildCallbacks.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   YooAsset 运行时 Settings 暂存构建回调
 ***************************************************************/

using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 在正式 Player 构建预处理阶段生成唯一运行时 YooAssetSettings 副本。
    /// </summary>
    internal sealed class YooAssetRuntimeSettingsBuildPreprocessor : IPreprocessBuildWithReport
    {
        /// <summary>
        /// 尽早生成 Resources 资产，确保后续构建内容收集可见。
        /// </summary>
        public int callbackOrder => int.MinValue + 100;

        /// <summary>
        /// 创建本轮构建的临时运行时 Settings；失败时中止构建。
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            YooAssetRuntimeSettingsStaging.StageForBuild(report);
        }
    }

    /// <summary>
    /// 在 Player 构建后处理开始阶段清理临时 YooAssetSettings 副本。
    /// </summary>
    internal sealed class YooAssetRuntimeSettingsBuildPostprocessor : IPostprocessBuildWithReport
    {
        /// <summary>
        /// 尽早清理，减少后续后处理器异常导致残留的窗口。
        /// </summary>
        public int callbackOrder => int.MinValue;

        /// <summary>
        /// 幂等清理本轮构建临时副本。
        /// </summary>
        public void OnPostprocessBuild(BuildReport report)
        {
            YooAssetRuntimeSettingsStaging.CleanupAfterBuild();
        }
    }
}
