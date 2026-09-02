/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPPlugin.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAP 调度插件主类，继承 SDKPluginBase
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 调度插件主类。
    /// 继承 SDKPluginBase，提供多渠道支付（Google/iOS/第三方/代金券）的业务入口。
    /// 核心支付入口固定为 PayAsync / RestorePurchasesAsync；商店特有能力通过 TryGetCapability 按功能接口取用，
    /// 避免随商店增多在此类上堆积转发方法。
    /// </summary>
    [SDKPluginConfigType(typeof(IAPPluginConfig))]
    public sealed partial class IAPPlugin : SDKPluginBase, IIAPStoreEventBridge, IIAPPlugin
    {

        /// <summary>
        /// 异步初始化 IAP 插件。
        /// 校验配置 → 构造商店运行时上下文 → 委派 DiscoverAndInitializeStoresAsync 通过反射扫描并初始化所有商店。
        /// 单个商店实例化或 InitializeAsync 失败时记录 Warning 后跳过，不影响其余商店初始化。
        /// </summary>
        /// <param name="config">由 SDKManager 注入的配置实例，必须为 IAPPluginConfig 类型。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected override async UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            ResetRuntimeTaskCancellation();
    
            IAPPluginConfig iapConfig = config as IAPPluginConfig;
            if (iapConfig == null)
            {
                LogWarning("IAPPlugin 初始化失败：config 为 null 或类型不匹配，期望 IAPPluginConfig。");
                return;
            }

            IAPLog.SetEnabled(iapConfig.EnableIAPLog);

            if (iapConfig.Products == null || iapConfig.Products.Count == 0)
            {
                LogWarning("IAPPlugin 初始化跳过：商品表为空，不创建任何商店。");
                return;
            }

            m_StoreContext = BuildStoreContext(iapConfig);
            m_Stores = new List<IIAPInternalStore>();
            m_StoreConfigMap = BuildStoreConfigMap(iapConfig);
            m_PurchasesTable = new IAPProductTableService(iapConfig.Products);

            // Store 实现通过 Attribute + 接口反射发现，核心插件只负责编排和路由。
            await DiscoverAndInitializeStoresAsync(ct);

            m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();
            m_EventManager?.Subscribe<SDKEventData.UserLogin>(OnUserLogin);
        }

        /// <summary>
        /// 异步释放 IAP 插件占用的资源。
        /// 依次调用各渠道商店的 DisposeAsync，确保资源按序释放。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        protected override async UniTask OnDisposeAsync(CancellationToken ct)
        {
            CancelRuntimeTasks();
            m_EventManager?.Unsubscribe<SDKEventData.UserLogin>(OnUserLogin);
            m_EventManager = null;
            m_CurrentUserId = null;
            m_HasDeferredCheckLocalOrders = false;
            m_IsCheckingLocalOrders = false;
            m_PendingCheckLocalOrders = false;

            if (m_Stores == null)
            {
                DisposeRuntimeTaskCancellation();
                return;
            }

            for (int i = 0; i < m_Stores.Count; i++)
            {
                await m_Stores[i].DisposeAsync(ct);
            }

            m_Stores = null;
            m_StoreContext = null;
            m_StoreConfigMap = null;
            m_PurchasesTable = null;
            DisposeRuntimeTaskCancellation();
        }

        /// <summary>
        /// 手动设置当前账号 UID，广播给所有商店。
        /// 通常无需主动调用——IAPPlugin 已在初始化时订阅 SDKEventData.UserLogin 自动同步；
        /// 仅在登录事件触达前 IAP 已使用或需要强制切换账号时使用。
        /// </summary>
        /// <param name="uid">已登录用户的唯一 ID。</param>
        public void SetUserId(string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                LogWarning("SetUserId：uid 为空，忽略本次账号同步。");
                return;
            }

            m_CurrentUserId = uid;

            if (m_Stores == null)
            {
                return;
            }

            for (int i = 0; i < m_Stores.Count; i++)
            {
                m_Stores[i].SetUserId(uid);
            }

            if (m_HasDeferredCheckLocalOrders)
            {
                m_HasDeferredCheckLocalOrders = false;
                RunBackgroundTask(CheckLocalOrdersAsync, "登录后自动补单扫描");
            }
        }

        /// <summary>
        /// 异步发起支付流程，根据请求类型路由到对应的商店。
        /// </summary>
        /// <param name="request">支付请求，实现 IIAPRequest 接口的具体子类实例。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>实现 IIAPResult 接口的支付结果。</returns>
        public async UniTask<T> PayAsync<T>(IIAPRequest request, CancellationToken ct = default) where T : class, IIAPResult
        {
            if (request == null)
            {
                LogWarning("IAPPlugin.PayAsync：request 为 null，拒绝处理。");
                var result = new IAPResult(0, (int)IAPPluginErrorCode.StoreNotAvailable, IAPErrorSource.PluginRouter, "request 为 null。", null);
                TrackRouterPayFail(result, null);
                return result as T;
            }
            IAPRequest iapRequest = request as IAPRequest;
            IIAPInternalStore store = FindStore(iapRequest);
            if (store == null)
            {
                LogWarning($"IAPPlugin.PayAsync：未找到能处理请求的商店，tableId={request.TableId}。");
                var result = new IAPResult(request.TableId, (int)IAPPluginErrorCode.StoreNotAvailable, IAPErrorSource.PluginRouter, "未找到匹配的支付渠道。", iapRequest?.CustomData, iapRequest?.ReceiptParam);
                TrackRouterPayFail(result, iapRequest);
                return result as T;
            }
            return await store.PayAsync(iapRequest, ct) as T;
        }

        /// <summary>
        /// 上报 IAPPlugin 路由层在命中具体 Store 前产生的 PayAsync 失败。
        /// </summary>
        /// <param name="result">待上报的失败支付结果。</param>
        /// <param name="request">原始支付请求；为空时表示 request 本身为空。</param>
        private void TrackRouterPayFail(IAPResult result, IAPRequest request)
        {
            if (result == null || result.IsSuccess)
            {
                return;
            }

            string channel = request == null ? "router" : request.StoreType.ToString().ToLowerInvariant();
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, result.TableId },
                { IAPTrackFields.ProductId, string.Empty },
                { IAPTrackFields.Debug, m_StoreContext?.DevelopMode == DevelopMode.Debug },
                { IAPTrackFields.Price, 0f },
                { IAPTrackFields.Channel, channel },
                { IAPTrackFields.Reason, result.ErrorCode },
                { IAPTrackFields.ReasonDetail, FormatPayFailureReasonDetail(result) },
            };
            AppendRouterCustomData(properties, result.CustomData);
            m_StoreContext?.TrackPlugin?.TrackEvent(IAPTrackEvents.LocalPayFail, properties);
        }

        /// <summary>
        /// 格式化 PayAsync 失败打点的可读详情，保留错误来源与错误码域。
        /// </summary>
        /// <param name="result">失败支付结果。</param>
        /// <returns>包含 ErrorSource、ErrorCode 和错误描述的详情字符串。</returns>
        private static string FormatPayFailureReasonDetail(IAPResult result)
        {
            return string.IsNullOrEmpty(result.ErrorDesc)
                ? $"{result.ErrorSource}:{result.ErrorCode}"
                : $"{result.ErrorSource}:{result.ErrorCode} {result.ErrorDesc}";
        }

        /// <summary>
        /// 将业务透传数据追加到路由层失败打点参数中；空值不写入。
        /// </summary>
        /// <param name="properties">待上报的打点参数字典。</param>
        /// <param name="customData">业务层透传字符串。</param>
        private static void AppendRouterCustomData(Dictionary<string, object> properties, string customData)
        {
            if (!string.IsNullOrEmpty(customData))
            {
                properties[IAPTrackFields.CustomData] = customData;
            }
        }

        /// <summary>
        /// 异步恢复历史已购商品，遍历所有商店收集恢复结果。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>所有商店恢复到的历史订单结果列表。</returns>
        public async UniTask<IReadOnlyList<T>> RestorePurchasesAsync<T>(CancellationToken ct = default) where T : class, IIAPResult
        {
            var results = new List<T>();
            if (m_Stores == null)
            {
                return results;
            }

            for (int i = 0; i < m_Stores.Count; i++)
            {
                // 各商店自行决定恢复订阅/非消耗品，插件层只聚合返回结果。
                IReadOnlyList<IAPResult> storeResults = await m_Stores[i].RestorePurchasesAsync(ct);
                for (int j = 0; j < storeResults.Count; j++)
                {
                    T item = storeResults[j] as T;
                    if (item != null)
                    {
                        results.Add(item);
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// 异步触发所有商店的本地补单扫描。
        /// 若调用时尚未同步账号 UID，会缓存一次扫描请求，并在 SetUserId 后自动补执行。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>所有商店补单扫描完成的异步任务。</returns>
        public async UniTask CheckLocalOrdersAsync(CancellationToken ct = default)
        {

            if (string.IsNullOrEmpty(m_CurrentUserId))
            {
                m_HasDeferredCheckLocalOrders = true;
                LogDebug("账号未登录，已缓存补单扫描请求，等待 SetUserId 后自动执行。");
                return;
            }

            if (m_IsCheckingLocalOrders)
            {
                m_PendingCheckLocalOrders = true;
                LogDebug("补单扫描正在执行，已标记当前轮结束后补跑一次。");
                return;
            }

            m_IsCheckingLocalOrders = true;
            try
            {
                do
                {
                    m_PendingCheckLocalOrders = false;
                    if (m_Stores == null)
                    {
                        return;
                    }

                    for (int i = 0; i < m_Stores.Count; i++)
                    {
                        await m_Stores[i].CheckLocalOrdersAsync(ct);
                    }
                } while (m_PendingCheckLocalOrders && !string.IsNullOrEmpty(m_CurrentUserId));
            }
            finally
            {
                m_IsCheckingLocalOrders = false;
            }

        }

        /// <summary>
        /// 运行时强制启用或禁用指定渠道商店。
        /// 禁用后该商店不再参与 PayAsync / RestorePurchasesAsync / CheckLocalOrdersAsync 路由；
        /// 从禁用转为启用时，若商店尚未完成初始化（首次启用懒初始化），会自动触发 InitializeAsync。
        /// </summary>
        /// <param name="storeType">目标渠道类型。</param>
        /// <param name="enabled">true = 启用，false = 禁用。</param>
        /// <param name="ct">取消令牌，仅在触发懒初始化时使用。</param>
        /// <returns>启用且触发懒初始化时返回初始化任务；其余情况返回 CompletedTask。</returns>
        public async UniTask SetStoreEnabled(IAPStoreType storeType, bool enabled, CancellationToken ct = default)
        {
            if (m_Stores == null)
            {
                return;
            }

            for (int i = 0; i < m_Stores.Count; i++)
            {
                if (m_Stores[i].StoreType != storeType)
                {
                    continue;
                }

                IIAPInternalStore store = m_Stores[i];
                store.SetEnabled(enabled);

                if (enabled)
                {
                    // 禁用态商店可能尚未初始化，重新启用时触发一次懒初始化。
                    IIAPStoreConfig cfg = null;
                    m_StoreConfigMap?.TryGetValue(storeType, out cfg);
                    await store.EnableAsync(m_PurchasesTable, cfg, m_StoreContext, ct);
                }
                return;
            }
            LogWarning($"SetStoreEnabled：未找到 StoreType={storeType} 对应的商店实例。");
        }

        /// <summary>
        /// 查询第一个实现了指定功能接口的商店，并以该接口类型返回。
        /// 业务层通过此方法取用商店特有能力（如 IIAPSubscriptionCapable、IIAPQueryCapable），
        /// 无需 IAPPlugin 为每种能力单独暴露转发方法。
        /// </summary>
        /// <typeparam name="T">目标功能接口类型，如 IIAPSubscriptionCapable。</typeparam>
        /// <param name="capability">找到时输出实现该接口的商店；未找到时输出 null。</param>
        /// <returns>找到匹配商店时返回 true，否则返回 false。</returns>
        public bool TryGetCapability<T>(out T capability) where T : class, IIAPCapable
        {
            if (m_Stores != null)
            {
                for (int i = 0; i < m_Stores.Count; i++)
                {
                    if (m_Stores[i] is T c)
                    {
                        capability = c;
                        return true;
                    }
                }
            }
            capability = null;
            return false;
        }
    }
}
