/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppsFlyerPlugin.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   AppsFlyer SDK插件主文件（public/override方法）
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppsFlyerSDK;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AppsFlyerPlugin.Runtime
{
    /// <summary>
    /// AppsFlyer SDK插件，继承SDKPluginBase，实现IAttributionPlugin归因打点契约。
    /// 负责SDK初始化、归因事件上报、归因数据接收及深度链接处理。
    /// </summary>
    public sealed partial class AppsFlyerPlugin : SDKPluginBase, IAttributionPlugin
    {
      
        /// <summary>
        /// 异步初始化AppsFlyer SDK。
        /// config 为 SDKManager 按 RequiredConfigType 自动从 IConfigManager 注入的 AppsFlyerPluginConfig；
        /// 内部按其 DevKey / AppId / LogEnable 字段初始化 AppsFlyer SDK。
        /// Editor环境下注入虚拟归因数据以供本地调试。
        /// </summary>
        /// <param name="config">由 SDKManager 注入的 AppsFlyerPluginConfig 配置。</param>
        /// <param name="ct">取消令牌，串联到内部 FetchDataAsync 调用。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            return InitializeInternalAsync(config, ct);
        }

        /// <summary>
        /// AppsFlyer SDK 实际初始化逻辑（async 实现）。
        /// 抽取为独立私有方法以保持 OnInitializeAsync 与基类 abstract 签名一致（无 async 修饰）。
        /// 主流程按"配置解析 → SDK 启动（含初始化/注入 TGA/发布 AFID）→ 订阅事件"三个阶段串接，
        /// 每阶段封装为独立私有方法（详见 AppsFlyerPlugin.Methods.cs Init 步骤段）。
        /// </summary>
        /// <param name="config">由 SDKManager 注入的 AppsFlyerPluginConfig 配置。</param>
        /// <param name="ct">取消令牌，串联到内部 FetchDataAsync 调用。</param>
        /// <returns>初始化完成的异步任务。</returns>
        private async UniTask InitializeInternalAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            try
            {
                m_ReportNetService = new AppsFlyerReportNetService();

                if (!TryParseAndCacheConfig(config, out var sdkComponent))
                {
                    return;
                }
                SubscribeEvents();
                await InitializeAFSDKAsync(sdkComponent, ct);
            

                Log.Debug(LogTag.AppsFlyer, "初始化完成。");
            }
            catch (System.Exception e)
            {
                if (m_ConversionListener != null)
                {
                    UnityEngine.Object.Destroy(m_ConversionListener.gameObject);
                    m_ConversionListener = null;
                }
                Log.Error(LogTag.AppsFlyer, $"OnInitializeAsync 初始化异常：{e}");
            }
        }

        /// <summary>
        /// 异步释放AppsFlyer SDK资源。
        /// 先退订 SDKEventData.UserLogin，再返回完成任务；AF SDK 无显式 shutdown 接口。
        /// </summary>
        /// <param name="ct">取消令牌，本插件不使用。</param>
        /// <returns>释放完成的异步任务。</returns>
        protected override UniTask OnDisposeAsync(CancellationToken ct)
        {
            if (m_EventManager != null)
            {
                m_EventManager.Unsubscribe<SDKEventData.UserLogin>(OnUserLogin);
                m_EventManager = null;
            }
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 上报携带自定义参数的埋点事件。
        /// eventName为空则直接返回；parameters为null时以空字典发送事件。
        /// 自动将object值转换为string，跳过空key或null value的条目。
        /// </summary>
        /// <param name="eventName">事件名称，不可为空。</param>
        /// <param name="parameters">事件附加参数字典，可为null。</param>
        public void TrackEvent(string eventName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                return;
            }
            var converted = new Dictionary<string, string>();
            if (parameters != null)
            {
                foreach (var pair in parameters)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                    {
                        continue;
                    }
                    converted[pair.Key] = pair.Value.ToString();
                }
            }
            AppsFlyer.sendEvent(eventName, converted);
        }

        /// <summary>
        /// 上报IAttributionPlugin契约要求的结构化埋点事件。
        /// evt为null或evt.Name为空则直接返回；否则将Parameters转换为string字典后调用sendEvent。
        /// </summary>
        /// <param name="evt">事件载荷，Name不可为空；Parameters可为null。</param>
        public void TrackEvent(TrackEvent evt)
        {
            if (evt == null || string.IsNullOrEmpty(evt.Name))
            {
                return;
            }
            var converted = new Dictionary<string, string>();
            if (evt.Parameters != null)
            {
                foreach (var pair in evt.Parameters)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                    {
                        continue;
                    }
                    converted[pair.Key] = pair.Value.ToString();
                }
            }
            AppsFlyer.sendEvent(evt.Name, converted);
        }

        /// <summary>
        /// IAttributionPlugin契约：归因数据就绪事件，在BuildAndPublishAttribution完成后主线程触发。
        /// </summary>
        public event Action<AttributionData> OnAttributionResolved
        {
            add { m_OnAttributionResolved += value; }
            remove { m_OnAttributionResolved -= value; }
        }

        /// <summary>
        /// 异步获取归因数据。
        /// 若m_Attribution已非null则立即返回缓存值；否则等待平台回调填充后返回。
        /// 取消令牌触发时抛出OperationCanceledException。
        /// </summary>
        /// <param name="ct">取消令牌，默认不取消。</param>
        /// <returns>归因数据载荷。</returns>
        public async UniTask<AttributionData> GetAttributionAsync(System.Threading.CancellationToken ct = default)
        {
            if (m_Attribution != null)
            {
                return m_Attribution;
            }
            await UniTask.WaitUntil(() => m_Attribution != null, cancellationToken: ct);
            return m_Attribution;
        }

        /// <summary>
        /// 设置当前用户的唯一标识。
        /// </summary>
        /// <param name="userId">用户唯一标识。</param>
        public void SetUserId(string userId)
        {
            AppsFlyer.setCustomerUserId(userId);
        }

        /// <summary>
        /// 获取AppsFlyer设备唯一标识符。
        /// </summary>
        /// <returns>AppsFlyer分配的设备ID字符串。</returns>
        public string GetAppsFlyerID()
        {
            return AppsFlyer.getAppsFlyerId();
        }

        /// <summary>
        /// 获取缓存的归因数据字典。
        /// </summary>
        /// <returns>归因数据键值对字典，未初始化时返回null。</returns>
        public Dictionary<string, object> GetConversionData()
        {
            return m_ConversionData;
        }

        /// <summary>
        /// 获取缓存的深度链接数据字典。
        /// </summary>
        /// <returns>深度链接数据键值对字典，未触发深度链接时返回null。</returns>
        public Dictionary<string, object> GetDeepLinkData()
        {
            return m_DeepLinkData;
        }

        /// <summary>
        /// 控制是否允许AppsFlyer采集TCF数据。
        /// </summary>
        /// <param name="shouldCollect">true表示允许采集，false表示禁止采集。</param>
        public void EnableTCFDataCollection(bool shouldCollect)
        {
            AppsFlyer.enableTCFDataCollection(shouldCollect);
        }

    }
}
#endif
