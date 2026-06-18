/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   AdChannelPluginBase 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    public abstract partial class AdChannelPluginBase
    {
        /// <summary>
        /// 缓存的变现打点插件列表；InitializeAsync 完成后由 CacheTrackPlugins 填充。
        /// </summary>
        private IReadOnlyList<ITrackPlugin> m_TrackPlugins;

        /// <summary>
        /// 各广告格式对应的 AdUnit 槽位列表；派生类在 InitChannelSDKAsync 内通过 RegisterAdUnits 注入。
        /// 键为 AdFormat，值为该格式下所有槽位。
        /// </summary>
        private Dictionary<AdFormat, List<AdUnit>> m_AdUnits = new();

        /// <summary>
        /// 当前活跃的请求批次列表；每次 RequestAsync 调用追加一个批次，首个成功结果触发 TrySetResult 后移除。
        /// 多个并发 RequestAsync 互不干扰，各批次独立等待。
        /// </summary>
        private List<RequestBatch> m_PendingBatches = new();

        /// <summary>
        /// 是否启用多渠道比价（eCPM 竞价）；AdPlugin 初始化时通过 ApplyGlobalConfig 写入。
        /// </summary>
        internal bool EnableBidding { get; private set; } = true;

        /// <summary>
        /// Banner ILRD 上报间隔次数；0 或负数表示不上报。AdPlugin 初始化时通过 ApplyGlobalConfig 写入。
        /// </summary>
        internal int BannerIlrdInterval { get; private set; } = 5;

        /// <summary>
        /// 是否全局静音所有广告；AdPlugin 初始化时通过 ApplyGlobalConfig 写入。
        /// </summary>
        protected internal bool MuteAd { get; private set; }

        /// <summary>
        /// 广告加载失败时的最大重试次数；AdPlugin 初始化时通过 ApplyGlobalConfig 写入。
        /// </summary>
        internal int RetryLoadAdMaxNum { get; private set; } = 3;

        /// <summary>
        /// 广告加载重试 N 次失败后再次发起加载之间的间隔时间（秒）；AdPlugin 初始化时通过 ApplyGlobalConfig 写入。
        /// </summary>
        internal float RetryLoadAdInterv { get; private set; } = 30f;
    }
}
