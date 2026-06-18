/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelType.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   广告渠道类型枚举
 ***************************************************************/

using System;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告聚合渠道类型枚举。
    /// 每个渠道的 IAdChannelConfig 实现须通过 Channel 属性返回对应值，
    /// 供 Inspector 和日志快速识别渠道身份。
    /// </summary>
    public enum AdChannelType
    {
        /// <summary>
        /// AppLovin MAX 广告聚合平台。
        /// </summary>
        MAX = 0,

        /// <summary>
        /// Google AdMob 广告平台。
        /// </summary>
        AdMob = 1,

        /// <summary>
        /// IronSource 广告聚合平台。
        /// </summary>
        IronSource = 2,
    }
}
