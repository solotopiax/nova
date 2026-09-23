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
        /// 打开支付页并返回包含回调状态、打开方式和失败详情的结构化结果。
        /// </summary>
        /// <param name="paymentUrl">已完成加密的支付 URL。</param>
        /// <param name="expectedClientOrderId">当前支付会话期望的客户端订单号。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>支付页结构化结果。</returns>
        UniTask<ThirdPayPaymentPageResult> OpenWithResultAsync(string paymentUrl, string expectedClientOrderId, CancellationToken ct);

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
        /// <summary>
        /// 当前正在运行的支付页会话。
        /// </summary>
        private ThirdPayWebViewSession m_CurrentSession;

        /// <summary>
        /// 标记支付页服务是否已经释放。
        /// </summary>
        private bool m_Disposed;

        /// <summary>
        /// 支付页导航栏标题；空值表示使用应用名称。
        /// </summary>
        private string m_TitleText = string.Empty;

        /// <summary>
        /// 支付页导航栏关闭按钮文本。
        /// </summary>
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
            return (await OpenWithResultAsync(paymentUrl, string.Empty, ct)).Result;
        }

        /// <summary>
        /// 打开支付页并返回包含回调状态、打开方式和失败详情的结构化结果。
        /// </summary>
        /// <param name="paymentUrl">已完成加密的支付 URL。</param>
        /// <param name="expectedClientOrderId">当前支付会话期望的客户端订单号；为空时保留旧调用方兼容行为。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>支付页结构化结果。</returns>
        public async UniTask<ThirdPayPaymentPageResult> OpenWithResultAsync(string paymentUrl, string expectedClientOrderId, CancellationToken ct)
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
            var session = new ThirdPayWebViewSession(expectedClientOrderId);
            m_CurrentSession = session;
            try
            {
                return await session.OpenWithResultAsync(paymentUrl, m_TitleText, m_CloseText, ct);
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
            m_CurrentSession?.CompleteAsDisposed();
            m_CurrentSession?.Dispose();
            m_CurrentSession = null;
        }

        /// <summary>
        /// 封装单次支付页的回调、完成源和 Unity 对象生命周期。
        /// </summary>
        private sealed class ThirdPayWebViewSession : ThirdPayLogOwner, IDisposable
        {
            /// <summary>
            /// 当前支付页期望接收的客户端订单号；用于拒绝其他会话的全局 Deep Link。
            /// </summary>
            private readonly string m_ExpectedClientOrderId;

            /// <summary>
            /// 向支付调用方发布页面终态的完成源。
            /// </summary>
            private readonly UniTaskCompletionSource<ThirdPayPaymentPageResult> m_CompletionSource = new();

            /// <summary>
            /// 当前支付页会话的实际打开方式。
            /// </summary>
            private ThirdPayExternalBrowserLaunchMode m_OpenMode = ThirdPayExternalBrowserLaunchMode.Failed;

            /// <summary>
            /// 标记当前支付页是否已经建立支付会话。
            /// </summary>
            private bool m_IsSessionEstablished;

            /// <summary>
            /// 当前支付调用的取消令牌注册。
            /// </summary>
            private CancellationTokenRegistration m_CancellationRegistration;

            /// <summary>
            /// 承载嵌入式 UniWebView 的临时 Unity 对象。
            /// </summary>
            private GameObject m_WebViewHost;

            /// <summary>
            /// Android 与 Editor 使用的嵌入式 UniWebView。
            /// </summary>
            private UniWebView m_WebView;

            /// <summary>
            /// iOS 使用的 Safe Browsing 支付页。
            /// </summary>
            private UniWebViewSafeBrowsing m_SafeBrowsing;

            /// <summary>
            /// 标记当前支付页会话是否已经释放。
            /// </summary>
            private bool m_Disposed;

            /// <summary>
            /// 创建单次支付页会话并冻结期望的客户端订单号。
            /// </summary>
            /// <param name="expectedClientOrderId">当前支付会话期望的客户端订单号。</param>
            public ThirdPayWebViewSession(string expectedClientOrderId)
            {
                m_ExpectedClientOrderId = expectedClientOrderId ?? string.Empty;
            }

            /// <summary>
            /// 创建并展示单次 UniWebView 支付页。
            /// </summary>
            /// <param name="paymentUrl">已完成加密的支付 URL。</param>
            /// <param name="titleText">支付页导航栏标题。</param>
            /// <param name="closeText">支付页导航栏关闭按钮文本。</param>
            /// <param name="ct">取消令牌。</param>
            /// <returns>支付页流程结果。</returns>
            public UniTask<ThirdPayPaymentPageResult> OpenWithResultAsync(string paymentUrl, string titleText, string closeText, CancellationToken ct)
            {
                m_CancellationRegistration = ct.Register(() => Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.OperationCancelled, ThirdPayPaymentFailureReason.OperationCancelled, 0, string.Empty, m_IsSessionEstablished)));
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
            /// <param name="titleText">支付页导航栏标题。</param>
            /// <param name="closeText">支付页导航栏关闭按钮文本。</param>
            private void OpenEmbeddedWebView(string paymentUrl, string titleText, string closeText)
            {
                m_OpenMode = ThirdPayExternalBrowserLaunchMode.EmbeddedWebView;
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
                    Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.Unknown, ThirdPayPaymentFailureReason.PaymentPageOpenFailed, 0, string.Empty, false));
                }
                else
                {
                    m_IsSessionEstablished = true;
                    LogDebug("第三方支付 WebView 已显示：Mode=Embedded");
                }
            }

            /// <summary>
            /// 创建 iOS 系统 Safe Browsing，并通过应用 Deep Link 接收支付终态。
            /// </summary>
            /// <param name="paymentUrl">已完成加密的支付 URL。</param>
            private void OpenSafeBrowsing(string paymentUrl)
            {
                m_OpenMode = ThirdPayExternalBrowserLaunchMode.IOSSafeBrowsing;
                if (!UniWebViewSafeBrowsing.IsSafeBrowsingSupported)
                {
                    LogWarning("第三方支付 WebView failed 返回：来源=SafeBrowsingUnsupported");
                    Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.Unknown, ThirdPayPaymentFailureReason.PaymentPageOpenFailed, 0, "SafeBrowsingUnsupported", false));
                    return;
                }

                Application.deepLinkActivated += OnDeepLinkActivated;
                m_SafeBrowsing = UniWebViewSafeBrowsing.Create(paymentUrl);
                m_SafeBrowsing.OnSafeBrowsingFinished += OnSafeBrowsingFinished;
                m_SafeBrowsing.Show();
                m_IsSessionEstablished = true;
                LogDebug("第三方支付 SafeBrowsing 已显示");
            }

            /// <summary>
            /// 尝试完成当前支付页；重复回调不会覆盖第一个终态。
            /// </summary>
            /// <param name="result">支付页终态。</param>
            public void Complete(ThirdPayPaymentPageResult result)
            {
                if (m_CompletionSource.TrySetResult(result))
                {
                    LogDebug($"第三方支付 WebView 完成：Result={result.Result}，Mode={result.OpenMode}，CallbackStatus={(int)result.CallbackStatus}，CloseReason={result.CloseReason}，FailureReason={result.FailureReason}");
                }
                else
                {
                    LogDebug($"第三方支付 WebView 重复完成已忽略：Result={result.Result}，CloseReason={result.CloseReason}");
                }
            }

            /// <summary>
            /// 使用当前会话真实打开状态完成 Store 释放结果。
            /// </summary>
            public void CompleteAsDisposed()
            {
                Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.StoreDisposed, ThirdPayPaymentFailureReason.StoreDisposed, 0, string.Empty, m_IsSessionEstablished));
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

                ThirdPayCallbackResolveResult resolveResult = ThirdPayWebViewCallbackResolver.ResolveCallback(message.Path, message.Args, out ThirdPayWebViewCallback callback);
                if ((resolveResult == ThirdPayCallbackResolveResult.Valid || resolveResult == ThirdPayCallbackResolveResult.UnknownStatus) && !IsCallbackForCurrentOrder(callback))
                {
                    LogWarning($"第三方支付 WebView 忽略非当前订单回调：ExpectedOrderId={m_ExpectedClientOrderId}，CallbackOrderId={callback.OrderId}");
                    return;
                }

                if (resolveResult == ThirdPayCallbackResolveResult.Valid)
                {
                    ThirdPayPaymentPageResult result = CreateCallbackResult(callback);
                    LogDebug($"第三方支付 WebView message 返回：Path={message.Path}，Args={FormatArgs(message.Args)}，Result={result.Result}，Status={callback.RawStatus}");
                    Complete(result);
                }
                else if (resolveResult == ThirdPayCallbackResolveResult.InvalidPayload)
                {
                    LogWarning($"第三方支付 WebView pay_callback 参数无效：Path={message.Path}，Args={FormatArgs(message.Args)}");
                    Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.PayCallbackInvalidPayload, ThirdPayPaymentFailureReason.PaymentCallbackInvalidPayload, 0, string.Empty, m_IsSessionEstablished));
                }
                else if (resolveResult == ThirdPayCallbackResolveResult.UnknownStatus)
                {
                    LogWarning($"第三方支付 WebView pay_callback 状态未知：Path={message.Path}，Status={callback.RawStatus}");
                    Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.PayCallbackUnknownStatus, ThirdPayPaymentFailureReason.PaymentCallbackUnknownStatus, callback.RawStatus, string.Empty, m_IsSessionEstablished));
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
                ThirdPayCallbackResolveResult resolveResult = ThirdPayWebViewCallbackResolver.ResolveCallback(message.Path, message.Args, out ThirdPayWebViewCallback callback);
                if (resolveResult == ThirdPayCallbackResolveResult.NotPaymentCallback)
                {
                    LogDebug($"第三方支付 SafeBrowsing 未识别 DeepLink，继续等待：Path={message.Path}，Args={FormatArgs(message.Args)}");
                    return;
                }

                if (resolveResult == ThirdPayCallbackResolveResult.InvalidPayload)
                {
                    LogWarning($"第三方支付 SafeBrowsing pay_callback 参数无效，无法确认所属订单并继续等待：Path={message.Path}，Args={FormatArgs(message.Args)}");
                    return;
                }

                if (!IsCallbackForCurrentOrder(callback))
                {
                    LogWarning($"第三方支付 SafeBrowsing 忽略非当前订单回调：ExpectedOrderId={m_ExpectedClientOrderId}，CallbackOrderId={callback.OrderId}");
                    return;
                }

                ThirdPayPaymentPageResult result;
                if (resolveResult == ThirdPayCallbackResolveResult.Valid)
                {
                    result = CreateCallbackResult(callback);
                }
                else
                {
                    result = ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.PayCallbackUnknownStatus, ThirdPayPaymentFailureReason.PaymentCallbackUnknownStatus, callback.RawStatus, string.Empty, m_IsSessionEstablished);
                }
                LogDebug($"第三方支付 SafeBrowsing message 返回：Path={message.Path}，Args={FormatArgs(message.Args)}，Result={result.Result}，Resolve={resolveResult}");
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
                Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Cancel, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.IOSSafeBrowsingDismissed, ThirdPayPaymentFailureReason.OperationCancelled, 0, string.Empty, m_IsSessionEstablished));
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
            /// 把 UniWebView 关闭回调统一映射为用户取消，并由会话自行清理资源。
            /// </summary>
            /// <param name="webView">请求关闭的 WebView。</param>
            /// <returns>固定返回 false，避免 UniWebView 与会话重复销毁同一对象。</returns>
            private bool OnShouldClose(UniWebView webView)
            {
                LogDebug("第三方支付 WebView 关闭返回：来源=OnShouldClose，Result=Cancel");
                Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Cancel, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.EmbeddedWebViewUserClosed, ThirdPayPaymentFailureReason.OperationCancelled, 0, string.Empty, m_IsSessionEstablished));
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
                Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.PaymentPageLoadingError, ThirdPayPaymentFailureReason.PaymentPageOpenFailed, errorCode, errorMessage, m_IsSessionEstablished));
            }

            /// <summary>
            /// 把 WebView 内容进程终止映射为支付页失败。
            /// </summary>
            /// <param name="webView">内容进程终止的 WebView。</param>
            private void OnWebContentProcessTerminated(UniWebView webView)
            {
                LogWarning("第三方支付 WebView failed 返回：来源=WebContentProcessTerminated");
                Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, m_OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.WebContentProcessTerminated, ThirdPayPaymentFailureReason.PaymentPageOpenFailed, 0, string.Empty, m_IsSessionEstablished));
            }

            /// <summary>
            /// 将字段完整的支付回调转换为支付页结构化结果。
            /// </summary>
            /// <param name="callback">已解析的支付页回调。</param>
            /// <returns>可供 Checkout 统一上报和验单的支付页结果。</returns>
            private ThirdPayPaymentPageResult CreateCallbackResult(ThirdPayWebViewCallback callback)
            {
                if (callback.Result == ThirdPayOpenResult.Success)
                {
                    return ThirdPayPaymentPageResult.CallbackSuccess(m_OpenMode, callback.Status);
                }

                if (callback.Result == ThirdPayOpenResult.Cancel)
                {
                    return ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Cancel, m_OpenMode, callback.Status, ThirdPayCloseReason.CloseCallback, ThirdPayPaymentFailureReason.OperationCancelled, 0, string.Empty, m_IsSessionEstablished);
                }

                return ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, m_OpenMode, callback.Status, ThirdPayCloseReason.PayCallbackFailed, ThirdPayPaymentFailureReason.PaymentCallbackFailed, callback.RawStatus, string.Empty, m_IsSessionEstablished);
            }

            /// <summary>
            /// 判断支付回调是否属于当前支付页会话；close_callback 不含订单号，按当前会话关闭处理。
            /// </summary>
            /// <param name="callback">已解析的支付页回调。</param>
            /// <returns>非 pay_callback、未配置期望订单号或订单号匹配时返回 true。</returns>
            private bool IsCallbackForCurrentOrder(ThirdPayWebViewCallback callback)
            {
                return !string.Equals(callback.Path, "pay_callback", StringComparison.Ordinal)
                       || string.IsNullOrEmpty(m_ExpectedClientOrderId)
                       || string.Equals(callback.OrderId, m_ExpectedClientOrderId, StringComparison.Ordinal);
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
