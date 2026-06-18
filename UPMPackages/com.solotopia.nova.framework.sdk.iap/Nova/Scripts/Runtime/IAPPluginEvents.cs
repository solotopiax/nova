/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPPluginEvents.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   IAPPlugin 对外事件容器
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAPPlugin 对外事件容器。
    /// 业务层通过 IAPPlugin.Events 订阅支付全生命周期通知；
    /// 所有事件均为 ReplayEvent，订阅时可立即收到历史值。
    /// </summary>
    public sealed class IAPPluginEvents
    {
        /// <summary>
        /// IAP 初始化结果事件，store 完成 InitializeAsync 后触发。
        /// </summary>
        public readonly ReplayEvent<IAPInitResult> InitResult = new ReplayEvent<IAPInitResult>();

        /// <summary>
        /// 支付/补单成功事件，最多缓存 32 条历史记录。
        /// </summary>
        public readonly ReplayEvent<IAPResult> PaySuccess = new ReplayEvent<IAPResult>(32);

        /// <summary>
        /// 支付失败事件，最多缓存 32 条历史记录。
        /// </summary>
        public readonly ReplayEvent<IAPResult> PayFailed = new ReplayEvent<IAPResult>(32);

        /// <summary>
        /// 订阅 Restore 完成事件，最多缓存 8 条历史记录。
        /// </summary>
        public readonly ReplayEvent<IReadOnlyList<IAPResult>> SubscriptionRestored = new ReplayEvent<IReadOnlyList<IAPResult>>(8);

        /// <summary>
        /// 非消耗品 Restore 完成事件，最多缓存 8 条历史记录。
        /// </summary>
        public readonly ReplayEvent<IReadOnlyList<IAPResult>> NonConsumeRestored = new ReplayEvent<IReadOnlyList<IAPResult>>(8);
    }
}
