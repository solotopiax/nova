/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayOrderStatus.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   第三方支付服务端订单状态映射
 ***************************************************************/

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付订单的客户端处理分类。
    /// </summary>
    internal enum ThirdPayOrderDisposition
    {
        /// <summary>
        /// 未识别的服务端状态。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 订单仍在等待支付或处理中。
        /// </summary>
        Pending = 1,

        /// <summary>
        /// 订单支付成功且允许客户端发货。
        /// </summary>
        Deliverable = 2,

        /// <summary>
        /// 订单已明确失败或过期。
        /// </summary>
        Failed = 3,

        /// <summary>
        /// 订单已经完成发货，不应重复发奖。
        /// </summary>
        AlreadyDelivered = 4,

        /// <summary>
        /// 服务端不存在该订单，属于终态失败。
        /// </summary>
        NotFound = 5,
    }

    /// <summary>
    /// 将服务端第三方支付订单状态转换为客户端处理分类。
    /// </summary>
    internal static class ThirdPayOrderStatusMapper
    {
        /// <summary>
        /// 映射服务端订单状态。
        /// </summary>
        /// <param name="status">服务端订单状态值。</param>
        /// <returns>客户端处理分类。</returns>
        public static ThirdPayOrderDisposition Map(int status)
        {
            switch (status)
            {
                case 1:
                case 2:
                    return ThirdPayOrderDisposition.Pending;
                case 3:
                    return ThirdPayOrderDisposition.Deliverable;
                case 4:
                    return ThirdPayOrderDisposition.Failed;
                case 5:
                    return ThirdPayOrderDisposition.AlreadyDelivered;
                case 6:
                    return ThirdPayOrderDisposition.NotFound;
                default:
                    return ThirdPayOrderDisposition.Unknown;
            }
        }
    }

    /// <summary>
    /// 服务端状态对本地订单、业务结果和事件发布的统一处理决策。
    /// </summary>
    internal readonly struct ThirdPayOrderResolution
    {
        /// <summary>
        /// 初始化订单处理决策。
        /// </summary>
        /// <param name="disposition">客户端处理分类。</param>
        /// <param name="removeOrder">是否移除本地订单。</param>
        /// <param name="isSuccess">是否属于支付成功状态。</param>
        /// <param name="canDeliver">是否允许客户端发货。</param>
        /// <param name="raiseTerminalEvent">是否需要发布终态事件。</param>
        private ThirdPayOrderResolution(ThirdPayOrderDisposition disposition, bool removeOrder, bool isSuccess, bool canDeliver, bool raiseTerminalEvent)
        {
            Disposition = disposition;
            RemoveOrder = removeOrder;
            IsSuccess = isSuccess;
            CanDeliver = canDeliver;
            RaiseTerminalEvent = raiseTerminalEvent;
        }

        /// <summary>
        /// 获取客户端处理分类。
        /// </summary>
        public ThirdPayOrderDisposition Disposition { get; }

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
        /// <param name="status">服务端订单状态值。</param>
        /// <returns>对应的客户端处理决策。</returns>
        public static ThirdPayOrderResolution FromStatus(int status)
        {
            ThirdPayOrderDisposition disposition = ThirdPayOrderStatusMapper.Map(status);
            switch (disposition)
            {
                case ThirdPayOrderDisposition.Deliverable:
                    return new ThirdPayOrderResolution(disposition, true, true, true, true);
                case ThirdPayOrderDisposition.Failed:
                    return new ThirdPayOrderResolution(disposition, true, false, false, true);
                case ThirdPayOrderDisposition.NotFound:
                    return new ThirdPayOrderResolution(disposition, true, false, false, true);
                case ThirdPayOrderDisposition.AlreadyDelivered:
                    return new ThirdPayOrderResolution(disposition, true, true, false, false);
                default:
                    return new ThirdPayOrderResolution(disposition, false, false, false, false);
            }
        }
    }
}
