/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPStoreEventBridge.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   IAP store → IAPPlugin 事件上报桥接接口
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP store 事件上报桥接接口。
    /// 各 store 的内部服务完成核心操作后，通过此接口向 IAPPlugin 层上报结果；
    /// IAPPlugin 实现此接口并将结果派发到 IAPPluginEvents。
    /// </summary>
    public interface IIAPStoreEventBridge
    {
        /// <summary>
        /// 上报 IAP 初始化结果。
        /// </summary>
        /// <param name="result">包含成功标志、失败原因与详情的初始化结果。</param>
        void RaiseInitResult(IAPInitResult result);

        /// <summary>
        /// 上报支付/补单成功结果。
        /// </summary>
        /// <param name="result">包含订单信息的 IAPResult。</param>
        void RaisePaySuccess(IAPResult result);

        /// <summary>
        /// 上报支付失败结果。
        /// </summary>
        /// <param name="result">包含失败原因的 IAPResult。</param>
        void RaisePayFailed(IAPResult result);

        /// <summary>
        /// 上报订阅 Restore 完成结果。
        /// </summary>
        /// <param name="results">本次 Restore 恢复到的订阅列表，空列表表示无需恢复。</param>
        void RaiseSubscriptionRestored(IReadOnlyList<IAPResult> results);

        /// <summary>
        /// 上报非消耗品 Restore 完成结果。
        /// </summary>
        /// <param name="results">本次 Restore 恢复到的非消耗品列表，空列表表示无需恢复。</param>
        void RaiseNonConsumeRestored(IReadOnlyList<IAPResult> results);
    }
}
