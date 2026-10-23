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
    [SDKPluginConfigType(typeof(AdPluginConfig))]
    public sealed partial class AdPlugin : SDKPluginBase, IAdPlugin, ISDKLoginReceiver, ISDKInitializationDependencies
    {
        /// <summary>
        /// 声明本插件所需配置类型，SDKManager 自动从 IConfigManager 拉取并注入。
        /// </summary>
        protected override Type ConfigType => typeof(AdPluginConfig);

        /// <summary>
        /// 按配置创建并持有渠道，先桥接事件与登记业务列表，再启动可选生命周期初始化。
        /// 聚合就绪不等待全部原厂初始化；单渠道失败保留所有权，释放时统一收回。
        /// </summary>
        /// <param name="config">由 SDKManager 注入的 AdPluginConfig。</param>
        /// <param name="ct">调用方初始化令牌。</param>
        /// <returns>渠道创建与启动观察完成的任务，不代表全部原厂初始化成功。</returns>
        protected override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (m_ChannelsDisposed) throw new ObjectDisposedException(Name);
            m_RuntimeConfig = config as AdPluginConfig;
            var adConfig = m_RuntimeConfig;
            if (adConfig == null) throw new InvalidOperationException("AdPlugin 配置缺失。");
            m_ChannelPlugins = new List<IAdInternalPlugin>();
            var configs = adConfig.ChannelConfigs.Items;
            for (int i = 0; i < configs.Count; i++)
            {
                if (m_ChannelsDisposed || ct.IsCancellationRequested) break;
                var channelConfig = configs[i];
                var channel = CreateChannel(channelConfig);
                if (channel == null) continue;
                var registration = new ChannelRegistration();
                // 先保存所有权和完成信号，再进入可能同步回调或抛错的准备流程。
                m_ChannelRegistrations.Add(channel, registration);
                PrepareAndInitializeChannelAsync(channel, channelConfig, adConfig, registration, ct).Forget();
            }
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 共享聚合清理任务；先发布完成源，再关闭入口并等待渠道准备、初始化退出。
        /// 清理不受调用方取消中断，重复调用等待同一实际释放终点。
        /// </summary>
        /// <param name="ct">调用方释放令牌；不用于中断本聚合的必需清理。</param>
        /// <returns>当前聚合的共享释放任务。</returns>
        protected override UniTask OnDisposeAsync(CancellationToken ct)
        {
            if (m_ChannelDisposalCompletion != null) return m_ChannelDisposalCompletion.Task;
            var completion = new UniTaskCompletionSource();
            m_ChannelDisposalCompletion = completion;
            DisposeChannelsAsync(completion).Forget();
            return completion.Task;
        }

        /// <summary>
        /// 查询指定广告格式是否有任意渠道正在播放中。
        /// 原生展示发出后保持占用，业务等待取消不改变播放状态。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>至少一个渠道正在播放返回 true，否则 false。</returns>
        public bool IsAdPlaying(AdFormat format)
        {
            for (int i = 0; i < m_ChannelPlugins.Count; i++)
                if (m_ChannelPlugins[i] is AdChannelPluginBase channel && channel.IsAdPlaying(format)) return true;
            return false;
        }

        /// <summary>
        /// 查询是否有广告渠道已取得用户明确的广告隐私授权决定。
        /// 多渠道场景按配置顺序查询，任一渠道已取得决定即返回 true。
        /// </summary>
        /// <returns>存在明确授权决定时返回 true；尚未设置或渠道均不支持查询时返回 false。</returns>
        public bool IsUserConsentSet()
        {
            return TryGetUserConsent(out _);
        }

        /// <summary>
        /// 查询用户是否同意广告隐私授权。
        /// 多渠道场景采用配置顺序中第一个已取得明确决定的渠道结果。
        /// </summary>
        /// <returns>用户已明确同意时返回 true；拒绝、尚未设置或渠道均不支持查询时返回 false。</returns>
        public bool HasUserConsent()
        {
            return TryGetUserConsent(out bool consented) && consented;
        }

        /// <summary>
        /// 等待所有已启用广告渠道在初始化期间触发的隐私授权流程结束。
        /// 不支持隐私流程的渠道会立即完成；调用前应先等待 Nova.SDK.InitializeTask，确保渠道列表已经创建。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>全部广告渠道隐私授权流程完成任务。</returns>
        public async UniTask WaitForPrivacyFlowAsync(CancellationToken ct = default)
        {
            if (m_ChannelPlugins == null || m_ChannelPlugins.Count == 0)
            {
                return;
            }

            var tasks = new List<UniTask>(m_ChannelPlugins.Count);
            for (int i = 0; i < m_ChannelPlugins.Count; i++)
            {
                tasks.Add(m_ChannelPlugins[i].WaitForPrivacyFlowAsync(ct));
            }

            await UniTask.WhenAll(tasks);
        }

        /// <summary>
        /// 异步获取广告 SDK 返回的有效国家或地区代码。
        /// 优先等待广告国家码数据槽位；等待超时后返回空字符串。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>大写国家或地区代码；不可用时返回空字符串。</returns>
        public async UniTask<string> GetCountryCodeAsync(CancellationToken ct = default)
        {
            TimeSpan waitTimeout = GetCountryCodeWaitTimeout();
            if (waitTimeout <= TimeSpan.Zero)
            {
                return string.Empty;
            }

            using (CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeoutCts.CancelAfter(waitTimeout);
                try
                {
                    object countryObj = await FetchDataAsync(SDKDataKeys.AdCountryCode, timeoutCts.Token);
                    string countryCode = countryObj as string;
                    if (TryNormalizeCountryCode(countryCode, out string normalizedCountryCode))
                    {
                        return normalizedCountryCode;
                    }

                    Log.Debug(LogTag.AD, $"广告国家码无效，返回空字符串：{countryCode ?? string.Empty}。");
                    return string.Empty;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    Log.Debug(LogTag.AD, "等待广告国家码超时，返回空字符串。");
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// 向所有渠道并行预加载并记录请求入口，返回成功或错误详情；首个成功后按既有规则更新活跃 Banner 渠道。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">自定义属性字典，透传到渠道层打点；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>首个完成的加载结果；全部失败时返回最后一次失败结果。</returns>
        public async UniTask<AdLoadResult> RequestAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default)
        {
            Log.Debug(LogTag.AD, $"业务准备请求广告：format={format}，渠道数={m_ChannelPlugins.Count}。");
            try
            {
                var result = await BroadcastRequestAsync(format, customProps, ct);
                if (format == AdFormat.Banner && result != null && result.Success)
                {
                    m_ActiveBannerChannel = SelectBestChannel(format);
                }
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"业务广告请求异常结束：format={format}，异常={ex.GetType().Name}，原因={ex.Message}。");
                throw;
            }
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
        /// 展示指定格式广告，按比价开关选择就绪渠道并记录展示入口；全屏占用时发布 Busy，Banner 请使用 ShowBanner()。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">自定义属性字典，透传到渠道层 show/show_result 成功与失败分支/hidden 打点；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        public async UniTask ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default)
        {
            Log.Debug(LogTag.AD, $"业务准备展示广告：format={format}。");
            IAdInternalPlugin best = null;
            try
            {
                ct.ThrowIfCancellationRequested();
                if (m_ChannelsDisposed) throw new ObjectDisposedException(Name);
                // 各渠道保留原生展示占用；取消业务等待后仍需等真实终态才能发出下一次全屏展示。
                if ((format == AdFormat.Rewarded || format == AdFormat.Interstitial || format == AdFormat.AppOpen)
                    && (IsAdPlaying(AdFormat.Rewarded) || IsAdPlaying(AdFormat.Interstitial) || IsAdPlaying(AdFormat.AppOpen)))
                {
                    Events.ShowFailed.Invoke(new AdResult { Format = format, ErrorMessage = "Busy(-12)：广告正在展示，请等待当前广告关闭。" });
                    return;
                }
                best = SelectBestChannel(format);
                if (best == null)
                {
                    Log.Warning(LogTag.AD, $"业务广告展示已跳过：format={format}，原因=无就绪广告渠道。");
                    return;
                }
                await best.ShowAsync(format, customProps, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"业务广告展示异常结束：format={format}，channel={best?.Channel.ToString() ?? "未选择"}，异常={ex.GetType().Name}，原因={ex.Message}。");
                throw;
            }
        }

        /// <summary>
        /// 显示 Banner 广告，委托给活跃 Banner 渠道。
        /// </summary>
        public void ShowBanner()
        {
            Log.Debug(LogTag.AD, $"业务准备显示 Banner：format={AdFormat.Banner}，channel={m_ActiveBannerChannel?.Channel.ToString() ?? "未选择"}。");
            m_ActiveBannerChannel?.ShowBanner();
        }

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
