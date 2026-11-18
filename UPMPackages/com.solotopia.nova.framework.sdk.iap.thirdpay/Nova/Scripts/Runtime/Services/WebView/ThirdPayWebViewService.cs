/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayWebViewService.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   第三方支付系统浏览器生命周期服务
 ***************************************************************/

using System;
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
    /// 保留支付页服务入口，内部只使用自有系统浏览器与 Unity Deep Link。
    /// </summary>
    internal sealed class ThirdPayWebViewService : ThirdPayLogOwner, IThirdPayWebViewService
    {
        private readonly Func<IThirdPaySafeBrowsing> m_BrowserFactory;
        private ThirdPayWebViewSession m_CurrentSession;
        private bool m_Disposed;

        /// <summary>
        /// 装配正式 iOS 系统浏览器；非设备平台由支持性检查明确失败。
        /// </summary>
        public ThirdPayWebViewService() : this(() => new ThirdPayIosSafeBrowsing()) { }

        /// <summary>
        /// 为平台与生命周期测试注入浏览器创建合同，不暴露到业务公开接口。
        /// </summary>
        internal ThirdPayWebViewService(Func<IThirdPaySafeBrowsing> browserFactory)
        {
            m_BrowserFactory = browserFactory ?? throw new ArgumentNullException(nameof(browserFactory));
        }

        /// <summary>
        /// 保留旧接口兼容；系统浏览器使用系统标题，不能自定义任意标题文案。
        /// </summary>
        public void SetTitleText(string titleText) { }

        /// <summary>
        /// 保留旧接口兼容；系统浏览器关闭按钮由系统负责。
        /// </summary>
        public void SetCloseText(string closeText) { }

        /// <summary>
        /// 打开系统支付页并返回原有粗粒度结果。
        /// </summary>
        public async UniTask<ThirdPayOpenResult> OpenAsync(string paymentUrl, CancellationToken ct)
        {
            return (await OpenWithResultAsync(paymentUrl, string.Empty, ct)).Result;
        }

        /// <summary>
        /// 在主线程建立唯一页面会话，所有返回路径都对称关闭原生页与事件。
        /// </summary>
        public async UniTask<ThirdPayPaymentPageResult> OpenWithResultAsync(string paymentUrl, string expectedClientOrderId, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            if (m_Disposed) throw new ObjectDisposedException(nameof(ThirdPayWebViewService));
            if (m_CurrentSession != null) throw new InvalidOperationException("第三方支付页已打开，不能并发创建第二个支付页。");
            if (string.IsNullOrEmpty(paymentUrl)) throw new ArgumentException("第三方支付 URL 不能为空。", nameof(paymentUrl));
            ct.ThrowIfCancellationRequested();
            var session = new ThirdPayWebViewSession(expectedClientOrderId, m_BrowserFactory());
            m_CurrentSession = session;
            try
            {
                return await session.OpenWithResultAsync(paymentUrl, ct);
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                session.Dispose();
                if (ReferenceEquals(m_CurrentSession, session)) m_CurrentSession = null;
            }
        }

        /// <summary>
        /// 主线程终止并释放当前页面；后台调用调度回 Unity 主线程执行。
        /// </summary>
        public void Dispose()
        {
            if (!PlayerLoopHelper.IsMainThread) { DisposeOnMainThreadAsync().Forget(); return; }
            if (m_Disposed) return;
            m_Disposed = true;
            m_CurrentSession?.CompleteAsDisposed();
            m_CurrentSession?.Dispose();
            m_CurrentSession = null;
        }

        /// <summary>
        /// 后台释放请求只在主线程操作原生页与托管会话。
        /// </summary>
        private async UniTaskVoid DisposeOnMainThreadAsync()
        {
            await UniTask.SwitchToMainThread();
            Dispose();
        }

        /// <summary>
        /// 单次支付页会话；先固定终态，再关闭页面，避免关闭回调覆盖支付成功。
        /// </summary>
        private sealed class ThirdPayWebViewSession : ThirdPayLogOwner, IDisposable
        {
            private static long s_NextSessionId;
            private readonly long m_SessionId = Interlocked.Increment(ref s_NextSessionId);
            private readonly string m_ExpectedClientOrderId;
            private readonly IThirdPaySafeBrowsing m_SafeBrowsing;
            private readonly UniTaskCompletionSource<ThirdPayPaymentPageResult> m_CompletionSource = new UniTaskCompletionSource<ThirdPayPaymentPageResult>();
            private CancellationTokenRegistration m_CancellationRegistration;
            private bool m_IsSessionEstablished;
            private bool m_Completed;
            private bool m_Disposed;

            /// <summary>
            /// 将订单身份和浏览器所有权固定到单次会话。
            /// </summary>
            public ThirdPayWebViewSession(string expectedClientOrderId, IThirdPaySafeBrowsing browser)
            {
                m_ExpectedClientOrderId = expectedClientOrderId;
                m_SafeBrowsing = browser ?? throw new ArgumentNullException(nameof(browser));
            }

            /// <summary>
            /// 先订阅回调和取消，再启动展示；等待终态不依赖显示请求是否已经结束。
            /// </summary>
            public UniTask<ThirdPayPaymentPageResult> OpenWithResultAsync(string url, CancellationToken ct)
            {
                Application.deepLinkActivated += OnDeepLinkActivated;
                m_SafeBrowsing.UserDismissed += OnSafeBrowsingFinished;
                m_CancellationRegistration = ct.Register(() => CancelOnMainThreadAsync().Forget());
                if (!m_Completed) OpenSafeBrowsing(url, ct).Forget();
                return m_CompletionSource.Task;
            }

            /// <summary>
            /// 只有原生展示完成才建立页面会话，迟到的展示必须关闭而非恢复已终结会话。
            /// </summary>
            private async UniTaskVoid OpenSafeBrowsing(string url, CancellationToken ct)
            {
                try
                {
                    bool shown = m_SafeBrowsing.IsSupported && await m_SafeBrowsing.ShowAsync(url, m_SessionId, ct);
                    await UniTask.SwitchToMainThread();
                    if (m_Completed || m_Disposed)
                    {
                        m_SafeBrowsing.Dismiss(m_SessionId);
                        return;
                    }
                    if (!shown)
                    {
                        LogWarning("第三方支付 WebView failed 返回：来源=SafeBrowsingOpenFailed");
                        Complete(Failure(ThirdPayCloseReason.Unknown, ThirdPayPaymentFailureReason.PaymentPageOpenFailed));
                        return;
                    }
                    m_IsSessionEstablished = true;
                    LogDebug("第三方支付 SafeBrowsing 已显示");
                }
                catch (OperationCanceledException)
                {
                    await CancelOnMainThreadAsync();
                }
                catch (Exception ex)
                {
                    await UniTask.SwitchToMainThread();
                    LogWarning($"第三方支付 WebView failed 返回：来源=SafeBrowsingException，Error={ex.Message}");
                    Complete(Failure(ThirdPayCloseReason.PaymentPageException, ThirdPayPaymentFailureReason.PaymentPageOpenException));
                }
            }

            /// <summary>
            /// 取消回调可能来自后台线程，统一调度以保持终态与原生操作串行。
            /// </summary>
            private async UniTask CancelOnMainThreadAsync()
            {
                await UniTask.SwitchToMainThread();
                Complete(Failure(ThirdPayCloseReason.OperationCancelled, ThirdPayPaymentFailureReason.OperationCancelled));
            }

            /// <summary>
            /// 接收 Unity 全局链接；非法与错单消息不结束当前支付。
            /// </summary>
            private void OnDeepLinkActivated(string url)
            {
                if (m_Completed || m_Disposed || !ThirdPayBrowserMessage.TryParse(url, out ThirdPayBrowserMessage message)) return;
                ThirdPayCallbackResolveResult resolve = ThirdPayWebViewCallbackResolver.ResolveCallback(message.Path, message.Args, out ThirdPayWebViewCallback callback);
                if (resolve == ThirdPayCallbackResolveResult.NotPaymentCallback || resolve == ThirdPayCallbackResolveResult.InvalidPayload) return;
                if (!IsCallbackForCurrentOrder(callback))
                {
                    LogWarning($"第三方支付 SafeBrowsing 忽略非当前订单回调：ExpectedOrderId={m_ExpectedClientOrderId}，CallbackOrderId={callback.OrderId}");
                    return;
                }
                ThirdPayPaymentPageResult result;
                if (resolve == ThirdPayCallbackResolveResult.UnknownStatus)
                    result = Failure(ThirdPayCloseReason.PayCallbackUnknownStatus, ThirdPayPaymentFailureReason.PaymentCallbackUnknownStatus, callback.RawStatus);
                else if (callback.Result == ThirdPayOpenResult.Success)
                    result = ThirdPayPaymentPageResult.CallbackSuccess(ThirdPayExternalBrowserLaunchMode.IOSSafeBrowsing, callback.Status);
                else if (callback.Result == ThirdPayOpenResult.Cancel)
                    result = ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Cancel, ThirdPayExternalBrowserLaunchMode.IOSSafeBrowsing, callback.Status, ThirdPayCloseReason.CloseCallback, ThirdPayPaymentFailureReason.OperationCancelled, 0, string.Empty, m_IsSessionEstablished);
                else
                    result = ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, ThirdPayExternalBrowserLaunchMode.IOSSafeBrowsing, callback.Status, ThirdPayCloseReason.PayCallbackFailed, ThirdPayPaymentFailureReason.PaymentCallbackFailed, callback.RawStatus, string.Empty, m_IsSessionEstablished);
                LogDebug($"第三方支付 WebView message 返回：Path={message.Path}，Result={result.Result}");
                Complete(result);
            }

            /// <summary>
            /// 不含订单号的 close_callback 作用于当前页面，其余回调精确匹配订单。
            /// </summary>
            private bool IsCallbackForCurrentOrder(ThirdPayWebViewCallback callback)
            {
                return !string.Equals(callback.Path, "pay_callback", StringComparison.Ordinal)
                    || string.IsNullOrEmpty(m_ExpectedClientOrderId)
                    || string.Equals(callback.OrderId, m_ExpectedClientOrderId, StringComparison.Ordinal);
            }

            /// <summary>
            /// 仅当前原生页面的用户关闭事件能结束当前会话。
            /// </summary>
            private void OnSafeBrowsingFinished(long sessionId)
            {
                if (sessionId != m_SessionId || m_Disposed) return;
                LogDebug("第三方支付 WebView 关闭返回：来源=IOSSafeBrowsingDismissed");
                Complete(ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Cancel, ThirdPayExternalBrowserLaunchMode.IOSSafeBrowsing, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.IOSSafeBrowsingDismissed, ThirdPayPaymentFailureReason.OperationCancelled, 0, string.Empty, m_IsSessionEstablished));
            }

            /// <summary>
            /// 保留实际展示状态的失败上下文，不把提交显示请求当作成功展示。
            /// </summary>
            private ThirdPayPaymentPageResult Failure(ThirdPayCloseReason closeReason, ThirdPayPaymentFailureReason failureReason, int code = 0)
            {
                return ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, ThirdPayExternalBrowserLaunchMode.IOSSafeBrowsing, ThirdPayWebViewCallbackStatus.Unknown, closeReason, failureReason, code, string.Empty, m_IsSessionEstablished);
            }

            /// <summary>
            /// 固定唯一终态后主动关闭原生页，完成等待前就确定清理所有权。
            /// </summary>
            public void Complete(ThirdPayPaymentPageResult result)
            {
                if (m_Completed) return;
                m_Completed = true;
                LogDebug($"第三方支付 WebView 完成：Result={result.Result}，CloseReason={result.CloseReason}");
                try { m_SafeBrowsing.Dismiss(m_SessionId); }
                finally { m_CompletionSource.TrySetResult(result); }
            }

            /// <summary>
            /// Store 释放也使用同一个终态门禁。
            /// </summary>
            public void CompleteAsDisposed()
            {
                Complete(Failure(ThirdPayCloseReason.StoreDisposed, ThirdPayPaymentFailureReason.StoreDisposed));
            }

            /// <summary>
            /// 对称移除 Unity、取消与浏览器事件，重复释放无副作用。
            /// </summary>
            public void Dispose()
            {
                if (m_Disposed) return;
                m_Disposed = true;
                Application.deepLinkActivated -= OnDeepLinkActivated;
                m_SafeBrowsing.UserDismissed -= OnSafeBrowsingFinished;
                m_CancellationRegistration.Dispose();
                m_SafeBrowsing.Dismiss(m_SessionId);
                m_SafeBrowsing.Dispose();
            }
        }
    }
}
