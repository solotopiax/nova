/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Track.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin MAX 渠道加载与收益打点辅助逻辑
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {
#if NOVA_APPLOVIN_MAX
        /// <summary>
        /// 构建 MAX 加载成功特有属性；这些属性通过 AdLoadResult.CustomProps 合并进 nova_ad_fill。
        /// </summary>
        /// <param name="adInfo">MAX 广告信息，包含收益、广告网络与瀑布流位置等字段。</param>
        /// <returns>用于补充 nova_ad_fill 的 MAX 特有属性字典。</returns>
        private static Dictionary<string, object> BuildMaxLoadProps(MaxSdkBase.AdInfo adInfo)
        {
            return new Dictionary<string, object>
            {
                { "nova_ad_publisher_revenue", adInfo.Revenue },
                { "nova_ad_network_name", adInfo.NetworkName },
                { "nova_ad_network_placement", adInfo.NetworkPlacement },
                { "nova_ad_network_placement_id", adInfo.Placement },
            };
        }

        /// <summary>
        /// 获取变现插件；已有缓存直接返回，否则立即查询并保存，缺失告警由查询方法统一处理。
        /// </summary>
        /// <param name="eventName">当前准备派发的事件名。</param>
        /// <returns>缓存或刚取得的插件；尚不可用时返回 null。</returns>
        private IMonetizeTrackPlugin GetRevenueMonetizeTracker(string eventName)
        {
            if (m_RevenueMonetizeTracker == null)
                m_RevenueMonetizeTracker = FindMaxRevenueTracker<IMonetizeTrackPlugin>(eventName);
            return m_RevenueMonetizeTracker;
        }

        /// <summary>
        /// 获取归因插件；已有缓存直接返回，否则立即查询并保存，缺失告警由查询方法统一处理。
        /// </summary>
        /// <param name="eventName">当前准备派发的事件名。</param>
        /// <returns>缓存或刚取得的插件；尚不可用时返回 null。</returns>
        private IAttributionPlugin GetRevenueAttributionTracker(string eventName)
        {
            if (m_RevenueAttributionTracker == null)
                m_RevenueAttributionTracker = FindMaxRevenueTracker<IAttributionPlugin>(eventName);
            return m_RevenueAttributionTracker;
        }

        /// <summary>
        /// 获取通用埋点插件；已有缓存直接返回，否则立即查询并保存，缺失告警由查询方法统一处理。
        /// </summary>
        /// <param name="eventName">当前准备派发的事件名。</param>
        /// <returns>缓存或刚取得的插件；尚不可用时返回 null。</returns>
        private ITrackPlugin GetRevenueEventTracker(string eventName)
        {
            if (m_RevenueEventTracker == null)
                m_RevenueEventTracker = FindMaxRevenueTracker<ITrackPlugin>(eventName);
            return m_RevenueEventTracker;
        }

        /// <summary>
        /// 结束收益派发会话并清空缓存，后续事件不再复用已释放的插件引用。
        /// </summary>
        private void ClearMaxRevenueTrackers()
        {
            m_RevenueTrackingSession = null;
            m_RevenueMonetizeTracker = null;
            m_RevenueAttributionTracker = null;
            m_RevenueEventTracker = null;
        }

        /// <summary>
        /// 在当前回调线程从 SDK 管理器查询可用插件，未找到或查询异常时记录警告，不阻断其他目标。
        /// </summary>
        /// <typeparam name="TTracker">收益打点能力接口。</typeparam>
        /// <param name="eventName">实际派发的事件名，用于缺失告警。</param>
        /// <returns>当前可用插件；尚未就绪时返回 null。</returns>
        private TTracker FindMaxRevenueTracker<TTracker>(string eventName) where TTracker : class, ISDKPlugin
        {
            try
            {
                ISDKManager sdkManager = FrameworkManagersGroup.GetManager<ISDKManager>();
                if (sdkManager != null && sdkManager.TryGet(out TTracker tracker))
                {
                    return tracker;
                }

                string reason = sdkManager == null ? "SDK 管理器尚未就绪" : "插件未就绪或未启用";
                Log.Warning(LogTag.Max, $"MAX 收益打点跳过：event={eventName}，tracker={typeof(TTracker).Name}，{reason}。");
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Max, $"MAX 收益打点插件获取失败：event={eventName}，tracker={typeof(TTracker).Name}，{ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 非 Banner 收益路径：每次收益回调都上传 ad_ilrd 和 ad_impression。
        /// </summary>
        /// <param name="format">当前广告格式。</param>
        /// <param name="adUnitId">MAX 广告位标识。</param>
        /// <param name="adInfo">MAX 收益回调携带的广告信息。</param>
        /// <param name="correlationProps">当前全屏曝光的本地关联诊断属性。</param>
        private void TrackMaxRevenue(AdFormat format, string adUnitId, MaxSdkBase.AdInfo adInfo, Dictionary<string, object> correlationProps)
        {
            decimal revenue = (decimal)adInfo.Revenue;
            TrackMaxAdIlrd(format, adUnitId, adInfo, revenue, correlationProps);
            TrackMaxAdImpression(format, adUnitId, adInfo, correlationProps);
        }

        /// <summary>
        /// MAX 全部 Banner 广告位共用次数和收益累计；达到间隔后上传 ad_ilrd，载荷使用本次回调的广告信息。
        /// </summary>
        /// <param name="adUnitId">MAX Banner 广告位标识。</param>
        /// <param name="adInfo">MAX Banner 收益回调携带的广告信息。</param>
        private void TrackMaxBannerIlrdAggregated(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            TrackBannerIlrdAggregated(adInfo.Revenue, revenue => TrackMaxAdIlrd(AdFormat.Banner, adUnitId, adInfo, revenue));
        }

        /// <summary>
        /// 构建并上传 MAX 的 ad_ilrd 事件；Banner 聚合后会传入累计收益，非 Banner 会传入单次收益。
        /// </summary>
        /// <param name="format">当前广告格式。</param>
        /// <param name="adUnitId">MAX 广告位标识。</param>
        /// <param name="adInfo">MAX 收益回调携带的广告信息。</param>
        /// <param name="revenue">本次上报使用的收益；Banner 为累计收益，非 Banner 为单次收益。</param>
        /// <param name="correlationProps">当前全屏曝光的本地关联诊断属性；Banner 或无法关联时可为 null。</param>
        /// <returns>至少向一个插件尝试派发时返回 true；全部不可用时返回 false，不代表后台接收成功。</returns>
        private bool TrackMaxAdIlrd(AdFormat format, string adUnitId, MaxSdkBase.AdInfo adInfo, decimal revenue, Dictionary<string, object> correlationProps = null)
        {
            string revenueText = FormatRevenue(revenue);
            double revenueValue = (double)revenue;
            var ilrdProps = new Dictionary<string, object>
            {
                { "nova_ad_format", (int)format },
                { "nova_ad_channel", Name },
                { "nova_ad_id", adUnitId },
                { "adunit_id", adInfo.AdUnitIdentifier },
                { "publisher_revenue", revenueValue },
                { "af_revenue", revenueText },
                { "network_name", adInfo.NetworkName },
                { "network_placement", adInfo.NetworkPlacement },
                { "creative_identifier", adInfo.CreativeIdentifier },
                { "country", m_CountryCode },
                { "network_placement_id", adInfo.Placement },
                { "value", revenueValue },
                { "currency", "USD" },
                { "waterfall_name", adInfo.WaterfallInfo?.Name },
            };

            MergeCorrelationProps(ilrdProps, correlationProps);

            return DispatchToAllTrackers("ad_ilrd", ilrdProps);
        }

        /// <summary>
        /// 构建并上传 MAX 的 ad_impression 事件；Banner 也必须每次收益回调即时上传该事件。
        /// </summary>
        /// <param name="format">当前广告格式。</param>
        /// <param name="adUnitId">MAX 广告位标识。</param>
        /// <param name="adInfo">MAX 收益回调携带的广告信息。</param>
        /// <param name="correlationProps">当前全屏曝光的本地关联诊断属性；Banner 或无法关联时可为 null。</param>
        /// <returns>至少向一个插件尝试派发时返回 true；全部不可用时返回 false，不代表后台接收成功。</returns>
        private bool TrackMaxAdImpression(AdFormat format, string adUnitId, MaxSdkBase.AdInfo adInfo, Dictionary<string, object> correlationProps = null)
        {
            var impressionProps = new Dictionary<string, object>
            {
                { "ad_source", adInfo.NetworkName },
                { "ad_unit_name", adInfo.AdUnitIdentifier },
                { "ad_format", (int)format },
                { "nova_ad_format", (int)format },
                { "value", adInfo.Revenue },
                { "currency", "USD" },
            };

            MergeCorrelationProps(impressionProps, correlationProps);

            return DispatchToAllTrackers("ad_impression", impressionProps);
        }

        /// <summary>
        /// 将曝光关联诊断属性合并到收益事件；既有收益字段优先，避免附加属性改写协议字段。
        /// </summary>
        /// <param name="target">收益事件属性。</param>
        /// <param name="correlationProps">曝光关联诊断属性，可为 null。</param>
        private static void MergeCorrelationProps(Dictionary<string, object> target, Dictionary<string, object> correlationProps)
        {
            if (correlationProps == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> property in correlationProps)
            {
                if (!target.ContainsKey(property.Key))
                {
                    target[property.Key] = property.Value;
                }
            }
        }

        /// <summary>
        /// 将收益格式化为不使用科学计数法的文本，供 af_revenue 和内部存档使用。
        /// </summary>
        /// <param name="revenue">需要格式化的收益值。</param>
        /// <returns>使用 invariant culture 的收益文本。</returns>
        private static string FormatRevenue(decimal revenue)
            => revenue.ToString("0.#############################", CultureInfo.InvariantCulture);

        /// <summary>
        /// 在当前回调线程获取并派发三个目标；缺失目标告警后跳过，不排队重试或阻断其他目标。
        /// </summary>
        /// <param name="eventName">收益相关事件名。</param>
        /// <param name="props">事件属性字典。</param>
        /// <returns>至少向一个插件尝试派发时返回 true；全部不可用时返回 false。</returns>
        private bool DispatchToAllTrackers(string eventName, Dictionary<string, object> props)
        {
            object session = m_RevenueTrackingSession;
            if (session == null) return false;

            var monetize = GetRevenueMonetizeTracker(eventName);
            var attribution = GetRevenueAttributionTracker(eventName);
            var eventTracker = GetRevenueEventTracker(eventName);

            bool dispatched = DispatchToRevenueTracker(monetize == null ? null : (Action<string, Dictionary<string, object>>)monetize.TrackEvent, "IMonetizeTrackPlugin", eventName, props, session);
            dispatched |= DispatchToRevenueTracker(attribution == null ? null : (Action<string, Dictionary<string, object>>)attribution.TrackEvent, "IAttributionPlugin", eventName, props, session);
            dispatched |= DispatchToRevenueTracker(eventTracker == null ? null : (Action<string, Dictionary<string, object>>)eventTracker.TrackEvent, "ITrackPlugin", eventName, props, session);
            return dispatched;
        }

        /// <summary>
        /// 向指定收益打点插件派发事件；单插件异常只记录日志，不中断其他插件派发。
        /// </summary>
        /// <param name="trackEvent">目标打点插件的 TrackEvent 方法。</param>
        /// <param name="trackerName">用于日志区分的插件类型名称。</param>
        /// <param name="eventName">收益相关事件名。</param>
        /// <param name="props">事件属性字典。</param>
        /// <param name="session">本次事件所属会话；释放后不再发起新的插件调用。</param>
        /// <returns>已尝试调用插件时返回 true；沿用既有派发口径，插件抛异常也不证明事件未被接收。</returns>
        private bool DispatchToRevenueTracker(Action<string, Dictionary<string, object>> trackEvent, string trackerName, string eventName, Dictionary<string, object> props, object session)
        {
            if (trackEvent == null || !ReferenceEquals(session, m_RevenueTrackingSession)) return false;
            try { trackEvent(eventName, props); }
            catch (Exception ex) { Log.Warning(LogTag.Max, $"MAX {trackerName}收益打点失败：{eventName}，{ex.Message}"); }
            return true;
        }
#endif
    }
}
