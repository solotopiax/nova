/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelConfigList.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   AdPluginConfig 渠道配置列表包装，供 AdChannelConfigListDrawer 注册 PropertyDrawer
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 已配置渠道的 IAdChannelConfig 列表包装。
    /// 以独立类型存在，使 AdChannelConfigListDrawer 可通过
    /// [CustomPropertyDrawer(typeof(AdChannelConfigList))] 精确匹配并绘制渠道配置列表。
    /// </summary>
    [Serializable]
    public sealed class AdChannelConfigList
    {
        /// <summary>
        /// 渠道配置实例列表；以 [SerializeReference] 多态存储不同渠道的配置。
        /// </summary>
        [SerializeReference]
        private List<IAdChannelConfig> m_Items = new List<IAdChannelConfig>();

        /// <summary>
        /// 是否启用多渠道比价（eCPM 竞价）；开启后 ShowAsync 选 Revenue 最高的就绪渠道展示，
        /// 关闭后按渠道列表顺序取第一个就绪渠道。
        /// </summary>
        [SerializeField]
        [Tooltip("是否启用多渠道比价（eCPM 竞价）；开启后 ShowAsync 选 Revenue 最高的就绪渠道展示，关闭后按渠道列表顺序取第一个就绪渠道。")]
        private bool m_EnableBidding = true;

        /// <summary>
        /// Banner ILRD（广告收入回调）上报间隔次数，默认 5 次上报一次。
        /// 值为 1 表示每次展示都上报；值为 0 或负数时视为不上报。
        /// </summary>
        [SerializeField]
        [Tooltip("Banner 广告收入回调的上报间隔，单位：次。默认 5 表示每 5 次展示上报一次；1 = 每次都上报；0 或负数 = 不上报。")]
        private int m_BannerIlrdInterval = 5;

        /// <summary>
        /// 是否全局静音所有广告；开启后广告视频/音频将被静音播放，默认 false。
        /// </summary>
        [SerializeField]
        [Tooltip("是否全局静音所有广告；开启后广告视频/音频将被静音播放，默认 false。")]
        private bool m_MuteAd = false;

        /// <summary>
        /// 广告加载失败时的最大重试次数；超出后停止本轮重试，等待下一次外部触发加载。
        /// </summary>
        [SerializeField]
        [Tooltip("广告加载失败时的最大重试次数；超出后停止本轮重试，等待下一次外部触发加载。")]
        private int m_RetryLoadAdMaxNum = 3;

        /// <summary>
        /// 广告加载重试 N 次仍失败后，再次发起加载之间的间隔时间（秒）。
        /// </summary>
        [SerializeField]
        [Tooltip("广告加载重试 N 次失败后，再次发起加载之间的间隔时间（秒）。")]
        private float m_RetryLoadAdInterv = 30f;

        /// <summary>
        /// 只读视图；AdPlugin 初始化时遍历此列表创建渠道实例。
        /// </summary>
        public IReadOnlyList<IAdChannelConfig> Items => m_Items;

        /// <summary>
        /// 是否启用多渠道比价（eCPM 竞价）。
        /// </summary>
        public bool EnableBidding => m_EnableBidding;

        /// <summary>
        /// Banner ILRD 上报间隔次数；0 或负数表示不上报。
        /// </summary>
        public int BannerIlrdInterval => m_BannerIlrdInterval;

        /// <summary>
        /// 是否全局静音所有广告。
        /// </summary>
        public bool MuteAd => m_MuteAd;

        /// <summary>
        /// 广告加载失败时的最大重试次数。
        /// </summary>
        public int RetryLoadAdMaxNum => m_RetryLoadAdMaxNum;

        /// <summary>
        /// 广告加载重试失败后再次加载的间隔时间（秒）。
        /// </summary>
        public float RetryLoadAdInterv => m_RetryLoadAdInterv;

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器专用可写访问器，供 AdChannelConfigListDrawer 增删渠道配置。
        /// </summary>
        public List<IAdChannelConfig> ItemsEditor => m_Items;
#endif
    }
}
