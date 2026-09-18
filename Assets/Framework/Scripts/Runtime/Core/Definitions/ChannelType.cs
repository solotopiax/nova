/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ChannelType.cs
 * author:    taoye
 * created:   2026/2/3
 * descrip:   游戏运营渠道类型
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 游戏运营渠道类型，用于描述包体分发与运营来源。
    /// </summary>
    [Serializable]
    public enum ChannelType : byte
    {
        /// <summary>
        /// 未指定渠道；仅用于配置尚未加载等内部状态，不可作为 Config 坐标。
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
