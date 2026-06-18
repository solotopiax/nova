/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BannerPosition.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   Banner 广告屏幕位置枚举
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Banner 广告在屏幕上的停靠位置枚举。
    /// 传入 IAdPlugin.SetBannerPosition 控制 Banner 锚点，各 SDK 实现映射到平台原生常量。
    /// </summary>
    public enum BannerPosition
    {
        /// <summary>
        /// 屏幕顶部居中。
        /// </summary>
        Top,

        /// <summary>
        /// 屏幕底部居中。
        /// </summary>
        Bottom,

        /// <summary>
        /// 屏幕左上角。
        /// </summary>
        TopLeft,

        /// <summary>
        /// 屏幕右上角。
        /// </summary>
        TopRight,

        /// <summary>
        /// 屏幕左下角。
        /// </summary>
        BottomLeft,

        /// <summary>
        /// 屏幕右下角。
        /// </summary>
        BottomRight,
    }
}
