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
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 渠道 store 抽象基类，实现 IIAPInternalStore。
    /// 封装各渠道共用能力：Context 注入、防重复支付、无效 SKU 过滤、账号 UID 切换、
    /// 埋点上报、订阅有效期判断、订阅倒计时扩展点。
    /// </summary>
    public abstract partial class IAPStoreBase : IIAPInternalStore
    {
        /// <summary>
        /// 当前 store 的渠道类型，由子类返回固定枚举值。
        /// 用于补单路由还原与诊断日志，替代旧有的字符串类型名。
        /// </summary>
        public abstract IAPStoreType StoreType { get; }

        /// <summary>
        /// 商品表，由 InitializeAsync 阶段注入；为 null 时表示该 store 在当前配置下被禁用，子类自行决定降级行为。
        /// </summary>
        protected IIAPProductTable Table { get; private set; }

        /// <summary>
        /// store 运行时上下文，由 InitializeAsync 阶段注入，提供持久化/埋点/UI/网络能力。
        /// </summary>
        protected IIAPStoreContext Context { get; private set; }

        /// <summary>
        /// 当前 store 是否已初始化就绪，可以接受支付请求。
        /// 默认返回 true（ThirdPay / Voucher 无异步初始化步骤）；
        /// 依赖平台异步初始化的渠道（如 MobileStore）须重写此属性。
        /// </summary>
        protected virtual bool IsStoreReady => true;

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
        /// 支付前置校验模板：依次检查 store 初始化就绪、防重入、配置表商品存在；
        /// 全部通过后执行 payCore 核心逻辑。
        /// 校验失败时广播 RaisePayFailed 并直接返回对应错误结果，子类无需重复编写。
        /// </summary>
        /// <param name="request">支付请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <param name="payCore">通过所有前置校验后执行的核心支付逻辑。</param>
        /// <returns>支付结果。</returns>
        protected async UniTask<IAPResult> PayGuardAsync(IAPRequest request, CancellationToken ct, Func<UniTask<IAPResult>> payCore)
        {
            // 前置校验按“渠道可用 -> 初始化状态 -> 支付重入 -> 商品存在”顺序短路。
            if (!m_IsEnabled)
            {
                var r = new IAPResult(request.TableId, (int)IAPPluginErrorCode.StoreNotAvailable, $"{StoreType} store 已被禁用。", request.CustomData);
                Context?.EventBridge?.RaisePayFailed(r);
                return r;
            }
            if (!IsStoreReady)
            {
                var r = new IAPResult(request.TableId, (int)IAPPluginErrorCode.StoreInitFailed, $"{StoreType} store 尚未初始化完成。", request.CustomData);
                Context?.EventBridge?.RaisePayFailed(r);
                return r;
            }
            if (IsInPay)
            {
                var r = new IAPResult(request.TableId, (int)IAPPluginErrorCode.AlreadyPurchasing, $"当前已有支付进行中（tableId={m_InPayTableId}）。", request.CustomData);
                Context?.EventBridge?.RaisePayFailed(r);
                return r;
            }
            if (Table != null && Table.FindByTableId(request.TableId) == null)
            {
                var r = new IAPResult(request.TableId, (int)IAPPluginErrorCode.ProductNotFound, $"TableId={request.TableId} 未在配置中找到对应商品。", request.CustomData);
                Context?.EventBridge?.RaisePayFailed(r);
                return r;
            }
            return await payCore();
        }

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
        /// 将指定商品 ID 标记为平台不可购买的 SKU。
        /// 购买前通过 IsUnavailableSku 检查，避免向平台发起必然失败的请求。
        /// </summary>
        /// <param name="productId">平台商品 ID。</param>
        protected void AddUnavailableSku(string productId)
        {
            if (!string.IsNullOrEmpty(productId))
            {
                m_UnavailableSkus?.Add(productId);
            }
        }

        /// <summary>
        /// 判断指定商品 ID 是否已被标记为不可购买。
        /// </summary>
        /// <param name="productId">平台商品 ID。</param>
        /// <returns>已标记为不可购买时返回 true，否则返回 false。</returns>
        protected bool IsUnavailableSku(string productId)
        {
            if (string.IsNullOrEmpty(productId) || m_UnavailableSkus == null)
            {
                return false;
            }

            return m_UnavailableSkus.Contains(productId);
        }

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

        /// <summary>
        /// 当前 store 使用的日志标签字符串，由子类返回对应渠道的 LogTag 常量。
        /// 供 LogDebug / LogWarning / LogError 方法内部使用，调用方无需每次指定标签。
        /// </summary>
        protected abstract string StoreLogTag { get; }

        /// <summary>
        /// 输出 Debug 级别日志，自动附带子类声明的 StoreLogTag。
        /// </summary>
        /// <param name="msg">日志内容。</param>
        protected void LogDebug(string msg) => Log.Debug(StoreLogTag, msg);

        /// <summary>
        /// 输出 Warning 级别日志，自动附带子类声明的 StoreLogTag。
        /// </summary>
        /// <param name="msg">日志内容。</param>
        protected void LogWarning(string msg) => Log.Warning(StoreLogTag, msg);

        /// <summary>
        /// 输出 Error 级别日志，自动附带子类声明的 StoreLogTag。
        /// </summary>
        /// <param name="msg">日志内容。</param>
        protected void LogError(string msg) => Log.Error(StoreLogTag, msg);

        /// <summary>
        /// 订阅倒计时扩展点。
        /// 子类按需覆写以接入具体计时器（如 DOTween / UniTask），到期后触发 Restore。
        /// 基类默认空实现，不引入额外依赖。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID。</param>
        /// <param name="leftSeconds">剩余秒数；≤0 时应停止并清除已有计时器。</param>
        protected virtual void StartSubscriptionCountdown(long tableId, long leftSeconds) { }

        /// <summary>
        /// 从持久化层读取指定订阅商品的到期时间戳（毫秒）。
        /// 子类可重写以切换存储 key 格式。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID。</param>
        /// <returns>到期 Unix 毫秒时间戳；未存档时返回 0。</returns>
        protected virtual long GetSubscriptionExpireTimeMs(long tableId)
        {
            return 0L;
        }

        /// <summary>
        /// 创建新建/反序列化失败时使用的空存档容器。
        /// 子类必须返回类型特化的 IIAPStorePersistData 实例（其内部集合字段已通过 EnsureInitialized 兜底）。
        /// </summary>
        /// <returns>新的空存档容器。</returns>
        protected virtual IIAPStorePersistData CreateEmptyPersistData()
        {
            return null;
        }

        /// <summary>
        /// 模板方法：从持久化层加载当前账号的存档容器。
        /// 仅在已登录（m_GameUID 非空）时读盘；未登录或 PersistManager 缺失时直接返回空容器，
        /// 防止把匿名占位档（item=data_）当真实账号读写。反序列化失败时落回空容器并记日志。
        /// </summary>
        /// <typeparam name="T">具体存档容器类型，须为 IIAPStorePersistData 实现类。</typeparam>
        /// <returns>已 EnsureInitialized 的存档容器。</returns>
        protected T LoadPersistData<T>() where T : class, IIAPStorePersistData
        {
            T data = null;
            // 未登录时不读写匿名占位档，避免后续登录账号加载到错误数据。
            if (string.IsNullOrEmpty(m_GameUID))
            {
                LogWarning($"{StoreType} LoadPersistData 在 m_GameUID 为空时被调用，返回空容器；存档读写须在 SetUserId 之后。");
            }
            else if (Context?.PersistManager != null)
            {
                // 持久化反序列化异常只影响当前 store 数据，兜底为空容器继续运行。
                try
                {
                    data = Context.PersistManager.GetObject<T>(PersistClassify, PersistItemKey, null);
                }
                catch (System.Exception ex)
                {
                    LogWarning($"{StoreType} 存档反序列化失败 uid={m_GameUID}：{ex.Message}");
                    data = null;
                }
            }
            if (data == null)
            {
                // CreateEmptyPersistData 由子类提供具体容器类型，随后统一 EnsureInitialized。
                data = CreateEmptyPersistData() as T;
            }

            data?.EnsureInitialized();
            LogDebug($"{StoreType} LoadPersistData key={BuildPersistLogKey()} value={SerializePersistValue(data)}");
            return data;
        }

        /// <summary>
        /// 模板方法：将存档容器单原子写入持久化层并立即 Save 当前 classify。
        /// 仅在已登录（m_GameUID 非空）时落盘；未登录、PersistManager 缺失或 data 为空时静默跳过，
        /// 防止匿名状态污染真实账号存档。
        /// </summary>
        /// <typeparam name="T">具体存档容器类型，须为 IIAPStorePersistData 实现类。</typeparam>
        /// <param name="data">待写入的存档容器。</param>
        protected void SavePersistData<T>(T data) where T : class, IIAPStorePersistData
        {
            if (string.IsNullOrEmpty(m_GameUID) || Context?.PersistManager == null || data == null)
            {
                return;
            }

            Context.PersistManager.SetObject<T>(PersistClassify, PersistItemKey, data);
            Context.PersistManager.Save(PersistClassify);
            LogDebug($"{StoreType} SavePersistData key={BuildPersistLogKey()} value={SerializePersistValue(data)}");
        }

        /// <summary>
        /// 构建 IAP store 存档日志中的完整 key，包含 PersistManager 的 classify 与 item 两段。
        /// </summary>
        /// <returns>可直接写入日志的 key 描述。</returns>
        private string BuildPersistLogKey()
        {
            return $"classify={PersistClassify}, item={PersistItemKey}";
        }

        /// <summary>
        /// 将存档对象安全序列化为日志字符串，避免日志辅助逻辑影响真实读写。
        /// </summary>
        /// <typeparam name="T">存档对象类型。</typeparam>
        /// <param name="data">待打印的存档对象。</param>
        /// <returns>JSON 字符串；序列化失败时返回错误摘要。</returns>
        private static string SerializePersistValue<T>(T data) where T : class, IIAPStorePersistData
        {
            if (data == null)
            {
                return "null";
            }

            try
            {
                return Util.Json.Serialize(data);
            }
            catch (Exception ex)
            {
                return $"<serialize failed: {ex.Message}>";
            }
        }
    }
}
