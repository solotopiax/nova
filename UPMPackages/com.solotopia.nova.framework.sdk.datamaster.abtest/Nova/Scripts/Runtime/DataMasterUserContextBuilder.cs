/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataMasterUserContextBuilder.cs
 * author:    taoye
 * created:   2026/7/15
 * descrip:   DataMaster 实时用户上下文纯构造器。
 ***************************************************************/

#if NOVA_STARLUS_DATAMASTER
using System;
using System.Collections.Generic;
using StarlusSDK.DataMaster;

namespace NovaFramework.SDK.StarlusDataMaster.ABTest.Runtime
{
    /// <summary>
    /// 单次事件上报所需的框架用户上下文快照。
    /// </summary>
    internal struct DataMasterUserContextSnapshot
    {
        public string PlayerId;
        public string DeviceId;
        public string MediaSource;
        public string CampaignName;
        public string InstallChannel;
        public long? InstallTimeMs;
        public string CountryCode;
        public string LanguageCode;
        public int? AppVersion;
        public string OsVersion;
    }

    /// <summary>
    /// 把实时框架快照映射为厂商事件用户上下文，并维护刷新请求的框架必传属性。
    /// </summary>
    internal static class DataMasterUserContextBuilder
    {
        /// <summary>
        /// 为单次事件创建全新的厂商用户上下文。
        /// </summary>
        internal static DMUserContext Build(
            DataMasterUserContextSnapshot snapshot,
            Dictionary<string, object> extraContext)
        {
            return new DMUserContext
            {
                PlayerId = NullIfEmpty(snapshot.PlayerId),
                DeviceId = NullIfEmpty(snapshot.DeviceId),
                MediaSource = NullIfEmpty(snapshot.MediaSource),
                CampaignName = NullIfEmpty(snapshot.CampaignName),
                InstallChannel = NullIfEmpty(snapshot.InstallChannel),
                InstallTimeMs = PositiveOrNull(snapshot.InstallTimeMs),
                CountryCode = NullIfEmpty(snapshot.CountryCode),
                LanguageCode = NullIfEmpty(snapshot.LanguageCode),
                AppVersion = PositiveOrNull(snapshot.AppVersion),
                OsVersion = NullIfEmpty(snapshot.OsVersion),
                ExtraContext = extraContext,
            };
        }

        /// <summary>
        /// 在刷新请求发出前强制更新框架管理的分流属性，其他项目属性保持不变。
        /// </summary>
        internal static void UpdateRefreshProperties(
            Dictionary<string, object> properties,
            int appVersion,
            long installTimeMs)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            properties["app_version"] = appVersion;
            properties["install_time"] = installTimeMs;
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static long? PositiveOrNull(long? value)
        {
            return value.HasValue && value.Value > 0 ? value : null;
        }

        private static int? PositiveOrNull(int? value)
        {
            return value.HasValue && value.Value > 0 ? value : null;
        }
    }
}
#endif
