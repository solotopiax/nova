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
    /// 广告变现事件载荷，由 AdPlugin.Events.RevenuePaid 事件携带。
    /// 渠道在收益派发处显式调用 StartRevenueReport；RaiseRevenue 仅更新状态和发布原始业务通知。
    /// 业务层可订阅 AdPlugin.Events.RevenuePaid 做业务记录，不应因此重复发送收益协议或渠道已派发的分析事件。
    /// 当前 MAX / AdMob 使用非负美元收益；本数据类不校验字段，也不自动生成时间戳。
    /// </summary>
    public sealed class AdEvent
    {
        /// <summary>
        /// 事件类型，用于 AdPlugin.Events.RevenuePaid 订阅方路由。
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
        /// 渠道填写的事件时间；AdMob 收益通知使用当前 UTC 时间，MAX 原始收益通知未填写时保持默认值。
        /// 不由数据类自动生成，也不随当前收益 Proto 传输。
        /// </summary>
        public DateTime Timestamp;
    }
}
