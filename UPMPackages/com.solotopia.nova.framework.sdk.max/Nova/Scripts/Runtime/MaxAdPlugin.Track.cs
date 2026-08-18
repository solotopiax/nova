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
        /// 在初始化主线程缓存收益即时打点所需的插件引用。
        /// 收益回调可能来自 SDK 原始线程，不允许在回调内访问 FrameworkComponentsGroup 或 SDKComponent。
        /// </summary>
        private void CacheMaxRevenueTrackers()
        {
            var sdkComponent = FrameworkComponentsGroup.GetComponent<SDKComponent>();
            if (sdkComponent == null)
            {
                Log.Warning(LogTag.Max, "MAX 收益打点插件缓存失败：SDKComponent 尚未就绪。");
                return;
            }

            sdkComponent.TryGet(out m_RevenueMonetizeTracker);
            sdkComponent.TryGet(out m_RevenueAttributionTracker);
            sdkComponent.TryGet(out m_RevenueEventTracker);

            if (m_RevenueMonetizeTracker == null)
            {
                Log.Warning(LogTag.Max, "MAX 收益打点插件缓存缺失：IMonetizeTrackPlugin 未找到。");
            }

            if (m_RevenueAttributionTracker == null)
            {
                Log.Warning(LogTag.Max, "MAX 收益打点插件缓存缺失：IAttributionPlugin 未找到。");
            }

            if (m_RevenueEventTracker == null)
            {
                Log.Warning(LogTag.Max, "MAX 收益打点插件缓存缺失：ITrackPlugin 未找到。");
            }
        }

        /// <summary>
        /// 非 Banner 收益路径：每次收益回调都上传 ad_ilrd 和 ad_impression。
        /// </summary>
        /// <param name="format">当前广告格式。</param>
        /// <param name="adUnitId">MAX 广告位标识。</param>
        /// <param name="adInfo">MAX 收益回调携带的广告信息。</param>
        private void TrackMaxRevenue(AdFormat format, string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            decimal revenue = (decimal)adInfo.Revenue;
            TrackMaxAdIlrd(format, adUnitId, adInfo, revenue);
            TrackMaxAdImpression(format, adUnitId, adInfo);
        }

        /// <summary>
        /// Banner 收益路径：ad_impression 每次即时上传；ad_ilrd 委托广告基类做全渠道统一累计和持久化。
        /// </summary>
        /// <param name="adUnitId">MAX Banner 广告位标识。</param>
        /// <param name="adInfo">MAX Banner 收益回调携带的广告信息。</param>
        private void TrackMaxBannerIlrdAggregated(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            TrackBannerIlrdAggregated(adUnitId, adInfo.Revenue, revenue
                => TrackMaxAdIlrd(AdFormat.Banner, adUnitId, adInfo, revenue));
        }

        /// <summary>
        /// 构建并上传 MAX 的 ad_ilrd 事件；Banner 聚合后会传入累计收益，非 Banner 会传入单次收益。
        /// </summary>
        /// <param name="format">当前广告格式。</param>
        /// <param name="adUnitId">MAX 广告位标识。</param>
        /// <param name="adInfo">MAX 收益回调携带的广告信息。</param>
        /// <param name="revenue">本次上报使用的收益；Banner 为累计收益，非 Banner 为单次收益。</param>
        /// <returns>至少存在一个收益打点插件并完成派发时返回 true；插件未缓存时返回 false。</returns>
        private bool TrackMaxAdIlrd(AdFormat format, string adUnitId, MaxSdkBase.AdInfo adInfo, decimal revenue)
        {
            if (!HasMaxRevenueTrackers())
            {
                Log.Warning(LogTag.Max, "MAX 收益打点插件尚未缓存，跳过 ad_ilrd 上传。");
                return false;
            }

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

            DispatchToAllTrackers("ad_ilrd", ilrdProps);
            return true;
        }

        /// <summary>
        /// 构建并上传 MAX 的 ad_impression 事件；Banner 也必须每次收益回调即时上传该事件。
        /// </summary>
        /// <param name="format">当前广告格式。</param>
        /// <param name="adUnitId">MAX 广告位标识。</param>
        /// <param name="adInfo">MAX 收益回调携带的广告信息。</param>
        /// <returns>至少存在一个收益打点插件并完成派发时返回 true；插件未缓存时返回 false。</returns>
        private bool TrackMaxAdImpression(AdFormat format, string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (!HasMaxRevenueTrackers())
            {
                Log.Warning(LogTag.Max, "MAX 收益打点插件尚未缓存，跳过 ad_impression 上传。");
                return false;
            }

            var impressionProps = new Dictionary<string, object>
            {
                { "ad_source", adInfo.NetworkName },
                { "ad_unit_name", adInfo.AdUnitIdentifier },
                { "ad_format", (int)format },
                { "value", adInfo.Revenue },
                { "currency", "USD" },
            };

            DispatchToAllTrackers("ad_impression", impressionProps);
            return true;
        }

        /// <summary>
        /// 判断收益即时打点所需的任一插件是否已经缓存。
        /// </summary>
        /// <returns>存在至少一个可用收益打点插件时返回 true。</returns>
        private bool HasMaxRevenueTrackers()
            => m_RevenueMonetizeTracker != null || m_RevenueAttributionTracker != null || m_RevenueEventTracker != null;

        /// <summary>
        /// 将收益格式化为不使用科学计数法的文本，供 af_revenue 和内部存档使用。
        /// </summary>
        /// <param name="revenue">需要格式化的收益值。</param>
        /// <returns>使用 invariant culture 的收益文本。</returns>
        private static string FormatRevenue(decimal revenue)
            => revenue.ToString("0.#############################", CultureInfo.InvariantCulture);

        /// <summary>
        /// 将收益事件同时派发给变现、归因和通用埋点插件。
        /// </summary>
        /// <param name="eventName">收益相关事件名。</param>
        /// <param name="props">事件属性字典。</param>
        private void DispatchToAllTrackers(string eventName, Dictionary<string, object> props)
        {
            DispatchToRevenueTracker(m_RevenueMonetizeTracker == null ? null : (Action<string, Dictionary<string, object>>)m_RevenueMonetizeTracker.TrackEvent, "变现", eventName, props);
            DispatchToRevenueTracker(m_RevenueAttributionTracker == null ? null : (Action<string, Dictionary<string, object>>)m_RevenueAttributionTracker.TrackEvent, "归因", eventName, props);
            DispatchToRevenueTracker(m_RevenueEventTracker == null ? null : (Action<string, Dictionary<string, object>>)m_RevenueEventTracker.TrackEvent, "通用", eventName, props);
        }

        /// <summary>
        /// 向指定收益打点插件派发事件；单插件异常只记录日志，不中断其他插件派发。
        /// </summary>
        /// <param name="trackEvent">目标打点插件的 TrackEvent 方法。</param>
        /// <param name="trackerName">用于日志区分的插件类型名称。</param>
        /// <param name="eventName">收益相关事件名。</param>
        /// <param name="props">事件属性字典。</param>
        private void DispatchToRevenueTracker(Action<string, Dictionary<string, object>> trackEvent, string trackerName, string eventName, Dictionary<string, object> props)
        {
            if (trackEvent == null) return;
            try { trackEvent(eventName, props); }
            catch (Exception ex) { Log.Warning(LogTag.Max, $"MAX {trackerName}收益打点失败：{eventName}，{ex.Message}"); }
        }
#endif
    }
}
