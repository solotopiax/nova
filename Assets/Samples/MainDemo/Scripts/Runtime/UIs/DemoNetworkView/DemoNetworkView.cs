/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoNetworkView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.9 — Network 模块演示视图（交互触发型）。
 *            演示 HTTP GET/POST 异步请求及 WebSocket 连接占位。
 *            API：Nova.Network.GetAsync / PostAsync / ConnectServer
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Network 模块演示视图，演示 HTTP GET/POST 异步请求与 WebSocket 连接占位。
    /// 继承 BaseDemoView 三段式骨架，交互区包含 URL 输入框与 Get/Post/Connect 三按钮。
    /// </summary>
    public sealed class DemoNetworkView : BaseDemoView
    {
        /// <summary>
        /// URL 输入框，默认值 https://httpbin.org/get。
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
        /// WebSocket 连接占位按钮。
        /// </summary>

        [SerializeField] private Button m_ConnectButton;

        /// <summary>
        /// 响应内容预览文本。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_ResponsePreview;

        /// <summary>
        /// 视图已关闭标志，防止异步回调在关闭后写入 UI。
        /// </summary>
        private bool m_IsClosed;

        /// <summary>
        /// 默认演示 URL。
        /// </summary>
        private const string c_DefaultUrl = "https://httpbin.org/get";

        /// <summary>
        /// WebSocket 演示地址占位。
        /// </summary>
        private const string c_WsPlaceholder = "wss://echo.websocket.org";

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
                m_UrlInput.text = c_DefaultUrl;
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
                SetButtonApiHint(m_ConnectButton, "Nova.Network.ConnectServer(Tcp, addr)");
            }
        }

        /// <summary>
        /// 视图打开：清空响应预览。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            m_IsClosed = false;

            if (m_ResponsePreview != null)
            {
                m_ResponsePreview.text = string.Empty;
            }
        }

        /// <summary>
        /// 视图关闭：标记已关闭状态，防止未完成的异步请求回调写入 UI。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_IsClosed = true;
            base.OnClose(isShutdown, userData);
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

        /// <summary>
        /// Connect 按钮点击：调用 ConnectServer 占位演示（连接 WebSocket 服务器）。
        /// </summary>
        private void OnConnectButtonClick()
        {
            if (Nova.Network == null)
            {
                AppendFeedback("Nova.Network 不可用", FeedbackLevel.Error);
                return;
            }

            Nova.Network.ConnectServer(WebSocketScope.NetChannelType.Tcp, c_WsPlaceholder, false);
            AppendFeedback($"Nova.Network.ConnectServer(Tcp, \"{c_WsPlaceholder}\", false) -> 连接已发起（占位演示）", FeedbackLevel.Success);
        }

        /// <summary>
        /// 执行 HTTP GET 请求的异步流程。
        /// </summary>
        private async UniTaskVoid ExecuteGetAsync()
        {
            if (Nova.Network == null)
            {
                AppendFeedback("Nova.Network 不可用", FeedbackLevel.Error);
                return;
            }

            string url = m_UrlInput != null ? m_UrlInput.text : c_DefaultUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                url = c_DefaultUrl;
            }

            AppendFeedback($"Nova.Network.GetAsync(\"{url}\") -> 请求中...", FeedbackLevel.Info);

            HttpResponse response = await Nova.Network.GetAsync(url);

            if (this == null || m_IsClosed)
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
            ReferencePool.Put(response);

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

        /// <summary>
        /// 执行 HTTP POST 请求的异步流程，body 使用固定演示 JSON。
        /// </summary>
        private async UniTaskVoid ExecutePostAsync()
        {
            if (Nova.Network == null)
            {
                AppendFeedback("Nova.Network 不可用", FeedbackLevel.Error);
                return;
            }

            string url = m_UrlInput != null ? m_UrlInput.text : c_DefaultUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                url = c_DefaultUrl;
            }

            string postUrl = url.Replace("/get", "/post");
            const string body = "{\"demo\":\"nova_network\",\"value\":42}";

            AppendFeedback($"Nova.Network.PostAsync(\"{postUrl}\") -> 请求中...", FeedbackLevel.Info);

            HttpResponse response = await Nova.Network.PostAsync(postUrl, body);

            if (this == null || m_IsClosed)
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
            ReferencePool.Put(response);

            if (m_ResponsePreview != null)
            {
                m_ResponsePreview.text = preview;
            }

            if (isSuccess)
            {
                AppendFeedback($"Nova.Network.PostAsync(\"{postUrl}\") -> {statusCode} OK len={len}", FeedbackLevel.Success);
            }
            else
            {
                AppendFeedback($"Nova.Network.PostAsync(\"{postUrl}\") -> {statusCode} 失败", FeedbackLevel.Error);
            }
        }
    }
}
