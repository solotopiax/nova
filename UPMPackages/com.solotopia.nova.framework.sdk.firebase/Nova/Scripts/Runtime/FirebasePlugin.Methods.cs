/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/4/21
 * descrip:   FirebasePlugin私有方法
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using Firebase.Extensions;

using Firebase.Messaging;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    public sealed partial class FirebasePlugin
    {
        /// <summary>
        /// 异步初始化 Firebase SDK。
        /// 检查并修复 Firebase 依赖，注册 FCM Token 与消息回调，获取 Analytics 实例 ID。
        /// Firebase 通过 FirebaseApp.DefaultInstance 自主初始化；config 仅提供初始化完成后的框架侧行为开关。
        /// </summary>
        /// <param name="config">Firebase 插件运行时配置。</param>
        /// <param name="ct">取消令牌，Firebase 初始化链路不支持取消，此参数不使用。</param>
        /// <returns>初始化完成的异步任务。</returns>
        protected override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            try
            {
                m_FcmTokenReadySource = new UniTaskCompletionSource<string>();
                if (!string.IsNullOrEmpty(m_TokenReceived))
                {
                    m_FcmTokenReadySource.TrySetResult(m_TokenReceived);
                }

                m_ReportNetService = new FirebaseReportNetService();
                m_RuntimeConfig = config as FirebasePluginConfig;
                InitializePushTaskServices();
                m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();
                m_EventManager.Subscribe<SDKEventData.UserLogin>(OnUserLogin);
#if (UNITY_IOS || UNITY_ANDROID)
                Firebase.FirebaseApp.LogLevel = Firebase.LogLevel.Warning;
                var tempTask = Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
                TaskContinueWithOnMainThread(tempTask, (_) =>
                {
                    var dependencyStatus = tempTask.Result;
                    if (dependencyStatus == Firebase.DependencyStatus.Available)
                    {
                        FirebaseMessaging.TokenReceived += OnTokenReceived;
                        FirebaseMessaging.MessageReceived += OnMessageReceived;
                        m_InitOver = true;
                        m_PushTaskDispatcher?.SetFirebaseReady(true);
                        ApplyPendingUserIdIfReady();
                        RequestDefaultNotificationPermissionIfEnabled().Forget();
                        StartDefaultTopicSync();

                        Firebase.Analytics.FirebaseAnalytics.GetAnalyticsInstanceIdAsync().ContinueWithOnMainThread(idTask =>
                        {
                            if (idTask.IsCompleted && !string.IsNullOrEmpty(idTask.Result))
                            {
                                m_AnalyticsInstanceId = idTask.Result;
                                PublishData(SDKDataKeys.FirebaseAnalyticsInstanceId, m_AnalyticsInstanceId);
                                Log.Debug(LogTag.Firebase, $"AnalyticsInstanceId : {m_AnalyticsInstanceId} 。");
                            }
                        });
                        Log.Debug(LogTag.Firebase, "初始化完成。");
                    }
                    else
                    {
                        Log.Warning(LogTag.Firebase, $"初始化失败，依赖状态：{dependencyStatus}。");
                    }
                });
#endif
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Firebase, $"OnInitializeAsync 初始化异常：{e}");
            }

            return UniTask.CompletedTask;
        }

#if (UNITY_IOS || UNITY_ANDROID)
        /// <summary>
        /// 等待 FCM Token 就绪，避免 iOS 尚未收到 APNs Token 时提前触发 Firebase Topic 操作。
        /// </summary>
        /// <param name="ct">取消令牌；插件释放或调用方取消时结束等待。</param>
        /// <returns>FCM Token 已就绪后完成的异步任务。</returns>
        private async UniTask WaitForFcmTokenAsync(CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(m_TokenReceived))
            {
                return;
            }

            Log.Debug(LogTag.Firebase, "FCM Token 尚未就绪，等待 TokenReceived 后再执行推送 Topic 操作。");
            await m_FcmTokenReadySource.Task.AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 根据 Firebase 配置请求通知权限；默认开启，业务可在 ConfigMaster 关闭该行为。
        /// 请求结果只记录系统权威状态，不阻塞 Firebase 初始化完成回调。
        /// </summary>
        private async UniTaskVoid RequestDefaultNotificationPermissionIfEnabled()
        {
            if (m_RuntimeConfig == null || !m_RuntimeConfig.AutoRequestNotificationPermission)
            {
                return;
            }

            if (Nova.Native == null)
            {
                Log.Warning(LogTag.Firebase, "Firebase 默认请求通知权限失败，Nova.Native 不可用。");
                return;
            }

            try
            {
                NotificationPermissionResult result =
                    await Nova.Native.RequestNotificationPermissionAsync();
                if (result.IsOperationSuccessful)
                {
                    Log.Debug(LogTag.Firebase, $"Firebase 默认通知权限请求完成，系统状态：{result.Status}。");
                }
                else
                {
                    Log.Warning(LogTag.Firebase, $"Firebase 默认通知权限请求失败，系统状态：{result.Status}，错误：{result.ErrorMessage}。");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Firebase, $"Firebase 默认请求通知权限异常：{e}");
            }
        }
