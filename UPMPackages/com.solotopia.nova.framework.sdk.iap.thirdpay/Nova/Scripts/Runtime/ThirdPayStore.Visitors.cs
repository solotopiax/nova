/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   ThirdPayStore 常量、字段与属性
 ***************************************************************/

using System.Runtime.CompilerServices;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 声明 ThirdPayStore 的运行时状态与内部协作者。
    /// </summary>
    public sealed partial class ThirdPayStore
    {
        /// <summary>
        /// 外部浏览器支付返回 App 后的默认自动验单延迟，单位为秒。
        /// </summary>
        private const float c_DefaultExternalBrowserReturnValidateDelaySeconds = 2.5f;

        /// <summary>
        /// 获取第三方支付打点使用的固定渠道标识。
        /// </summary>
        protected override string TrackChannel => IAPStoreType.ThirdPay.ToString().ToLowerInvariant();

        /// <summary>
        /// 当前 Store 使用的 Nova 日志标签。
        /// </summary>
        protected override string LogTag => NovaFramework.Runtime.LogTag.IAPThirdPay;

        /// <summary>
        /// 获取当前 Store 是否已具备基础配置且必需配置项齐备。
        /// </summary>
        protected override bool IsStoreReady => m_Hub.Config != null && m_Hub.ConfigReady;

        /// <summary>
        /// 集中持有第三方支付共享状态、内部服务和生命周期资源的强类型容器。
        /// </summary>
        private readonly ThirdPayServiceHub m_Hub;

        /// <summary>
        /// 按最终返回的 IAPResult 保存本次支付失败的精确埋点上下文，避免在 PayAsync 边界从粗粒度错误码反推时丢失原因。
        /// </summary>
        private readonly ConditionalWeakTable<IAPResult, ThirdPayReturnedFailureTrackContext> m_ReturnedFailureTrackContexts = new ConditionalWeakTable<IAPResult, ThirdPayReturnedFailureTrackContext>();

        /// <summary>
        /// 获取当前账号 UID，供 Hub 内部服务按调用时状态读取。
        /// </summary>
        internal string CurrentUserId => m_GameUID;

        /// <summary>
        /// 获取当前业务配置允许的最大验单次数。
        /// </summary>
        internal int ConfiguredMaxValidateAttempts => Context?.RetryValidateMaxNum ?? 3;

    }
}
