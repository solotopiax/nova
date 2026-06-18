/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdPlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   AdPlugin 私有方法 + UniTask 广告扩展工具
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    public sealed partial class AdPlugin
    {
        /// <summary>
        /// 选出已就绪的渠道。
        /// 比价模式开启时选 Revenue 最高的渠道；关闭时按列表顺序取第一个就绪渠道。
        /// 是否支持由 IsReady 隐式覆盖：未注册槽位的渠道 IsReady 必为 false。
        /// </summary>
        /// <param name="format">广告格式，用于 IsReady / GetMaxRevenue 查询。</param>
        /// <returns>选中的就绪渠道；无就绪渠道时返回 null。</returns>
        private IAdInternalPlugin SelectBestChannel(AdFormat format)
        {
            bool enableBidding = m_ChannelPlugins.Count > 0 && m_ChannelPlugins[0] is AdChannelPluginBase first && first.EnableBidding;
            if (enableBidding)
            {
                IAdInternalPlugin best = null;
                float bestRevenue = -1f;
                for (int i = 0; i < m_ChannelPlugins.Count; i++)
                {
                    var ch = m_ChannelPlugins[i];
                    if (!ch.IsReady(format)) continue;
                    float rev = ch.GetMaxRevenue(format);
                    if (rev > bestRevenue)
                    {
                        bestRevenue = rev;
                        best = ch;
                    }
                }
                return best;
            }
            else
            {
                for (int i = 0; i < m_ChannelPlugins.Count; i++)
                {
                    var ch = m_ChannelPlugins[i];
                    if (ch.IsReady(format)) return ch;
                }
                return null;
            }
        }

        /// <summary>
        /// 向所有渠道并行发起请求，通过循环 WhenAny 取首个成功结果。
        /// 未注册该 format 的渠道由 AdChannelPluginBase.RequestAsync 直接返回 Success=false 自然过滤。
        /// 失败（Success=false）的渠道结果被暂存，直到取到第一个成功结果或全部完成。
        /// OperationCanceledException 直接向上传播；其他异常记录警告后当作失败结果处理。
        /// </summary>
        /// <param name="format">广告格式，透传到各渠道。</param>
        /// <param name="customProps">自定义属性字典，透传到各渠道；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>首个成功的 AdLoadResult；全部失败时返回最后一次失败结果。</returns>
        private async UniTask<AdLoadResult> BroadcastRequestAsync(AdFormat format, Dictionary<string, object> customProps, CancellationToken ct)
        {
            var tasks = new List<UniTask<AdLoadResult>>();
            for (int i = 0; i < m_ChannelPlugins.Count; i++)
            {
                var ch = m_ChannelPlugins[i];
                tasks.Add(SafeRequestAsync(ch, format, customProps, ct));
            }
            if (tasks.Count == 0) return BuildLoadFailure(format, null, -1, "未注册可用广告渠道。");
            AdLoadResult lastFailure = null;
            while (tasks.Count > 0)
            {
                var (winIndex, result) = await UniTask.WhenAny(tasks);
                if (result != null && result.Success) return result;
                if (result != null) lastFailure = result;
                tasks.RemoveAt(winIndex);
            }
            return lastFailure ?? BuildLoadFailure(format, null, -2, "广告请求未返回结果。");
        }

        /// <summary>
        /// 包裹单个渠道的 RequestAsync，返回 AdLoadResult；OperationCanceledException 直接传播，其他异常降级为失败结果。
        /// </summary>
        /// <param name="channel">目标渠道插件。</param>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">自定义属性字典，透传到渠道；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>渠道返回的加载结果；异常时返回 Success=false。</returns>
        private static async UniTask<AdLoadResult> SafeRequestAsync(IAdInternalPlugin channel, AdFormat format, Dictionary<string, object> customProps, CancellationToken ct)
        {
            try
            {
                return await channel.RequestAsync(format, customProps: customProps, ct: ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.AD, $"渠道 {channel.GetType().Name} RequestAsync({format}) 失败：{e.Message}");
                return BuildLoadFailure(format, null, -3, e.Message);
            }
        }

        /// <summary>
        /// 构造统一的加载失败结果。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识，可为 null。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误描述。</param>
        /// <returns>Success=false 的加载结果。</returns>
        private static AdLoadResult BuildLoadFailure(AdFormat format, string placementId, int errorCode, string errorMessage)
        {
            return new AdLoadResult
            {
                Success = false,
                Format = format,
                PlacementId = placementId,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
            };
        }

        /// <summary>
        /// 反射创建渠道实例，应用全局配置并启动 SDK 异步初始化。
        /// 渠道未启用、实例化失败或不继承 AdChannelPluginBase 时返回 null。
        /// </summary>
        /// <param name="channelConfig">渠道配置，含类型与启用状态。</param>
        /// <param name="adConfig">全局广告配置，用于写入渠道全局属性。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>创建成功的渠道实例；跳过时返回 null。</returns>
        private static IAdInternalPlugin CreateChannel(IAdChannelConfig channelConfig, AdPluginConfig adConfig, CancellationToken ct)
        {
            if (!channelConfig.Enabled) return null;
            var channel = Activator.CreateInstance(channelConfig.PluginType) as IAdInternalPlugin;
            if (channel == null)
            {
                Log.Warning(LogTag.AD, $"渠道 {channelConfig.PluginType?.Name} 实例化失败或未实现 IAdInternalPlugin，已跳过。");
                return null;
            }
            if (channel is AdChannelPluginBase channelBase)
            {
                channelBase.ApplyGlobalConfig(adConfig.ChannelConfigs);
                channelBase.InitializeAsync(channelConfig, ct).Forget();
            }
            return channel;
        }

        /// <summary>
        /// 订阅渠道的七类事件，桥接到 Events 容器的对应 ObservableEvent。
        /// </summary>
        /// <param name="channel">已创建的渠道实例。</param>
        private void WireChannelEvents(IAdInternalPlugin channel)
        {
            channel.OnAdRevenuePaid += e => Events.RevenuePaid.Invoke(e);
            channel.OnAdLoaded += e => Events.AdLoaded.Invoke(e);
            channel.OnAdLoadFailed += e => Events.AdLoadFailed.Invoke(e);
            channel.OnInitResult += v => Events.InitResult.Invoke(v);
            channel.OnShowCompleted += r => Events.ShowCompleted.Invoke(r);
            channel.OnShowFailed += r => Events.ShowFailed.Invoke(r);
            channel.OnAdClosed += r => Events.AdClosed.Invoke(r);
        }

        /// <summary>
        /// 将渠道实例注册到 m_ChannelPlugins 列表。
        /// </summary>
        /// <param name="channel">已配置并已订阅事件的渠道实例。</param>
        private void RegisterChannel(IAdInternalPlugin channel)
        {
            m_ChannelPlugins.Add(channel);
        }

        /// <summary>
        /// SDKEventData.UserLogin 事件处理器；遍历所有 channel 调用其 SetUserId，单个 channel 异常 try/catch 隔离。
        /// </summary>
        /// <param name="sender">事件源（SDKManager 实例）。</param>
        /// <param name="e">事件数据，期望为 SDKEventData.UserLogin。</param>
        private void OnUserLogin(object sender, EventData e)
        {
            if (!(e is SDKEventData.UserLogin login)) return;
            for (int i = 0; i < m_ChannelPlugins.Count; i++)
            {
                try
                {
                    m_ChannelPlugins[i].SetUserId(login.UserId);
                }
                catch (Exception ex)
                {
                    Log.Error(LogTag.AD, $"渠道 SetUserId 失败：{m_ChannelPlugins[i]?.Channel}，{ex}");
                }
            }
        }
    }
}
