/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPPlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAPPlugin 私有辅助方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAPPlugin 私有辅助方法。
    /// </summary>
    public sealed partial class IAPPlugin
    {
        /// <summary>
        /// 扫描当前 AppDomain 下所有程序集，挑出标注 IAPStoreAttribute 且实现 IIAPInternalStore 的具体类型，
        /// 逐个委派 TryInitializeStoreAsync 完成实例化与初始化。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>扫描与初始化全部完成的异步任务。</returns>
        private async UniTask DiscoverAndInitializeStoresAsync(CancellationToken ct)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int aIdx = 0; aIdx < assemblies.Length; aIdx++)
            {
                // 单个程序集类型加载失败时只跳过该程序集，避免影响其他 store 发现。
                Type[] types = SafeGetTypes(assemblies[aIdx]);
                if (types == null)
                {
                    continue;
                }

                for (int tIdx = 0; tIdx < types.Length; tIdx++)
                {
                    Type t = types[tIdx];
                    // 候选类型必须同时满足接口、Attribute、公开非抽象类三类约束。
                    if (!IsCandidateStoreType(t))
                    {
                        continue;
                    }

                    await TryInitializeStoreAsync(t, ct);
                }
            }
        }

        /// <summary>
        /// 安全获取程序集导出的全部类型，遇 ReflectionTypeLoadException 时记录 Warning 并返回 null，
        /// 让上层跳过该程序集而不中断整个扫描流程。
        /// </summary>
        /// <param name="assembly">待扫描的程序集。</param>
        /// <returns>程序集中的类型数组；加载失败时返回 null。</returns>
        private static Type[] SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Log.Warning(LogTag.IAPPlugin, $"扫描程序集 {assembly.GetName().Name} 时发生 ReflectionTypeLoadException，跳过。异常：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 判断给定类型是否符合 IAPStore 候选条件：公开、非抽象、是 class、实现 IIAPInternalStore 且标注 IAPStoreAttribute。
        /// </summary>
        /// <param name="t">待检测的类型。</param>
        /// <returns>满足全部条件返回 true，否则返回 false。</returns>
        private static bool IsCandidateStoreType(Type t)
        {
            if (t == null || !t.IsClass || t.IsAbstract || !t.IsPublic)
            {
                return false;
            }

            if (!typeof(IIAPInternalStore).IsAssignableFrom(t))
            {
                return false;
            }

            if (!Attribute.IsDefined(t, typeof(IAPStoreAttribute), inherit: false))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 对单个候选类型完成实例化与 InitializeAsync 调用，成功后加入 m_Stores。
        /// 实例化与初始化阶段的普通异常被吞掉并记录 Warning，OperationCanceledException 仍向上抛出以保持取消语义。
        /// </summary>
        /// <param name="t">已通过 IsCandidateStoreType 校验的 store 类型。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>该 store 初始化完成或被跳过的异步任务。</returns>
        private async UniTask TryInitializeStoreAsync(Type t, CancellationToken ct)
        {
            Log.Debug(LogTag.IAPPlugin, $"发现 IAPStore 实现：{t.FullName}，开始实例化。");

            IIAPInternalStore store;
            try
            {
                store = Activator.CreateInstance(t) as IIAPInternalStore;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.IAPPlugin, $"实例化 {t.FullName} 失败，跳过该 store。异常：{ex.Message}");
                return;
            }

            if (store == null)
            {
                Log.Warning(LogTag.IAPPlugin, $"{t.FullName} 实例化结果无法转换为 IIAPInternalStore，跳过。");
                return;
            }

            IIAPStoreConfig storeConfig = null;
            if (m_StoreConfigMap != null)
            {
                m_StoreConfigMap.TryGetValue(store.StoreType, out storeConfig);
            }

            bool enabled = storeConfig == null || storeConfig.Enabled;
            if (!enabled)
            {
                // 配置禁用时保留实例但跳过 InitializeAsync，后续 SetStoreEnabled 可懒初始化。
                store.SetEnabled(false);
                m_Stores.Add(store);
                Log.Debug(LogTag.IAPPlugin, $"IAPStore {t.FullName} 已禁用，跳过初始化（懒初始化）。");
                return;
            }

            try
            {
                await store.InitializeAsync(m_PurchasesTable, storeConfig, m_StoreContext, ct);
                m_Stores.Add(store);
                Log.Debug(LogTag.IAPPlugin, $"IAPStore {t.FullName} 初始化完成。");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.IAPPlugin, $"IAPStore {t.FullName} InitializeAsync 失败，跳过该 store。异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 构造 IAP store 运行时上下文，从各框架 Manager 中获取跨模块依赖。
        /// </summary>
        /// <param name="config">IAPPlugin 配置，用于提取 EnableAlwaysPaySucceed 调试开关。</param>
        /// <returns>构造完成的 IAPStoreContext 实例。</returns>
        private IAPStoreContext BuildStoreContext(IAPPluginConfig config)
        {
            IFileFragmentManager persistManager = FrameworkManagersGroup.GetManager<IFileFragmentManager>();
            IUIManager uiManager = FrameworkManagersGroup.GetManager<IUIManager>();
            INetworkManager networkManager = FrameworkManagersGroup.GetManager<INetworkManager>();
            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            DevelopMode developMode = configManager?.DevelopMode ?? DevelopMode.Debug;

            ITrackPlugin trackPlugin = null;
            SDKComponent sdkComponent = FrameworkComponentsGroup.GetComponent<SDKComponent>();
            if (sdkComponent != null)
            {
                sdkComponent.TryGet<ITrackPlugin>(out trackPlugin);
            }

            return new IAPStoreContext(persistManager, trackPlugin, uiManager, networkManager, config.EnableAlwaysPaySucceed, config.RetryValidateMaxNum, config.SkipLoadingForReplenish, config.LoadingPanelPrefab, this, developMode);
        }

        /// <summary>
        /// 在 store 列表中找到第一个能处理指定请求的 store。
        /// </summary>
        /// <param name="request">待处理的支付请求。</param>
        /// <returns>能处理请求的 store；若无匹配则返回 null。</returns>
        private IIAPInternalStore FindStore(IAPRequest request)
        {
            if (m_Stores == null)
            {
                return null;
            }

            for (int i = 0; i < m_Stores.Count; i++)
            {
                if (m_Stores[i].CanHandle(request))
                {
                    return m_Stores[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 把 IAPPluginConfig.StoreConfigs 索引为 Dictionary IAPStoreType 到 IIAPStoreConfig 的映射，
        /// 供 TryInitializeStoreAsync 按 store.StoreType 查表。
        /// 跳过 null（SerializeReference Missing）与 StoreType.None；同 StoreType 重复时保留首条 + Warning。
        /// </summary>
        /// <param name="iapConfig">IAP 插件配置实例。</param>
        /// <returns>构建完成的渠道路由表；StoreConfigs 为 null 时返回空字典。</returns>
        private static Dictionary<IAPStoreType, IIAPStoreConfig> BuildStoreConfigMap(IAPPluginConfig iapConfig)
        {
            var map = new Dictionary<IAPStoreType, IIAPStoreConfig>();
            if (iapConfig.StoreConfigs == null)
            {
                return map;
            }

            for (int i = 0; i < iapConfig.StoreConfigs.Count; i++)
            {
                IIAPStoreConfig cfg = iapConfig.StoreConfigs[i];
                if (cfg == null)
                {
                    continue;
                }

                if (cfg.StoreType == IAPStoreType.None)
                {
                    Log.Warning(LogTag.IAPPlugin, $"忽略 StoreType=None 的配置：{cfg.GetType().FullName}");
                    continue;
                }

                if (map.ContainsKey(cfg.StoreType))
                {
                    // 同渠道重复配置会导致路由歧义，因此保留首条并显式告警。
                    Log.Warning(LogTag.IAPPlugin, $"渠道 {cfg.StoreType} 配置重复，保留首条，丢弃 {cfg.GetType().FullName}");
                    continue;
                }

                map[cfg.StoreType] = cfg;
            }

            return map;
        }

        /// <summary>
        /// 缓存一条待条件满足后执行的事件。
        /// </summary>
        /// <param name="eventAction">待执行的异步事件。</param>
        /// <param name="eventName">事件名称，仅用于日志诊断。</param>
        private void CacheEvent(Func<UniTask> eventAction, string eventName)
        {
            if (eventAction == null)
            {
                return;
            }

            m_EventCaches.Add(eventAction);
            Log.Debug(LogTag.IAPPlugin, "IAPPlugin 已缓存延后执行事件：{0}。", eventName);
        }

        /// <summary>
        /// 按入队顺序回放缓存事件。
        /// </summary>
        /// <returns>全部可执行缓存事件回放完成的异步任务。</returns>
        private async UniTask ReplayEventCachesAsync()
        {   
            if (m_IsReplayingEventCaches)
            {
                Log.Debug(LogTag.IAPPlugin, "IAPPlugin 正在回放缓存事件，跳过。");
                return;
            }

            m_IsReplayingEventCaches = true;
            m_EventCaches.ForEach(async eventAction => await eventAction());
            m_EventCaches.Clear();
            m_IsReplayingEventCaches = false;
        }

        /// <summary>
        /// 接收 store 初始化结果并派发到 Events.InitResult。
        /// </summary>
        /// <param name="result">包含成功标志、失败原因与详情的初始化结果。</param>
        void IIAPStoreEventBridge.RaiseInitResult(IAPInitResult result) => Events.InitResult.Invoke(result);

        /// <summary>
        /// 接收支付成功结果并派发到 Events.PaySuccess。
        /// </summary>
        /// <param name="result">包含订单信息的 IAPResult。</param>
        void IIAPStoreEventBridge.RaisePaySuccess(IAPResult result) => Events.PaySuccess.Invoke(result);

        /// <summary>
        /// 接收支付失败结果并派发到 Events.PayFailed。
        /// </summary>
        /// <param name="result">包含失败原因的 IAPResult。</param>
        void IIAPStoreEventBridge.RaisePayFailed(IAPResult result) => Events.PayFailed.Invoke(result);

        /// <summary>
        /// 接收订阅 Restore 结果并派发到 Events.SubscriptionRestored。
        /// </summary>
        /// <param name="results">本次 Restore 恢复的订阅列表。</param>
        void IIAPStoreEventBridge.RaiseSubscriptionRestored(IReadOnlyList<IAPResult> results) => Events.SubscriptionRestored.Invoke(results);

        /// <summary>
        /// 接收非消耗品 Restore 结果并派发到 Events.NonConsumeRestored。
        /// </summary>
        /// <param name="results">本次 Restore 恢复的非消耗品列表。</param>
        void IIAPStoreEventBridge.RaiseNonConsumeRestored(IReadOnlyList<IAPResult> results) => Events.NonConsumeRestored.Invoke(results);

        /// <summary>
        /// SDKEventData.UserLogin 事件回调，自动将登录 UID 广播给所有 store。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件数据，须为 SDKEventData.UserLogin 类型。</param>
        private void OnUserLogin(object sender, EventData e)
        {
            if (e is SDKEventData.UserLogin login)
            {
                SetUserId(login.UserId);
            }
        }

    }
}
