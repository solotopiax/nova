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
    /// 核心支付入口固定为 PayAsync / RestorePurchasesAsync；store 特有能力通过 TryGetCapability 按功能接口取用，
    /// 避免随 store 增多在此类上堆积转发方法。
    /// </summary>
    public sealed partial class IAPPlugin : SDKPluginBase, IIAPStoreEventBridge, IIAPPlugin
    {

        /// <summary>
        /// 异步初始化 IAP 插件。
        /// 校验配置 → 构造 store 运行时上下文 → 委派 DiscoverAndInitializeStoresAsync 通过反射扫描并初始化所有 store。
        /// 单个 store 实例化或 InitializeAsync 失败时记录 Warning 后跳过，不影响其余 store 初始化。
        /// </summary>
        /// <param name="config">由 SDKManager 注入的配置实例，必须为 IAPPluginConfig 类型。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected override async UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            IAPPluginConfig iapConfig = config as IAPPluginConfig;
            if (iapConfig == null)
            {
                Log.Warning(LogTag.IAPPlugin, "IAPPlugin 初始化失败：config 为 null 或类型不匹配，期望 IAPPluginConfig。");
                return;
            }

            if (iapConfig.Products == null || iapConfig.Products.Count == 0)
            {
                Log.Warning(LogTag.IAPPlugin, "IAPPlugin 初始化跳过：商品表为空，不创建任何 store。");
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
        /// 依次调用各渠道 store 的 DisposeAsync，确保资源按序释放。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        protected override async UniTask OnDisposeAsync(CancellationToken ct)
        {
            m_EventManager?.Unsubscribe<SDKEventData.UserLogin>(OnUserLogin);
            m_EventManager = null;
            m_CurrentUserId = null;
            m_EventCaches.Clear();
            m_IsReplayingEventCaches = false;

            if (m_Stores == null)
            {
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
        }

        /// <summary>
        /// 手动设置当前账号 UID，广播给所有 store。
        /// 通常无需主动调用——IAPPlugin 已在初始化时订阅 SDKEventData.UserLogin 自动同步；
        /// 仅在登录事件触达前 IAP 已使用或需要强制切换账号时使用。
        /// </summary>
        /// <param name="uid">已登录用户的唯一 ID。</param>
        public void SetUserId(string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                Log.Warning(LogTag.IAPPlugin, "SetUserId：uid 为空，忽略本次账号同步。");
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

            ReplayEventCachesAsync().Forget();
        }

        /// <summary>
        /// 异步发起支付流程，根据 request 类型路由到对应的 store。
        /// </summary>
        /// <param name="request">支付请求，实现 IIAPRequest 接口的具体子类实例。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>实现 IIAPResult 接口的支付结果。</returns>
        public async UniTask<T> PayAsync<T>(IIAPRequest request, CancellationToken ct = default) where T : class, IIAPResult
        {
            if (request == null)
            {
                Log.Warning(LogTag.IAPPlugin, "IAPPlugin.PayAsync：request 为 null，拒绝处理。");
                return new IAPResult(0, (int)IAPPluginErrorCode.StoreNotAvailable, "request 为 null。", null) as T;
            }
            IAPRequest iapRequest = request as IAPRequest;
            IIAPInternalStore store = FindStore(iapRequest);
            if (store == null)
            {
                Log.Warning(LogTag.IAPPlugin, "IAPPlugin.PayAsync：未找到能处理请求的 store，tableId={0}。", request.TableId);
                return new IAPResult(request.TableId, (int)IAPPluginErrorCode.StoreNotAvailable, "未找到匹配的支付渠道。", iapRequest?.CustomData) as T;
            }
            return await store.PayAsync(iapRequest, ct) as T;
        }

        /// <summary>
        /// 异步恢复历史已购商品，遍历所有 store 收集恢复结果。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>所有 store 恢复到的历史订单结果列表。</returns>
        public async UniTask<IReadOnlyList<T>> RestorePurchasesAsync<T>(CancellationToken ct = default) where T : class, IIAPResult
        {
            var results = new List<T>();
            if (m_Stores == null)
            {
                return results;
            }

            for (int i = 0; i < m_Stores.Count; i++)
            {
                // 各 store 自行决定恢复订阅/非消耗品，插件层只聚合返回结果。
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
        /// 异步触发所有 store 的本地补单扫描。
        /// 若调用时尚未同步账号 UID，会缓存一次扫描请求，并在 SetUserId 后自动补执行。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>所有 store 补单扫描完成的异步任务。</returns>
        public async UniTask CheckLocalOrdersAsync(CancellationToken ct = default)
        {

            if (string.IsNullOrEmpty(m_CurrentUserId))
            {
                CacheEvent(() => CheckLocalOrdersAsync(CancellationToken.None), "补单扫描");
                Log.Debug(LogTag.IAPPlugin, "账号未登录，已缓存补单扫描请求，等待 SetUserId 后自动执行。");
            }
            else
            {
                for (int i = 0; i < m_Stores.Count; i++)
                {
                    await m_Stores[i].CheckLocalOrdersAsync(ct);
                }
            }

        }

        /// <summary>
        /// 运行时强制启用或禁用指定渠道 store。
        /// 禁用后该 store 不再参与 PayAsync / RestorePurchasesAsync / CheckLocalOrdersAsync 路由；
        /// 从禁用转为启用时，若 store 尚未完成初始化（首次启用懒初始化），会自动触发 InitializeAsync。
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
                    // 禁用态 store 可能尚未初始化，重新启用时触发一次懒初始化。
                    IIAPStoreConfig cfg = null;
                    m_StoreConfigMap?.TryGetValue(storeType, out cfg);
                    await store.EnableAsync(m_PurchasesTable, cfg, m_StoreContext, ct);
                }
                return;
            }
            Log.Warning(LogTag.IAPPlugin, "SetStoreEnabled：未找到 StoreType={0} 对应的 store 实例。", storeType);
        }

        /// <summary>
        /// 查询第一个实现了指定功能接口的 store，并以该接口类型返回。
        /// 业务层通过此方法取用 store 特有能力（如 IIAPSubscriptionCapable、IIAPQueryCapable），
        /// 无需 IAPPlugin 为每种能力单独暴露转发方法。
        /// </summary>
        /// <typeparam name="T">目标功能接口类型，如 IIAPSubscriptionCapable。</typeparam>
        /// <param name="capability">找到时输出实现该接口的 store；未找到时输出 null。</param>
        /// <returns>找到匹配 store 时返回 true，否则返回 false。</returns>
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
