/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LauncherStage.cs
 * author:    taoye
 * created:   2026/3/27
 * descrip:   启动阶段枚举
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 启动阶段枚举，用于 LauncherUIController 映射对应阶段的 UI 文本。
    /// </summary>
    public enum LauncherStage
    {
        /// <summary>
        /// 版本检查阶段。
        /// </summary>
        CheckVersion,

        /// <summary>
        /// 热更新下载阶段。
        /// </summary>
        Hotfix,

        /// <summary>
        /// 资源预加载阶段。
        /// </summary>
        Preload,
    }
}
