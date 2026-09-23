/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayExternalBrowserPaySession.cs
 * author:    yingzheng
 * created:   2026/8/27
 * descrip:   ThirdPay 外部浏览器支付运行期会话
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 单笔外部浏览器支付的运行期状态。
    /// </summary>
    internal sealed class ThirdPayExternalBrowserPaySession : IDisposable
    {
        /// <summary>
        /// 初始化外部浏览器支付会话。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <param name="userId">订单创建时的用户 UID。</param>
        /// <param name="customData">支付请求透传数据。</param>
        /// <param name="receiptParam">票据透传参数。</param>
        public ThirdPayExternalBrowserPaySession(string clientOrderId, long tableId, string userId, string customData, string receiptParam)
        {
            ClientOrderId = clientOrderId;
            TableId = tableId;
            UserId = userId ?? string.Empty;
            CustomData = customData ?? string.Empty;
            ReceiptParam = receiptParam ?? string.Empty;
            SessionCts = new CancellationTokenSource();
        }

        /// <summary>
        /// 客户端订单号。
        /// </summary>
        public string ClientOrderId { get; }

        /// <summary>
        /// 支付商品表行 ID。
        /// </summary>
        public long TableId { get; }

        /// <summary>
        /// 订单创建时的用户 UID。
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// 支付请求透传数据，返回验单完成时回传给 PayAsync 调用方。
        /// </summary>
        public string CustomData { get; }

        /// <summary>
        /// 票据透传参数，返回验单完成时回传给 PayAsync 调用方。
        /// </summary>
        public string ReceiptParam { get; }

        /// <summary>
        /// 外部浏览器返回验单结果完成源，用于桥接生命周期回调到 PayAsync await 点。
        /// </summary>
        public UniTaskCompletionSource<IAPResult> PayTcs { get; } = new UniTaskCompletionSource<IAPResult>();

        /// <summary>
        /// 当前浏览器支付是否已经触发过离开 App。
        /// </summary>
        public bool HasLeftApp;

        /// <summary>
        /// 当前会话是否已经进入验单流程。
        /// </summary>
        public bool IsValidating;

        /// <summary>
        /// 当前会话是否已经完成或被清理。
        /// </summary>
        public bool Completed;

        /// <summary>
        /// 外部支付页实际采用的打开方式。
        /// </summary>
        public ThirdPayExternalBrowserLaunchMode OpenMode = ThirdPayExternalBrowserLaunchMode.Failed;

        /// <summary>
        /// 当前会话是否已经成功提交支付页打开请求。
        /// </summary>
        public bool IsPaymentPageEstablished;

        /// <summary>
        /// 当前会话最后一次可归因的支付回调状态。
        /// </summary>
        public ThirdPayWebViewCallbackStatus CallbackStatus = ThirdPayWebViewCallbackStatus.Unknown;

        /// <summary>
        /// 当前会话最终关闭原因；成功回调时保持 Unknown。
        /// </summary>
        public ThirdPayCloseReason CloseReason = ThirdPayCloseReason.Unknown;

        /// <summary>
        /// 当前会话最终失败原因；无明确失败时保持 Unknown。
        /// </summary>
        public ThirdPayPaymentFailureReason FailureReason = ThirdPayPaymentFailureReason.Unknown;

        /// <summary>
        /// 支付页或原生能力返回的补充错误码。
        /// </summary>
        public int NativeErrorCode;

        /// <summary>
        /// 支付页或原生能力返回的补充错误描述。
        /// </summary>
        public string NativeErrorMessage = string.Empty;

        /// <summary>
        /// 返回 App 后的倒计时等待期是否已经持有 Loading 引用。
        /// </summary>
        public bool ReturnWaitingRefAdded;

        /// <summary>
        /// App 返回前台的版本号；每次稳定返回前台都会递增，用于让旧延迟任务失效。
        /// </summary>
        public int ReturnVersion;

        /// <summary>
        /// 会话级取消源；会话清理时取消验单和延迟任务。
        /// </summary>
        public CancellationTokenSource SessionCts;

        /// <summary>
        /// 当前返回前台后的延迟验单取消源；反复切前后台时会被替换。
        /// </summary>
        public CancellationTokenSource DelayCts;

        /// <summary>
        /// 支付页成功或关闭终态是否已经上报；通过原子门禁避免 Deep Link 与返回倒计时重复上报。
        /// </summary>
        private int m_PaymentPageTrackFinalized;

        /// <summary>
        /// 尝试取得当前支付页会话唯一终态埋点权。
        /// </summary>
        /// <returns>本次调用首次取得终态埋点权时返回 true。</returns>
        public bool TryFinalizePaymentPageTrack()
        {
            return Interlocked.CompareExchange(ref m_PaymentPageTrackFinalized, 1, 0) == 0;
        }

        /// <summary>
        /// 记录当前会话的支付页结果上下文，供成功回调或最终关闭埋点统一读取。
        /// </summary>
        /// <param name="pageResult">需要记录的支付页结果。</param>
        public void RecordPaymentPageResult(ThirdPayPaymentPageResult pageResult)
        {
            OpenMode = pageResult.OpenMode;
            IsPaymentPageEstablished = pageResult.IsSessionEstablished;
            CallbackStatus = pageResult.CallbackStatus;
            CloseReason = pageResult.CloseReason;
            FailureReason = pageResult.FailureReason;
            NativeErrorCode = pageResult.NativeErrorCode;
            NativeErrorMessage = pageResult.NativeErrorMessage ?? string.Empty;
        }

        /// <summary>
        /// 记录一个比无回调兜底更具体的关闭候选；已经记录明确回调错误时不再被弱原因覆盖。
        /// </summary>
        /// <param name="callbackStatus">支付回调状态。</param>
        /// <param name="closeReason">支付页关闭原因。</param>
        /// <param name="failureReason">支付失败原因。</param>
        /// <param name="nativeErrorCode">回调原始状态或原生错误码。</param>
        /// <param name="nativeErrorMessage">原生错误描述。</param>
        public void RecordCloseCandidate(ThirdPayWebViewCallbackStatus callbackStatus, ThirdPayCloseReason closeReason, ThirdPayPaymentFailureReason failureReason, int nativeErrorCode = 0, string nativeErrorMessage = "")
        {
            if (CloseReason != ThirdPayCloseReason.Unknown && closeReason == ThirdPayCloseReason.ExternalBrowserReturnedWithoutCallback)
            {
                return;
            }

            CallbackStatus = callbackStatus;
            CloseReason = closeReason;
            FailureReason = failureReason;
            NativeErrorCode = nativeErrorCode;
            NativeErrorMessage = nativeErrorMessage ?? string.Empty;
        }

        /// <summary>
        /// 构造当前会话用于成功或关闭埋点的结构化支付页结果。
        /// </summary>
        /// <param name="result">支付页结果。</param>
        /// <returns>包含当前会话上下文的支付页结果。</returns>
        public ThirdPayPaymentPageResult BuildPaymentPageResult(ThirdPayOpenResult result)
        {
            return ThirdPayPaymentPageResult.Completed(result, OpenMode, CallbackStatus, CloseReason, FailureReason, NativeErrorCode, NativeErrorMessage, IsPaymentPageEstablished);
        }

        /// <summary>
        /// 取消并释放当前返回前台延迟验单计时器。
        /// </summary>
        public void CancelReturnDelay()
        {
            CancellationTokenSource delayCts = DelayCts;
            if (delayCts == null)
            {
                return;
            }

            DelayCts = null;
            if (!delayCts.IsCancellationRequested)
            {
                delayCts.Cancel();
            }

            delayCts.Dispose();
        }

        /// <summary>
        /// 释放会话取消源和延迟验单计时器。
        /// </summary>
        public void Dispose()
        {
            CancelReturnDelay();
            if (SessionCts == null)
            {
                return;
            }

            if (!SessionCts.IsCancellationRequested)
            {
                SessionCts.Cancel();
            }

            SessionCts.Dispose();
            SessionCts = null;
        }
    }
}
