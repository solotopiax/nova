/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppsFlyerPlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   AppsFlyerPlugin私有方法
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Threading;
using AppsFlyerSDK;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.AppsFlyerPlugin.Runtime
{
    public sealed partial class AppsFlyerPlugin
    {
        /// <summary>
        /// 初始化阶段 1：解析并缓存运行时配置。
        /// 把 SDKManager 注入的 ISDKPluginConfig 强转为 AppsFlyerPluginConfig 缓存到 m_RuntimeConfig，
        /// 同时拉取 SDKComponent 句柄；任一守卫不通过（component 缺失 / DevKey 空 / AppId 空）返回 false 让上层早返回。
        /// </summary>
        /// <param name="config">SDKManager 注入的配置实例。</param>
        /// <param name="sdkComponent">输出参数：SDKComponent 句柄，供后续阶段挂载 ConversionListener / 取 ITrackPlugin。</param>
        /// <returns>true 表示守卫全过、可继续后续阶段；false 表示已有 Warning 日志、应早返回。</returns>
        private bool TryParseAndCacheConfig(ISDKPluginConfig config, out SDKComponent sdkComponent)
        {
            sdkComponent = null;
            m_RuntimeConfig = config as AppsFlyerPluginConfig;

            sdkComponent = FrameworkComponentsGroup.GetComponent<SDKComponent>();
            if (sdkComponent == null)
            {
                Log.Warning(LogTag.AppsFlyer, "sdkComponent 尚未就绪。");
                return false;
            }

            if (string.IsNullOrEmpty(m_RuntimeConfig?.DevKey))
            {
                Log.Warning(LogTag.AppsFlyer, "AF DevKey 为空，SDK 初始化跳过。");
                return false;
            }

            if (string.IsNullOrEmpty(m_RuntimeConfig?.AppId))
            {
                Log.Warning(LogTag.AppsFlyer, "AF AppId 为空，SDK 初始化跳过。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 初始化阶段 2：完整执行 AppsFlyer SDK 启动流程（SDK 初始化 → 注入 TGA 标识 → 启动并发布 AppsFlyer ID）。
        /// 步骤顺序：
        ///   ① setIsDebug + 创建 ConversionListener + initSDK + EnableTCFDataCollection；
        ///   ② 通过 ITrackPlugin.FetchDataAsync 并行 await TGA 设备 ID / 访客 ID，写入 setAdditionalData；
        ///   ③ iOS 上等待 ATT 授权（≤60 秒）后 startSDK；
        ///   ④ Editor 模式注入 mock 归因数据并发布 mock AFID，真机模式从 GetAppsFlyerID() 取真实 ID 后 PublishData。
        /// 调用前所有依赖（DevKey / AppId / SDKComponent）已在阶段 1 校验通过。
        /// </summary>
        /// <param name="sdkComponent">阶段 1 拉到的 SDKComponent 句柄，用作 ConversionListener 父节点 + ITrackPlugin 来源。</param>
        /// <param name="ct">取消令牌，串联到 FetchDataAsync 调用。</param>
        /// <returns>SDK 启动完成的异步任务。</returns>
        private async UniTask InitializeAFSDKAsync(SDKComponent sdkComponent, CancellationToken ct)
        {
            AppsFlyer.setIsDebug(m_RuntimeConfig.LogEnable);
            m_ConversionListener = CreateConversionListener(sdkComponent.transform);
            AppsFlyer.initSDK(m_RuntimeConfig.DevKey, m_RuntimeConfig.AppId, m_ConversionListener);
            EnableTCFDataCollection(true);

            var trackPlugin = sdkComponent.Get<ITrackPlugin>();
            var (tgaDevicesIdObj, tgaDistinctIdObj) = await UniTask.WhenAll(
                trackPlugin.FetchDataAsync(SDKDataKeys.TGADevicesId, ct),
                trackPlugin.FetchDataAsync(SDKDataKeys.TGADistinctId, ct));
            var customData = new Dictionary<string, string>
            {
                { "ta_distinct_id", tgaDistinctIdObj as string ?? string.Empty },
                { "ta_devices_id", tgaDevicesIdObj as string ?? string.Empty },
                { "app_id", m_RuntimeConfig.AppId },
            };
            AppsFlyer.setAdditionalData(customData);

#if UNITY_IOS
            AppsFlyer.waitForATTUserAuthorizationWithTimeoutInterval(60);
#endif

            AppsFlyer.startSDK();

#if UNITY_EDITOR
            string editorConversionData = "{\"af_status\":\"Non-organic\",\"af_message\":\"is_first_launch\",\"is_first_launch\":\"true\",\"media_source\":\"editor_debug\",\"campaign\":\"debug_campaign\",\"adset\":\"debug_adset\",\"adset_id\":\"0\",\"campaign_id\":\"0\",\"click_time\":\"2026-04-20 00:00:00\",\"install_time\":\"2026-04-20 00:00:00\",\"af_siteid\":null,\"agency\":null,\"cost_cents_USD\":\"0\",\"http_referrer\":null,\"is_branded_link\":null,\"is_mobile_data_terms_signed\":null,\"iscache\":null,\"orig_cost\":\"0.0\",\"out_of_store\":\"false\",\"retargeting_conversion_type\":null}";
            HandleConversionDataSuccess(editorConversionData);
            PublishData(SDKDataKeys.AppsFlyerId, "editor_mock_afid");
            Log.Debug(LogTag.AppsFlyer, "Editor 模式：AppsFlyerID 使用 mock 值发布: editor_mock_afid");
#else
            PublishData(SDKDataKeys.AppsFlyerId, GetAppsFlyerID());
#endif
        }

        /// <summary>
        /// 初始化阶段 3：拉取 EventManager 并订阅 SDKEventData.UserLogin 事件。
        /// 订阅后用户登录时触发 OnUserLogin 回调，由其调用 SetUserId 与上报 NetService。
        /// </summary>
        private void SubscribeEvents()
        {
            m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();
            m_EventManager.Subscribe<SDKEventData.UserLogin>(OnUserLogin);
        }

        /// <summary>
        /// 创建 AppsFlyerConversionListener 宿主 GameObject 并挂载到指定父节点。
        /// 绑定当前 Plugin 作为回调目标，返回 listener 实例供 AppsFlyer.initSDK 使用。
        /// </summary>
        /// <param name="parent">父节点 Transform，通常为 SDKComponent.transform。</param>
        /// <returns>已绑定当前 Plugin 的 AppsFlyerConversionListener 实例。</returns>
        private AppsFlyerConversionListener CreateConversionListener(Transform parent)
        {
            var listenerGo = new GameObject("AppsFlyerConversionListener");
            listenerGo.transform.SetParent(parent, false);
            var listener = listenerGo.AddComponent<AppsFlyerConversionListener>();
            listener.Bind(this);
            return listener;
        }

        /// <summary>
        /// 解析深度链接数据字符串并更新深度链接数据缓存。
        /// </summary>
        /// <param name="datasString">深度链接或归因数据的JSON字符串。</param>
        private void ParseDeepLinkData(string datasString)
        {
            m_DeepLinkData = AppsFlyer.CallbackStringToDictionary(datasString);
        }

        /// <summary>
        /// 从原始归因字典构建AttributionData并发布至IAttributionPlugin契约。
        /// 重复调用时更新m_Attribution并再次触发m_OnAttributionResolved；raw为null时直接返回。
        /// </summary>
        /// <param name="raw">AF回调原始键值对字典；由onConversionDataSuccess或onAppOpenAttribution传入。</param>
        private void BuildAndPublishAttribution(Dictionary<string, object> raw)
        {
            if (raw == null)
            {
                return;
            }
            bool isOrganic = raw.TryGetValue("af_status", out var statusVal) && string.Equals(statusVal?.ToString(), "Organic", System.StringComparison.OrdinalIgnoreCase);
            var data = new AttributionData
            {
                MediaSource = raw.TryGetValue("media_source", out var v0) ? v0?.ToString() : null,
                Campaign = raw.TryGetValue("campaign", out var v1) ? v1?.ToString() : null,
                CampaignId = raw.TryGetValue("campaign_id", out var v2) ? v2?.ToString() : null,
                AdSet = raw.TryGetValue("adset", out var v3) ? v3?.ToString() : null,
                AdSetId = raw.TryGetValue("adset_id", out var v4) ? v4?.ToString() : null,
                AdCreative = raw.TryGetValue("af_ad", out var v5) ? v5?.ToString() : null,
                Channel = raw.TryGetValue("af_channel", out var v6) ? v6?.ToString() : null,
                IsOrganic = isOrganic,
                RawData = ConvertRawToStringDict(raw)
            };
            m_Attribution = data;
            m_OnAttributionResolved?.Invoke(m_Attribution);
        }

        /// <summary>
        /// 将 Dictionary<string, object> 转换为 Dictionary<string, string>，null值映射为空字符串。
        /// </summary>
        /// <param name="raw">原始键值对字典。</param>
        /// <returns>转换后的全字符串键值对字典。</returns>
        private static Dictionary<string, string> ConvertRawToStringDict(Dictionary<string, object> raw)
        {
            var result = new Dictionary<string, string>(raw.Count);
            foreach (var pair in raw)
            {
                result[pair.Key] = pair.Value?.ToString();
            }
            return result;
        }

        /// <summary>
        /// 供AppsFlyerConversionListener路由的归因数据成功回调处理。
        /// 更新归因数据缓存，首次启动时同步解析深度链接数据，随后发布归因结果。
        /// </summary>
        /// <param name="conversionData">归因数据JSON字符串。</param>
        internal void HandleConversionDataSuccess(string conversionData)
        {
            if (string.IsNullOrEmpty(conversionData))
            {
                return;
            }
            m_ConversionData = AppsFlyer.CallbackStringToDictionary(conversionData);
            if (m_ConversionData.TryGetValue("is_first_launch", out var firstLaunchValue)
                && string.Equals(firstLaunchValue?.ToString(), "true", System.StringComparison.OrdinalIgnoreCase))
            {
                ParseDeepLinkData(conversionData);
            }
            BuildAndPublishAttribution(m_ConversionData);
            Log.Debug(LogTag.AppsFlyer, $"onConversionDataSuccess => {conversionData}");
        }

        /// <summary>
        /// SDKEventData.UserLogin 事件处理器；调用 AppsFlyer 的 SetUserId 同步用户身份，
        /// 然后以 Fire-and-Forget 方式触发 ReportOnLoginAsync 走异步上报流程。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件数据，期望为 SDKEventData.UserLogin。</param>
        private void OnUserLogin(object sender, EventData e)
        {
            if (!(e is SDKEventData.UserLogin login))
            {
                return;
            }
            SetUserId(login.UserId);
            ReportOnLoginAsync().Forget();
        }

        /// <summary>
        /// 登录后异步上报 AppsFlyerId 至服务端：先 await FetchDataAsync 等待 AppsFlyerId 数据槽位就绪，
        /// 直接拿到 fetch 返回值作为协议参数（不再二次调用 GetAppsFlyerID），与初始化期"await FetchDataAsync 拿到值后写入 setAdditionalData"模式一致；
        /// 把"初始化结果"与"登录结果"统一为"先 await 拿值、再用值"的可等待过程，便于其他模块复用同一形态。
        /// AppsFlyerId 由本插件在 InitializeAFSDKAsync 末尾通过 PublishData 发布，故 fetch 走 this（IAttributionPlugin 自身）；
        /// m_ReportNetService 或 m_RuntimeConfig 为 null（守卫早返回路径）时静默跳过；
        /// CancellationToken 暂用 default（无 Plugin 生命周期 CTS）；OperationCanceledException 静默吞，其他异常仅记日志不上抛。
        /// </summary>
        /// <returns>UniTaskVoid，专用于 Fire-and-Forget 调用。</returns>
        private async UniTaskVoid ReportOnLoginAsync()
        {
            if (m_ReportNetService == null || m_RuntimeConfig == null)
            {
                return;
            }
            try
            {
                object afIdObj = await FetchDataAsync(SDKDataKeys.AppsFlyerId, default);
                string afId = afIdObj as string ?? string.Empty;
                m_ReportNetService.Async(m_RuntimeConfig.ReportCmdName, afId).Forget();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(LogTag.AppsFlyer, $"ReportOnLoginAsync 上报异常：{ex}");
            }
        }

        /// <summary>
        /// 供AppsFlyerConversionListener路由的归因数据失败回调处理。
        /// </summary>
        /// <param name="error">错误描述信息。</param>
        internal void HandleConversionDataFail(string error)
        {
            Log.Warning(LogTag.AppsFlyer, $"onConversionDataFail => {error}");
        }

        /// <summary>
        /// 供AppsFlyerConversionListener路由的深度链接打开回调处理。
        /// 更新深度链接数据缓存并发布归因结果。
        /// </summary>
        /// <param name="attributionData">深度链接归因数据JSON字符串。</param>
        internal void HandleAppOpenAttribution(string attributionData)
        {
            ParseDeepLinkData(attributionData);
            BuildAndPublishAttribution(AppsFlyer.CallbackStringToDictionary(attributionData));
            Log.Debug(LogTag.AppsFlyer, $"onAppOpenAttribution => {attributionData}");
        }

        /// <summary>
        /// 供AppsFlyerConversionListener路由的深度链接打开失败回调处理。
        /// </summary>
        /// <param name="error">错误描述信息。</param>
        internal void HandleAppOpenAttributionFailure(string error)
        {
            Log.Warning(LogTag.AppsFlyer, $"onAppOpenAttributionFailure => {error}");
        }
    }
}
#endif
