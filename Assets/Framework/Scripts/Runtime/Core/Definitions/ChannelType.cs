/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ChannelType.cs
 * author:    taoye
 * created:   2026/2/3
 * descrip:   业务渠道类型
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 业务渠道类型。
    /// </summary>
    [Serializable]
    public enum ChannelType : byte
    {
        /// <summary>
        /// 无效渠道。
        /// </summary>
        None = 0,

        /// <summary>
        /// 官网包渠道。
        /// </summary>
        Official = 1,

        /// <summary>
        /// 谷歌商店渠道。
        /// </summary>
        Google = 2,

        /// <summary>
        /// 苹果商店渠道。
        /// </summary>
        Apple = 3,

        /// <summary>
        /// 微信渠道。
        /// </summary>
        WeChat = 4,

        /// <summary>
        /// 抖音渠道。
        /// </summary>
        TikTok = 5,

        /// <summary>
        /// 支付宝渠道。
        /// </summary>
        Alipay = 6,
    }
}
