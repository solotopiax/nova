/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAdView.Visitors.cs
 * author:    nova-create-sample
 * created:   2026/06/03
 * descrip:   DemoAdView 演示 View — 字段与属性
 ***************************************************************/

using NovaFramework.SDK.AdPlugin.Runtime;
using TMPro;
using UnityEngine;

namespace NovaFramework.Sdk.Ad.Samples.Runtime
{
    /// <summary>
    /// DemoAdView 演示 View 的字段声明。
    /// </summary>
    public sealed partial class DemoAdView
    {
        /// <summary>
        /// 插屏广告回传数据输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_InterstitialCustomDataInput;

        /// <summary>
        /// 激励视频广告回传数据输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_RewardedCustomDataInput;

        /// <summary>
        /// 开屏广告回传数据输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_AppOpenCustomDataInput;

        /// <summary>
        /// Banner 回传数据输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_BannerCustomDataInput;

        /// <summary>
        /// Debug UI 当前选择的广告渠道。
        /// </summary>
        private AdChannelType m_SelectedAdChannel = AdChannelType.MAX;

        /// <summary>
        /// 当前 Demo 直接持有的广告聚合插件实例，用于访问 AdPlugin.Events。
        /// </summary>
        private AdPlugin m_AdPlugin;

        /// <summary>
        /// 是否已经订阅 AdPlugin.Events，防止 OnOpen 重入导致重复注册。
        /// </summary>
        private bool m_EventsSubscribed;
    }
}
