/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreBase.Methods.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   IAPStoreBase 私有/保护辅助方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAPStoreBase 私有/保护辅助方法。
    /// </summary>
    public abstract partial class IAPStoreBase
    {
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
                var r = new IAPResult(request.TableId, (int)IAPPluginErrorCode.StoreNotAvailable, IAPErrorSource.PluginRouter, $"{StoreType} store 已被禁用。", request.CustomData, request.ReceiptParam);
                return CompletePayGuardFailure(r);
            }

            if (!IsStoreReady)
            {
                var r = new IAPResult(request.TableId, (int)IAPPluginErrorCode.StoreInitFailed, IAPErrorSource.PluginRouter, $"{StoreType} store 尚未初始化完成。", request.CustomData, request.ReceiptParam);
                return CompletePayGuardFailure(r);
            }

            if (IsInPay)
            {
                var r = new IAPResult(request.TableId, (int)IAPPluginErrorCode.AlreadyPurchasing, IAPErrorSource.PluginRouter, $"当前已有支付进行中（tableId={m_InPayTableId}）。", request.CustomData, request.ReceiptParam);
                return CompletePayGuardFailure(r);
            }

            if (Table != null && Table.FindByTableId(request.TableId) == null)
            {
                var r = new IAPResult(request.TableId, (int)IAPPluginErrorCode.ProductNotFound, IAPErrorSource.PluginRouter, $"TableId={request.TableId} 未在配置中找到对应商品。", request.CustomData, request.ReceiptParam);
                return CompletePayGuardFailure(r);
            }

            return await payCore();
        }

        /// <summary>
        /// 统一完成支付前置校验失败：派发失败事件、按需补齐失败打点并返回原结果。
        /// </summary>
        /// <param name="result">支付前置校验生成的失败结果。</param>
        /// <returns>原始失败结果。</returns>
        private IAPResult CompletePayGuardFailure(IAPResult result)
        {
            Context?.EventBridge?.RaisePayFailed(result);
            if (ShouldTrackPayGuardFailure(result))
            {
                TrackPayFailureResult(result, result?.ErrorCode ?? 0, result?.ErrorDesc);
            }

            OnPayGuardFailureTracked(result);
            return result;
        }

        /// <summary>
        /// 判断当前 Store 是否由基类直接上报支付前置校验失败打点。
        /// </summary>
        /// <param name="result">支付前置校验生成的失败结果。</param>
        /// <returns>需要由基类上报时返回 true；由子类返回边界统一上报时返回 false。</returns>
        protected virtual bool ShouldTrackPayGuardFailure(IAPResult result)
        {
            return true;
        }

        /// <summary>
        /// 支付前置校验失败打点处理后的扩展钩子，供子类按需记录状态。
        /// </summary>
        /// <param name="result">支付前置校验生成的失败结果。</param>
        protected virtual void OnPayGuardFailureTracked(IAPResult result)
        {
        }

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
        /// 清空平台不可购买 SKU 标记。
        /// 重新向平台拉取商品前调用，避免上一轮网络失败留下的临时失败状态污染后续成功结果。
        /// </summary>
        protected void ClearUnavailableSkus()
        {
            m_UnavailableSkus?.Clear();
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
                catch (Exception ex)
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

        /// <summary>
        /// 若 Context.LoadingPanelPrefab 配置了路径且尚未绑定呈现器，则创建默认 Loading 呈现器，
        /// 并将其 Show/Hide 绑定为 m_LoadingGuard 的显隐回调。
        /// 路径为空时保持未绑定状态，Loading 行为为空操作；业务层可在初始化后再次调用
        /// BindLoadingCallbacks 覆盖为自定义 UI。
        /// </summary>
        private void TryBindDefaultLoadingPanel()
        {
            if (m_LoadingPresenter != null)
            {
                return;
            }

            string path = Context?.LoadingPanelPrefab;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            m_LoadingPresenter = new IAPLoadingPanelPresenter(path);
            BindLoadingCallbacks(m_LoadingPresenter.Show, m_LoadingPresenter.Hide);
        }
    }
}
