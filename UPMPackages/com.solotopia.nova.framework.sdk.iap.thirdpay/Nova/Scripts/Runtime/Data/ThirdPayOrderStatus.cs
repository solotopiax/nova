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
    }
}
