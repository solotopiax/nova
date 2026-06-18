/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPInternalStore.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAP 内部 store 统一接口，由各渠道实现
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 内部渠道 store 接口。
    /// 每种支付渠道（Google/iOS/第三方/代金券）对应一个实现。
    /// 由 IAPPlugin 在运行时按 CanHandle 结果路由请求。
    /// </summary>
    public interface IIAPInternalStore
    {
        /// <summary>
        /// 当前 store 的渠道类型。
        /// 用于补单路由还原与诊断日志，替代旧有的字符串类型名。
        /// </summary>
        IAPStoreType StoreType { get; }

        /// <summary>
        /// 当前 store 是否处于启用状态。
        /// false 时 PayAsync / RestorePurchasesAsync / CheckLocalOrdersAsync 均会立即返回 StoreDisabled 错误，
        /// 且 store 可能尚未完成 InitializeAsync（懒初始化未触发）。
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 运行时启用或禁用该 store。
        /// 从 false→true 且 store 尚未初始化时，须先调用 EnableAsync 完成懒初始化，再翻转标志。
        /// 已初始化的 store 仅翻转标志，不重新初始化。
        /// </summary>
        /// <param name="enabled">true = 启用，false = 禁用。</param>
        void SetEnabled(bool enabled);

        /// <summary>
        /// 懒初始化入口：仅在 store 尚未初始化时执行 InitializeAsync；已初始化则立即返回。
        /// 由 IAPPlugin.SetStoreEnabled(true) 在 store 首次被启用时调用。
        /// </summary>
        /// <param name="table">商品表接口。</param>
        /// <param name="config">store 专属配置。</param>
        /// <param name="ctx">store 运行时上下文。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        UniTask EnableAsync(IIAPProductTable table, IIAPStoreConfig config, IIAPStoreContext ctx, CancellationToken ct);

        /// <summary>
        /// 判断当前 store 是否能处理指定请求。
        /// 通常通过 request 的具体类型做类型匹配。
        /// </summary>
        /// <param name="request">待判断的支付请求。</param>
        /// <returns>能处理时返回 true，否则返回 false。</returns>
        bool CanHandle(IAPRequest request);

        /// <summary>
        /// 异步初始化 store，在插件初始化阶段由 IAPPlugin 调用。
        /// </summary>
        /// <param name="table">所有 store 共用的商品表接口，可为 null；为 null 时表示该 store 在当前配置下被禁用，实现层自行决定降级行为。</param>
        /// <param name="config">store 专属配置，由实现层定义具体类型。</param>
        /// <param name="ctx">store 运行时上下文，包含跨模块依赖引用。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        UniTask InitializeAsync(IIAPProductTable table, IIAPStoreConfig config, IIAPStoreContext ctx, CancellationToken ct);

        /// <summary>
        /// 异步发起支付流程。
        /// 调用前须确保 CanHandle 返回 true。
        /// </summary>
        /// <param name="request">支付请求，已通过 CanHandle 确认可处理。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>包含支付结果和订单信息的 IAPResult。</returns>
        UniTask<IAPResult> PayAsync(IAPRequest request, CancellationToken ct);

        /// <summary>
        /// 异步恢复历史已购商品。
        /// 仅适用于支持恢复购买的渠道（如 Google/iOS）；不支持的实现返回空列表。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>恢复到的所有历史订单结果列表。</returns>
        UniTask<IReadOnlyList<IAPResult>> RestorePurchasesAsync(CancellationToken ct);

        /// <summary>
        /// 异步扫描本地未完成订单并触发补单验单流程。
        /// 须在用户登录成功、SetUserId 调用后手动触发，确保按正确账号加载存档。
        /// 不支持补单的渠道返回 CompletedTask。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>补单扫描完成的异步任务。</returns>
        UniTask CheckLocalOrdersAsync(CancellationToken ct);

        /// <summary>
        /// 设置当前账号 UID，供存档隔离与补单路由使用。
        /// 由 IAPPlugin 在收到 SDKEventData.UserLogin 事件时自动广播；同 UID 重复调用须幂等。
        /// </summary>
        /// <param name="uid">已登录用户的唯一 ID。</param>
        void SetUserId(string uid);

        /// <summary>
        /// 异步释放 store 占用的资源。
        /// 在插件 DisposeAsync 阶段由 IAPPlugin 逐一调用。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        UniTask DisposeAsync(CancellationToken ct);
    }
}
