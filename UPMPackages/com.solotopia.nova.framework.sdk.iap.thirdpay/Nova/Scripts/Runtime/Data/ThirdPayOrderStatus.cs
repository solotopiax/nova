/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayOrderStatus.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   第三方支付服务端订单状态处理
 ***************************************************************/

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 服务端状态对本地订单、业务结果和事件发布的统一处理决策。
    /// </summary>
    internal readonly struct ThirdPayOrderResolution
    {
        /// <summary>
        /// 初始化订单处理决策。
        /// </summary>
        /// <param name="removeOrder">是否移除本地订单。</param>
        /// <param name="isSuccess">是否属于支付成功状态。</param>
        /// <param name="canDeliver">是否允许客户端发货。</param>
        /// <param name="raiseTerminalEvent">是否需要发布终态事件。</param>
        private ThirdPayOrderResolution(bool removeOrder, bool isSuccess, bool canDeliver, bool raiseTerminalEvent)
        {
            RemoveOrder = removeOrder;
            IsSuccess = isSuccess;
            CanDeliver = canDeliver;
            RaiseTerminalEvent = raiseTerminalEvent;
        }

        /// <summary>
        /// 获取是否移除本地订单。
        /// </summary>
        public bool RemoveOrder { get; }

        /// <summary>
        /// 获取是否属于支付成功状态。
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 获取是否允许客户端发货。
        /// </summary>
        public bool CanDeliver { get; }

        /// <summary>
        /// 获取是否需要发布终态事件。
        /// </summary>
        public bool RaiseTerminalEvent { get; }

        /// <summary>
        /// 根据服务端订单状态创建客户端处理决策。
        /// </summary>
        /// <param name="status">服务端订单状态。</param>
        /// <returns>对应的客户端处理决策。</returns>
        public static ThirdPayOrderResolution FromStatus(PbNetThirdVerifyOrderStatus status)
        {
            switch (status)
            {
                case PbNetThirdVerifyOrderStatus.Processing:
                    return new ThirdPayOrderResolution(false, false, false, false);
                case PbNetThirdVerifyOrderStatus.Paid:
                    return new ThirdPayOrderResolution(true, true, true, true);
                case PbNetThirdVerifyOrderStatus.PendingPayment:
                case PbNetThirdVerifyOrderStatus.FailedOrExpired:
                    return new ThirdPayOrderResolution(true, false, false, true);
                case PbNetThirdVerifyOrderStatus.Delivered:
                    return new ThirdPayOrderResolution(true, true, false, false);
                case PbNetThirdVerifyOrderStatus.NotFound:
                    return new ThirdPayOrderResolution(true, false, false, true);
                default:
                    return new ThirdPayOrderResolution(false, false, false, false);
            }
        }

        /// <summary>
        /// 将服务端原始订单状态归一化为客户端打点状态，避免直接复用服务端枚举语义。
        /// </summary>
        /// <param name="status">服务端原始订单状态。</param>
        /// <returns>客户端归一化订单状态。</returns>
        public static ThirdPayClientOrderStatus ToClientOrderStatus(PbNetThirdVerifyOrderStatus status)
        {
            switch (status)
            {
                case PbNetThirdVerifyOrderStatus.Paid: return ThirdPayClientOrderStatus.SuccessDeliverable;
                case PbNetThirdVerifyOrderStatus.Delivered: return ThirdPayClientOrderStatus.SuccessAlreadyDelivered;
                case PbNetThirdVerifyOrderStatus.PendingPayment: return ThirdPayClientOrderStatus.PaymentPending;
                case PbNetThirdVerifyOrderStatus.Processing: return ThirdPayClientOrderStatus.Processing;
                case PbNetThirdVerifyOrderStatus.FailedOrExpired: return ThirdPayClientOrderStatus.FailedOrExpired;
                case PbNetThirdVerifyOrderStatus.NotFound: return ThirdPayClientOrderStatus.NotFound;
                default: return ThirdPayClientOrderStatus.UnknownServerStatus;
            }
        }

        /// <summary>
        /// 根据服务端终态生成本地订单删除原因。
        /// </summary>
        /// <param name="status">服务端原始订单状态。</param>
        /// <returns>对应的本地订单删除原因；不应删除的状态返回未知原因。</returns>
        public static ThirdPayOrderDeleteReason ToDeleteReason(PbNetThirdVerifyOrderStatus status)
        {
            switch (status)
            {
                case PbNetThirdVerifyOrderStatus.Paid: return ThirdPayOrderDeleteReason.Paid;
                case PbNetThirdVerifyOrderStatus.Delivered: return ThirdPayOrderDeleteReason.AlreadyDelivered;
                case PbNetThirdVerifyOrderStatus.PendingPayment: return ThirdPayOrderDeleteReason.PendingPaymentExhausted;
                case PbNetThirdVerifyOrderStatus.FailedOrExpired: return ThirdPayOrderDeleteReason.FailedOrExpired;
                case PbNetThirdVerifyOrderStatus.NotFound: return ThirdPayOrderDeleteReason.NotFound;
                default: return ThirdPayOrderDeleteReason.Unknown;
            }
        }

        /// <summary>
        /// 将服务端订单状态映射到细分的 ThirdPay 验单失败原因。
        /// </summary>
        /// <param name="status">服务端原始订单状态。</param>
        /// <returns>对应的细分支付失败原因。</returns>
        public static ThirdPayPaymentFailureReason ToFailureReason(PbNetThirdVerifyOrderStatus status)
        {
            switch (status)
            {
                case PbNetThirdVerifyOrderStatus.PendingPayment: return ThirdPayPaymentFailureReason.ServerOrderPendingPayment;
                case PbNetThirdVerifyOrderStatus.Processing: return ThirdPayPaymentFailureReason.ServerOrderProcessing;
                case PbNetThirdVerifyOrderStatus.FailedOrExpired: return ThirdPayPaymentFailureReason.ServerOrderFailedOrExpired;
                case PbNetThirdVerifyOrderStatus.NotFound: return ThirdPayPaymentFailureReason.ServerOrderNotFound;
                case PbNetThirdVerifyOrderStatus.Paid:
                case PbNetThirdVerifyOrderStatus.Delivered:
                    return ThirdPayPaymentFailureReason.Unknown;
                default:
                    return ThirdPayPaymentFailureReason.UnknownServerOrderStatus;
            }
        }
    }
}