#endif

        /// <summary>
        /// 异步释放 Firebase SDK 资源。
        /// 反注册 FCM Token 与消息回调，防止释放后的悬挂引用。
        /// </summary>
        /// <param name="ct">取消令牌，Firebase 反注册为同步操作，此参数不使用。</param>
        /// <returns>释放完成的异步任务。</returns>
        protected override UniTask OnDisposeAsync(CancellationToken ct)
        {
            CancelPushTaskFlush();
            CancelDefaultTopicSync();
            m_FcmTokenReadySource.TrySetCanceled();
            if (m_EventManager != null)
            {
                m_EventManager.Unsubscribe<SDKEventData.UserLogin>(OnUserLogin);
                m_EventManager = null;
            }
#if (UNITY_IOS || UNITY_ANDROID)
            FirebaseMessaging.TokenReceived -= OnTokenReceived;
            FirebaseMessaging.MessageReceived -= OnMessageReceived;
#endif
            return UniTask.CompletedTask;
        }

#if (UNITY_IOS || UNITY_ANDROID)
        /// <summary>
        /// FCM Token 接收回调，切换到主线程后更新 Token 缓存并触发 OnTokenRefreshed 事件。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="token">Token 接收事件参数。</param>
        private void OnTokenReceived(object sender, TokenReceivedEventArgs token)
        {
            RunMainThread(() =>
            {
                m_TokenReceived = token.Token;
                if (!string.IsNullOrEmpty(m_TokenReceived))
                {
                    m_FcmTokenReadySource.TrySetResult(m_TokenReceived);
                }
                PublishData(SDKDataKeys.FirebasePushToken, m_TokenReceived);
                Log.Debug(LogTag.Firebase, $"收到推送Token：{token.Token}。");
                m_OnTokenRefreshed?.Invoke(new PushToken { Value = m_TokenReceived, Provider = "FCM" });
            });
        }

        /// <summary>
        /// FCM消息接收回调，切换到主线程后处理消息数据。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">消息接收事件参数。</param>
        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            RunMainThread(() => DoOnMessageClickedDatas(e.Message));
        }

        /// <summary>
        /// 处理推送消息点击数据，标记冷启动状态并对MessageId去重。
        /// </summary>
        /// <param name="message">Firebase推送消息对象。</param>
        private void DoOnMessageClickedDatas(FirebaseMessage message)
        {
            if (message == null || !message.NotificationOpened)
            {
                return;
            }
            m_IsNotificationLaunch = true;
            if (!m_RuntimeReceivedMessageIDs.Contains(message.MessageId))
            {
                m_RuntimeReceivedMessageIDs.Add(message.MessageId);

     
                // GM 后台创建的任务 ID
                string pushTaskId = string.Empty;
                if (message.Data != null && message.Data.Count > 0 && message.Data.TryGetValue("push_task_id", out string messagePushTaskId))
                {
                    pushTaskId = messagePushTaskId;
                }

                // 对应协议上传的 task_key 
                string pushTaskKey = string.Empty;
                if (message.Data != null && message.Data.Count > 0 && message.Data.TryGetValue("task_key", out string messagePushTaskKey))
                {
                    pushTaskKey = messagePushTaskKey;
                }

                // GM 后台创建的模版 ID
                string templateId = string.Empty;
                if (message.Data != null && message.Data.Count > 0 && message.Data.TryGetValue("template_id", out string messageTemplateId))
                {
                    templateId = messageTemplateId;
                }
                 
                SDKComponent sdkComponent = FrameworkComponentsGroup.GetComponent<SDKComponent>();
                if (sdkComponent != null && sdkComponent.TryGet<ITrackPlugin>(out ITrackPlugin trackPlugin))
                {
                    trackPlugin.TrackEvent("nova_firebase_fcm_click", new Dictionary<string, object>
                    {
                        { "nova_firebase_fcm_message_id", message.MessageId },
                        { "nova_firebase_push_task_id", pushTaskId },
                        { "nova_firebase_push_task_key", pushTaskKey },
                        { "nova_firebase_template_id", templateId },
                    });
                }
                Log.Debug(LogTag.Firebase, $"推送点击，MessageId：{message.MessageId}。");
            }
        }

        /// <summary>
        /// 等待Task完成后切换到主线程执行回调。
        /// </summary>
        /// <param name="task">要等待的异步任务。</param>
        /// <param name="callBack">任务完成后在主线程执行的回调。</param>
        private async void TaskContinueWithOnMainThread(Task task, Action<Task> callBack)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Firebase, $"TaskContinueWithOnMainThread 等待任务异常：{e}");
                return;
            }

            if (!task.IsCompletedSuccessfully)
            {
                return;
            }

            await UniTask.SwitchToMainThread();
            try
            {
                callBack(task);
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Firebase, $"TaskContinueWithOnMainThread 回调异常：{e}");
            }
        }

        /// <summary>
        /// 将指定Action切换到Unity主线程执行。
        /// </summary>
        /// <param name="action">要在主线程执行的委托。</param>
        private void RunMainThread(Action action)
        {
            UniTask.Post(action);
        }
