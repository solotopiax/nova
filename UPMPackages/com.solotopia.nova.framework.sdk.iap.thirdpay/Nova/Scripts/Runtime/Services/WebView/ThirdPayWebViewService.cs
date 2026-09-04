/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayWebViewService.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   第三方支付 UniWebView 生命周期服务
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// ThirdPay 程序集内部的支付页服务契约。
    /// </summary>
    internal interface IThirdPayWebViewService : IDisposable
    {
        /// <summary>
        /// 打开支付页并等待成功、取消或失败终态。
        /// </summary>
        /// <param name="paymentUrl">已完成加密的支付 URL。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>支付页流程结果。</returns>
        UniTask<ThirdPayOpenResult> OpenAsync(string paymentUrl, CancellationToken ct);

        /// <summary>
        /// 设置 WebView 导航栏标题。
        /// </summary>
        /// <param name="titleText">标题文本。</param>
        void SetTitleText(string titleText);

        /// <summary>
        /// 设置 WebView 导航栏关闭按钮文本。
        /// </summary>
        /// <param name="closeText">关闭文本。</param>
        void SetCloseText(string closeText);
    }

    /// <summary>
    /// 使用 UniWebView 全屏展示第三方支付页面，并对称管理原生 WebView 资源。
    /// </summary>
    internal sealed class ThirdPayWebViewService : ThirdPayLogOwner, IThirdPayWebViewService
    {
        private ThirdPayWebViewSession m_CurrentSession;
        private bool m_Disposed;
        private string m_TitleText = string.Empty;
        private string m_CloseText = "close";

        /// <summary>
        /// 初始化 ThirdPay WebView 服务。
        /// </summary>
        public ThirdPayWebViewService()
        {
        }

        /// <summary>
        /// 设置 WebView 导航栏标题；空值时在打开页面时使用应用名称。
        /// </summary>
        /// <param name="titleText">标题文本。</param>
        public void SetTitleText(string titleText)
        {
            m_TitleText = titleText ?? string.Empty;
        }

        /// <summary>
        /// 设置 WebView 导航栏关闭按钮文本；空值时恢复为 close。
        /// </summary>
        /// <param name="closeText">关闭文本。</param>
        public void SetCloseText(string closeText)
        {
            m_CloseText = string.IsNullOrEmpty(closeText) ? "close" : closeText;
        }

        /// <summary>
        /// 打开支付页并等待页面回调；同一服务实例不允许并发打开多个支付页。
        /// </summary>
        /// <param name="paymentUrl">已完成加密的支付 URL。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>支付页流程结果。</returns>
        public async UniTask<ThirdPayOpenResult> OpenAsync(string paymentUrl, CancellationToken ct)
        {
            if (m_Disposed)
            {
                throw new ObjectDisposedException(nameof(ThirdPayWebViewService));
            }

            if (m_CurrentSession != null)
            {
                throw new InvalidOperationException("第三方支付页已打开，不能并发创建第二个支付页。");
            }

            if (string.IsNullOrEmpty(paymentUrl))
            {
                throw new ArgumentException("第三方支付 URL 不能为空。", nameof(paymentUrl));
            }

            ct.ThrowIfCancellationRequested();
            var session = new ThirdPayWebViewSession();
            m_CurrentSession = session;
            try
            {
                return await session.OpenAsync(paymentUrl, m_TitleText, m_CloseText, ct);
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                session.Dispose();
                if (ReferenceEquals(m_CurrentSession, session))
                {
                    m_CurrentSession = null;
                }
            }
        }

        /// <summary>
        /// 终止当前支付页并释放服务；后续不能再次打开支付页。
        /// </summary>
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            m_CurrentSession?.Complete(ThirdPayOpenResult.Failed);
            m_CurrentSession?.Dispose();
            m_CurrentSession = null;
        }

        /// <summary>
        /// 封装单次支付页的回调、完成源和 Unity 对象生命周期。
        /// </summary>
        private sealed class ThirdPayWebViewSession : ThirdPayLogOwner, IDisposable
        {
            private readonly UniTaskCompletionSource<ThirdPayOpenResult> m_CompletionSource = new();
            private CancellationTokenRegistration m_CancellationRegistration;
            private GameObject m_WebViewHost;
            private UniWebView m_WebView;
            private UniWebViewSafeBrowsing m_SafeBrowsing;
            private bool m_Disposed;

            /// <summary>
            /// 创建并展示单次 UniWebView 支付页。
            /// </summary>
            /// <param name="paymentUrl">已完成加密的支付 URL。</param>
            /// <param name="ct">取消令牌。</param>
            /// <returns>支付页流程结果。</returns>
            public UniTask<ThirdPayOpenResult> OpenAsync(string paymentUrl, string titleText, string closeText, CancellationToken ct)
            {
                m_CancellationRegistration = ct.Register(() => m_CompletionSource.TrySetCanceled(ct));
#if UNITY_IOS && !UNITY_EDITOR
                OpenSafeBrowsing(paymentUrl);
#else
                OpenEmbeddedWebView(paymentUrl, titleText, closeText);
#endif
                return m_CompletionSource.Task;
            }

            /// <summary>
            /// 创建 Android 与 Editor 使用的嵌入式 UniWebView，并接入页面回调和 URL 重写。
            /// </summary>
            /// <param name="paymentUrl">已完成加密的支付 URL。</param>
            private void OpenEmbeddedWebView(string paymentUrl, string titleText, string closeText)
            {
                m_WebViewHost = new GameObject("ThirdPayWebView");
                m_WebView = m_WebViewHost.AddComponent<UniWebView>();
                // 不依赖业务侧 RectTransform，显式按当前屏幕铺满支付页。
                m_WebView.Frame = new Rect(0f, 0f, Screen.width, Screen.height);
                m_WebView.SetBackButtonEnabled(true);
                m_WebView.SetOpenLinksInExternalBrowser(false);
                m_WebView.EmbeddedToolbar.SetPosition(UniWebViewToolbarPosition.Top);
                m_WebView.EmbeddedToolbar.SetDoneButtonText(string.IsNullOrEmpty(closeText) ? "close" : closeText);
                m_WebView.EmbeddedToolbar.SetTitleText(string.IsNullOrEmpty(titleText) ? Application.productName : titleText);
                m_WebView.EmbeddedToolbar.HideNavigationButtons();
                m_WebView.EmbeddedToolbar.Show();

                foreach (string scheme in ThirdPayUrlRewriteRules.GetSchemes())
                {
                    m_WebView.AddUrlScheme(scheme);
                }

                m_WebView.OnMessageReceived += OnMessageReceived;
                m_WebView.OnShouldClose += OnShouldClose;
                m_WebView.OnLoadingErrorReceived += OnLoadingErrorReceived;
                m_WebView.OnWebContentProcessTerminated += OnWebContentProcessTerminated;
                m_WebView.Load(paymentUrl);
                if (!m_WebView.Show())
                {
                    LogWarning("第三方支付 WebView failed 返回：来源=ShowFailed");
                    Complete(ThirdPayOpenResult.Failed);
                }
                else
                {
                    LogDebug("第三方支付 WebView 已显示：Mode=Embedded");
                }
            }

            /// <summary>
            /// 创建 iOS 系统 Safe Browsing，并通过应用 Deep Link 接收支付终态。
            /// </summary>
            /// <param name="paymentUrl">已完成加密的支付 URL。</param>
            private void OpenSafeBrowsing(string paymentUrl)
            {
                if (!UniWebViewSafeBrowsing.IsSafeBrowsingSupported)
                {
                    LogWarning("第三方支付 WebView failed 返回：来源=SafeBrowsingUnsupported");
                    Complete(ThirdPayOpenResult.Failed);
                    return;
                }

                Application.deepLinkActivated += OnDeepLinkActivated;
                m_SafeBrowsing = UniWebViewSafeBrowsing.Create(paymentUrl);
                m_SafeBrowsing.OnSafeBrowsingFinished += OnSafeBrowsingFinished;
                m_SafeBrowsing.Show();
                LogDebug("第三方支付 SafeBrowsing 已显示");
            }

            /// <summary>
            /// 尝试完成当前支付页；重复回调不会覆盖第一个终态。
            /// </summary>
            /// <param name="result">支付页终态。</param>
            public void Complete(ThirdPayOpenResult result)
            {
                if (m_CompletionSource.TrySetResult(result))
                {
                    LogDebug($"第三方支付 WebView 完成：Result={result}");
                }
                else
                {
                    LogDebug($"第三方支付 WebView 重复完成已忽略：Result={result}");
                }
            }

            /// <summary>
            /// 处理支付页 Scheme 消息，包括 AlipayConnect 重写和支付终态回调。
            /// </summary>
            /// <param name="webView">消息来源 WebView。</param>
            /// <param name="message">UniWebView 消息。</param>
            private void OnMessageReceived(UniWebView webView, UniWebViewMessage message)
            {
                LogDebug($"第三方支付 WebView 收到 message：Raw={message.RawMessage}，Path={message.Path}，Args={FormatArgs(message.Args)}");
                if (ThirdPayUrlRewriteRules.TryRewrite(message.RawMessage, out string rewrittenUrl))
                {
                    LogDebug($"第三方支付 WebView URL 重写：Raw={message.RawMessage}，Rewrite={rewrittenUrl}");
                    webView.Load(rewrittenUrl);
                    return;
                }

                if (ThirdPayWebViewCallbackResolver.TryResolve(message.Path, message.Args, out ThirdPayOpenResult result))
                {
                    LogDebug($"第三方支付 WebView message 返回：Path={message.Path}，Args={FormatArgs(message.Args)}，Result={result}");
                    Complete(result);
                }
                else
                {
                    LogDebug($"第三方支付 WebView 未识别 message，继续等待：Path={message.Path}，Args={FormatArgs(message.Args)}");
                }
            }

            /// <summary>
            /// 处理 iOS Safe Browsing 返回应用时携带的支付 Deep Link。
            /// </summary>
            /// <param name="url">应用收到的 Deep Link URL。</param>
            private void OnDeepLinkActivated(string url)
            {
                if (string.IsNullOrEmpty(url))
                {
                    LogDebug("第三方支付 SafeBrowsing 收到空 DeepLink，继续等待。");
                    return;
                }

                LogDebug($"第三方支付 SafeBrowsing 收到 DeepLink：URL={url}");
                var message = new UniWebViewMessage(url);
                if (!ThirdPayWebViewCallbackResolver.TryResolve(message.Path, message.Args, out ThirdPayOpenResult result))
                {
                    LogDebug($"第三方支付 SafeBrowsing 未识别 DeepLink，继续等待：Path={message.Path}，Args={FormatArgs(message.Args)}");
                    return;
                }

                LogDebug($"第三方支付 SafeBrowsing message 返回：Path={message.Path}，Args={FormatArgs(message.Args)}，Result={result}");
                DismissSafeBrowsing();
                Complete(result);
            }

            /// <summary>
            /// 把用户主动关闭 iOS Safe Browsing 映射为取消终态。
            /// </summary>
            /// <param name="safeBrowsing">触发关闭事件的 Safe Browsing 实例。</param>
            private void OnSafeBrowsingFinished(UniWebViewSafeBrowsing safeBrowsing)
            {
                Application.deepLinkActivated -= OnDeepLinkActivated;
                if (ReferenceEquals(m_SafeBrowsing, safeBrowsing))
                {
                    m_SafeBrowsing.OnSafeBrowsingFinished -= OnSafeBrowsingFinished;
                    m_SafeBrowsing = null;
                }

                LogDebug("第三方支付 SafeBrowsing 关闭返回：Result=Cancel");
                Complete(ThirdPayOpenResult.Cancel);
            }

            /// <summary>
            /// 注销 iOS Deep Link 与关闭事件，并主动关闭仍在展示的 Safe Browsing。
            /// </summary>
            private void DismissSafeBrowsing()
            {
                Application.deepLinkActivated -= OnDeepLinkActivated;
                if (m_SafeBrowsing == null)
                {
                    return;
                }

                UniWebViewSafeBrowsing safeBrowsing = m_SafeBrowsing;
                m_SafeBrowsing = null;
                safeBrowsing.OnSafeBrowsingFinished -= OnSafeBrowsingFinished;
                safeBrowsing.Dismiss();
            }

            /// <summary>
            /// 把工具栏关闭或 Android 返回键统一映射为用户取消，并由会话自行清理资源。
            /// </summary>
            /// <param name="webView">请求关闭的 WebView。</param>
            /// <returns>固定返回 false，避免 UniWebView 与会话重复销毁同一对象。</returns>
            private bool OnShouldClose(UniWebView webView)
            {
                LogDebug("第三方支付 WebView 关闭返回：来源=OnShouldClose，Result=Cancel");
                Complete(ThirdPayOpenResult.Cancel);
                return false;
            }

            /// <summary>
            /// 把页面加载错误映射为支付页失败。
            /// </summary>
            /// <param name="webView">加载失败的 WebView。</param>
            /// <param name="errorCode">平台错误码。</param>
            /// <param name="errorMessage">平台错误信息。</param>
            /// <param name="payload">UniWebView 原生错误载荷。</param>
            private void OnLoadingErrorReceived(UniWebView webView, int errorCode, string errorMessage, UniWebViewNativeResultPayload payload)
            {
                LogWarning($"第三方支付 WebView failed 返回：来源=LoadingError，ErrorCode={errorCode}，Error={errorMessage}，Payload={payload}");
                Complete(ThirdPayOpenResult.Failed);
            }

            /// <summary>
            /// 把 WebView 内容进程终止映射为支付页失败。
            /// </summary>
            /// <param name="webView">内容进程终止的 WebView。</param>
            private void OnWebContentProcessTerminated(UniWebView webView)
            {
                LogWarning("第三方支付 WebView failed 返回：来源=WebContentProcessTerminated");
                Complete(ThirdPayOpenResult.Failed);
            }

            /// <summary>
            /// 格式化 UniWebView 回调参数，便于支付链路日志定位。
            /// </summary>
            /// <param name="args">UniWebView message 参数。</param>
            /// <returns>稳定的 key=value 参数描述。</returns>
            private static string FormatArgs(IReadOnlyDictionary<string, string> args)
            {
                return args == null || args.Count == 0 ? "{}" : "{" + string.Join(", ", args.Select(pair => $"{pair.Key}={pair.Value}")) + "}";
            }

            /// <summary>
            /// 注销回调并销毁 UniWebView 宿主。
            /// </summary>
            public void Dispose()
            {
                if (m_Disposed)
                {
                    return;
                }

                m_Disposed = true;
                m_CancellationRegistration.Dispose();
                DismissSafeBrowsing();
                if (m_WebView != null)
                {
                    m_WebView.OnMessageReceived -= OnMessageReceived;
                    m_WebView.OnShouldClose -= OnShouldClose;
                    m_WebView.OnLoadingErrorReceived -= OnLoadingErrorReceived;
                    m_WebView.OnWebContentProcessTerminated -= OnWebContentProcessTerminated;
                    m_WebView.Hide();
                }

                if (m_WebViewHost != null)
                {
                    UnityEngine.Object.Destroy(m_WebViewHost);
                }

                m_WebView = null;
                m_WebViewHost = null;
            }
        }
    }
}
