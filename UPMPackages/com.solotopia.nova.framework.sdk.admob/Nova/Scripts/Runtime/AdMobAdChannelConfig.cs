/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdMobAdChannelConfig.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   Google AdMob 渠道配置，存储 AppId、全屏广告组与 Banner 真实 ID
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace NovaFramework.SDK.AdMobAdPlugin.Runtime
{
    /// <summary>
    /// Google AdMob 渠道配置。
    /// 实现 IAdChannelConfig，存储 AdMob SDK 所需的 AppId、全屏广告组与 Banner 真实 ID。
    /// 在 AdPluginConfig 的渠道配置列表中以 [SerializeReference] 多态存储。
    /// </summary>
    [Serializable]
    public sealed class AdMobAdChannelConfig : IAdChannelConfig
    {
        /// <summary>
        /// 渠道类型标识。
        /// </summary>
        public AdChannelType Channel => AdChannelType.AdMob;

        /// <summary>
        /// 对应的渠道插件类型，AdPlugin 用于反射创建实例。
        /// </summary>
        public Type PluginType => typeof(AdMobAdPlugin);

        /// <summary>
        /// 是否启用此渠道。
        /// </summary>
        [SerializeField, Tooltip("是否开启此渠道。")]
        private bool m_Enabled = true;
        /// <summary>
        /// 是否启用此渠道的只读属性。
        /// </summary>
        public bool Enabled => m_Enabled;

        /// <summary>
        /// Android 平台 App ID。
        /// </summary>
        [SerializeField, Tooltip("Android 平台 App ID，格式 ca-app-pub-XXXXXXXXXXXXXXXX~XXXXXXXXXX。")]
        private string m_AppIdAndroid;
        /// <summary>
        /// Android 平台 App ID 只读属性。
        /// </summary>
        public string AppIdAndroid => m_AppIdAndroid;

        /// <summary>
        /// iOS 平台 App ID。
        /// </summary>
        [SerializeField, Tooltip("iOS 平台 App ID，格式 ca-app-pub-XXXXXXXXXXXXXXXX~XXXXXXXXXX。")]
        private string m_AppIdIOS;
        /// <summary>
        /// iOS 平台 App ID 只读属性。
        /// </summary>
        public string AppIdIOS => m_AppIdIOS;

        /// <summary>是否将用户标记为低于同意年龄；传入官方 UMP 请求，默认 false。</summary>
        [SerializeField, Tooltip("是否将当前用户标记为低于隐私同意年龄。应按项目实际年龄策略配置，默认关闭。")]
        private bool m_TagForUnderAgeOfConsent;
        /// <summary>隐私同意年龄标记，由 UMP 流程读取。</summary>
        public bool TagForUnderAgeOfConsent => m_TagForUnderAgeOfConsent;

        /// <summary>
        /// 激励视频广告组；每项一组，项内按英文逗号或竖线分隔的 ID 顺序尝试。
        /// </summary>
        [SerializeField, FormerlySerializedAs("m_RVPlacementIds"), Tooltip("激励视频广告组：每项是一组，项内用英文逗号 , 或竖线 | 分隔按顺序尝试的广告 ID；组间并行，空列表表示未配置。")]
        private List<string> m_RVPlacementGroups = new List<string>();

        /// <summary>
        /// 激励视频广告组原始表达式的只读视图；配置编辑可改变后续读取，运行时应物化独立计划。
        /// </summary>
        public IReadOnlyList<string> RVPlacementGroups => GetGroupExpressions(m_RVPlacementGroups);

        /// <summary>
        /// 兼容旧接口的真实广告 ID 快照；按组顺序解析展开，配置无效时返回空列表。
        /// </summary>
        public IReadOnlyList<string> RVPlacementIds => GetPlacementIds(AdFormat.Rewarded, m_RVPlacementGroups);

        /// <summary>
        /// 插屏广告组；每项一组，项内按英文逗号或竖线分隔的 ID 顺序尝试。
        /// </summary>
        [SerializeField, FormerlySerializedAs("m_InterPlacementIds"), Tooltip("插屏广告组：每项是一组，项内用英文逗号 , 或竖线 | 分隔按顺序尝试的广告 ID；组间并行，空列表表示未配置。")]
        private List<string> m_InterPlacementGroups = new List<string>();

        /// <summary>
        /// 插屏广告组原始表达式的只读视图；配置编辑可改变后续读取，运行时应物化独立计划。
        /// </summary>
        public IReadOnlyList<string> InterPlacementGroups => GetGroupExpressions(m_InterPlacementGroups);

        /// <summary>
        /// 兼容旧接口的真实广告 ID 快照；按组顺序解析展开，配置无效时返回空列表。
        /// </summary>
        public IReadOnlyList<string> InterPlacementIds => GetPlacementIds(AdFormat.Interstitial, m_InterPlacementGroups);

        /// <summary>
        /// Banner 广告位 ID 列表；空列表表示不启用 Banner。
        /// </summary>
        [SerializeField, Tooltip("Banner 广告位真实 ID 列表；每项只填写一个 ID，不支持逗号或竖线广告组表达式。")]
        private List<string> m_BannerPlacementIds = new List<string>();
        /// <summary>
        /// Banner 广告位 ID 列表只读属性。
        /// </summary>
        public IReadOnlyList<string> BannerPlacementIds => m_BannerPlacementIds;

        /// <summary>
        /// 开屏广告组；每项一组，项内按英文逗号或竖线分隔的 ID 顺序尝试。
        /// </summary>
        [SerializeField, FormerlySerializedAs("m_AppOpenPlacementIds"), Tooltip("开屏广告组：每项是一组，项内用英文逗号 , 或竖线 | 分隔按顺序尝试的广告 ID；组间并行，空列表表示未配置。")]
        private List<string> m_AppOpenPlacementGroups = new List<string>();

        /// <summary>
        /// 开屏广告组原始表达式的只读视图；配置编辑可改变后续读取，运行时应物化独立计划。
        /// </summary>
        public IReadOnlyList<string> AppOpenPlacementGroups => GetGroupExpressions(m_AppOpenPlacementGroups);

        /// <summary>
        /// 兼容旧接口的真实广告 ID 快照；按组顺序解析展开，配置无效时返回空列表。
        /// </summary>
        public IReadOnlyList<string> AppOpenPlacementIds => GetPlacementIds(AdFormat.AppOpen, m_AppOpenPlacementGroups);

        /// <summary>
        /// 返回原始表达式的不可写视图，不缓存解析结果或暴露可写 List。
        /// </summary>
        private static IReadOnlyList<string> GetGroupExpressions(List<string> expressions)
        {
            return expressions == null ? Array.AsReadOnly(Array.Empty<string>()) : expressions.AsReadOnly();
        }

        /// <summary>
        /// 通过共享解析器获取当前配置真实 ID 快照；任何错误均阻断旧接口消费。
        /// </summary>
        private static IReadOnlyList<string> GetPlacementIds(AdFormat format, IReadOnlyList<string> expressions)
        {
            AdGroupPlanValidationResult result = AdGroupConfigParser.Parse(format, expressions);
            return result.IsValid ? result.EffectivePlacementIds : Array.AsReadOnly(Array.Empty<string>());
        }
    }
}
