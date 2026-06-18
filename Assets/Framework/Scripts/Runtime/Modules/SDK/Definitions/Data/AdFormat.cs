/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdFormat.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   广告格式枚举
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 广告展示格式枚举。
    /// 用于 IAdPlugin / IAdChannelPlugin 通用三段式接口（RequestAsync / IsReady / ShowAsync）的 format 参数。
    /// AdChannelPluginBase 派生类在 OnRequestAsync / OnIsReady / OnShowAsync 中按 format 分支处理各自支持的格式。
    /// </summary>
    public enum AdFormat
    {
        /// <summary>
        /// 激励视频广告，用户完整观看后触发奖励回调。
        /// </summary>
        Rewarded = 0,

        /// <summary>
        /// 插屏广告，全屏展示，适合关卡切换等自然间隙节点。
        /// </summary>
        Interstitial = 1,

        /// <summary>
        /// 横幅广告，固定位置常驻展示，通过 BannerPosition 控制屏幕锚点。
        /// </summary>
        Banner = 2,

        /// <summary>
        /// 开屏广告，应用冷启动或热启动时全屏展示。
        /// </summary>
        AppOpen = 3,

        /// <summary>
        /// 原生广告，由业务层自定义渲染模板，仅数据由 SDK 填充。
        /// </summary>
        Native = 4,
    }
}
