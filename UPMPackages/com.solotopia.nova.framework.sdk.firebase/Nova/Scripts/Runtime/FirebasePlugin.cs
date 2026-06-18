/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePlugin.cs
 * author:    yingzheng
 * created:   2026/4/21
 * descrip:   Firebase SDK插件主文件（public/override方法）
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
#if (UNITY_IOS || UNITY_ANDROID)
using Firebase.Analytics;
using Firebase.Messaging;
using Firebase.Extensions;
#endif

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase SDK 插件，继承 SDKPluginBase，实现 Analytics 埋点（IMonetizeTrackPlugin）与 FCM 推送（IPushPlugin）契约。
    /// 负责 Firebase 初始化、Analytics 事件上报、FCM Token 接收及推送主题订阅管理。
    /// </summary>
    public sealed partial class FirebasePlugin : SDKPluginBase, IMonetizeTrackPlugin, IPushPlugin
    {
        /// <summary>
        /// 上报携带自定义参数的埋点事件。
        /// eventName 为空或 null 时静默返回；parameters 为 null 或空时走无参路径。
        /// 数值类型转换为 double，其余类型转换为 string。
        /// </summary>
        /// <param name="eventName">事件名称，不可为空。</param>
        /// <param name="parameters">事件附加参数字典；可为 null，为 null 或空时等同于无参上报。</param>
        public void TrackEvent(string eventName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                return;
            }
            if (!m_InitOver)
            {
                return;
            }
#if (UNITY_IOS || UNITY_ANDROID)
            if (parameters == null || parameters.Count == 0)
            {
                FirebaseAnalytics.LogEvent(eventName);
                return;
            }
            var paramList = new List<Parameter>();
            foreach (var pair in parameters)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                {
                    continue;
                }
                if (pair.Value is double || pair.Value is float || pair.Value is int || pair.Value is long)
                {
                    paramList.Add(new Parameter(pair.Key, System.Convert.ToDouble(pair.Value)));
                }
                else
                {
                    paramList.Add(new Parameter(pair.Key, pair.Value.ToString()));
                }
            }
            FirebaseAnalytics.LogEvent(eventName, paramList.ToArray());
#endif
        }

        /// <summary>
        /// 上报 ITrackPlugin 统一载荷格式的自定义埋点事件。
        /// Name 为空或 null 时静默返回；数值类型参数转换为 double，其余转换为 string。
        /// </summary>
        /// <param name="evt">事件载荷，Name 不可为空；Parameters 可为 null。</param>
        public void TrackEvent(TrackEvent evt)
        {
            if (evt == null || string.IsNullOrEmpty(evt.Name))
            {
                return;
            }
            if (!m_InitOver)
            {
                return;
            }
#if (UNITY_IOS || UNITY_ANDROID)
            if (evt.Parameters == null || evt.Parameters.Count == 0)
            {
                FirebaseAnalytics.LogEvent(evt.Name);
                return;
            }
            var paramList = new List<Parameter>();
            foreach (var pair in evt.Parameters)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                {
                    continue;
                }
                if (pair.Value is double || pair.Value is float || pair.Value is int || pair.Value is long)
                {
                    paramList.Add(new Parameter(pair.Key, System.Convert.ToDouble(pair.Value)));
                }
                else
                {
                    paramList.Add(new Parameter(pair.Key, pair.Value.ToString()));
                }
            }
            FirebaseAnalytics.LogEvent(evt.Name, paramList.ToArray());
