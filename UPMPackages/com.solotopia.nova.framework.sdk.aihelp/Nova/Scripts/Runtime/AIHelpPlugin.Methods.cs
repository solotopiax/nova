/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AIHelpPlugin.Methods.cs
 * author:    taoye
 * created:   2026/7/9
 * descrip:   AIHelpPlugin 私有接线：自动订阅框架登录事件同步用户；桥接 vendor
 *            异步事件监听为插件公开 C# event；域名解析辅助方法。引用 vendor
 *            一律 global::AIHelp.*。
 *            注：vendor AIHelpDelegate.AsyncEventListener 实际签名为
 *            (string jsonEventData, Action<string> acknowledge)，并非按 EventType
 *            回传 int + string；vendor EventType 亦无 MESSAGE_COUNT_ARRIVE 成员，
 *            故事件桥接改用 EventType.MessageArrival / EventType.UnreadTaskCount，
 *            公开 event 参数为 vendor 回传的原始 JSON 字符串，交由业务自行解析。
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AIHelp.Runtime
{
    public sealed partial class AIHelpPlugin
    {
        /// <summary>
        /// 拉取框架 EventManager 并订阅 SDKEventData.UserLogin。
        /// 订阅后用户登录时触发 OnUserLogin，自动把 uid 同步给 AIHelp。
        /// </summary>
        private void SubscribeEvents()
        {
            m_EventManager = FrameworkManagersGroup.GetManager<IEventManager>();
            m_EventManager.Subscribe<SDKEventData.UserLogin>(OnUserLogin);
        }

        /// <summary>
        /// SDKEventData.UserLogin 处理器：取已登录用户 ID 后自动以 uid 同步登录到 AIHelp。
        /// 富参（name/serverId/tags）由业务按需显式调用 Login 重载。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件数据，期望为 SDKEventData.UserLogin。</param>
        private void OnUserLogin(object sender, EventData e)
        {
            if (!(e is SDKEventData.UserLogin login) || string.IsNullOrEmpty(login.UserId))
            {
                return;
            }
            Login(login.UserId);
        }

        /// <summary>
        /// 注册 vendor 异步事件监听：把消息到达 / 未读工单数变化桥接为插件公开 event。
        /// </summary>
        private void RegisterAIHelpEventListeners()
        {
            global::AIHelp.AIHelpSupport.RegisterAsyncEventListener(global::AIHelp.EventType.MessageArrival, OnAIHelpMessageArrival);
            global::AIHelp.AIHelpSupport.RegisterAsyncEventListener(global::AIHelp.EventType.UnreadTaskCount, OnAIHelpUnreadTaskCount);
        }

        /// <summary>
        /// 注销 vendor 异步事件监听。
        /// </summary>
        private void UnregisterAIHelpEventListeners()
        {
            global::AIHelp.AIHelpSupport.UnregisterAsyncEventListener(global::AIHelp.EventType.MessageArrival);
            global::AIHelp.AIHelpSupport.UnregisterAsyncEventListener(global::AIHelp.EventType.UnreadTaskCount);
        }

        /// <summary>
        /// vendor 消息到达事件回调：原样转发 JSON 到 OnMessageArrived。本插件不需要向 vendor
        /// 回执，忽略 acknowledge。
        /// </summary>
        /// <param name="jsonEventData">vendor 回传的原始事件 JSON。</param>
        /// <param name="acknowledge">vendor 提供的回执回调，本事件无需回执。</param>
        private void OnAIHelpMessageArrival(string jsonEventData, Action<string> acknowledge)
        {
            // vendor 在 Android JNI 线程（非 Unity 主线程）同步触发本回调，此处经 UniTask.Post
            // 切回主线程再抛公开事件，避免消费方在非主线程操作 Unity UI / 图形（如 TMP）导致
            // "Graphics device is null" / NullReferenceException 崩溃。
            UniTask.Post(() => OnMessageArrived?.Invoke(jsonEventData));
        }

        /// <summary>
        /// vendor 未读工单数事件回调：原样转发 JSON 到 OnUnreadTaskCountChanged。本插件不需要向
        /// vendor 回执，忽略 acknowledge。
        /// </summary>
        /// <param name="jsonEventData">vendor 回传的原始事件 JSON。</param>
        /// <param name="acknowledge">vendor 提供的回执回调，本事件无需回执。</param>
        private void OnAIHelpUnreadTaskCount(string jsonEventData, Action<string> acknowledge)
        {
            // 同 OnAIHelpMessageArrival：vendor 在 Android JNI 线程触发，经 UniTask.Post 切回主线程再抛事件。
            UniTask.Post(() => OnUnreadTaskCountChanged?.Invoke(jsonEventData));
        }

        /// <summary>
        /// 从完整 URL 中提取纯域名：去掉开头的 https:// 或 http:// scheme，并截断第一个 '/' 之后的路径部分。
        /// </summary>
        /// <param name="url">完整 URL，可能为空。</param>
        /// <returns>纯域名（host），url 为空或解析不出域名时返回空串。</returns>
        private static string ExtractDomain(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            string host = url;
            if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                host = host.Substring("https://".Length);
            }
            else if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                host = host.Substring("http://".Length);
            }

            int slashIndex = host.IndexOf('/');
            if (slashIndex >= 0)
            {
                host = host.Substring(0, slashIndex);
            }

            return host;
        }
    }
}