#endif

#if !UNITY_WEBGL
        /// <summary>
        /// 在 Firebase 初始化完成后应用已记录的用户 ID，并通知 push task 调度器用户身份是否就绪。
        /// </summary>
        private void ApplyPendingUserIdIfReady()
        {
            if (!m_InitOver)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(m_PendingUserId))
            {
                return;
            }

#if (UNITY_IOS || UNITY_ANDROID)
            Firebase.Analytics.FirebaseAnalytics.SetUserId(m_PendingUserId);
            if (Firebase.Crashlytics.Crashlytics.IsCrashlyticsCollectionEnabled)
            {
                Firebase.Crashlytics.Crashlytics.SetUserId(m_PendingUserId);
            }
#endif
            m_PushTaskDispatcher?.SetUserReady();
        }

        /// <summary>
        /// SDKEventData.UserLogin 事件处理器；调用 Firebase 的 SetUserId 同步用户身份，
        /// 然后以 Fire-and-Forget 方式触发 ReportOnLoginAsync 走异步上报流程。
        /// SetUserId 会记录用户 ID；若 Firebase 尚未初始化完成，会在初始化成功后补同步。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件数据，期望为 SDKEventData.UserLogin。</param>
        private void OnUserLogin(object sender, EventData e)
        {
            if (!(e is SDKEventData.UserLogin login))
            {
                return;
            }
            SetUserId(login.UserId);
            ReportOnLoginAsync().Forget();
        }

        /// <summary>
        /// 登录后异步上报 Firebase 标识至服务端：先 await FetchDataAsync 等待 FirebasePushToken / FirebaseAnalyticsInstanceId
        /// 数据槽位就绪，再解析国家码和当前时区作为协议参数，与 AppsFlyerPlugin / TGAPlugin 的 ReportOnLoginAsync 同构。
        /// 把"初始化结果"与"登录结果"统一为"先 await 拿值、再用值"的可等待过程。
        /// 数据槽位由本插件自身发布（FirebasePushToken 在 OnTokenReceived 主线程回调，
        /// FirebaseAnalyticsInstanceId 在 GetAnalyticsInstanceIdAsync 主线程回调）；
        /// m_ReportNetService 或 m_RuntimeConfig 为 null（守卫早返回路径）时静默跳过；
        /// CancellationToken 暂用 default；OperationCanceledException 静默吞，其他异常仅记日志不上抛。
        /// </summary>
        /// <returns>UniTaskVoid，专用于 Fire-and-Forget 调用。</returns>
        private async UniTaskVoid ReportOnLoginAsync()
        {
            if (m_RuntimeConfig == null)
            {
                Log.Error(LogTag.Firebase, $"ReportOnLoginAsync 上报 Firebase 标识失败，m_RuntimeConfig 为 null。");
                return;
            }

            try
            {
                object pushTokenObj = await FetchDataAsync(SDKDataKeys.FirebasePushToken, default);
                object instanceIdObj = await FetchDataAsync(SDKDataKeys.FirebaseAnalyticsInstanceId, default);
                string pushToken = pushTokenObj as string ?? string.Empty;
                string instanceId = instanceIdObj as string ?? string.Empty;
                string resolvedCountryCode = await ResolveFirebaseCountryCodeAsync(default);
                string country = FirebaseDefaultTopicBuilder.NormalizeReportCountryCode(resolvedCountryCode);
                TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
                string timezoneOffset = FirebaseDefaultTopicBuilder.FormatReportTimezoneOffset(utcOffset);
                m_ReportNetService.Async(m_RuntimeConfig.ReportCmdName, pushToken, instanceId, country, timezoneOffset).Forget();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Error(LogTag.Firebase, $"ReportOnLoginAsync 上报异常：{ex}");
            }
        }
#endif
    }
}
#endif
