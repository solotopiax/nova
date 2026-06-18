/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdPlugin.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   广告聚合调度层公开接口，sealed partial，实现 IAdPlugin
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告聚合调度层（sealed partial）。
    /// 初始化时根据 AdPluginConfig.ChannelConfigs 反射创建并启用渠道；
    /// RequestAsync 时并行广播给所有支持该格式的渠道；
    /// ShowAsync 时选当前 Revenue 最高的就绪渠道执行；
    /// 渠道事件统一桥接到 Events（AdPluginEvents）容器，支持 Sticky/Replay 补发模式。
    /// 业务层通过 Nova.SDK.Get<IAdPlugin>() 取得此实例，不感知具体渠道。
    /// </summary>
    public sealed partial class AdPlugin : SDKPluginBase, IAdPlugin
    {
        /// <summary>
        /// 声明本插件所需配置类型，SDKManager 自动从 IConfigManager 拉取并注入。
        /// </summary>
        protected override Type ConfigType => typeof(AdPluginConfig);

        /// <summary>
        /// 初始化实现：按配置创建渠道实例、桥接事件、注册到列表。
        /// 跳过 Enabled=false 的渠道；实例化失败时记录警告并继续。
        /// </summary>
        /// <param name="config">由 SDKManager 注入的 AdPluginConfig。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            var adConfig = config as AdPluginConfig;
            m_ChannelPlugins = new List<IAdInternalPlugin>();
            if (adConfig == null) return UniTask.CompletedTask;
            var configs = adConfig.ChannelConfigs.Items;
            for (int i = 0; i < configs.Count; i++)
            {
                var channel = CreateChannel(configs[i], adConfig, ct);
                if (channel == null) continue;
                WireChannelEvents(channel);
                RegisterChannel(channel);
            }
            m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();
            m_EventManager.Subscribe<SDKEventData.UserLogin>(OnUserLogin);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 释放实现：调用所有 ObservableEvent 缓冲的 Clear() 释放内存。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        protected override UniTask OnDisposeAsync(CancellationToken ct)
        {
            if (m_EventManager != null)
            {
                m_EventManager.Unsubscribe<SDKEventData.UserLogin>(OnUserLogin);
                m_EventManager = null;
            }
            Events.InitResult.Clear();
            Events.AdLoaded.Clear();
            Events.AdLoadFailed.Clear();
            Events.ShowCompleted.Clear();
            Events.ShowFailed.Clear();
            Events.RevenuePaid.Clear();
            Events.AdClosed.Clear();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 查询指定广告格式是否有任意渠道正在播放中。
        /// 当前渠道基类无通用播放状态接口，业务层协调防重入；默认返回 false。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>至少一个渠道正在播放返回 true，否则 false。</returns>
        public bool IsAdPlaying(AdFormat format) => false;

        /// <summary>
        /// 向所有支持指定格式的渠道并行发起预加载请求。
        /// 返回统一的 AdLoadResult；Success=true 表示加载成功，Success=false 时携带错误码和错误描述。
        /// Banner 格式会在首个成功结果返回后更新 m_ActiveBannerChannel。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">自定义属性字典，透传到渠道层打点；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>首个完成的加载结果；全部失败时返回最后一次失败结果。</returns>
        public async UniTask<AdLoadResult> RequestAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default)
        {
            var result = await BroadcastRequestAsync(format, customProps, ct);
            if (format == AdFormat.Banner && result != null && result.Success)
                m_ActiveBannerChannel = SelectBestChannel(format);
            return result;
        }

        /// <summary>
        /// 查询是否有任意渠道的指定格式广告已就绪。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>至少一个渠道就绪返回 true，否则 false。</returns>
        public bool IsReady(AdFormat format)
        {
            for (int i = 0; i < m_ChannelPlugins.Count; i++)
                if (m_ChannelPlugins[i].IsReady(format)) return true;
            return false;
        }

        /// <summary>
        /// 展示指定格式广告，选 Revenue 最高的就绪渠道执行。
        /// AdFormat.Banner 不适用此方法，Banner 展示请使用 ShowBanner()。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">自定义属性字典，透传到渠道层 show/show_result/hidden 打点；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        public async UniTask ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default)
        {
            var best = SelectBestChannel(format);
            if (best == null)
            {
                Log.Warning(LogTag.AD, $"无就绪的 {format} 广告渠道，ShowAsync 已跳过。");
                return;
            }
            await best.ShowAsync(format, customProps, ct);
        }

        /// <summary>
        /// 显示 Banner 广告，委托给活跃 Banner 渠道。
        /// </summary>
        public void ShowBanner() => m_ActiveBannerChannel?.ShowBanner();

        /// <summary>
        /// 隐藏 Banner 广告，委托给活跃 Banner 渠道。
        /// </summary>
        public void HideBanner() => m_ActiveBannerChannel?.HideBanner();

        /// <summary>
        /// 销毁 Banner 广告，委托给活跃 Banner 渠道并清空记录。
        /// </summary>
        public void DestroyBanner()
        {
            m_ActiveBannerChannel?.DestroyBanner();
            m_ActiveBannerChannel = null;
        }

        /// <summary>
        /// 通过枚举更新 Banner 停靠位置，委托给活跃 Banner 渠道。
        /// </summary>
        /// <param name="position">Banner 停靠位置。</param>
        public void UpdateBannerPosition(BannerPosition position) => m_ActiveBannerChannel?.UpdateBannerPosition(position);

        /// <summary>
        /// 通过像素坐标更新 Banner 位置，委托给活跃 Banner 渠道。
        /// </summary>
        /// <param name="position">Banner 屏幕坐标（逻辑像素）。</param>
        public void UpdateBannerPosition(Vector2 position) => m_ActiveBannerChannel?.UpdateBannerPosition(position);

        /// <summary>
        /// 开启 Banner 自动刷新，委托给活跃 Banner 渠道。
        /// </summary>
        public void StartBannerAutoRefresh() => m_ActiveBannerChannel?.StartBannerAutoRefresh();

        /// <summary>
        /// 停止 Banner 自动刷新，委托给活跃 Banner 渠道。
        /// </summary>
        public void StopBannerAutoRefresh() => m_ActiveBannerChannel?.StopBannerAutoRefresh();

        /// <summary>
        /// 设置 Banner 宽度，委托给活跃 Banner 渠道。
        /// </summary>
        /// <param name="width">Banner 宽度（逻辑像素）。</param>
        public void SetBannerWidth(float width) => m_ActiveBannerChannel?.SetBannerWidth(width);

        /// <summary>
        /// 获取 Banner 自适应高度，委托给活跃 Banner 渠道；无活跃渠道时返回 -1。
        /// </summary>
        /// <param name="width">指定宽度，-1 表示屏幕宽度。</param>
        /// <returns>自适应高度（逻辑像素）；无活跃渠道时返回 -1。</returns>
        public float GetAdaptiveBannerHeight(float width = -1f)
            => m_ActiveBannerChannel?.GetAdaptiveBannerHeight(width) ?? -1f;

        /// <summary>
        /// 设置 Banner 背景色，委托给活跃 Banner 渠道。
        /// </summary>
        /// <param name="color">背景颜色。</param>
        public void SetBannerBackgroundColor(Color color) => m_ActiveBannerChannel?.SetBannerBackgroundColor(color);
    }
}
