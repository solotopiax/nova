/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPResult.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAP 支付结果，包含成功/失败两种构造路径
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 支付结果。
    /// 实现 IIAPResult 接口，提供成功路径（带/不带订阅到期时间）和失败路径两种构造方式。
    /// </summary>
    public sealed class IAPResult : IIAPResult
    {

        /// <inheritdoc/>
        public long TableId { get; }

        /// <inheritdoc/>
        public bool IsSuccess { get; }


        /// <inheritdoc/>
        public IAPErrorSource ErrorSource { get; }

        /// <inheritdoc/>
        public int ErrorCode { get; }

        /// <inheritdoc/>
        public string ErrorDesc { get; }

        /// <inheritdoc/>
        public string OrderId { get; }

        /// <summary>
        /// 是否为补单恢复的历史订单；true 表示本次结果来自补单而非新购。
        /// </summary>
        public bool IsRecoveredOrder { get; }

        /// <summary>
        /// 是否可以发货；false 表示验单流程尚未完成或服务器拒绝。
        /// </summary>
        public bool CanDeliver { get; }

        /// <summary>
        /// 调用方传入的自定义数据，原样回传。
        /// </summary>
        public string CustomData { get; }

        /// <summary>
        /// 票据透传字符串，来自 IAPRequest.ReceiptParam。
        /// 由 Store 从平台票据或服务端验单响应回填，支付、补单和恢复均可带回；无透传时为 null。外部只读。
        /// </summary>
        public string ReceiptParam { get; }


        /// <summary>
        /// 订阅到期时间（毫秒 Unix 时间戳），仅订阅类商品成功时有值，其余情况为 0。
        /// </summary>
        public long SubscriptionExpireTimeMs { get; }

        /// <summary>
        /// 构造支付成功结果。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="orderId">订单唯一 ID。</param>
        /// <param name="isRecoveredOrder">是否为补单恢复的历史订单。</param>
        /// <param name="canDeliver">是否可以发货。</param>
        /// <param name="customData">调用方传入的自定义数据。</param>
        /// <param name="receiptParam">平台票据透传字符串；无则为 null。</param>
        public IAPResult(long tableId, string orderId, bool isRecoveredOrder, bool canDeliver, string customData, string receiptParam = null)
        {
            IsSuccess = true;
            ErrorCode = 0;
            ErrorSource = IAPErrorSource.None;
            TableId = tableId;
            OrderId = orderId;
            IsRecoveredOrder = isRecoveredOrder;
            CanDeliver = canDeliver;
            CustomData = customData;
            ReceiptParam = receiptParam;
        }

        /// <summary>
        /// 构造携带订阅到期时间的支付成功结果。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="orderId">订单唯一 ID。</param>
        /// <param name="isRecoveredOrder">是否为补单恢复的历史订单。</param>
        /// <param name="canDeliver">是否可以发货。</param>
        /// <param name="customData">调用方传入的自定义数据。</param>
        /// <param name="subscriptionExpireTimeMs">订阅到期时间（毫秒 Unix 时间戳）。</param>
        /// <param name="receiptParam">平台票据透传字符串；无则为 null。</param>
        /// <returns>包含订阅到期时间的成功结果实例。</returns>
        public static IAPResult SuccessWithExpire(long tableId, string orderId, bool isRecoveredOrder, bool canDeliver, string customData, long subscriptionExpireTimeMs, string receiptParam = null)
        {
            return new IAPResult(tableId, orderId, isRecoveredOrder, canDeliver, customData, subscriptionExpireTimeMs, receiptParam);
        }

        /// <summary>
        /// 构造支付失败结果，errorCode 为各 store 自定义错误码枚举强转的 int。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="errorCode">store 自定义错误码枚举强转的 int 值。</param>
        /// <param name="errorSource">错误码来源，标识 errorCode 所属的错误码枚举。</param>
        /// <param name="errorDesc">错误描述。</param>
        /// <param name="customData">调用方传入的自定义数据。</param>
        /// <param name="receiptParam">平台票据透传字符串；无则为 null。</param>
        public IAPResult(long tableId, int errorCode, IAPErrorSource errorSource, string errorDesc, string customData, string receiptParam = null)
        {
            IsSuccess = false;
            ErrorCode = errorCode;
            ErrorSource = errorSource;
            TableId = tableId;
            ErrorDesc = errorDesc;
            CustomData = customData;
            ReceiptParam = receiptParam;
        }

        /// <summary>
        /// 构造已经取得稳定订单号的支付失败结果。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="errorCode">store 自定义错误码枚举强转的 int 值。</param>
        /// <param name="errorSource">错误码来源，标识 errorCode 所属的错误码枚举。</param>
        /// <param name="errorDesc">错误描述。</param>
        /// <param name="customData">调用方传入的自定义数据。</param>
        /// <param name="orderId">已经发送到服务端的稳定订单号。</param>
        /// <param name="isRecoveredOrder">是否为补单恢复的历史订单。</param>
        /// <param name="receiptParam">平台票据透传字符串；无则为 null。</param>
        public IAPResult(long tableId, int errorCode, IAPErrorSource errorSource, string errorDesc, string customData, string orderId,
            bool isRecoveredOrder, string receiptParam = null)
            : this(tableId, errorCode, errorSource, errorDesc, customData, receiptParam)
        {
            OrderId = orderId;
            IsRecoveredOrder = isRecoveredOrder;
        }

        /// <summary>
        /// 内部构造：带订阅到期时间的成功结果，由 SuccessWithExpire 工厂方法调用。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="orderId">订单唯一 ID。</param>
        /// <param name="isRecoveredOrder">是否为补单恢复的历史订单。</param>
        /// <param name="canDeliver">是否可以发货。</param>
        /// <param name="customData">调用方传入的自定义数据。</param>
        /// <param name="subscriptionExpireTimeMs">订阅到期时间（毫秒 Unix 时间戳）。</param>
        /// <param name="receiptParam">平台票据透传字符串；无则为 null。</param>
        private IAPResult(long tableId, string orderId, bool isRecoveredOrder, bool canDeliver, string customData, long subscriptionExpireTimeMs, string receiptParam)
        {
            IsSuccess = true;
            ErrorCode = 0;
            ErrorSource = IAPErrorSource.None;
            TableId = tableId;
            OrderId = orderId;
            IsRecoveredOrder = isRecoveredOrder;
            CanDeliver = canDeliver;
            CustomData = customData;
            SubscriptionExpireTimeMs = subscriptionExpireTimeMs;
            ReceiptParam = receiptParam;
        }
    }
}
