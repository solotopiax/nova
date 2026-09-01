/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreBase.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAP store 公共抽象基类，封装上下文、防重入、SKU 过滤、账号、埋点
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 渠道 store 抽象基类，实现 IIAPInternalStore。
    /// 封装各渠道共用能力：Context 注入、防重复支付、无效 SKU 过滤、账号 UID 切换、
    /// 埋点上报、订阅有效期判断、订阅倒计时扩展点。
    /// </summary>
    public abstract partial class IAPStoreBase : IAPLogOwner, IIAPInternalStore
    {
        /// <summary>
        /// 当前 store 的渠道类型，由子类返回固定枚举值。
        /// 用于补单路由还原与诊断日志，替代旧有的字符串类型名。
        /// </summary>
        public abstract IAPStoreType StoreType { get; }

        /// <summary>
        /// 异步初始化 store，保存商品表与运行时上下文并重置运行时状态。
        /// 子类重写时须调用 base.InitializeAsync。
        /// </summary>
        /// <param name="table">所有 store 共用的商品表（IIAPProductTable 接口实现）。</param>
        /// <param name="config">store 专属配置。</param>
        /// <param name="ctx">store 运行时上下文，包含跨模块依赖引用。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        public virtual UniTask InitializeAsync(IIAPProductTable table, IIAPStoreConfig config, IIAPStoreContext ctx, CancellationToken ct)
        {
            Table = table;
            Context = ctx;
            m_InPayTableId = 0;
            m_UnavailableSkus = new HashSet<string>();
            m_IsInitialized = true;
            TryBindDefaultLoadingPanel();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 运行时启用或禁用该 store。
        /// 禁用后 PayAsync / RestorePurchasesAsync / CheckLocalOrdersAsync 均立即返回 StoreDisabled；
        /// 已初始化的 store 重新启用时无需再次初始化，直接翻转标志即可。
        /// </summary>
        /// <param name="enabled">true = 启用，false = 禁用。</param>
        public void SetEnabled(bool enabled) => m_IsEnabled = enabled;

        /// <summary>
        /// 懒初始化入口：仅在尚未初始化时执行 InitializeAsync；已初始化则立即返回。
        /// </summary>
        /// <param name="table">商品表（IIAPProductTable 接口实现）。</param>
        /// <param name="config">store 专属配置。</param>
        /// <param name="ctx">store 运行时上下文。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        public UniTask EnableAsync(IIAPProductTable table, IIAPStoreConfig config, IIAPStoreContext ctx, CancellationToken ct)
        {
            if (m_IsInitialized)
            {
                return UniTask.CompletedTask;
            }

            return InitializeAsync(table, config, ctx, ct);
        }

        /// <summary>
        /// 判断当前 store 是否能处理指定请求，由子类通过请求类型做匹配。
        /// </summary>
        /// <param name="request">待判断的支付请求。</param>
        /// <returns>能处理时返回 true，否则返回 false。</returns>
        public abstract bool CanHandle(IAPRequest request);

        /// <summary>
        /// 异步发起支付流程，由子类实现具体渠道逻辑。
        /// </summary>
        /// <param name="request">支付请求，已通过 CanHandle 确认可处理。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>包含支付结果和订单信息的 IAPResult。</returns>
        public abstract UniTask<IAPResult> PayAsync(IAPRequest request, CancellationToken ct);

        /// <summary>
        /// 异步恢复历史已购商品。
        /// 默认实现返回空列表；已禁用或尚未初始化时同样返回空列表不上报错误。
        /// 不支持恢复购买的渠道无需重写。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>恢复到的历史订单结果列表；默认为空列表。</returns>
        public virtual UniTask<IReadOnlyList<IAPResult>> RestorePurchasesAsync(CancellationToken ct)
        {
            if (!m_IsEnabled)
            {
                LogWarning($"{StoreType} store 已被禁用，跳过 RestorePurchasesAsync。");
                return UniTask.FromResult<IReadOnlyList<IAPResult>>(new List<IAPResult>());
            }

            return UniTask.FromResult<IReadOnlyList<IAPResult>>(new List<IAPResult>());
        }

        /// <summary>
        /// 异步扫描本地未完成订单并触发补单验单流程。
        /// 须在用户登录成功、SetUserId 调用后手动触发；已禁用或尚未初始化时静默跳过。
        /// 不支持补单的渠道无需重写。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>补单扫描完成的异步任务。</returns>
        public virtual UniTask CheckLocalOrdersAsync(CancellationToken ct)
        {
            if (!m_IsEnabled)
            {
                LogWarning($"{StoreType} store 已被禁用，跳过 CheckLocalOrdersAsync。");
                return UniTask.CompletedTask;
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步释放 store 占用的资源。
        /// 默认实现为空，持有非托管资源的子类须重写此方法。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        public virtual UniTask DisposeAsync(CancellationToken ct)
        {
            m_InPayTableId = 0;
            m_IsInitialized = false;
            m_GameUID = string.Empty;
            m_UnavailableSkus?.Clear();
            m_LoadingGuard.Clear();
            m_LoadingPresenter?.Dispose();
            m_LoadingPresenter = null;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 切换当前登录用户 UID。
        /// 用户登录后调用，确保存档按账号隔离；uid 为空或与当前相同时静默跳过。
        /// </summary>
        /// <param name="uid">已登录用户的唯一 ID。</param>
        public virtual void SetUserId(string uid)
        {
            if (string.IsNullOrEmpty(uid) || m_GameUID == uid)
            {
                return;
            }

            m_GameUID = uid;
        }

        /// <summary>
        /// 绑定 Loading 显示/隐藏回调，由业务层在 store 初始化完成后注入具体 UI 实现。
        /// </summary>
        /// <param name="onPush">显示 Loading 的回调。</param>
        /// <param name="onPop">隐藏 Loading 的回调。</param>
        public void BindLoadingCallbacks(Action onPush, Action onPop) => m_LoadingGuard.Bind(onPush, onPop);

        /// <summary>
        /// 显示 Loading（受 LoadingGuard.ShouldShow 控制）。
        /// </summary>
        public void AddWaitingRef() => m_LoadingGuard.Push();

        /// <summary>
        /// 显示 Loading（forceShow 直接决定是否显示）。
        /// </summary>
        /// <param name="forceShow">为 true 时强制显示，为 false 时跳过。</param>
        public void AddWaitingRef(bool forceShow) => m_LoadingGuard.Push(forceShow);

        /// <summary>
        /// 隐藏一层 Loading（受 LoadingGuard.ShouldShow 控制）。
        /// </summary>
        public void SubWaitingRef() => m_LoadingGuard.Pop();

        /// <summary>
        /// 隐藏一层 Loading（forceShow 直接决定是否执行）。
        /// </summary>
        /// <param name="forceShow">为 true 时强制执行，为 false 时跳过。</param>
        public void SubWaitingRef(bool forceShow) => m_LoadingGuard.Pop(forceShow);

        /// <summary>
        /// 判断指定订阅商品是否仍在有效期内。
        /// 从持久化层读取到期时间戳并与当前 UTC 时间比较；
        /// 同时作为 IIAPSubscriptionCapable 接口实现暴露给业务层。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID。</param>
        /// <returns>订阅有效期内返回 true，否则返回 false。</returns>
        public bool InSubscriptionPeriod(long tableId)
        {
            if (Context?.PersistManager == null)
            {
                return false;
            }

            long expireTimeMs = GetSubscriptionExpireTimeMs(tableId);
            return expireTimeMs > 0 && expireTimeMs >= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
