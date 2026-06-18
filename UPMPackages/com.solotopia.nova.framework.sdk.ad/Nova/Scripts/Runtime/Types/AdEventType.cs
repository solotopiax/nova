/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdEventType.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   广告事件类型枚举
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告生命周期事件类型枚举。
    /// 用于后续扩展 AdEvent 事件分类；当前阶段保留定义，各 SDK 按需映射到平台回调。
    /// </summary>
    public enum AdEventType
    {
        /// <summary>
        /// 广告收入回传事件，由 Mediation SDK 在广告曝光后触发。
        /// </summary>
        Revenue = 0,

        /// <summary>
        /// 广告加载成功事件。
        /// </summary>
        Loaded = 1,

        /// <summary>
        /// 广告加载失败事件。
        /// </summary>
        LoadFailed = 2,

        /// <summary>
        /// 广告展示开始事件。
        /// </summary>
        Displayed = 3,

        /// <summary>
        /// 广告被点击事件。
        /// </summary>
        Clicked = 4,

        /// <summary>
        /// 广告展示结束事件（关闭/跳过）。
        /// </summary>
        Closed = 5,
    }
}
