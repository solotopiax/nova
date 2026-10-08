/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoWechatMiniGameMaterials.cs
 * author:    taoye
 * created:   2026/10/8
 * descrip:   微信 Demo 专属演示物料
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Sdk.Wechat.Minigame.Samples.Runtime
{
    /// <summary>仅供微信 Demo 读取的物料；不改变框架正式业务行为，也不替代真实微信后台配置。</summary>
    [Serializable]
    public sealed class DemoWechatMiniGameMaterials
    {
        [SerializeField, Min(1), Tooltip("仅演示：游戏币购买表 ID。")]
        private int m_CurrencyTableId = 2;
        [SerializeField, Min(1), Tooltip("仅演示：道具购买表 ID。")]
        private int m_ItemTableId = 1;
        [SerializeField, Tooltip("仅演示：从微信后台填写真实订阅模板 ID；空值表示未配置。")]
        private string m_TemplateId = string.Empty;
        [SerializeField, Tooltip("仅演示：业务服务端通知配置 ID；空值表示未配置。")]
        private string m_NoticeConfigId = string.Empty;
        [SerializeField, Tooltip("仅演示：通知任务业务幂等键；请填写用于当前演示任务的键。")]
        private string m_NoticeTaskKey = string.Empty;
        [SerializeField, Tooltip("仅演示：引用微信正式广告配置中的激励视频 Placement，不是微信广告位 ID。")]
        private string m_RewardedPlacement = "demo_rewarded";
        [SerializeField, Tooltip("仅演示：引用微信正式广告配置中的插屏 Placement，不是微信广告位 ID。")]
        private string m_InterstitialPlacement = "demo_interstitial";
        [SerializeField, Tooltip("仅演示：引用微信正式广告配置中的 Banner Placement，不是微信广告位 ID。")]
        private string m_BannerPlacement = "demo_banner";
        [SerializeField, Tooltip("仅演示：微信后台激励视频广告位 ID。空值表示未配置，不写入正式广告位列表。")]
        private string m_RewardedAdUnitId = string.Empty;
        [SerializeField, Tooltip("仅演示：微信后台插屏广告位 ID。空值表示未配置，不写入正式广告位列表。")]
        private string m_InterstitialAdUnitId = string.Empty;
        [SerializeField, Tooltip("仅演示：微信后台 Banner 广告位 ID。空值表示未配置，不写入正式广告位列表。")]
        private string m_BannerAdUnitId = string.Empty;
        /// <summary>仅演示的激励视频广告位 ID；默认未配置。</summary>
        public string RewardedAdUnitId => m_RewardedAdUnitId?.Trim() ?? string.Empty;
        /// <summary>仅演示的插屏广告位 ID；默认未配置。</summary>
        public string InterstitialAdUnitId => m_InterstitialAdUnitId?.Trim() ?? string.Empty;
        /// <summary>仅演示的 Banner 广告位 ID；默认未配置。</summary>
        public string BannerAdUnitId => m_BannerAdUnitId?.Trim() ?? string.Empty;
        /// <summary>游戏币演示购买表 ID。</summary>
        public int CurrencyTableId => m_CurrencyTableId;
        /// <summary>道具演示购买表 ID。</summary>
        public int ItemTableId => m_ItemTableId;
        /// <summary>真实订阅模板 ID；默认未配置。</summary>
        public string TemplateId => m_TemplateId?.Trim() ?? string.Empty;
        /// <summary>业务通知配置 ID；默认未配置。</summary>
        public string NoticeConfigId => m_NoticeConfigId?.Trim() ?? string.Empty;
        /// <summary>通知任务业务幂等键；默认未配置。</summary>
        public string NoticeTaskKey => m_NoticeTaskKey?.Trim() ?? string.Empty;
        /// <summary>激励视频演示 Placement。</summary>
        public string RewardedPlacement => m_RewardedPlacement?.Trim() ?? string.Empty;
        /// <summary>插屏演示 Placement。</summary>
        public string InterstitialPlacement => m_InterstitialPlacement?.Trim() ?? string.Empty;
        /// <summary>Banner 演示 Placement。</summary>
        public string BannerPlacement => m_BannerPlacement?.Trim() ?? string.Empty;
    }
}
