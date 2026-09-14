/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePlugin.DefaultTopics.cs
 * author:    Codex
 * created:   2026/8/13
 * descrip:   FirebasePlugin 默认推送 Topic 同步
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

#if (UNITY_IOS || UNITY_ANDROID)
using Firebase.Messaging;
#endif

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    public sealed partial class FirebasePlugin
    {
        /// <summary>
        /// 默认 Topic 订阅状态的持久化分类名。
        /// 使用独立分类避免和业务层文件片段数据混在同一命名空间。
        /// </summary>
        private const string c_DefaultTopicPersistClassify = "FirebaseDefaultTopics";

        /// <summary>
        /// 基础默认 Topic 状态持久化条目名。
        /// 记录 all、语言、平台和时区四类 Topic 的上一次成功订阅状态。
        /// </summary>
        private const string c_BaseTopicStatePersistItem = "BaseState";

        /// <summary>
        /// 国家默认 Topic 状态持久化条目名。
        /// 国家码由广告 SDK 异步发布，因此与基础状态分开保存。
        /// </summary>
        private const string c_CountryTopicStatePersistItem = "CountryState";

        /// <summary>
        /// Language.Unspecified 对应的历史语言标记，不应作为有效语言 Topic 保留。
        /// </summary>
        private static readonly string s_UnspecifiedLanguageTopicFlag = LanguageMetadata.GetFlag(Language.Unspecified);

        /// <summary>
        /// 单个 Topic 操作的结果。
        /// </summary>
        private enum TopicSubscriptionOperationResult
        {
            /// <summary>
            /// Firebase Topic 操作成功。
            /// </summary>
            Succeeded,

            /// <summary>
            /// Firebase Topic 操作失败，且不是当前可恢复的 APNs Token 未就绪场景。
            /// </summary>
            Failed,

            /// <summary>
            /// Firebase iOS 原生层尚未拿到 APNs Token，当前 Topic 操作需要延后重试。
            /// </summary>
            ApnsTokenNotReady,
        }

        /// <summary>
        /// Firebase 初始化完成后启动默认 Topic 同步。
        /// 基础 Topic 同步任务会等待 FCM Token 就绪；语言 Topic 还需等待 Localization 发布真实当前语言后再同步。
        /// 国家 Topic 通过 AdPlugin.GetCountryCodeAsync 读取最终国家码；广告模块负责等待、超时和上次成功缓存兜底。
        /// </summary>
        private void StartDefaultTopicSync()
        {
#if (UNITY_IOS || UNITY_ANDROID)
            m_DefaultTopicSyncCts?.Cancel();
            m_DefaultTopicSyncCts?.Dispose();
            m_DefaultTopicSyncCts = new CancellationTokenSource();
            CancellationToken ct = m_DefaultTopicSyncCts.Token;
            SubscribeDefaultTopicLocalizationRefresh();
            SyncDefaultBaseTopicsAsync(ct, Language.Unspecified).Forget();
            WaitAndSyncCountryTopicAsync(ct).Forget();
#endif
        }

        /// <summary>
        /// 取消默认 Topic 同步后台任务。
        /// 插件释放时调用，避免后台等待国家码或 Firebase 订阅任务在释放后继续访问插件状态。
        /// </summary>
        private void CancelDefaultTopicSync()
        {
            UnsubscribeDefaultTopicLocalizationRefresh();
            m_DefaultBaseTopicApnsRetryScheduled = false;
            m_DefaultCountryTopicApnsRetryScheduled = false;

            if (m_DefaultTopicSyncCts == null)
            {
                return;
            }

            m_DefaultTopicSyncCts.Cancel();
            m_DefaultTopicSyncCts.Dispose();
            m_DefaultTopicSyncCts = null;
        }

        /// <summary>
        /// 应用从后台恢复前台后请求一次默认 Topic 补偿同步。
        /// Topic 未变化时会被存档差异直接跳过。
        /// </summary>
        private void RequestDefaultTopicSyncOnForeground()
        {
#if (UNITY_IOS || UNITY_ANDROID)
            if (!m_InitOver || m_DefaultTopicSyncCts == null)
            {
                return;
            }

            CancellationToken ct = m_DefaultTopicSyncCts.Token;
            SyncDefaultBaseTopicsAsync(ct, Language.Unspecified).Forget();
            WaitAndSyncCountryTopicAsync(ct).Forget();
#endif
        }

        /// <summary>
        /// 安排基础默认 Topic 在 APNs Token 后续就绪后再次同步。
        /// </summary>
        /// <param name="preferredLanguage">本轮基础 Topic 同步使用的语言提示。</param>
        /// <param name="ct">默认 Topic 同步取消令牌。</param>
        private void ScheduleDefaultBaseTopicApnsRetry(Language preferredLanguage, CancellationToken ct)
        {
#if (UNITY_IOS || UNITY_ANDROID)
            if (m_DefaultBaseTopicApnsRetryScheduled || ct.IsCancellationRequested)
            {
                return;
            }

            m_DefaultBaseTopicApnsRetryScheduled = true;
            RetryDefaultBaseTopicAfterApnsDelayAsync(preferredLanguage, ct).Forget();
#endif
        }

        /// <summary>
        /// 延迟后重新同步基础默认 Topic。
        /// </summary>
        /// <param name="preferredLanguage">本轮基础 Topic 同步使用的语言提示。</param>
        /// <param name="ct">默认 Topic 同步取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async UniTaskVoid RetryDefaultBaseTopicAfterApnsDelayAsync(Language preferredLanguage, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(s_DefaultTopicApnsRetryDelay, cancellationToken: ct);
                m_DefaultBaseTopicApnsRetryScheduled = false;
                await SyncDefaultBaseTopicsAsync(ct, preferredLanguage);
            }
            catch (OperationCanceledException)
            {
                m_DefaultBaseTopicApnsRetryScheduled = false;
            }
        }

        /// <summary>
        /// 安排国家默认 Topic 在 APNs Token 后续就绪后再次同步。
        /// </summary>
        /// <param name="ct">默认 Topic 同步取消令牌。</param>
        private void ScheduleDefaultCountryTopicApnsRetry(CancellationToken ct)
        {
#if (UNITY_IOS || UNITY_ANDROID)
            if (m_DefaultCountryTopicApnsRetryScheduled || ct.IsCancellationRequested)
            {
                return;
            }

            m_DefaultCountryTopicApnsRetryScheduled = true;
            RetryDefaultCountryTopicAfterApnsDelayAsync(ct).Forget();
#endif
        }

        /// <summary>
        /// 延迟后重新解析国家码并同步国家默认 Topic。
        /// </summary>
        /// <param name="ct">默认 Topic 同步取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async UniTaskVoid RetryDefaultCountryTopicAfterApnsDelayAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(s_DefaultTopicApnsRetryDelay, cancellationToken: ct);
                m_DefaultCountryTopicApnsRetryScheduled = false;
                await WaitAndSyncCountryTopicAsync(ct);
            }
            catch (OperationCanceledException)
            {
                m_DefaultCountryTopicApnsRetryScheduled = false;
            }
        }

        /// <summary>
        /// 订阅本地化刷新事件，用于在当前语言初始化完成或切换后同步语言 Topic。
        /// </summary>
        private void SubscribeDefaultTopicLocalizationRefresh()
        {
            if (m_DefaultTopicLocalizationSubscribed || m_EventManager == null)
            {
                return;
            }

            m_EventManager.Subscribe<LocalizationRefreshEventData>(OnLocalizationRefresh);
            m_DefaultTopicLocalizationSubscribed = true;
        }

        /// <summary>
        /// 退订本地化刷新事件。
        /// </summary>
        private void UnsubscribeDefaultTopicLocalizationRefresh()
        {
            if (!m_DefaultTopicLocalizationSubscribed || m_EventManager == null)
            {
                return;
            }

            m_EventManager.Unsubscribe<LocalizationRefreshEventData>(OnLocalizationRefresh);
            m_DefaultTopicLocalizationSubscribed = false;
        }

        /// <summary>
        /// 本地化刷新后同步语言 Topic。
        /// 事件数据来自引用池，只复制语言值并把实际 Firebase 操作放到异步任务中执行。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">事件数据。</param>
        private void OnLocalizationRefresh(object sender, EventData e)
        {
            if (!(e is LocalizationRefreshEventData localizationRefresh))
            {
                return;
            }

            Language newLanguage = localizationRefresh.NewLanguage;
            if (newLanguage == Language.Unspecified || m_DefaultTopicSyncCts == null)
            {
                return;
            }

            CancellationToken ct = m_DefaultTopicSyncCts.Token;
            SyncDefaultBaseTopicsAsync(ct, newLanguage).Forget();
        }

        /// <summary>
        /// 同步基础默认 Topic。
        /// 若持久化可用，则只处理旧状态和当前状态的差异；语言未就绪时保留旧语言 Topic，全部操作成功后才覆盖保存当前状态。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <param name="preferredLanguage">事件传入的已就绪语言；为 Unspecified 时从 Localization Manager 当前状态解析。</param>
        /// <returns>异步任务。</returns>
        private async UniTask SyncDefaultBaseTopicsAsync(CancellationToken ct, Language preferredLanguage)
        {
            bool lockTaken = false;
            try
            {
                await m_DefaultBaseTopicSyncLock.WaitAsync(ct);
                lockTaken = true;

                IFileFragmentManager persistManager = GetDefaultTopicPersistManager();
                FirebaseTopicSubscriptionState oldState = persistManager?.GetObject<FirebaseTopicSubscriptionState>(
                    c_DefaultTopicPersistClassify,
                    c_BaseTopicStatePersistItem,
                    null);
                FirebaseTopicSubscriptionState currentState = BuildCurrentBaseTopicState(oldState, preferredLanguage);
                FirebaseTopicSubscriptionDiff diff = FirebaseDefaultTopicBuilder.BuildTopicDiff(oldState?.Topics, currentState.Topics);

                if (diff.IsEmpty)
                {
                    Log.Debug(LogTag.Firebase, "默认基础推送 Topic 未变化，跳过同步。");
                    return;
                }

                TopicSubscriptionOperationResult result = await ApplyTopicDiffAsync(diff, ct);
                if (result != TopicSubscriptionOperationResult.Succeeded)
                {
                    Log.Warning(LogTag.Firebase, "默认基础推送 Topic 同步失败，本次不更新存档。");
                    if (result == TopicSubscriptionOperationResult.ApnsTokenNotReady)
                    {
                        ScheduleDefaultBaseTopicApnsRetry(preferredLanguage, ct);
                    }

                    return;
                }

                SaveBaseTopicState(persistManager, currentState);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(LogTag.Firebase, $"默认基础推送 Topic 同步异常：{ex}");
            }
            finally
            {
                if (lockTaken)
                {
                    m_DefaultBaseTopicSyncLock.Release();
                }
            }
        }

        /// <summary>
        /// 等待广告聚合层解析国家码并同步国家默认 Topic。
        /// Firebase 不直接读取广告数据槽位，也不使用系统区域作为本地兜底。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async UniTask WaitAndSyncCountryTopicAsync(CancellationToken ct)
        {
            try
            {
                string countryCode = await ResolveFirebaseCountryCodeAsync(ct);
                await SyncDefaultCountryTopicAsync(countryCode, ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(LogTag.Firebase, $"默认国家推送 Topic 等待或同步异常：{ex}");
            }
        }

        /// <summary>
        /// 解析 Firebase 使用的国家码。
        /// 国家码等待、超时和上次成功缓存均由广告聚合插件负责；不可用时返回空字符串。
        /// </summary>
        /// <param name="ct">外层取消令牌。</param>
        /// <returns>待使用的国家码；不可用时返回空字符串。</returns>
        private async UniTask<string> ResolveFirebaseCountryCodeAsync(CancellationToken ct)
        {
            ISDKManager sdkManager = FrameworkManagersGroup.GetManager<ISDKManager>();
            if (sdkManager == null)
            {
                return string.Empty;
            }

            await sdkManager.WaitForInitializedAsync(ct);
            if (!sdkManager.TryGet<IAdPlugin>(out IAdPlugin adPlugin))
            {
                Log.Debug(LogTag.Firebase, "广告插件不可用，默认国家 Topic 和 Firebase 登录上报国家码将使用空字符串。");
                return string.Empty;
            }

            return await adPlugin.GetCountryCodeAsync(ct);
        }

        /// <summary>
        /// 同步国家默认 Topic。
        /// 国家码无效时不订阅、不退订且不覆盖旧存档；有效且变化时先退订旧国家 Topic，再订阅新国家 Topic。
        /// </summary>
        /// <param name="countryCode">待同步的国家或地区代码；空值和 IV 会被视为无效。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async UniTask SyncDefaultCountryTopicAsync(string countryCode, CancellationToken ct)
        {
            DevelopMode developMode = ResolveDefaultTopicDevelopMode();
            if (!FirebaseDefaultTopicBuilder.TryBuildCountryState(developMode, countryCode, out FirebaseCountryTopicSubscriptionState currentState))
            {
                Log.Debug(LogTag.Firebase, $"国家码无效，跳过国家推送 Topic 同步：{countryCode}。");
                return;
            }

            IFileFragmentManager persistManager = GetDefaultTopicPersistManager();
            FirebaseCountryTopicSubscriptionState oldState = persistManager?.GetObject<FirebaseCountryTopicSubscriptionState>(
                c_DefaultTopicPersistClassify,
                c_CountryTopicStatePersistItem,
                null);

            if (oldState != null && string.Equals(oldState.Topic, currentState.Topic, StringComparison.Ordinal))
            {
                Log.Debug(LogTag.Firebase, $"默认国家推送 Topic 未变化，跳过同步：{currentState.Topic}。");
                return;
            }

            string[] oldTopics = string.IsNullOrEmpty(oldState?.Topic) ? Array.Empty<string>() : new[] { oldState.Topic };
            string[] currentTopics = new[] { currentState.Topic };
            FirebaseTopicSubscriptionDiff diff = FirebaseDefaultTopicBuilder.BuildTopicDiff(oldTopics, currentTopics);
            TopicSubscriptionOperationResult result = await ApplyTopicDiffAsync(diff, ct);
            if (result != TopicSubscriptionOperationResult.Succeeded)
            {
                Log.Warning(LogTag.Firebase, "默认国家推送 Topic 同步失败，本次不更新存档。");
                if (result == TopicSubscriptionOperationResult.ApnsTokenNotReady)
                {
                    ScheduleDefaultCountryTopicApnsRetry(ct);
                }

                return;
            }

            SaveCountryTopicState(persistManager, currentState);
        }

        /// <summary>
        /// 构建当前基础默认 Topic 状态。
        /// 语言来自已就绪的 Localization Manager；未就绪时沿用旧存档语言，避免启动早期误退订旧语言 Topic。
        /// </summary>
        /// <param name="oldState">上一次成功保存的基础 Topic 状态。</param>
        /// <param name="preferredLanguage">事件传入的已就绪语言；为 Unspecified 时从 Localization Manager 当前状态解析。</param>
        /// <returns>当前基础默认 Topic 状态。</returns>
        private static FirebaseTopicSubscriptionState BuildCurrentBaseTopicState(
            FirebaseTopicSubscriptionState oldState,
            Language preferredLanguage)
        {
            string persistedLanguage = oldState?.Language ?? string.Empty;
            if (string.Equals(persistedLanguage, s_UnspecifiedLanguageTopicFlag, StringComparison.Ordinal))
            {
                persistedLanguage = string.Empty;
            }

            string language = TryResolveDefaultTopicLanguage(preferredLanguage, out string resolvedLanguage)
                ? resolvedLanguage
                : persistedLanguage;
            string platform = ResolveTopicPlatform();
            DevelopMode developMode = ResolveDefaultTopicDevelopMode();
            TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
            return FirebaseDefaultTopicBuilder.BuildBaseState(developMode, language, platform, utcOffset);
        }

        /// <summary>
        /// 尝试解析默认语言 Topic 使用的语言标记。
        /// Localization 只在当前语言初始化或切换完成后才提供真实语言，Unspecified 不会进入 Topic。
        /// </summary>
        /// <param name="preferredLanguage">事件传入的已就绪语言；为 Unspecified 时从 Localization Manager 当前状态解析。</param>
        /// <param name="language">解析后的语言标记。</param>
        /// <returns>语言已就绪且存在有效标记时返回 true。</returns>
        private static bool TryResolveDefaultTopicLanguage(Language preferredLanguage, out string language)
        {
            language = string.Empty;
            Language currentLanguage = preferredLanguage;
            if (currentLanguage == Language.Unspecified)
            {
                ILocalizationManager localizationManager = FrameworkManagersGroup.GetManager<ILocalizationManager>();
                if (localizationManager == null)
                {
                    return false;
                }

                currentLanguage = localizationManager.Language;
            }

            if (currentLanguage == Language.Unspecified)
            {
                return false;
            }

            language = LanguageMetadata.GetFlag(currentLanguage);
            return !string.IsNullOrEmpty(language);
        }

        /// <summary>
        /// 解析默认 Topic 使用的平台标记。
        /// 非移动平台不生成平台 Topic，保持 Firebase Messaging 默认订阅只在 iOS 和 Android 生效。
        /// </summary>
        /// <returns>平台标记。</returns>
        private static string ResolveTopicPlatform()
        {
#if UNITY_IOS
            return "iOS";
#elif UNITY_ANDROID
            return "Android";
#else
            return string.Empty;
#endif
        }

        /// <summary>
        /// 解析默认 Topic 使用的开发模式。
        /// 开发模式来源于 Config Manager；Config 未加载完成时按 Debug 处理，避免误订阅正式分群。
        /// </summary>
        /// <returns>默认 Topic 使用的开发模式。</returns>
        private static DevelopMode ResolveDefaultTopicDevelopMode()
        {
            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null || !configManager.IsLoadOver)
            {
                return DevelopMode.Debug;
            }

            return configManager.DevelopMode;
        }

        /// <summary>
        /// 获取默认 Topic 状态使用的文件片段持久化管理器。
        /// 管理器不可用时返回 null，调用方仍可执行当前 Topic 订阅但不会保存状态。
        /// </summary>
        /// <returns>文件片段持久化管理器，获取失败时返回 null。</returns>
        private static IFileFragmentManager GetDefaultTopicPersistManager()
        {
            try
            {
                return FrameworkManagersGroup.GetManager<IFileFragmentManager>();
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Firebase, $"获取文件片段持久化管理器失败，默认推送 Topic 状态无法持久化：{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 保存基础默认 Topic 状态。
        /// 持久化管理器为空时只记录警告，不影响已完成的 Firebase 订阅操作。
        /// </summary>
        /// <param name="persistManager">文件片段持久化管理器。</param>
        /// <param name="state">待保存基础状态。</param>
        private static void SaveBaseTopicState(IFileFragmentManager persistManager, FirebaseTopicSubscriptionState state)
        {
            if (persistManager == null)
            {
                Log.Warning(LogTag.Firebase, "文件片段持久化管理器不可用，默认基础推送 Topic 状态未保存。");
                return;
            }

            persistManager.SetObject(c_DefaultTopicPersistClassify, c_BaseTopicStatePersistItem, state);
            persistManager.Save(c_DefaultTopicPersistClassify);
            Log.Debug(LogTag.Firebase, "默认基础推送 Topic 状态已保存。");
        }

        /// <summary>
        /// 保存国家默认 Topic 状态。
        /// 持久化管理器为空时只记录警告，不影响已完成的 Firebase 订阅操作。
        /// </summary>
        /// <param name="persistManager">文件片段持久化管理器。</param>
        /// <param name="state">待保存国家状态。</param>
        private static void SaveCountryTopicState(IFileFragmentManager persistManager, FirebaseCountryTopicSubscriptionState state)
        {
            if (persistManager == null)
            {
                Log.Warning(LogTag.Firebase, "文件片段持久化管理器不可用，默认国家推送 Topic 状态未保存。");
                return;
            }

            persistManager.SetObject(c_DefaultTopicPersistClassify, c_CountryTopicStatePersistItem, state);
            persistManager.Save(c_DefaultTopicPersistClassify);
            Log.Debug(LogTag.Firebase, $"默认国家推送 Topic 状态已保存：{state.Topic}。");
        }

        /// <summary>
        /// 应用 Topic 差异。
        /// 先退订旧 Topic，再订阅新 Topic；任一步失败都会返回失败结果，调用方不得覆盖持久化状态。
        /// </summary>
        /// <param name="diff">Topic 订阅差异。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>全部操作成功返回 Succeeded，否则返回失败原因。</returns>
        private async UniTask<TopicSubscriptionOperationResult> ApplyTopicDiffAsync(FirebaseTopicSubscriptionDiff diff, CancellationToken ct)
        {
            for (int i = 0; i < diff.UnsubscribeTopics.Count; i++)
            {
                TopicSubscriptionOperationResult result =
                    await SetTopicSubscriptionInternalAsync(diff.UnsubscribeTopics[i], false, ct);
                if (result != TopicSubscriptionOperationResult.Succeeded)
                {
                    return result;
                }
            }

            for (int i = 0; i < diff.SubscribeTopics.Count; i++)
            {
                TopicSubscriptionOperationResult result =
                    await SetTopicSubscriptionInternalAsync(diff.SubscribeTopics[i], true, ct);
                if (result != TopicSubscriptionOperationResult.Succeeded)
                {
                    return result;
                }
            }

            return TopicSubscriptionOperationResult.Succeeded;
        }

        /// <summary>
        /// 执行单个 Firebase Topic 的订阅或退订操作。
        /// 该方法会观察 Firebase 返回的 Task，APNs Token 未就绪时做有界重试，其他失败记录错误。
        /// </summary>
        /// <param name="topic">完整 Firebase Topic。</param>
        /// <param name="subscribed">true 表示订阅，false 表示退订。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>Topic 操作结果。</returns>
        private async UniTask<TopicSubscriptionOperationResult> SetTopicSubscriptionInternalAsync(
            string topic,
            bool subscribed,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                Log.Warning(LogTag.Firebase, "Firebase 推送 Topic 为空，订阅操作已跳过。");
                return TopicSubscriptionOperationResult.Failed;
            }

            if (!m_InitOver)
            {
                Log.Warning(LogTag.Firebase, $"Firebase 尚未初始化，无法{(subscribed ? "订阅" : "退订")}推送 Topic：{topic}。");
                return TopicSubscriptionOperationResult.Failed;
            }

            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
#if (UNITY_IOS || UNITY_ANDROID)
                    await WaitForFcmTokenAsync(ct);
#endif
#if (UNITY_IOS || UNITY_ANDROID)
                    if (subscribed)
                    {
                        await FirebaseMessaging.SubscribeAsync(topic);
                        Log.Debug(LogTag.Firebase, $"已订阅推送 Topic：{topic}。");
                    }
                    else
                    {
                        await FirebaseMessaging.UnsubscribeAsync(topic);
                        Log.Debug(LogTag.Firebase, $"已退订推送 Topic：{topic}。");
                    }
#else
                    Log.Debug(LogTag.Firebase, $"当前平台不执行 Firebase 推送 Topic 操作：{topic}。");
#endif
                    return TopicSubscriptionOperationResult.Succeeded;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (IsApnsTokenNotReadyException(ex))
                {
                    if (attempt >= s_TopicSubscriptionApnsRetryDelays.Length)
                    {
                        Log.Warning(LogTag.Firebase, $"Firebase 推送 Topic {(subscribed ? "订阅" : "退订")}等待 APNs Token 超时，本次操作未完成，等待后续同步或业务再次调用重试：{topic}，{ex.Message}");
                        return TopicSubscriptionOperationResult.ApnsTokenNotReady;
                    }

                    TimeSpan delay = s_TopicSubscriptionApnsRetryDelays[attempt];
                    Log.Warning(LogTag.Firebase, $"Firebase 推送 Topic {(subscribed ? "订阅" : "退订")}等待 APNs Token 就绪后重试：{topic}，第 {attempt + 1}/{s_TopicSubscriptionApnsRetryDelays.Length} 次，延迟 {delay.TotalSeconds:0} 秒。异常：{ex.Message}");
                    await UniTask.Delay(delay, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    Log.Error(LogTag.Firebase, $"Firebase 推送 Topic {(subscribed ? "订阅" : "退订")}失败：{topic}，{ex}");
                    return TopicSubscriptionOperationResult.Failed;
                }
            }
        }

        /// <summary>
        /// 判断异常是否为 Firebase iOS 原生层 APNs Token 尚未就绪。
        /// </summary>
        /// <param name="ex">待判断异常。</param>
        /// <returns>APNs Token 未就绪返回 true。</returns>
        private static bool IsApnsTokenNotReadyException(Exception ex)
        {
            while (ex != null)
            {
                if (!string.IsNullOrEmpty(ex.Message) &&
                    ex.Message.IndexOf(c_ApnsTokenNotReadyExceptionMessage, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                ex = ex.InnerException;
            }

            return false;
        }
    }
}
#endif
