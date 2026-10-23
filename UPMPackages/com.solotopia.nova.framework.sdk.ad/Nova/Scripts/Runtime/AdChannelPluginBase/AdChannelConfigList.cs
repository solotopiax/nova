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
        /// 全部渠道与格式共用的广告组重试等待序列；各组独立推进，到末项后回到首项继续循环。
        /// </summary>
        [SerializeField]
        [Tooltip("广告组本轮全部 ID 失败后按列表顺序等待（秒）；各组独立推进，到末项后回到首项继续循环，本组任意 ID 加载成功后从首项重新计数。列表为空或包含非有限正数时整体回退 2、4、8、16、32 秒。")]
        private float[] m_GroupRetryIntervalsSeconds = CreateDefaultGroupRetryIntervalsSeconds();

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
        /// 返回有效的广告组重试等待序列；空列表或任意非有限正数都会使整组配置回退默认值。
        /// </summary>
        public IReadOnlyList<float> GroupRetryIntervalsSeconds
        {
            get
            {
                bool isValid = m_GroupRetryIntervalsSeconds != null && m_GroupRetryIntervalsSeconds.Length > 0;
                if (isValid)
                {
                    foreach (float interval in m_GroupRetryIntervalsSeconds)
                    {
                        if (float.IsNaN(interval) || float.IsInfinity(interval) || interval <= 0f)
                        {
                            isValid = false;
                            break;
                        }
                    }
                }

                if (isValid)
                {
                    return m_GroupRetryIntervalsSeconds;
                }

                Log.Warning(LogTag.AD, "广告组重试间隔列表必须包含有限正数，当前配置无效，整体回退 2、4、8、16、32 秒。");
                return CreateDefaultGroupRetryIntervalsSeconds();
            }
        }

        /// <summary>
        /// 创建独立的默认重试间隔数组，避免配置或渠道间共享可修改的默认数据。
        /// </summary>
        internal static float[] CreateDefaultGroupRetryIntervalsSeconds()
        {
            return new[] { 2f, 4f, 8f, 16f, 32f };
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器专用可写访问器，供 AdChannelConfigListDrawer 增删渠道配置。
        /// </summary>
        public List<IAdChannelConfig> ItemsEditor => m_Items;
#endif
    }
}
