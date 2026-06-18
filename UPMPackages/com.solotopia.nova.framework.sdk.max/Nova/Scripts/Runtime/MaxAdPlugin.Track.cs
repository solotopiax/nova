/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Track.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin MAX 渠道特有打点扩展（加载成功 / 收益）
 ***************************************************************/

using System.Collections.Generic;
using System.Globalization;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {
        /// <summary>
        /// 构建加载成功时 MAX 特有的打点属性字典，由各格式 OnXxxLoaded 回调填入 AdLoadResult.CustomProps。
        /// 基类 RaiseAdLoaded 打 nova_ad_fill 事件时会自动将 CustomProps 合并进去。
        /// </summary>
        /// <param name="adInfo">MAX 广告信息，含网络名称、网络位置及收益数据。</param>
        /// <returns>包含 MAX 特有属性的字典。</returns>
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
        /// 收益回调时向所有变现打点插件分别上报 ad_ilrd 和 ad_impression 两条事件。
        /// ad_ilrd 包含收益精度、瀑布流名称等 MAX 特有维度；ad_impression 为标准展示归因字段。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="adInfo">MAX 广告信息，含收益与网络元数据。</param>
        private void TrackMaxRevenue(AdFormat format, string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            var sdkComponent = FrameworkComponentsGroup.GetComponent<SDKComponent>();
            if (sdkComponent == null)
            {
                Log.Warning(LogTag.Max, "sdkComponent 尚未就绪。");
                return;
            }

            // double 极小值（如 0.000012）序列化会产生科学计数法，af_revenue 须转 decimal 再格式化为固定小数点字符串
            string afRevenue = ((decimal)adInfo.Revenue).ToString("G29", CultureInfo.InvariantCulture);

            var ilrdProps = new Dictionary<string, object>
            {
                { "nova_ad_format", (int)format },
                { "nova_ad_channel", Name },
                { "nova_ad_id", adUnitId },
                { "adunit_id", adInfo.AdUnitIdentifier },
                { "publisher_revenue", adInfo.Revenue },
                { "af_revenue", afRevenue },
                { "network_name", adInfo.NetworkName },
                { "network_placement", adInfo.NetworkPlacement },
                { "creative_identifier", adInfo.CreativeIdentifier },
                { "country", m_CountryCode },
                { "network_placement_id", adInfo.Placement },
                { "value", adInfo.Revenue },
                { "currency", "USD" },
                { "waterfall_name", adInfo.WaterfallInfo?.Name },
            };


    

            var impressionProps = new Dictionary<string, object>
            {
                { "ad_source", adInfo.NetworkName },
                { "ad_unit_name", adInfo.AdUnitIdentifier },
                { "ad_format", (int)format },
                { "value", adInfo.Revenue },
                { "currency", "USD" },
            };

            DispatchToAllTrackers("ad_ilrd", ilrdProps);
            DispatchToAllTrackers("ad_impression", impressionProps);

            void DispatchToAllTrackers(string eventName, Dictionary<string, object> props)
            {
                if (sdkComponent.TryGet<IMonetizeTrackPlugin>(out var monetize))
                {
                    monetize.TrackEvent(eventName, props);
                }
                if (sdkComponent.TryGet<IAttributionPlugin>(out var attribution))
                {
                    attribution.TrackEvent(eventName, props);
                }
                if (sdkComponent.TryGet<ITrackPlugin>(out var track))
                {
                    track.TrackEvent(eventName, props);
                }
            }
        }
    }
}
