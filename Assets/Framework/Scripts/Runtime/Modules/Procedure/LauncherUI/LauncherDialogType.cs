/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LauncherDialogType.cs
 * author:    taoye
 * created:   2026/3/27
 * descrip:   启动阶段弹窗类型枚举
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 启动阶段弹窗类型枚举，用于 LauncherUIController 映射弹窗文本。
    /// </summary>
    public enum LauncherDialogType
    {
        /// <summary>
        /// 强制大版本更新提示。
        /// </summary>
        ForcedDownload = 0,

        /// <summary>
        /// 推荐大版本更新提示。
        /// </summary>
        RecommendedDownload = 1,

        /// <summary>
        /// 热更新失败提示（含资源补丁整批下载失败后的重试/取消决策）。
        /// 确认回调触发重试下载；取消回调根据 QuitOnFailedOrCancel 决定是否强退或跳过补丁进入游戏。
        /// 文本内容由业务侧在 Inspector 配置对应的 LauncherDialogLocalizedText 条目。
        /// </summary>
        HotfixFailed = 2,
        
        /// <summary>
        /// 预加载失败提示。
        /// </summary>
        PreloadFailed = 3,
    }
}
