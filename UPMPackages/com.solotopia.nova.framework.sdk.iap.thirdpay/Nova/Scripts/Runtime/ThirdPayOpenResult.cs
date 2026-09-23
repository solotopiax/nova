/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayOpenResult.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   第三方支付页打开结果三态枚举
 ***************************************************************/

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付页打开结果。
    /// 由框架内支付页服务返回，区分用户路径以决定主链路下一步。
    /// </summary>
    internal enum ThirdPayOpenResult
    {
        /// <summary>
        /// 应用内支付页确认支付流程完成，可以立即发起验单。
        /// </summary>
        Success = 1,

        /// <summary>
        /// 用户主动取消支付页面。
        /// </summary>
        Cancel = 2,

        /// <summary>
        /// 打开失败 / 进程异常 / 不确定状态，调用方按“支付页未打开成功”处理。
        /// </summary>
        Failed = 3,
    }

    /// <summary>
    /// 支付页的结构化终态结果，统一承载页面结果、打开方式、回调状态和原生错误信息。
    /// </summary>
    internal readonly struct ThirdPayPaymentPageResult
    {
        /// <summary>
        /// 创建支付页结构化结果。
        /// </summary>
        /// <param name="result">支付页终态结果。</param>
        /// <param name="openMode">支付页实际打开方式。</param>
        /// <param name="callbackStatus">pay_callback 返回的状态。</param>
        /// <param name="closeReason">支付页关闭原因。</param>
        /// <param name="failureReason">支付流程失败原因。</param>
        /// <param name="nativeErrorCode">支付页或原生能力错误码。</param>
        /// <param name="nativeErrorMessage">支付页或原生能力错误描述。</param>
        /// <param name="isSessionEstablished">是否已经提交并建立支付会话。</param>
        public ThirdPayPaymentPageResult(ThirdPayOpenResult result, ThirdPayExternalBrowserLaunchMode openMode, ThirdPayWebViewCallbackStatus callbackStatus, ThirdPayCloseReason closeReason, ThirdPayPaymentFailureReason failureReason, int nativeErrorCode, string nativeErrorMessage, bool isSessionEstablished)
        {
            Result = result;
            OpenMode = openMode;
            CallbackStatus = callbackStatus;
            CloseReason = closeReason;
            FailureReason = failureReason;
            NativeErrorCode = nativeErrorCode;
            NativeErrorMessage = nativeErrorMessage ?? string.Empty;
            IsSessionEstablished = isSessionEstablished;
        }

        /// <summary>
        /// 支付页终态结果。
        /// </summary>
        public ThirdPayOpenResult Result { get; }

        /// <summary>
        /// 支付页实际打开方式。
        /// </summary>
        public ThirdPayExternalBrowserLaunchMode OpenMode { get; }

        /// <summary>
        /// pay_callback 返回的状态；未收到回调时为 Unknown。
        /// </summary>
        public ThirdPayWebViewCallbackStatus CallbackStatus { get; }

        /// <summary>
        /// 支付页关闭原因。
        /// </summary>
        public ThirdPayCloseReason CloseReason { get; }

        /// <summary>
        /// 支付流程失败原因。
        /// </summary>
        public ThirdPayPaymentFailureReason FailureReason { get; }

        /// <summary>
        /// 支付页或原生能力错误码。
        /// </summary>
        public int NativeErrorCode { get; }

        /// <summary>
        /// 支付页或原生能力错误描述。
        /// </summary>
        public string NativeErrorMessage { get; }

        /// <summary>
        /// 是否已经提交并建立支付会话；Application.OpenURL 回退仅表示请求已提交，不表示确认展示成功。
        /// </summary>
        public bool IsSessionEstablished { get; }

        /// <summary>
        /// 判断当前结果是否为有效的支付成功回调。
        /// </summary>
        /// <returns>收到状态为 Success 或 SuccessWithoutChannelSync 的 pay_callback 时返回 true。</returns>
        public bool HasSuccessfulPayCallback => Result == ThirdPayOpenResult.Success && (CallbackStatus == ThirdPayWebViewCallbackStatus.Success || CallbackStatus == ThirdPayWebViewCallbackStatus.SuccessWithoutChannelSync);

        /// <summary>
        /// 创建支付页已建立但尚未收到回调的结果。
        /// </summary>
        /// <param name="openMode">实际打开方式。</param>
        /// <returns>等待回调或返回前台的支付页结果。</returns>
        public static ThirdPayPaymentPageResult Opened(ThirdPayExternalBrowserLaunchMode openMode)
        {
            return new ThirdPayPaymentPageResult(ThirdPayOpenResult.Success, openMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.Unknown, ThirdPayPaymentFailureReason.Unknown, 0, string.Empty, true);
        }

        /// <summary>
        /// 创建支付页成功回调结果。
        /// </summary>
        /// <param name="openMode">实际打开方式。</param>
        /// <param name="callbackStatus">支付回调状态。</param>
        /// <returns>可立即验单的支付页结果。</returns>
        public static ThirdPayPaymentPageResult CallbackSuccess(ThirdPayExternalBrowserLaunchMode openMode, ThirdPayWebViewCallbackStatus callbackStatus)
        {
            return new ThirdPayPaymentPageResult(ThirdPayOpenResult.Success, openMode, callbackStatus, ThirdPayCloseReason.Unknown, ThirdPayPaymentFailureReason.Unknown, 0, string.Empty, true);
        }

        /// <summary>
        /// 创建支付页关闭或失败结果。
        /// </summary>
        /// <param name="result">支付页终态结果。</param>
        /// <param name="openMode">实际打开方式。</param>
        /// <param name="callbackStatus">支付回调状态。</param>
        /// <param name="closeReason">支付页关闭原因。</param>
        /// <param name="failureReason">支付流程失败原因。</param>
        /// <param name="nativeErrorCode">支付页或原生能力错误码。</param>
        /// <param name="nativeErrorMessage">支付页或原生能力错误描述。</param>
        /// <param name="isSessionEstablished">是否已经建立支付会话。</param>
        /// <returns>包含失败和关闭上下文的支付页结果。</returns>
        public static ThirdPayPaymentPageResult Completed(ThirdPayOpenResult result, ThirdPayExternalBrowserLaunchMode openMode, ThirdPayWebViewCallbackStatus callbackStatus, ThirdPayCloseReason closeReason, ThirdPayPaymentFailureReason failureReason, int nativeErrorCode, string nativeErrorMessage, bool isSessionEstablished)
        {
            return new ThirdPayPaymentPageResult(result, openMode, callbackStatus, closeReason, failureReason, nativeErrorCode, nativeErrorMessage, isSessionEstablished);
        }
    }
}
