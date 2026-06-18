/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdRequestReason.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   广告请求原因枚举，携带在 nova_ad_request 事件中
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告请求原因，描述本次 RequestAsync 由何种场景触发。
    /// 通过 nova_ad_request 事件的 nova_ad_reason 字段上报。
    /// </summary>
    public enum AdRequestReason
    {
        /// <summary>
        /// 外部首次主动调起（默认值）。
        /// </summary>
        Auto = 0,

        /// <summary>
        /// 业务层显式调起。
        /// </summary>
        Manual = 1,

        /// <summary>
        /// 状态机失败后自动重试。
        /// </summary>
        Retry = 2,

        /// <summary>
        /// 广告展示后自动续杯补量。
        /// </summary>
        AutoRefill = 3,
    }
}
