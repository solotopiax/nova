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
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    public sealed partial class AdPlugin
    {
        /// <summary>
        /// 按渠道配置顺序读取第一个明确的广告隐私授权结果。
        /// 单个渠道查询异常时记录警告并继续查询后续渠道。
        /// </summary>
        /// <param name="consented">找到明确决定时返回该渠道的授权结果；否则为 false。</param>
        /// <returns>找到明确授权决定时返回 true，否则返回 false。</returns>
        private bool TryGetUserConsent(out bool consented)
        {
            consented = false;
            if (m_ChannelPlugins == null)
            {
                return false;
            }

            for (int i = 0; i < m_ChannelPlugins.Count; i++)
            {
                IAdInternalPlugin channel = m_ChannelPlugins[i];
                try
                {
                    if (!channel.IsUserConsentSet())
                    {
                        continue;
                    }

                    consented = channel.HasUserConsent();
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Warning(LogTag.AD, $"渠道广告隐私授权状态查询失败：{channel?.Channel}，{ex.Message}");
                }
            }

            return false;
        }

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
            channel.OnInitResult += success => OnChannelInitResult(channel, success);
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
            TryPublishCountryCodeFromChannel(channel);
        }

        /// <summary>
        /// 渠道初始化完成回调，转发初始化事件并在成功时发布渠道国家码。
        /// 国家码通常只有渠道 SDK 初始化完成后才可用，因此在此处做一次内部发布收口。
        /// </summary>
        /// <param name="channel">触发初始化结果的渠道。</param>
        /// <param name="success">渠道初始化是否成功。</param>
        private void OnChannelInitResult(IAdInternalPlugin channel, bool success)
        {
            Events.InitResult.Invoke(success);
            if (success)
            {
                TryPublishCountryCodeFromChannel(channel);
            }
        }

        /// <summary>
        /// 尝试从指定广告渠道读取国家或地区代码并发布到 SDK 数据槽位。
        /// 渠道尚未初始化、未返回、返回 IV 占位值或抛出异常时仅记录诊断，不发布数据。
        /// </summary>
        /// <param name="channel">广告渠道实例。</param>
        private void TryPublishCountryCodeFromChannel(IAdInternalPlugin channel)
        {
            if (channel == null)
            {
                return;
            }

            try
            {
                TryPublishCountryCode(channel.GetCountryCode());
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"渠道国家代码发布失败：{channel.Channel}，{ex.Message}");
            }
        }

        /// <summary>
        /// 尝试发布广告国家或地区代码到 SDK 数据槽位。
        /// 只发布非空且不等于 IV 的国家码，并统一转换为大写，供其他 SDK 通过 FetchDataAsync 等待。
        /// </summary>
        /// <param name="countryCode">渠道返回的国家或地区代码。</param>
        private void TryPublishCountryCode(string countryCode)
        {
            if (!TryNormalizeCountryCode(countryCode, out string normalizedCountryCode))
            {
                return;
            }

            PublishData(SDKDataKeys.AdCountryCode, normalizedCountryCode);
            SaveCountryCodeCache(normalizedCountryCode);
            Log.Debug(LogTag.AD, $"已发布广告国家代码：{normalizedCountryCode}。");
        }

        /// <summary>
        /// 规范化广告国家或地区代码。
        /// 返回 false 表示输入为空、清洗后为空或为广告 SDK 的 IV 占位值，不应发布或用于订阅。
        /// </summary>
        /// <param name="countryCode">原始国家或地区代码。</param>
        /// <param name="normalizedCountryCode">规范化后的大写国家或地区代码。</param>
        /// <returns>国家码有效返回 true，否则返回 false。</returns>
        private static bool TryNormalizeCountryCode(string countryCode, out string normalizedCountryCode)
        {
            normalizedCountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? string.Empty
                : countryCode.Trim().ToUpper(CultureInfo.InvariantCulture);

            return normalizedCountryCode.Length > 0
                   && !string.Equals(normalizedCountryCode, "IV", StringComparison.Ordinal);
        }

        /// <summary>
        /// 获取广告国家码等待超时时间。
        /// 配置值小于等于 0、NaN 或无穷大时不等待，直接读取本地缓存。
        /// </summary>
        /// <returns>等待超时时间。</returns>
        private TimeSpan GetCountryCodeWaitTimeout()
        {
            float timeoutSeconds = m_RuntimeConfig?.CountryCodeWaitTimeoutSeconds
                                   ?? c_DefaultCountryCodeWaitTimeoutSeconds;
            if (float.IsNaN(timeoutSeconds) || float.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0f)
            {
                return TimeSpan.Zero;
            }

            double timeoutMilliseconds = Math.Min(timeoutSeconds * 1000d, int.MaxValue);
            return TimeSpan.FromMilliseconds(timeoutMilliseconds);
        }

        /// <summary>
        /// 读取广告模块上次成功获取到的国家码缓存。
        /// 缓存为空、为 IV 或持久化模块不可用时返回空字符串。
        /// </summary>
        /// <returns>大写国家或地区代码；不可用时返回空字符串。</returns>
        private static string ReadCountryCodeCache()
        {
            try
            {
                IFileFragmentManager persistManager = FrameworkManagersGroup.GetManager<IFileFragmentManager>();
                string countryCode = persistManager.GetObject<string>(
                    c_CountryCodePersistClassify,
                    c_CountryCodePersistItem,
                    string.Empty);
                return TryNormalizeCountryCode(countryCode, out string normalizedCountryCode)
                    ? normalizedCountryCode
                    : string.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"读取广告国家码缓存失败：{ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 保存广告模块上次成功获取到的国家码。
        /// 只有有效国家码会写入缓存，空值和 IV 会被忽略。
        /// </summary>
        /// <param name="normalizedCountryCode">已规范化的国家或地区代码。</param>
        private static void SaveCountryCodeCache(string normalizedCountryCode)
        {
            if (!TryNormalizeCountryCode(normalizedCountryCode, out string countryCode))
            {
                return;
            }

            try
            {
                IFileFragmentManager persistManager = FrameworkManagersGroup.GetManager<IFileFragmentManager>();
                persistManager.SetObject(c_CountryCodePersistClassify, c_CountryCodePersistItem, countryCode);
                persistManager.Save(c_CountryCodePersistClassify);
                Log.Debug(LogTag.AD, $"广告国家码缓存已更新：{countryCode}。");
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"保存广告国家码缓存失败：{ex.Message}");
            }
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
