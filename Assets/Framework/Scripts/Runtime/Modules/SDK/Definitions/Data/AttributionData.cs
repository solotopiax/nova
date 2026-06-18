/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AttributionData.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   归因数据载荷数据类
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 归因数据载荷，由 IAttributionPlugin.GetAttributionAsync 返回或 OnAttributionResolved 事件携带。
    /// 字段来自 AppsFlyer / Adjust 等归因 SDK 的安装归因回调，仅填充平台实际返回的字段，未返回的字段留 null。
    /// </summary>
    public sealed class AttributionData
    {
        /// <summary>
        /// 归因媒体来源（如 "googleadwords_int"、"Facebook Ads"）。
        /// </summary>
        public string MediaSource;

        /// <summary>
        /// 归因广告系列名称。
        /// </summary>
        public string Campaign;

        /// <summary>
        /// 归因广告系列 ID。
        /// </summary>
        public string CampaignId;

        /// <summary>
        /// 归因广告组名称。
        /// </summary>
        public string AdSet;

        /// <summary>
        /// 归因广告组 ID。
        /// </summary>
        public string AdSetId;

        /// <summary>
        /// 归因广告创意名称。
        /// </summary>
        public string AdCreative;

        /// <summary>
        /// 归因渠道（如 "UAC_APP_INSTALL"）。
        /// </summary>
        public string Channel;

        /// <summary>
        /// 是否为自然流量安装（无付费归因）。
        /// </summary>
        public bool IsOrganic;

        /// <summary>
        /// 原始归因数据键值对，保留平台返回的全量字段供业务层按需读取。
        /// </summary>
        public Dictionary<string, string> RawData;
    }
}
