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
        /// 按既有查询顺序选择就绪渠道；比价开启取最高收益，关闭取配置序首个就绪项。
        /// </summary>
        /// <param name="format">广告格式，用于 IsReady / GetMaxRevenue 查询。</param>
        /// <returns>选中的就绪渠道；无就绪渠道时返回 null。</returns>
        private IAdInternalPlugin SelectBestChannel(AdFormat format)
        {
            if (m_ChannelsDisposed || m_ChannelPlugins == null) return null;
            bool enableBidding = m_ChannelPlugins.Count > 0 && m_ChannelPlugins[0] is AdChannelPluginBase first && first.EnableBidding;
            if (enableBidding)
            {
                IAdInternalPlugin best = null;
                float bestRevenue = -1f;
                for (int i = 0; i < m_ChannelPlugins.Count; i++)
                {
                    var ch = m_ChannelPlugins[i];
                    bool ready = ch.IsReady(format);
                    if (!ready)
                    {
                        continue;
                    }
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
                    bool ready = ch.IsReady(format);
                    if (!ready) continue;
                    return ch;
                }
                return null;
            }
        }

        /// <summary>
        /// 并行请求全部渠道，每个单次 UniTask 只观察一次，首个成功或全部失败完成业务等待。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">本次业务属性。</param>
        /// <param name="ct">只取消本次业务等待，不取消原厂加载。</param>
        /// <returns>首个成功结果或最后实际失败结果。</returns>
        private async UniTask<AdLoadResult> BroadcastRequestAsync(AdFormat format, Dictionary<string, object> customProps, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (m_ChannelsDisposed) throw new ObjectDisposedException(Name);
            var channels = new List<IAdInternalPlugin>(m_ChannelPlugins);
            if (channels.Count == 0)
            {
                return BuildLoadFailure(format, null, -1, "未注册可用广告渠道。");
            }
            var batch = new BroadcastBatch { Remaining = channels.Count };
            using var waiterCts = CancellationTokenSource.CreateLinkedTokenSource(ct, m_ChannelLifetimeCts.Token);
            var waiterToken = waiterCts.Token;
            using var cancellation = waiterToken.Register(() => batch.Completion.TrySetCanceled(waiterToken));
            // 冻结渠道总数后逐一发出请求；同步完成也不会使尚未启动的渠道从广播中消失。
            for (int i = 0; i < channels.Count; i++)
            {
                ObserveChannelRequestAsync(channels[i], format, customProps, waiterToken, batch).Forget();
            }
            var result = await batch.Completion.Task;
            return result;
        }

        /// <summary>
        /// 仅等待一次渠道任务，在汇聚完成后仍接收迟到结果与异常，避免未观察任务。
        /// </summary>
        /// <param name="channel">当前渠道。</param>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">本次属性。</param>
        /// <param name="ct">本次等待令牌。</param>
        /// <param name="batch">共享汇聚状态。</param>
        private static async UniTask ObserveChannelRequestAsync(IAdInternalPlugin channel, AdFormat format, Dictionary<string, object> customProps, CancellationToken ct, BroadcastBatch batch)
        {
            AdLoadResult result;
            try { result = await channel.RequestAsync(format, customProps: customProps, ct: ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                lock (batch.Sync) batch.Remaining--;
                batch.Completion.TrySetCanceled(ct);
                return;
            }
            catch (OperationCanceledException)
            {
                // 单渠道释放或内部取消视为该渠道失败，不能取消仍能成功的其他渠道。
                result = BuildLoadFailure(format, null, -3, "渠道请求已结束。");
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"渠道广告请求异常：format={format}，channel={channel.Channel}，异常={ex.GetType().Name}，原因={ex.Message}");
                result = BuildLoadFailure(format, null, -3, ex.Message);
            }
            AdLoadResult completion = null;
            lock (batch.Sync)
            {
                batch.Remaining--;
                if (result != null && result.Success) completion = result;
                else
                {
                    if (result != null) batch.LastFailure = result;
                    if (batch.Remaining == 0) completion = batch.LastFailure ?? BuildLoadFailure(format, null, -2, "广告请求未返回结果。");
                }
            }
            // 不在锁内完成，业务 continuation 可能同步重入 Request/Dispose。
            if (completion != null) batch.Completion.TrySetResult(completion);
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
        /// 校验启用状态并反射构造渠道；构造失败只影响本渠道，配置在持有实例后另行应用。
        /// </summary>
        /// <param name="channelConfig">渠道配置，包含类型与启用状态。</param>
        /// <returns>成功构造的旧渠道接口实例，未启用或构造失败时为 null。</returns>
        private static IAdInternalPlugin CreateChannel(IAdChannelConfig channelConfig)
        {
            if (channelConfig == null) return null;
            Type pluginType = null;
            try
            {
                if (!channelConfig.Enabled) return null;
                pluginType = channelConfig.PluginType;
                if (pluginType == null) return null;
                var channel = Activator.CreateInstance(pluginType) as IAdInternalPlugin;
                if (channel == null)
                    Log.Warning(LogTag.AD, $"渠道 {pluginType.Name} 未实现 IAdInternalPlugin，已跳过。");
                return channel;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"广告渠道构造失败，已隔离：{pluginType?.Name}，{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存七类准确委托并桥接业务事件；首次绑定前建立完整退订动作，部分绑定失败也可收回。
        /// </summary>
        /// <param name="channel">本聚合已经持有的渠道实例。</param>
        /// <param name="registration">本渠道的有效门、退订动作与初始化退出信号。</param>
        private void WireChannelEvents(IAdInternalPlugin channel, ChannelRegistration registration)
        {
            if (registration.Unsubscribe != null) return;
            Action<AdEvent> revenue = e => { if (!m_ChannelsDisposed && registration.Active) Events.RevenuePaid.Invoke(e); };
            Action<AdLoadResult> loaded = e => { if (!m_ChannelsDisposed && registration.Active) Events.AdLoaded.Invoke(e); };
            Action<AdLoadResult> failed = e => { if (!m_ChannelsDisposed && registration.Active) Events.AdLoadFailed.Invoke(e); };
            Action<bool> initialized = success => { if (!m_ChannelsDisposed && registration.Active) OnChannelInitResult(channel, success); };
            Action<AdResult> shown = r => { if (!m_ChannelsDisposed && registration.Active) Events.ShowCompleted.Invoke(r); };
            Action<AdResult> showFailed = r => { if (!m_ChannelsDisposed && registration.Active) Events.ShowFailed.Invoke(r); };
            Action<AdResult> closed = r => { if (!m_ChannelsDisposed && registration.Active) Events.AdClosed.Invoke(r); };
            registration.Unsubscribe = () =>
            {
                TryUnsubscribeChannelEvent(channel, () => channel.OnAdRevenuePaid -= revenue);
                TryUnsubscribeChannelEvent(channel, () => channel.OnAdLoaded -= loaded);
                TryUnsubscribeChannelEvent(channel, () => channel.OnAdLoadFailed -= failed);
                TryUnsubscribeChannelEvent(channel, () => channel.OnInitResult -= initialized);
                TryUnsubscribeChannelEvent(channel, () => channel.OnShowCompleted -= shown);
                TryUnsubscribeChannelEvent(channel, () => channel.OnShowFailed -= showFailed);
                TryUnsubscribeChannelEvent(channel, () => channel.OnAdClosed -= closed);
            };
            channel.OnAdRevenuePaid += revenue;
            channel.OnAdLoaded += loaded;
            channel.OnAdLoadFailed += failed;
            channel.OnInitResult += initialized;
            channel.OnShowCompleted += shown;
            channel.OnShowFailed += showFailed;
            channel.OnAdClosed += closed;
        }

        /// <summary>
        /// 隔离单个外部事件移除器异常，保证其余准确委托仍逐一尝试退订。
        /// </summary>
        /// <param name="channel">诊断用渠道实例。</param>
        /// <param name="unsubscribe">单个原始委托的移除操作。</param>
        private static void TryUnsubscribeChannelEvent(IAdInternalPlugin channel, Action unsubscribe)
        {
            try { unsubscribe(); }
            catch (Exception ex) { Log.Warning(LogTag.AD, $"广告渠道事件退订失败，继续清理：{channel.GetType().Name}，{ex.Message}"); }
        }

        /// <summary>
        /// 依次应用配置、桥接事件、登记业务列表与观察可选初始化；最外层完成信号覆盖所有准备分支。
        /// 失败实例保留释放所有权；没有可选生命周期的旧渠道成功准备后保持可用，不自动初始化。
        /// </summary>
        /// <param name="channel">已经登记所有权的渠道实例。</param>
        /// <param name="channelConfig">当前渠道配置。</param>
        /// <param name="adConfig">本轮聚合配置，用于应用基类全局开关。</param>
        /// <param name="registration">进入本方法前已经登记的渠道持有记录。</param>
        /// <param name="ct">调用方初始化令牌，与聚合生命周期共同控制初始化等待。</param>
        /// <returns>真实准备与初始化退出任务；释放通过独立完成信号等待，不重复观察此任务。</returns>
        private async UniTask PrepareAndInitializeChannelAsync(IAdInternalPlugin channel, IAdChannelConfig channelConfig, AdPluginConfig adConfig, ChannelRegistration registration, CancellationToken ct)
        {
            bool prepared = false;
            Exception failure = null;
            try
            {
                using var initializationCts = CancellationTokenSource.CreateLinkedTokenSource(ct, m_ChannelLifetimeCts.Token);
                var initializationToken = initializationCts.Token;
                initializationToken.ThrowIfCancellationRequested();
                if (m_ChannelsDisposed || !registration.Active) return;
                if (channel is AdChannelPluginBase channelBase)
                    channelBase.ApplyGlobalConfig(adConfig.ChannelConfigs);
                initializationToken.ThrowIfCancellationRequested();
                if (m_ChannelsDisposed || !registration.Active) return;
                WireChannelEvents(channel, registration);
                initializationToken.ThrowIfCancellationRequested();
                if (m_ChannelsDisposed || !registration.Active) return;
                RegisterChannel(channel);
                initializationToken.ThrowIfCancellationRequested();
                if (m_ChannelsDisposed || !registration.Active) return;
                if (channel is IAdChannelLifecycle lifecycle)
                    await lifecycle.InitializeAsync(channelConfig, initializationToken);
                initializationToken.ThrowIfCancellationRequested();
                prepared = !m_ChannelsDisposed && registration.Active;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { failure = ex; }
            finally
            {
                try
                {
                    // 取消续体可能来自后台线程，集合、有效门和业务失败通知仍在主线程收口。
                    await UniTask.SwitchToMainThread();
                    if (!prepared)
                    {
                        registration.Active = false;
                        m_ChannelPlugins.Remove(channel);
                    }
                    if (failure != null && !m_ChannelsDisposed)
                    {
                        Log.Warning(LogTag.AD, $"广告渠道准备或初始化失败，已隔离：{channel.GetType().Name}，{failure.Message}");
                        OnChannelInitResult(channel, false);
                    }
                }
                finally { registration.InitializationCompletion.TrySetResult(); }
            }
        }

        /// <summary>
        /// 关闭本聚合、取消等待，并在各渠道准备和真实初始化退出后准确退订与释放。
        /// 单渠道或取消回调失败被隔离，非预期异常通过预先发布的共享完成源返回。
        /// </summary>
        /// <param name="completion">首次释放时建立并保存的共享完成源，所有退出路径必须结算。</param>
        /// <returns>内部清理任务，重复调用只观察共享完成源。</returns>
        private async UniTask DisposeChannelsAsync(UniTaskCompletionSource completion)
        {
            Exception failure = null;
            try
            {
                await UniTask.SwitchToMainThread();
                m_ChannelsDisposed = true;
                var registrations = new List<KeyValuePair<IAdInternalPlugin, ChannelRegistration>>(m_ChannelRegistrations);
                foreach (var entry in registrations) entry.Value.Active = false;
                m_ChannelRegistrations.Clear();
                m_ChannelPlugins.Clear();
                m_ActiveBannerChannel = null;
                m_RuntimeConfig = null;
                // Cancel 可能同步唤起初始化或业务续体，关闭门和所有权快照必须先准备完成。
                try { m_ChannelLifetimeCts.Cancel(); }
                catch (Exception ex) { Log.Warning(LogTag.AD, $"广告生命周期取消回调失败，继续释放：{ex.Message}"); }
                foreach (var entry in registrations)
                {
                    var channel = entry.Key;
                    var registration = entry.Value;
                    await registration.InitializationCompletion.Task;
                    await UniTask.SwitchToMainThread();
                    // 最终退订必须晚于准备退出，覆盖自定义 += 重入释放后仍完成订阅的情况。
                    var unsubscribe = registration.Unsubscribe;
                    registration.Unsubscribe = null;
                    try { unsubscribe?.Invoke(); }
                    catch (Exception ex) { Log.Warning(LogTag.AD, $"广告渠道事件退订失败，继续释放：{channel.GetType().Name}，{ex.Message}"); }
                    try
                    {
                        if (channel is IAdChannelLifecycle lifecycle)
                            await lifecycle.DisposeAsync(CancellationToken.None);
                        else if (channel is IDisposable disposable)
                            disposable.Dispose();
                    }
                    catch (Exception ex) { Log.Warning(LogTag.AD, $"广告渠道释放失败，已隔离：{channel.GetType().Name}，{ex.Message}"); }
                }
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                try
                {
                    await UniTask.SwitchToMainThread();
                    m_RuntimeConfig = null;
                    Events.InitResult.Clear();
                    Events.AdLoaded.Clear();
                    Events.AdLoadFailed.Clear();
                    Events.ShowCompleted.Clear();
                    Events.ShowFailed.Clear();
                    Events.RevenuePaid.Clear();
                    Events.AdClosed.Clear();
                    m_ChannelLifetimeCts.Dispose();
                }
                catch (Exception ex) { if (failure == null) failure = ex; }
                finally
                {
                    if (failure == null) completion.TrySetResult();
                    else completion.TrySetException(failure);
                }
            }
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
        /// 配置值小于等于 0、NaN 或无穷大时不等待，直接返回空字符串。
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
        /// SDKManager 登录能力入口；向已创建的渠道同步账号，单渠道异常隔离。
        /// </summary>
        /// <param name="sender">事件源（SDKManager 实例）。</param>
        /// <param name="e">事件数据，期望为 SDKEventData.UserLogin。</param>
        public void OnSDKLogin(string userId, long sessionId)
        {
            for (int i = 0; i < m_ChannelPlugins.Count; i++)
            {
                try
                {
                    m_ChannelPlugins[i].SetUserId(userId);
                }
                catch (Exception ex)
                {
                    Log.Error(LogTag.AD, $"渠道 SetUserId 失败：{m_ChannelPlugins[i]?.Channel}，{ex}");
                }
            }
        }
    }
}