#endif
        }

        /// <summary>
        /// 设置用户属性键值对，上报至 Firebase Analytics。
        /// SDK 未初始化或参数为空时静默返回。
        /// </summary>
        /// <param name="key">属性键名，不可为空。</param>
        /// <param name="value">属性值，不可为 null。</param>
        public void SetUserProperty(string key, string value)
        {
            if (!m_InitOver)
            {
                return;
            }
#if (UNITY_IOS || UNITY_ANDROID)
            FirebaseAnalytics.SetUserProperty(key, value);
#endif
        }

        /// <summary>
        /// 设置当前用户的唯一标识，同步至 Analytics 与 Crashlytics。
        /// </summary>
        /// <param name="userId">用户唯一标识。</param>
        public void SetUserId(string userId)
        {
            if (!m_InitOver)
            {
                return;
            }
#if (UNITY_IOS || UNITY_ANDROID)
            FirebaseAnalytics.SetUserId(userId);
            if (Firebase.Crashlytics.Crashlytics.IsCrashlyticsCollectionEnabled)
            {
                Firebase.Crashlytics.Crashlytics.SetUserId(userId);
            }
#endif
        }

        /// <summary>
        /// 推送令牌刷新事件，在 FCM 下发新 Token 时于主线程触发。
        /// </summary>
        public event Action<PushToken> OnTokenRefreshed
        {
            add { m_OnTokenRefreshed += value; }
            remove { m_OnTokenRefreshed -= value; }
        }

        /// <summary>
        /// 异步获取当前设备的 FCM 推送令牌。
        /// 令牌未就绪时等待直至收到或取消；就绪后立即返回带 FCM Provider 的 PushToken。
        /// </summary>
        /// <param name="ct">取消令牌，默认不取消。</param>
        /// <returns>推送令牌，含 Provider="FCM" 与令牌字符串。</returns>
        public async UniTask<PushToken> GetTokenAsync(CancellationToken ct = default)
        {
            await UniTask.WaitUntil(() => !string.IsNullOrEmpty(m_TokenReceived), cancellationToken: ct);
            return new PushToken { Value = m_TokenReceived, Provider = "FCM" };
        }

        /// <summary>
        /// 订阅或取消订阅指定 FCM 推送主题。
        /// 内部委托 SubscribeAsync / UnsubscribeAsync 执行；SDK 未初始化时由委托方法记录 Warning 并返回。
        /// </summary>
        /// <param name="topic">主题名称，由业务层定义。</param>
        /// <param name="subscribed">true 订阅，false 取消订阅。</param>
        public void SetTopicSubscribed(string topic, bool subscribed)
        {
            if (subscribed)
            {
                SubscribeAsync(topic);
            }
            else
            {
                UnsubscribeAsync(topic);
            }
        }

        /// <summary>
        /// 启用或禁用 Firebase Analytics 数据收集。
        /// </summary>
        /// <param name="enabled">为 true 时启用，为 false 时禁用。</param>
        public void SetAnalyticsEnabled(bool enabled)
        {
            if (!m_InitOver)
            {
                return;
            }
#if (UNITY_IOS || UNITY_ANDROID)
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(enabled);
#endif
        }

        /// <summary>
        /// 同步读取 Firebase Cloud Messaging 推送 Token 缓存值。
        /// </summary>
        /// <returns>FCM 推送 Token 字符串，未收到时返回空字符串。</returns>
        public string GetToken()
        {
            return m_TokenReceived;
        }

        /// <summary>
        /// 获取 Firebase Analytics 分析实例 ID。
        /// </summary>
        /// <returns>Analytics 实例 ID 字符串，未初始化时返回空字符串。</returns>
        public string GetAnalyticsInstanceId()
        {
            return m_AnalyticsInstanceId;
        }

        /// <summary>
        /// 订阅指定 Firebase Cloud Messaging 推送主题（异步发起，不等待结果）。
        /// </summary>
        /// <param name="topic">要订阅的主题名称。</param>
        public void SubscribeAsync(string topic)
        {
            if (!m_InitOver)
            {
                Log.Warning(LogTag.Firebase, $"SubscribeAsync：SDK 尚未初始化，无法订阅主题：{topic}。");
                return;
            }
#if (UNITY_IOS || UNITY_ANDROID)
            FirebaseMessaging.SubscribeAsync(topic);
#endif
        }

        /// <summary>
        /// 取消订阅指定 Firebase Cloud Messaging 推送主题（异步发起，不等待结果）。
        /// </summary>
        /// <param name="topic">要取消订阅的主题名称。</param>
        public void UnsubscribeAsync(string topic)
        {
            if (!m_InitOver)
            {
                Log.Warning(LogTag.Firebase, $"UnsubscribeAsync：SDK 尚未初始化，无法取消订阅主题：{topic}。");
                return;
            }
#if (UNITY_IOS || UNITY_ANDROID)
            FirebaseMessaging.UnsubscribeAsync(topic);
#endif
        }
    }
}
#endif
