/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoNetworkView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.9 — Network 模块演示视图（交互触发型）。
 *            演示 HTTP GET/POST 与独占 WebSocket 二进制回显确认。
 *            API：Nova.Network.GetAsync / PostAsync / IWebSocketManager
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Network 模块演示视图，演示 HTTP GET/POST 与独占 WebSocket 二进制回显确认。
    /// 继承 BaseDemoView 三段式骨架，交互区包含 URL 输入框与 Get/Post/Connect 按钮。
    /// </summary>
    public sealed class DemoNetworkView : BaseDemoView
    {
        /// <summary>
        /// GET URL 输入框，默认值为 Postman Echo 的最小回显示例。
        /// </summary>

        [SerializeField] private TMP_InputField m_UrlInput;

        /// <summary>
        /// HTTP GET 请求按钮。
        /// </summary>

        [SerializeField] private Button m_GetButton;

        /// <summary>
        /// HTTP POST 请求按钮。
        /// </summary>

        [SerializeField] private Button m_PostButton;

        /// <summary>
        /// WebSocket 二进制回显按钮：连接成功发送，已连接时再次发送；断线后再次点击重连。
        /// </summary>

        [SerializeField] private Button m_ConnectButton;

        /// <summary>
        /// 响应内容预览文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_ResponsePreview;

        /// <summary>
        /// 视图已关闭标志，防止异步回调在关闭后写入 UI。
        /// </summary>
        private bool m_IsClosed = true;

        /// <summary>本次打开共同拥有的HTTP取消令牌源，关闭后取消旧请求并释放令牌源。</summary>
        private CancellationTokenSource m_RequestCts;

        /// <summary>打开和关闭时递增，旧请求不能在视图重开后写入新一代 UI。</summary>
        private int m_ViewGeneration;

        /// <summary>弱键标记只记本 Sample 曾创建的通道及当前租约，不延长 Manager、通道或视图寿命。</summary>
        private static readonly ConditionalWeakTable<WebSocketScope.NetChannelBase, WebSocketEchoLease> s_WebSocketLeases = new();

        /// <summary>租约仅持有不引用视图的身份令牌；空 Owner 表示 Sample 已释放，未知通道不接管。</summary>
        private sealed class WebSocketEchoLease
        {
            public object Owner;
        }

        /// <summary>本视图唯一的会话令牌，关闭和重开之间仍不与其它视图共享所有权。</summary>
        private readonly object m_WebSocketOwner = new();
        private IWebSocketManager m_WebSocketManager;
        private WebSocketScope.NetChannelBase m_WebSocketChannel;
        private string m_WebSocketAddress;
        private bool m_WebSocketConnecting;
        private int m_WebSocketAttempt;
        private int m_WebSocketPendingSequence;
        private string m_WebSocketPendingNonce;

        /// <summary>Sample 专用 echo 协议号，不使用框架认证/心跳协议号；只将完整回显视为本次 ack。</summary>
        private const int c_WebSocketEchoCommandId = 790001;
        private const float c_WebSocketEchoTimeoutSeconds = 10f;

        /// <summary>
        /// 默认 GET 演示地址。
        /// </summary>
        private const string c_DefaultGetUrl = "https://httpbin.org/get?message=hello";

        /// <summary>
        /// 固定 POST 演示地址。
        /// </summary>
        private const string c_DefaultPostUrl = "https://httpbin.org/post";

        /// <summary>
        /// 固定 WebSocket 连接演示地址。
        /// </summary>
        private const string c_DefaultWebSocketUrl = "wss://ws.postman-echo.com/raw";

        /// <summary>
        /// 视图初始化：注册按钮事件，设置标题与 API 副标题，填充默认 URL。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Network 演示");

            if (m_UrlInput != null)
            {
                m_UrlInput.text = c_DefaultGetUrl;
            }

            if (m_GetButton != null)
            {
                m_GetButton.onClick.AddListener(OnGetButtonClick);
                SetButtonApiHint(m_GetButton, "Nova.Network.GetAsync(url)");
            }

            if (m_PostButton != null)
            {
                m_PostButton.onClick.AddListener(OnPostButtonClick);
                SetButtonApiHint(m_PostButton, "Nova.Network.PostAsync(url, body)");
            }

            if (m_ConnectButton != null)
            {
                m_ConnectButton.onClick.AddListener(OnConnectButtonClick);
                SetButtonApiHint(m_ConnectButton, "ws/wss URL → Connect / echo ack / reconnect");
            }

        }

        /// <summary>
        /// 打开时建立新的视图代际并清空响应预览，旧请求不能更新本次 UI。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            CancelRequests();
            m_RequestCts = new CancellationTokenSource();
            m_IsClosed = false;
            m_ViewGeneration++;

            if (m_ResponsePreview != null)
            {
                m_ResponsePreview.text = string.Empty;
            }
        }

        /// <summary>
        /// 关闭时使当前代际失效，退订并断开自有 WebSocket，取消 HTTP 与 echo 等待；HTTP 响应仍由原 finally 唯一归池。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_IsClosed = true;
            m_ViewGeneration++;
            CancelRequests();
            base.OnClose(isShutdown, userData);
        }

        /// <summary>销毁时使视图代际失效，退订断开自有 WebSocket 并取消本轮等待；物理关闭由框架完成，释放终态后才可重用。</summary>
        private void OnDestroy()
        {
            m_IsClosed = true;
            m_ViewGeneration++;
            CancelRequests();
        }

        /// <summary>释放本视图独占的长连接并取消 HTTP/echo 等待；池化 HTTP 响应继续由原 finally 归还。</summary>
        private void CancelRequests()
        {
            ReleaseWebSocket();
            CancellationTokenSource cancellation = m_RequestCts;
            m_RequestCts = null;
            if (cancellation == null) return;
            try
            {
                cancellation.Cancel();
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        /// <summary>
        /// GET 按钮点击：发送 HTTP GET 请求并将状态码与响应长度写入反馈区。
        /// </summary>
        private void OnGetButtonClick()
        {
            ExecuteGetAsync().Forget();
        }

        /// <summary>
        /// POST 按钮点击：发送 HTTP POST 请求并将结果写入反馈区。
        /// </summary>
        private void OnPostButtonClick()
        {
            ExecutePostAsync().Forget();
        }

        /// <summary>用现有 URL 的 ws/wss 值连接；HTTP 输入沿用原公开 echo 默认值，不更改 HTTP 按钮语义。</summary>
        private void OnConnectButtonClick()
        {
            if (m_IsClosed || m_RequestCts == null) return;
            IWebSocketManager manager = Nova.Network != null ? Nova.Network.WebSocketManager : null;
            if (manager == null)
            {
                AppendFeedback("WebSocket Manager 不可用", FeedbackLevel.Error);
                return;
            }
            string input = m_UrlInput != null ? m_UrlInput.text?.Trim() ?? string.Empty : string.Empty;
            string address = c_DefaultWebSocketUrl;
            if (input.StartsWith("ws:", StringComparison.OrdinalIgnoreCase) || input.StartsWith("wss:", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(input, UriKind.Absolute, out Uri uri) || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
                {
                    AppendFeedback("请输入不带账号密码的有效 ws/wss URL", FeedbackLevel.Error);
                    return;
                }
                address = uri.AbsoluteUri;
            }
            if (!ReferenceEquals(manager, m_WebSocketManager) || address != m_WebSocketAddress) ReleaseWebSocket();
            if (!TryFindWebSocketChannel(manager, address, out WebSocketScope.NetChannelBase channel))
            {
                AppendFeedback("同地址有多个通道，不能确定唯一所有者", FeedbackLevel.Error);
                return;
            }
            if (m_WebSocketManager == null)
            {
                if (channel != null && (!s_WebSocketLeases.TryGetValue(channel, out WebSocketEchoLease lease) || lease.Owner != null ||
                    channel.IsConnected || !channel.IsDisconnectedSubjectively || !IsWebSocketFullyClosed(channel)))
                {
                    AppendFeedback("通道忙碌、尚未清场或归其它会话所有，不能接管或断开", FeedbackLevel.Warn);
                    return;
                }
                m_WebSocketManager = manager;
                m_WebSocketAddress = address;
                m_WebSocketChannel = channel;
                if (channel != null) s_WebSocketLeases.GetValue(channel, _ => new WebSocketEchoLease()).Owner = m_WebSocketOwner;
                manager.OnConnectSuccess += OnWebSocketConnected;
                manager.OnConnectFail += OnWebSocketConnectFailed;
                manager.OnDisconnect += OnWebSocketDisconnected;
                manager.OnReceiveMessage += OnWebSocketReceived;
            }
            else if (!ReferenceEquals(channel, m_WebSocketChannel) || !OwnsWebSocketChannel())
            {
                AppendFeedback("通道身份已变化，请关闭后重新打开视图", FeedbackLevel.Error);
                return;
            }
            if (m_WebSocketConnecting || m_WebSocketPendingNonce != null)
            {
                AppendFeedback(m_WebSocketConnecting ? "正在连接或等待通道就绪" : "正在等待本次 echo ack", FeedbackLevel.Info);
                return;
            }
            if (channel != null && channel.IsConnected)
            {
                if (!channel.IsAuthenticatedSuccess)
                {
                    AppendFeedback("通道尚未就绪，本次没有发送 echo", FeedbackLevel.Warn);
                    return;
                }
                SendWebSocketEcho();
                return;
            }
            m_WebSocketConnecting = true;
            m_WebSocketAttempt++;
            try
            {
                if (channel == null)
                {
                    manager.ConnectServer(WebSocketScope.NetChannelType.Tcp, address, false);
                    if (!TryFindWebSocketChannel(manager, address, out channel) || channel == null) throw new InvalidOperationException();
                    m_WebSocketChannel = channel;
                    s_WebSocketLeases.GetValue(channel, _ => new WebSocketEchoLease()).Owner = m_WebSocketOwner;
                }
                else manager.ReconnectServer(WebSocketScope.NetChannelType.Tcp, address);
                AppendFeedback("WebSocket echo 已发起连接；不代表消息已收到", FeedbackLevel.Info);
            }
            catch (Exception exception)
            {
                ReleaseWebSocket();
                AppendFeedback("WebSocket 连接失败：" + exception.GetType().Name, FeedbackLevel.Error);
            }
        }

        /// <summary>精确查找同类型同地址通道；重复地址拒绝，避免 Manager 的首项路由误触其它会话。</summary>
        private static bool TryFindWebSocketChannel(IWebSocketManager manager, string address, out WebSocketScope.NetChannelBase result)
        {
            result = null;
            foreach (WebSocketScope.NetChannelBase channel in manager.NetChannels)
            {
                if (!channel.Equals(WebSocketScope.NetChannelType.Tcp, address)) continue;
                if (result != null) return false;
                result = channel;
            }
            return true;
        }

        /// <summary>仅使用公开释放终态，拒绝尚在异步关闭的旧通道；内部 IsClosing 在关闭后仍为 true，不能作复用条件。</summary>
        private static bool IsWebSocketFullyClosed(WebSocketScope.NetChannelBase channel)
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            return channel.WebSocketInstanceID == 0;
#else
            return channel.WebSocket == null;
#endif
        }

        /// <summary>只有完整租约与当前唯一通道身份均匹配，才允许发送或主动断开。</summary>
        private bool OwnsWebSocketChannel() => m_WebSocketManager != null && m_WebSocketChannel != null &&
            s_WebSocketLeases.TryGetValue(m_WebSocketChannel, out WebSocketEchoLease lease) && ReferenceEquals(lease.Owner, m_WebSocketOwner) &&
            TryFindWebSocketChannel(m_WebSocketManager, m_WebSocketAddress, out WebSocketScope.NetChannelBase current) && ReferenceEquals(current, m_WebSocketChannel);

        /// <summary>匹配公开事件的索引及地址，旧会话或其它通道不能影响本视图。</summary>
        private bool IsCurrentWebSocketEvent(int index, string address) => !m_IsClosed && OwnsWebSocketChannel() &&
            address == m_WebSocketAddress && index >= 0 && index < m_WebSocketManager.NetChannels.Count &&
            ReferenceEquals(m_WebSocketManager.NetChannels[index], m_WebSocketChannel);

        /// <summary>连接成功只开始等待公开认证状态；不在认证协程前把业务消息放入离线缓冲。</summary>
        private void OnWebSocketConnected(int index, string address)
        {
            if (!IsCurrentWebSocketEvent(index, address) || m_RequestCts == null) return;
            WaitForWebSocketReadyAsync(m_WebSocketAttempt, m_ViewGeneration, m_RequestCts.Token).Forget();
        }

        /// <summary>等待当前连接就绪，超时或关闭只结束本次等待；不重初始化、不替换框架认证策略。</summary>
        private async UniTask WaitForWebSocketReadyAsync(int attempt, int generation, CancellationToken token)
        {
            float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, m_WebSocketManager.AuthenticateTimeout);
            try
            {
                while (this != null && !m_IsClosed && generation == m_ViewGeneration && attempt == m_WebSocketAttempt && OwnsWebSocketChannel())
                {
                    if (m_WebSocketChannel.IsAuthenticatedSuccess)
                    {
                        m_WebSocketConnecting = false;
                        SendWebSocketEcho();
                        return;
                    }
                    if (!m_WebSocketChannel.IsConnected || Time.realtimeSinceStartup >= deadline) break;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
                if (this != null && !m_IsClosed && generation == m_ViewGeneration && attempt == m_WebSocketAttempt)
                {
                    m_WebSocketConnecting = false;
                    AppendFeedback("WebSocket 未就绪；本次没有发送 echo", FeedbackLevel.Warn);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>连接失败保留自身通道供下一次显式点击重连，不自动重放消息。</summary>
        private void OnWebSocketConnectFailed(int index, string address)
        {
            if (!IsCurrentWebSocketEvent(index, address)) return;
            m_WebSocketAttempt++;
            m_WebSocketConnecting = false;
            m_WebSocketPendingNonce = null;
            AppendFeedback("WebSocket 连接失败；再次点击可重连", FeedbackLevel.Warn);
        }

        /// <summary>实际断开使本次 ack 失效；只有用户下一次点击才重连，不占用其它会话。</summary>
        private void OnWebSocketDisconnected(int index, string address)
        {
            if (!IsCurrentWebSocketEvent(index, address)) return;
            m_WebSocketAttempt++;
            m_WebSocketConnecting = false;
            m_WebSocketPendingNonce = null;
            AppendFeedback("WebSocket 已断开；再次点击可重连", FeedbackLevel.Warn);
        }

        /// <summary>只发送生成的 nonce；true 转移消息所有权，false 或发送前异常由当前调用唯一回池。</summary>
        private void SendWebSocketEcho()
        {
            if (m_IsClosed || !OwnsWebSocketChannel() || !m_WebSocketChannel.IsAuthenticatedSuccess || m_WebSocketPendingNonce != null) return;
            IWebSocketManager manager = m_WebSocketManager;
            WebSocketScope.NetMessageBase owned = null;
            try
            {
                owned = manager.CreateMessage(WebSocketScope.NetChannelType.Tcp);
                if (!(owned is WebSocketScope.TcpMessage message)) throw new InvalidOperationException();
                string nonce = Guid.NewGuid().ToString("N");
                message.Initialize(m_WebSocketChannel, c_WebSocketEchoCommandId, new List<string> { nonce });
                int sequence = message.SequenceID;
                m_WebSocketPendingSequence = sequence;
                m_WebSocketPendingNonce = nonce;
                if (!manager.SendMessage(WebSocketScope.NetChannelType.Tcp, m_WebSocketAddress, message))
                {
                    m_WebSocketPendingNonce = null;
                    AppendFeedback("WebSocket 未接受发送；消息已由本次调用回收", FeedbackLevel.Warn);
                    return;
                }
                owned = null;
                AppendFeedback("WebSocket echo 已入队 seq=" + sequence + "；等待匹配 ack", FeedbackLevel.Info);
                WaitForWebSocketAckTimeoutAsync(nonce, m_ViewGeneration, m_RequestCts.Token).Forget();
            }
            catch (Exception exception)
            {
                m_WebSocketPendingNonce = null;
                AppendFeedback("WebSocket 发送失败：" + exception.GetType().Name, FeedbackLevel.Error);
            }
            finally
            {
                if (owned != null) manager.RecycleMessage(owned);
            }
        }

        /// <summary>仅复制框架回调中的值；框架随后回池，业务不缓存或再次回收接收消息。</summary>
        private void OnWebSocketReceived(WebSocketScope.NetChannelBase channel, WebSocketScope.NetMessageBase message)
        {
            if (m_IsClosed || !OwnsWebSocketChannel() || !ReferenceEquals(channel, m_WebSocketChannel) ||
                !(message is WebSocketScope.TcpMessage tcp) || tcp.CommandID != c_WebSocketEchoCommandId ||
                tcp.SequenceID != m_WebSocketPendingSequence || m_WebSocketPendingNonce == null ||
                tcp.Messages.Count != 1 || tcp.Messages[0] != m_WebSocketPendingNonce) return;
            int sequence = tcp.SequenceID;
            string nonce = tcp.Messages[0];
            m_WebSocketPendingNonce = null;
            if (m_ResponsePreview != null) m_ResponsePreview.text = "WebSocket echo ack seq=" + sequence + " nonce=" + nonce;
            AppendFeedback("WebSocket echo ack 匹配 seq=" + sequence, FeedbackLevel.Success);
        }

        /// <summary>迟到或重复回显不能确认新请求；超时不重放，保留连接供显式再试。</summary>
        private async UniTask WaitForWebSocketAckTimeoutAsync(string nonce, int generation, CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(c_WebSocketEchoTimeoutSeconds), DelayType.Realtime, cancellationToken: token);
                if (this == null || m_IsClosed || generation != m_ViewGeneration || m_WebSocketPendingNonce != nonce) return;
                m_WebSocketPendingNonce = null;
                AppendFeedback("WebSocket echo ack 超时；本次不重放", FeedbackLevel.Warn);
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>先退订再主动关闭精确自有通道；释放弱租约，未知或歧义通道永不被顺手断开。</summary>
        private void ReleaseWebSocket()
        {
            IWebSocketManager manager = m_WebSocketManager;
            WebSocketScope.NetChannelBase channel = m_WebSocketChannel;
            string address = m_WebSocketAddress;
            bool owned = OwnsWebSocketChannel();
            m_WebSocketAttempt++;
            m_WebSocketConnecting = false;
            m_WebSocketPendingNonce = null;
            m_WebSocketManager = null;
            m_WebSocketChannel = null;
            m_WebSocketAddress = null;
            if (manager == null) return;
            manager.OnConnectSuccess -= OnWebSocketConnected;
            manager.OnConnectFail -= OnWebSocketConnectFailed;
            manager.OnDisconnect -= OnWebSocketDisconnected;
            manager.OnReceiveMessage -= OnWebSocketReceived;
            if (channel != null && s_WebSocketLeases.TryGetValue(channel, out WebSocketEchoLease lease) && ReferenceEquals(lease.Owner, m_WebSocketOwner)) lease.Owner = null;
            if (!owned) return;
            try { manager.DisconnectServer(WebSocketScope.NetChannelType.Tcp, address); }
            catch (Exception exception) { Debug.LogWarning("Demo WebSocket 关闭失败：" + exception.GetType().Name); }
        }

        /// <summary>
        /// 执行 GET 并仅更新本次打开的视图；所有返回和异常路径均归还已获得的池化响应。
        /// </summary>
        private async UniTaskVoid ExecuteGetAsync()
        {
            if (m_IsClosed || m_RequestCts == null) return;
            CancellationToken cancellationToken = m_RequestCts.Token;

            if (Nova.Network == null)
            {
                AppendFeedback("Nova.Network 不可用", FeedbackLevel.Error);
                return;
            }

            string url = m_UrlInput != null ? m_UrlInput.text : c_DefaultGetUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                url = c_DefaultGetUrl;
            }

            AppendFeedback($"Nova.Network.GetAsync(\"{url}\") -> 请求中...", FeedbackLevel.Info);

            int generation = m_ViewGeneration;
            HttpResponse response = null;
            try
            {
                response = await Nova.Network.GetAsync(url, -1f, null, cancellationToken);

                if (this == null || m_IsClosed || cancellationToken.IsCancellationRequested || generation != m_ViewGeneration)
                {
                    return;
                }

                if (response == null)
                {
                    AppendFeedback("Nova.Network.GetAsync -> 响应为空", FeedbackLevel.Error);
                    return;
                }

                int statusCode = response.StatusCode;
                bool isSuccess = response.IsSuccess;
                int len = response.Body != null ? response.Body.Length : 0;
                string preview = response.Body != null ? (len > 200 ? response.Body.Substring(0, 200) + "..." : response.Body) : string.Empty;

                if (m_ResponsePreview != null)
                {
                    m_ResponsePreview.text = preview;
                }

                if (isSuccess)
                {
                    AppendFeedback($"Nova.Network.GetAsync(\"{url}\") -> {statusCode} OK len={len}", FeedbackLevel.Success);
                }
                else
                {
                    AppendFeedback($"Nova.Network.GetAsync(\"{url}\") -> {statusCode} 失败", FeedbackLevel.Error);
                }
            }
            finally
            {
                if (response != null) ReferencePool.Put(response);
            }
        }

        /// <summary>
        /// 使用固定演示 JSON 执行 POST；旧视图不更新，所有路径归还已获得的池化响应。
        /// </summary>
        private async UniTaskVoid ExecutePostAsync()
        {
            if (m_IsClosed || m_RequestCts == null) return;
            CancellationToken cancellationToken = m_RequestCts.Token;

            if (Nova.Network == null)
            {
                AppendFeedback("Nova.Network 不可用", FeedbackLevel.Error);
                return;
            }

            const string body = "{\"message\":\"hello\"}";

            AppendFeedback($"Nova.Network.PostAsync(\"{c_DefaultPostUrl}\", \"{body}\") -> 请求中...", FeedbackLevel.Info);

            int generation = m_ViewGeneration;
            HttpResponse response = null;
            try
            {
                response = await Nova.Network.PostAsync(c_DefaultPostUrl, body, -1f, null, cancellationToken);

                if (this == null || m_IsClosed || cancellationToken.IsCancellationRequested || generation != m_ViewGeneration)
                {
                    return;
                }

                if (response == null)
                {
                    AppendFeedback("Nova.Network.PostAsync -> 响应为空", FeedbackLevel.Error);
                    return;
                }

                int statusCode = response.StatusCode;
                bool isSuccess = response.IsSuccess;
                int len = response.Body != null ? response.Body.Length : 0;
                string preview = response.Body != null ? (len > 200 ? response.Body.Substring(0, 200) + "..." : response.Body) : string.Empty;

                if (m_ResponsePreview != null)
                {
                    m_ResponsePreview.text = preview;
                }

                if (isSuccess)
                {
                    AppendFeedback($"Nova.Network.PostAsync(\"{c_DefaultPostUrl}\", \"{body}\") -> {statusCode} OK len={len}", FeedbackLevel.Success);
                }
                else
                {
                    AppendFeedback($"Nova.Network.PostAsync(\"{c_DefaultPostUrl}\", \"{body}\") -> {statusCode} 失败", FeedbackLevel.Error);
                }
            }
            finally
            {
                if (response != null) ReferencePool.Put(response);
            }
        }
    }
}
