/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdEvent.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   广告变现埋点事件数据类
 ***************************************************************/

using System;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告变现埋点事件载荷，由 IAdPlugin.OnAdRevenuePaid 事件携带。
    /// 业务层在 IAdPlugin.OnAdRevenuePaid 事件回调中接收此载荷；具体上报路径由业务层自行决定（扇出到变现埋点插件或直接记录）。
    /// 不变量：Revenue 单位为 USD，值 >= 0；Timestamp 为 UTC 时间。
    /// </summary>
    public sealed class AdEvent
    {
        /// <summary>
        /// 事件类型，用于 IAdPlugin.OnAdEvent 订阅方路由。
        /// </summary>
        public AdEventType EventType;

        /// <summary>
        /// 本次广告曝光的广告格式。
        /// </summary>
        public AdFormat Format;

        /// <summary>
        /// 广告位唯一标识。
        /// </summary>
        public string PlacementId;

        /// <summary>
        /// 广告真实投放网络名称（如 "AdMob"、"Meta"、"IronSource"）。
        /// Mediation SDK 必须填写；直接接入可留 null。
        /// </summary>
        public string Network;

        /// <summary>
        /// 本次曝光广告收入，单位 USD，值 >= 0。
        /// </summary>
        public double Revenue;

        /// <summary>
        /// Revenue 对应的货币代码，固定为 "USD"。
        /// </summary>
        public string Currency;

        /// <summary>
        /// 收入精度描述符，由 Mediation SDK 填写。
        /// 典型值：publisher_defined / exact / estimated。
        /// </summary>
        public string Precision;

        /// <summary>
        /// 事件发生的 UTC 时间戳。
        /// </summary>
        public DateTime Timestamp;
    }
}
