/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPPlugin.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   IAP 插件抽象接口，外部通过 sdkComponent.TryGet<IIAPPlugin>() 获取
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// IAP 支付插件接口（最小面）。
    /// 外部业务层通过 SDKComponent.TryGet&lt;IIAPPlugin&gt;() 获取实例，
    /// 无需直接依赖 IAPPlugin 具体类。
    /// 事件订阅、CheckLocalOrders 等高级功能通过转型 IAPPlugin 访问。
    /// </summary>
    public interface IIAPPlugin : ISDKPlugin
    {
        /// <summary>
        /// 设置当前登录用户 ID，广播给所有 store。
        /// 通常无需主动调用——IAPPlugin 已在初始化时订阅 SDKEventData.UserLogin 自动同步；
        /// 仅在登录事件触达前 IAP 已使用或需要强制切换账号时使用。
        /// </summary>
        /// <param name="userId">已登录用户的唯一 ID。</param>
        void SetUserId(string userId);

        /// <summary>
        /// 异步发起支付流程，根据 request 路由到对应 store。
        /// T 通常传入 IAPResult 或其子类；as T 转型失败时返回 null，调用方应做 null 检查。
        /// </summary>
        /// <typeparam name="T">期望的结果类型，需实现 IIAPResult。</typeparam>
        /// <param name="request">支付请求，实现 IIAPRequest 接口的具体子类实例。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>支付结果；T 类型不匹配时返回 null。</returns>
        UniTask<T> PayAsync<T>(IIAPRequest request, CancellationToken ct = default) where T : class, IIAPResult;

        /// <summary>
        /// 异步恢复历史已购商品，遍历所有 store 收集恢复结果。
        /// </summary>
        /// <typeparam name="T">期望的结果类型，需实现 IIAPResult。</typeparam>
        /// <param name="ct">取消令牌。</param>
        /// <returns>所有 store 恢复到的历史订单结果列表。</returns>
        UniTask<IReadOnlyList<T>> RestorePurchasesAsync<T>(CancellationToken ct = default) where T : class, IIAPResult;
    }
}
