/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.Track.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   ThirdPayStore 支付打点转发
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 承载 ThirdPayStore 的支付打点实现。
    /// </summary>
    public sealed partial class ThirdPayStore
    {
        /// <summary>
        /// PayAsync 失败结果对应的精确原因和附加属性。
        /// </summary>
        private sealed class ThirdPayReturnedFailureTrackContext
        {
            /// <summary>
            /// 创建返回失败埋点上下文。
            /// </summary>
            /// <param name="reason">ThirdPay 细分失败原因。</param>
            /// <param name="properties">需要追加到 local_pay_fail 的上下文字段。</param>
            /// <param name="shouldTrackLocalPayFailure">PayAsync 返回失败时是否仍需上报本地支付失败。</param>
            public ThirdPayReturnedFailureTrackContext(ThirdPayPaymentFailureReason reason, Dictionary<string, object> properties, bool shouldTrackLocalPayFailure)
            {
                Reason = reason;
                Properties = properties ?? new Dictionary<string, object>();
                ShouldTrackLocalPayFailure = shouldTrackLocalPayFailure;
            }

            /// <summary>
            /// ThirdPay 细分失败原因。
            /// </summary>
            public ThirdPayPaymentFailureReason Reason { get; }

            /// <summary>
            /// 支付页或验单链路候选附加属性。
            /// </summary>
            public Dictionary<string, object> Properties { get; }

            /// <summary>
            /// PayAsync 返回失败时是否仍需上报本地支付失败；支付页已有终态或结果来自验单时为 false。
            /// </summary>
            public bool ShouldTrackLocalPayFailure { get; }
        }

        /// <summary>
        /// 上报用户发起第三方支付。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        internal void TrackBuyInternal(IAPThirdPayRequest request)
        {
            if (request == null)
            {
                return;
            }

            TrackBuy(request.TableId, ResolveThirdProductId(request.TableId), IsTrackDebugMode(), ResolvePrice(request.TableId), request.CustomData);
        }

        /// <summary>
        /// 上报客户端第三方订单、平台授权和支付 URL 均准备成功。
        /// </summary>
        /// <param name="order">已保存的本地订单。</param>
        internal void TrackCreateOrderSuccessInternal(ThirdPayOrderRecord order)
        {
            if (order == null)
            {
                return;
            }

            Dictionary<string, object> properties = CreateThirdPayTrackProperties(order.TableId, order.CustomData);
            AppendTrackProperties(properties, BuildOrderIdProperties(order.ClientOrderId, string.Empty));
            EmitTrackEvent(IAPTrackEvents.CreateOrderSuccess, properties);
        }

        /// <summary>
        /// 上报第三方订单、平台授权或支付 URL 准备失败，并以独立字段记录创建失败枚举。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="reason">创建订单失败原因。</param>
        /// <param name="clientOrderId">已生成但尚未完成创建的客户端订单号。</param>
        /// <param name="nativeErrorCode">原生或服务端补充错误码。</param>
        /// <param name="nativeErrorMessage">原生或服务端补充错误描述。</param>
        internal void TrackCreateOrderFailInternal(IAPThirdPayRequest request, ThirdPayCreateOrderFailureReason reason, string clientOrderId = "", int nativeErrorCode = 0, string nativeErrorMessage = "")
        {
            if (request == null)
            {
                return;
            }

            Dictionary<string, object> properties = CreateThirdPayTrackProperties(request.TableId, request.CustomData);
            AppendTrackProperties(properties, BuildOrderIdProperties(clientOrderId, string.Empty));
            properties[IAPTrackFields.ThirdPayCreateFailureReason] = (int)reason;
            properties[IAPTrackFields.NativeErrorCode] = nativeErrorCode;
            properties[IAPTrackFields.NativeErrorMessage] = nativeErrorMessage ?? string.Empty;
            properties[IAPTrackFields.Reason] = (int)MapCreateOrderFailureReasonToErrorCode(reason);
            properties[IAPTrackFields.ReasonDetail] = ThirdPayTrackReasonDescriptions.GetCreateOrderFailureDetail(reason);
            properties[IAPTrackFields.NetError] = false;
            EmitTrackEvent(IAPTrackEvents.CreateOrderFail, properties);
        }

        /// <summary>
        /// 兼容旧调用方上报创建订单失败；文本详情作为原生补充信息保留，枚举采用未知原因。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="reasonDetail">旧调用方提供的诊断文本。</param>
        internal void TrackCreateOrderFailInternal(IAPThirdPayRequest request, string reasonDetail)
        {
            TrackCreateOrderFailInternal(request, ThirdPayCreateOrderFailureReason.Unknown, string.Empty, 0, reasonDetail);
        }

        /// <summary>
        /// 上报已建立支付会话但未收到成功 pay_callback 的第三方支付页关闭。
        /// </summary>
        /// <param name="order">当前本地订单。</param>
        /// <param name="pageResult">支付页终态及原生上下文。</param>
        internal void TrackThirdPayCloseOrderInternal(ThirdPayOrderRecord order, ThirdPayPaymentPageResult pageResult)
        {
            if (order == null)
            {
                return;
            }

            ThirdPayCloseReason closeReason = pageResult.CloseReason;
            Dictionary<string, object> properties = CreateThirdPayTrackProperties(order.TableId, order.CustomData);
            AppendTrackProperties(properties, BuildPaymentPageProperties(order.ClientOrderId, string.Empty, pageResult));
            properties[IAPTrackFields.ThirdPayTypeId] = 0;
            properties[IAPTrackFields.ReasonDetail] = ThirdPayTrackReasonDescriptions.GetCloseReasonDetail(closeReason);
            EmitTrackEvent(IAPTrackEvents.ThirdPayCloseOrder, properties);
        }

        /// <summary>
        /// 兼容旧调用方上报未知关闭原因的第三方支付页关闭。
        /// </summary>
        /// <param name="order">当前本地订单。</param>
        internal void TrackThirdPayCloseOrderInternal(ThirdPayOrderRecord order)
        {
            TrackThirdPayCloseOrderInternal(order, ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Cancel, ThirdPayExternalBrowserLaunchMode.Failed, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.Unknown, ThirdPayPaymentFailureReason.Unknown, 0, string.Empty, true));
        }

        /// <summary>
        /// 上报收到成功 pay_callback 后的本地支付成功事件。
        /// </summary>
        /// <param name="order">当前本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="pageResult">支付页成功回调结果。</param>
        internal void TrackLocalPaySuccessInternal(ThirdPayOrderRecord order, bool isRecovered, ThirdPayPaymentPageResult pageResult)
        {
            if (order == null || !pageResult.HasSuccessfulPayCallback)
            {
                return;
            }

            Dictionary<string, object> properties = CreateThirdPayTrackProperties(order.TableId, order.CustomData);
            AppendTrackProperties(properties, BuildPaymentPageProperties(order.ClientOrderId, string.Empty, pageResult));
            properties[IAPTrackFields.AddOrder] = isRecovered;
            EmitTrackEvent(IAPTrackEvents.LocalPaySuccess, properties);
        }

        /// <summary>
        /// 兼容旧调用方上报支付成功；该重载按成功回调处理。
        /// </summary>
        /// <param name="order">当前本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        internal void TrackLocalPaySuccessInternal(ThirdPayOrderRecord order, bool isRecovered)
        {
            TrackLocalPaySuccessInternal(order, isRecovered, ThirdPayPaymentPageResult.CallbackSuccess(ThirdPayExternalBrowserLaunchMode.Failed, ThirdPayWebViewCallbackStatus.Success));
        }

        /// <summary>
        /// 上报已知原因的 ThirdPay 本地支付失败，并保留支付页、订单号和原生错误上下文。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="reason">支付失败原因。</param>
        /// <param name="clientOrderId">关联的客户端订单号。</param>
        /// <param name="pageResult">支付页终态及原生上下文。</param>
        internal void TrackLocalPayFailInternal(IAPThirdPayRequest request, ThirdPayPaymentFailureReason reason, string clientOrderId, ThirdPayPaymentPageResult pageResult)
        {
            if (request == null)
            {
                return;
            }

            Dictionary<string, object> properties = CreateThirdPayTrackProperties(request.TableId, request.CustomData);
            AppendTrackProperties(properties, BuildPaymentPageProperties(clientOrderId, string.Empty, pageResult));
            properties[IAPTrackFields.ThirdPayFailureReason] = (int)reason;
            properties[IAPTrackFields.Reason] = (int)MapPaymentFailureReasonToErrorCode(reason);
            properties[IAPTrackFields.ReasonDetail] = ThirdPayTrackReasonDescriptions.GetPaymentFailureDetail(reason);
            EmitTrackEvent(IAPTrackEvents.LocalPayFail, properties);
        }

        /// <summary>
        /// 兼容旧调用方上报本地支付失败，并将旧错误码映射到 ThirdPay 失败原因域。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="reason">旧 ThirdPay 错误码。</param>
        /// <param name="reasonDetail">旧调用方提供的诊断文本。</param>
        internal void TrackLocalPayFailInternal(IAPThirdPayRequest request, IAPThirdPayErrorCode reason, string reasonDetail)
        {
            ThirdPayPaymentFailureReason failureReason = MapThirdPayErrorCodeToFailureReason(reason);
            TrackLocalPayFailInternal(request, failureReason, string.Empty, ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, ThirdPayExternalBrowserLaunchMode.Failed, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.Unknown, failureReason, (int)reason, reasonDetail, false));
        }

        /// <summary>
        /// 上报 ThirdPay PayAsync 返回边界的本地支付失败；支付页已有终态或结果来自验单时不重复上报。
        /// </summary>
        /// <param name="result">PayAsync 返回的支付结果。</param>
        internal void TrackReturnedPayFailureInternal(IAPResult result)
        {
            if (result == null || result.IsSuccess)
            {
                return;
            }

            ThirdPayPaymentFailureReason reason;
            Dictionary<string, object> properties;
            if (m_ReturnedFailureTrackContexts.TryGetValue(result, out ThirdPayReturnedFailureTrackContext context))
            {
                reason = context.Reason;
                properties = new Dictionary<string, object>(context.Properties);
                m_ReturnedFailureTrackContexts.Remove(result);
                if (!context.ShouldTrackLocalPayFailure)
                {
                    return;
                }
            }
            else
            {
                reason = MapPayFailureResultToThirdPayReason(result);
                ThirdPayPaymentPageResult fallbackPageResult = ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, ThirdPayExternalBrowserLaunchMode.Failed, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.Unknown, reason, result.ErrorCode, result.ErrorDesc, false);
                properties = BuildPaymentPageProperties(result.OrderId, string.Empty, fallbackPageResult);
            }

            properties[IAPTrackFields.ThirdPayFailureReason] = (int)reason;
            if (!properties.ContainsKey(IAPTrackFields.ThirdPayOpenMode))
            {
                properties[IAPTrackFields.ThirdPayOpenMode] = (int)ThirdPayExternalBrowserLaunchMode.Failed;
            }

            if (!properties.ContainsKey(IAPTrackFields.ThirdPayCallbackStatus))
            {
                properties[IAPTrackFields.ThirdPayCallbackStatus] = (int)ThirdPayWebViewCallbackStatus.Unknown;
            }

            if (!properties.ContainsKey(IAPTrackFields.ThirdPayCloseReason))
            {
                properties[IAPTrackFields.ThirdPayCloseReason] = (int)ThirdPayCloseReason.Unknown;
            }

            if (!properties.ContainsKey(IAPTrackFields.NativeErrorCode))
            {
                properties[IAPTrackFields.NativeErrorCode] = result.ErrorCode;
            }

            if (!properties.ContainsKey(IAPTrackFields.NativeErrorMessage))
            {
                properties[IAPTrackFields.NativeErrorMessage] = result.ErrorDesc ?? string.Empty;
            }

            Dictionary<string, object> eventProperties = CreateThirdPayTrackProperties(result.TableId, result.CustomData);
            AppendTrackProperties(eventProperties, properties);
            eventProperties[IAPTrackFields.Reason] = (int)MapPaymentFailureReasonToErrorCode(reason);
            eventProperties[IAPTrackFields.ReasonDetail] = ThirdPayTrackReasonDescriptions.GetPaymentFailureDetail(reason);
            EmitTrackEvent(IAPTrackEvents.LocalPayFail, eventProperties);
        }

        /// <summary>
        /// 为支付页或创建阶段产生的失败结果绑定精确 ThirdPay 埋点上下文。
        /// </summary>
        /// <param name="result">最终将由 PayAsync 返回的失败结果。</param>
        /// <param name="reason">细分失败原因。</param>
        /// <param name="clientOrderId">客户端订单号；尚未建单时为空。</param>
        /// <param name="pageResult">支付页结构化结果；尚未打开支付页时为空。</param>
        /// <returns>原失败结果，便于调用方直接返回。</returns>
        internal IAPResult AttachReturnedPayFailureContext(IAPResult result, ThirdPayPaymentFailureReason reason, string clientOrderId = "", ThirdPayPaymentPageResult? pageResult = null)
        {
            if (result == null || result.IsSuccess)
            {
                return result;
            }

            ThirdPayPaymentPageResult trackedPageResult = pageResult ?? ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, ThirdPayExternalBrowserLaunchMode.Failed, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.Unknown, reason, result.ErrorCode, result.ErrorDesc, false);
            Dictionary<string, object> properties = BuildPaymentPageProperties(clientOrderId, string.Empty, trackedPageResult);
            RememberReturnedPayFailureContext(result, reason, properties, !trackedPageResult.IsSessionEstablished);
            return result;
        }

        /// <summary>
        /// 为验单产生的失败结果绑定状态上下文，并标记返回边界不得再上报 local_pay_fail。
        /// </summary>
        /// <param name="result">最终可能由 PayAsync 返回的失败结果。</param>
        /// <param name="order">关联的本地订单。</param>
        /// <param name="context">本次验单上下文。</param>
        /// <returns>原失败结果，便于调用方直接加入结果列表。</returns>
        internal IAPResult AttachReturnedValidationFailureContext(IAPResult result, ThirdPayOrderRecord order, ThirdPayValidationTrackContext context)
        {
            if (result == null || result.IsSuccess)
            {
                return result;
            }

            RememberReturnedPayFailureContext(result, context.FailureReason, BuildValidationProperties(order, context), false);
            return result;
        }

        /// <summary>
        /// 记录失败结果的唯一精确埋点上下文；相同结果重复绑定时以后一次为准。
        /// </summary>
        /// <param name="result">失败结果实例。</param>
        /// <param name="reason">细分失败原因。</param>
        /// <param name="properties">需要随 local_pay_fail 上报的属性。</param>
        /// <param name="shouldTrackLocalPayFailure">PayAsync 返回失败时是否仍需上报本地支付失败。</param>
        private void RememberReturnedPayFailureContext(IAPResult result, ThirdPayPaymentFailureReason reason, Dictionary<string, object> properties, bool shouldTrackLocalPayFailure)
        {
            m_ReturnedFailureTrackContexts.Remove(result);
            m_ReturnedFailureTrackContexts.Add(result, new ThirdPayReturnedFailureTrackContext(reason, properties, shouldTrackLocalPayFailure));
        }

        /// <summary>
        /// 上报一批订单的可重试或最终验单失败，并为每笔订单写入统一验单上下文。
        /// </summary>
        /// <param name="orders">验单订单列表。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="protocolMessage">协议错误描述。</param>
        /// <param name="context">服务端、客户端状态和失败原因。</param>
        /// <param name="isFinal">是否为最终失败。</param>
        internal void TrackValidationFailureBatchInternal(IReadOnlyList<ThirdPayOrderRecord> orders, bool isRecovered, int validateCount, bool netError, int protocolCode, string protocolMessage, ThirdPayValidationTrackContext context, bool isFinal)
        {
            if (orders == null)
            {
                return;
            }

            foreach (ThirdPayOrderRecord order in orders)
            {
                if (isFinal)
                {
                    TrackValidateFailFinishInternal(order, isRecovered, validateCount, netError, protocolCode, protocolMessage, context);
                }
                else
                {
                    TrackValidateFailInternal(order, isRecovered, validateCount, netError, protocolCode, protocolMessage, context);
                }
            }
        }

        /// <summary>
        /// 兼容旧调用方批量上报验单失败；未知状态通过默认上下文表示。
        /// </summary>
        /// <param name="orders">验单订单列表。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="reasonDetail">旧调用方提供的诊断文本。</param>
        /// <param name="isFinal">是否为最终失败。</param>
        internal void TrackValidationFailureBatchInternal(IReadOnlyList<ThirdPayOrderRecord> orders, bool isRecovered, int validateCount, bool netError, int protocolCode, string reasonDetail, bool isFinal)
        {
            ThirdPayPaymentFailureReason failureReason = netError ? ThirdPayPaymentFailureReason.ValidationNetworkError : ThirdPayPaymentFailureReason.Unknown;
            TrackValidationFailureBatchInternal(orders, isRecovered, validateCount, netError, protocolCode, reasonDetail, new ThirdPayValidationTrackContext(default, 0, ThirdPayClientOrderStatus.Unknown, string.Empty, failureReason, protocolCode, reasonDetail), isFinal);
        }

        /// <summary>
        /// 上报首次支付订单的第一次验单失败。
        /// </summary>
        /// <param name="orders">首次支付订单列表。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        internal void TrackFirstValidationFailureBatchInternal(IReadOnlyList<ThirdPayOrderRecord> orders, int validateCount, bool netError)
        {
            if (orders == null)
            {
                return;
            }

            foreach (ThirdPayOrderRecord order in orders)
            {
                if (order != null)
                {
                    Dictionary<string, object> properties = CreateThirdPayTrackProperties(order.TableId, order.CustomData);
                    AppendTrackProperties(properties, BuildOrderIdProperties(order.ClientOrderId, string.Empty));
                    properties[IAPTrackFields.AddOrder] = false;
                    properties[IAPTrackFields.ValidateCount] = validateCount;
                    properties[IAPTrackFields.NetError] = netError;
                    EmitTrackEvent(IAPTrackEvents.FirstPayOrderValidate, properties);
                }
            }
        }

        /// <summary>
        /// 上报单笔订单可重试的验单失败，并写入服务端和客户端订单状态。
        /// </summary>
        /// <param name="order">本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="protocolMessage">协议错误描述。</param>
        /// <param name="context">服务端、客户端状态和失败原因。</param>
        internal void TrackValidateFailInternal(ThirdPayOrderRecord order, bool isRecovered, int validateCount, bool netError, int protocolCode, string protocolMessage, ThirdPayValidationTrackContext context)
        {
            if (order == null)
            {
                return;
            }

            ThirdPayPaymentFailureReason reason = context.FailureReason;
            Dictionary<string, object> properties = CreateThirdPayValidationTrackProperties(order, isRecovered, validateCount, netError, context);
            properties[IAPTrackFields.ProtocolCode] = protocolCode;
            properties[IAPTrackFields.ProtocolMessage] = protocolMessage ?? string.Empty;
            properties[IAPTrackFields.Reason] = (int)IAPThirdPayErrorCode.ServerValidationFailed;
            properties[IAPTrackFields.ReasonDetail] = ThirdPayTrackReasonDescriptions.GetPaymentFailureDetail(reason);
            EmitTrackEvent(IAPTrackEvents.ValidateFail, properties);
        }

        /// <summary>
        /// 兼容旧调用方上报可重试验单失败。
        /// </summary>
        /// <param name="order">本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="reasonDetail">旧调用方提供的诊断文本。</param>
        internal void TrackValidateFailInternal(ThirdPayOrderRecord order, bool isRecovered, int validateCount, bool netError, int protocolCode, string reasonDetail)
        {
            ThirdPayPaymentFailureReason failureReason = netError ? ThirdPayPaymentFailureReason.ValidationNetworkError : ThirdPayPaymentFailureReason.Unknown;
            TrackValidateFailInternal(order, isRecovered, validateCount, netError, protocolCode, reasonDetail, new ThirdPayValidationTrackContext(default, 0, ThirdPayClientOrderStatus.Unknown, string.Empty, failureReason, protocolCode, reasonDetail));
        }

        /// <summary>
        /// 上报单笔订单最终验单失败，并写入服务端和客户端订单状态。
        /// </summary>
        /// <param name="order">本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="protocolMessage">协议错误描述。</param>
        /// <param name="context">服务端、客户端状态和失败原因。</param>
        internal void TrackValidateFailFinishInternal(ThirdPayOrderRecord order, bool isRecovered, int validateCount, bool netError, int protocolCode, string protocolMessage, ThirdPayValidationTrackContext context)
        {
            if (order == null)
            {
                return;
            }

            ThirdPayPaymentFailureReason reason = context.FailureReason;
            Dictionary<string, object> properties = CreateThirdPayValidationTrackProperties(order, isRecovered, validateCount, netError, context);
            properties[IAPTrackFields.ProtocolCode] = protocolCode;
            properties[IAPTrackFields.ProtocolMessage] = protocolMessage ?? string.Empty;
            properties[IAPTrackFields.Reason] = (int)IAPThirdPayErrorCode.ServerValidationFailed;
            properties[IAPTrackFields.ReasonDetail] = ThirdPayTrackReasonDescriptions.GetPaymentFailureDetail(reason);
            EmitTrackEvent(IAPTrackEvents.ValidateFailFinish, properties);
        }

        /// <summary>
        /// 兼容旧调用方上报最终验单失败。
        /// </summary>
        /// <param name="order">本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="reasonDetail">旧调用方提供的诊断文本。</param>
        internal void TrackValidateFailFinishInternal(ThirdPayOrderRecord order, bool isRecovered, int validateCount, bool netError, int protocolCode, string reasonDetail)
        {
            ThirdPayPaymentFailureReason failureReason = netError ? ThirdPayPaymentFailureReason.ValidationNetworkError : ThirdPayPaymentFailureReason.Unknown;
            TrackValidateFailFinishInternal(order, isRecovered, validateCount, netError, protocolCode, reasonDetail, new ThirdPayValidationTrackContext(default, 0, ThirdPayClientOrderStatus.Unknown, string.Empty, failureReason, protocolCode, reasonDetail));
        }

        /// <summary>
        /// 上报单笔订单服务端验单成功。
        /// </summary>
        /// <param name="order">本地订单。</param>
        /// <param name="tableId">服务端确认的支付表行 ID。</param>
        /// <param name="orderId">服务端订单号。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        internal void TrackValidateSuccessInternal(ThirdPayOrderRecord order, long tableId, string orderId, bool isRecovered, int validateCount)
        {
            if (order == null)
            {
                return;
            }

            Dictionary<string, object> properties = CreateThirdPayTrackProperties(tableId, order.CustomData);
            AppendTrackProperties(properties, BuildOrderIdProperties(order.ClientOrderId, orderId));
            properties[IAPTrackFields.AddOrder] = isRecovered;
            properties[IAPTrackFields.ValidateCount] = validateCount;
            EmitTrackEvent(IAPTrackEvents.ValidateSuccess, properties);
        }

        /// <summary>
        /// 上报单笔订单服务端验单成功，并附加验单场景、服务端状态和客户端归一化状态。
        /// </summary>
        /// <param name="order">本地订单。</param>
        /// <param name="tableId">服务端确认的支付表行 ID。</param>
        /// <param name="orderId">服务端订单号。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="context">服务端、客户端状态和失败原因。</param>
        internal void TrackValidateSuccessInternal(ThirdPayOrderRecord order, long tableId, string orderId, bool isRecovered, int validateCount, ThirdPayValidationTrackContext context)
        {
            if (order == null)
            {
                return;
            }

            Dictionary<string, object> properties = CreateThirdPayTrackProperties(tableId, order.CustomData);
            AppendTrackProperties(properties, BuildValidationProperties(order, context));
            properties[IAPTrackFields.AddOrder] = isRecovered;
            properties[IAPTrackFields.ValidateCount] = validateCount;
            EmitTrackEvent(IAPTrackEvents.ValidateSuccess, properties);
        }

        /// <summary>
        /// 实际移除本地订单后上报删除事件；未移除时绝不产生删除打点。
        /// </summary>
        /// <param name="order">待删除的本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">删除前已执行的验单次数。</param>
        /// <param name="context">服务端、客户端状态和失败原因。</param>
        /// <param name="reason">本地订单删除原因。</param>
        /// <param name="persist">是否立即持久化删除结果。</param>
        /// <returns>实际删除订单并完成打点时返回 true。</returns>
        internal bool RemoveLocalOrderAndTrack(ThirdPayOrderRecord order, bool isRecovered, int validateCount, ThirdPayValidationTrackContext context, ThirdPayOrderDeleteReason reason, bool persist)
        {
            if (order == null || string.IsNullOrEmpty(order.ClientOrderId) || !m_Hub.PersistContext.RemoveOrder(order.ClientOrderId, persist))
            {
                return false;
            }

            Dictionary<string, object> properties = CreateThirdPayTrackProperties(order.TableId, order.CustomData);
            AppendTrackProperties(properties, BuildValidationProperties(order, context));
            properties[IAPTrackFields.AddOrder] = isRecovered;
            properties[IAPTrackFields.ValidateCount] = validateCount;
            properties[IAPTrackFields.ReasonDetail] = ThirdPayTrackReasonDescriptions.GetOrderDeleteReasonDetail(reason);
            properties[IAPTrackFields.OrderDeleteReason] = (int)reason;
            EmitTrackEvent(IAPTrackEvents.ThirdPayOrderRemoved, properties);
            return true;
        }

        /// <summary>
        /// 构造 ThirdPay 渠道共有的商品打点字段。
        /// </summary>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <param name="customData">业务透传数据。</param>
        /// <returns>包含通用字段的 ThirdPay 打点属性。</returns>
        private Dictionary<string, object> CreateThirdPayTrackProperties(long tableId, string customData)
        {
            return CreateTrackProperties(tableId, ResolveThirdProductId(tableId), IsTrackDebugMode(), ResolvePrice(tableId), customData);
        }

        /// <summary>
        /// 构造 ThirdPay 验单和删单事件共有的状态字段。
        /// </summary>
        /// <param name="order">关联的本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单尝试次数。</param>
        /// <param name="netError">是否为网络错误。</param>
        /// <param name="context">服务端与客户端验单状态。</param>
        /// <returns>包含双订单号和验单状态的 ThirdPay 属性。</returns>
        private Dictionary<string, object> CreateThirdPayValidationTrackProperties(ThirdPayOrderRecord order, bool isRecovered, int validateCount, bool netError, ThirdPayValidationTrackContext context)
        {
            Dictionary<string, object> properties = CreateThirdPayTrackProperties(order.TableId, order.CustomData);
            AppendTrackProperties(properties, BuildValidationProperties(order, context));
            properties[IAPTrackFields.AddOrder] = isRecovered;
            properties[IAPTrackFields.ValidateCount] = validateCount;
            properties[IAPTrackFields.NetError] = netError;
            return properties;
        }

        /// <summary>
        /// 构造支付页相关埋点字段，保证关闭、成功和失败事件的属性名一致。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <param name="serverOrderId">服务端订单号。</param>
        /// <param name="pageResult">支付页终态及原生上下文。</param>
        /// <returns>可直接追加到基础打点的属性字典。</returns>
        private static Dictionary<string, object> BuildPaymentPageProperties(string clientOrderId, string serverOrderId, ThirdPayPaymentPageResult pageResult)
        {
            Dictionary<string, object> properties = BuildOrderIdProperties(clientOrderId, serverOrderId);
            properties[IAPTrackFields.ThirdPayOpenMode] = (int)pageResult.OpenMode;
            properties[IAPTrackFields.ThirdPayCallbackStatus] = (int)pageResult.CallbackStatus;
            properties[IAPTrackFields.ThirdPayCloseReason] = (int)pageResult.CloseReason;
            properties[IAPTrackFields.ThirdPayFailureReason] = (int)pageResult.FailureReason;
            properties[IAPTrackFields.NativeErrorCode] = pageResult.NativeErrorCode;
            properties[IAPTrackFields.NativeErrorMessage] = pageResult.NativeErrorMessage ?? string.Empty;
            return properties;
        }

        /// <summary>
        /// 构造验单失败和订单删除共用的状态字段。
        /// </summary>
        /// <param name="order">关联的本地订单。</param>
        /// <param name="context">服务端、客户端状态和失败原因。</param>
        /// <returns>可直接追加到基础打点的属性字典。</returns>
        private static Dictionary<string, object> BuildValidationProperties(ThirdPayOrderRecord order, ThirdPayValidationTrackContext context)
        {
            Dictionary<string, object> properties = BuildOrderIdProperties(order?.ClientOrderId, context.ServerOrderId);
            properties[IAPTrackFields.ValidationScene] = (int)context.Scene;
            properties[IAPTrackFields.ServerOrderStatus] = context.ServerOrderStatus;
            properties[IAPTrackFields.ClientOrderStatus] = (int)context.ClientOrderStatus;
            properties[IAPTrackFields.ThirdPayFailureReason] = (int)context.FailureReason;
            properties[IAPTrackFields.ProtocolCode] = context.ProtocolCode;
            properties[IAPTrackFields.ProtocolMessage] = context.ProtocolMessage ?? string.Empty;
            return properties;
        }

        /// <summary>
        /// 构造 ThirdPay 统一的客户端/服务端订单号字段。
        /// </summary>
        /// <param name="clientOrderId">客户端生成的订单号。</param>
        /// <param name="serverOrderId">服务端确认的订单号。</param>
        /// <returns>包含双订单号的属性字典；尚未取得的一侧为空字符串。</returns>
        private static Dictionary<string, object> BuildOrderIdProperties(string clientOrderId, string serverOrderId)
        {
            return new Dictionary<string, object>
            {
                { IAPTrackFields.ClientOrderId, clientOrderId ?? string.Empty },
                { IAPTrackFields.ServerOrderId, serverOrderId ?? string.Empty },
            };
        }

        /// <summary>
        /// 将细分创建订单失败原因映射回既有 nova_reason 使用的 ThirdPay 粗粒度错误码。
        /// </summary>
        /// <param name="reason">细分创建订单失败原因。</param>
        /// <returns>兼容既有数据分析的 ThirdPay 错误码。</returns>
        private static IAPThirdPayErrorCode MapCreateOrderFailureReasonToErrorCode(ThirdPayCreateOrderFailureReason reason)
        {
            switch (reason)
            {
                case ThirdPayCreateOrderFailureReason.PaymentDisabledByServer:
                case ThirdPayCreateOrderFailureReason.ProductNotConfigured:
                    return IAPThirdPayErrorCode.StoreNotAvailable;
                case ThirdPayCreateOrderFailureReason.GoogleAuthorizationConnectionFailed:
                case ThirdPayCreateOrderFailureReason.GoogleTokenCreationFailed:
                case ThirdPayCreateOrderFailureReason.GooglePaymentUrlBuildFailed:
                    return IAPThirdPayErrorCode.BillingNotReady;
                case ThirdPayCreateOrderFailureReason.GoogleUserCancelled:
                    return IAPThirdPayErrorCode.UserCancelled;
                default:
                    return IAPThirdPayErrorCode.StoreInitFailed;
            }
        }

        /// <summary>
        /// 将细分支付失败原因映射回既有 nova_reason 使用的 ThirdPay 粗粒度错误码。
        /// </summary>
        /// <param name="reason">细分支付失败原因。</param>
        /// <returns>兼容既有数据分析的 ThirdPay 错误码。</returns>
        private static IAPThirdPayErrorCode MapPaymentFailureReasonToErrorCode(ThirdPayPaymentFailureReason reason)
        {
            switch (reason)
            {
                case ThirdPayPaymentFailureReason.StoreDisabled:
                case ThirdPayPaymentFailureReason.PaymentDisabledByServer:
                case ThirdPayPaymentFailureReason.ProductNotConfigured:
                    return IAPThirdPayErrorCode.StoreNotAvailable;
                case ThirdPayPaymentFailureReason.StoreNotInitialized:
                case ThirdPayPaymentFailureReason.UserIdMissing:
                case ThirdPayPaymentFailureReason.PaymentConfigUnavailable:
                case ThirdPayPaymentFailureReason.EmbeddedWebViewServiceUnavailable:
                case ThirdPayPaymentFailureReason.ExternalBrowserServiceUnavailable:
                    return IAPThirdPayErrorCode.StoreInitFailed;
                case ThirdPayPaymentFailureReason.GoogleAuthorizationConnectionFailed:
                case ThirdPayPaymentFailureReason.GoogleTokenCreationFailed:
                case ThirdPayPaymentFailureReason.GooglePaymentUrlBuildFailed:
                    return IAPThirdPayErrorCode.BillingNotReady;
                case ThirdPayPaymentFailureReason.PaymentUrlBuildFailed:
                case ThirdPayPaymentFailureReason.PaymentUrlMissing:
                    return IAPThirdPayErrorCode.StoreInitFailed;
                case ThirdPayPaymentFailureReason.GoogleUserCancelled:
                case ThirdPayPaymentFailureReason.OperationCancelled:
                    return IAPThirdPayErrorCode.UserCancelled;
                case ThirdPayPaymentFailureReason.PaymentPageOpenFailed:
                case ThirdPayPaymentFailureReason.PaymentPageOpenException:
                case ThirdPayPaymentFailureReason.SystemBrowserFallbackUrlEmpty:
                case ThirdPayPaymentFailureReason.PaymentCallbackFailed:
                case ThirdPayPaymentFailureReason.PaymentCallbackInvalidPayload:
                case ThirdPayPaymentFailureReason.PaymentCallbackUnknownStatus:
                case ThirdPayPaymentFailureReason.CallbackOrderMismatch:
                case ThirdPayPaymentFailureReason.CallbackIgnoredAfterValidationStarted:
                case ThirdPayPaymentFailureReason.ExternalBrowserReturnedWithoutCallback:
                    return IAPThirdPayErrorCode.WebViewClosed;
                case ThirdPayPaymentFailureReason.ValidationNetworkError:
                    return IAPThirdPayErrorCode.NetworkError;
                case ThirdPayPaymentFailureReason.ServerOrderPendingPayment:
                case ThirdPayPaymentFailureReason.ServerOrderProcessing:
                    return IAPThirdPayErrorCode.OrderPending;
                case ThirdPayPaymentFailureReason.ValidationServiceUnavailable:
                case ThirdPayPaymentFailureReason.ValidationResponseOrderMissing:
                case ThirdPayPaymentFailureReason.ServerOrderFailedOrExpired:
                case ThirdPayPaymentFailureReason.ServerOrderNotFound:
                case ThirdPayPaymentFailureReason.UnknownServerOrderStatus:
                    return IAPThirdPayErrorCode.ServerValidationFailed;
                default:
                    return IAPThirdPayErrorCode.StoreInitFailed;
            }
        }

        /// <summary>
        /// 将 PayAsync 返回的通用或 ThirdPay 错误结果映射到 ThirdPay 支付失败原因域。
        /// </summary>
        /// <param name="result">PayAsync 返回的失败结果。</param>
        /// <returns>可写入 ThirdPay 失败原因字段的稳定枚举。</returns>
        private static ThirdPayPaymentFailureReason MapPayFailureResultToThirdPayReason(IAPResult result)
        {
            if (result == null)
            {
                return ThirdPayPaymentFailureReason.Unknown;
            }

            if (result.ErrorSource == IAPErrorSource.PluginRouter)
            {
                switch ((IAPPluginErrorCode)result.ErrorCode)
                {
                    case IAPPluginErrorCode.StoreNotAvailable: return ThirdPayPaymentFailureReason.StoreDisabled;
                    case IAPPluginErrorCode.StoreInitFailed: return ThirdPayPaymentFailureReason.StoreNotInitialized;
                    case IAPPluginErrorCode.AlreadyPurchasing: return ThirdPayPaymentFailureReason.AlreadyPurchasing;
                    case IAPPluginErrorCode.ProductNotFound: return ThirdPayPaymentFailureReason.ProductNotFound;
                    default: return ThirdPayPaymentFailureReason.Unknown;
                }
            }

            if (result.ErrorSource != IAPErrorSource.ThirdPay || !Enum.IsDefined(typeof(IAPThirdPayErrorCode), result.ErrorCode))
            {
                return ThirdPayPaymentFailureReason.Unknown;
            }

            return MapThirdPayErrorCodeToFailureReason((IAPThirdPayErrorCode)result.ErrorCode);
        }

        /// <summary>
        /// 将原有 ThirdPay 错误码映射到细分支付失败原因域。
        /// </summary>
        /// <param name="errorCode">原有 ThirdPay 错误码。</param>
        /// <returns>细分后的支付失败原因。</returns>
        private static ThirdPayPaymentFailureReason MapThirdPayErrorCodeToFailureReason(IAPThirdPayErrorCode errorCode)
        {
            switch (errorCode)
            {
                case IAPThirdPayErrorCode.StoreInitFailed: return ThirdPayPaymentFailureReason.StoreNotInitialized;
                case IAPThirdPayErrorCode.UserCancelled: return ThirdPayPaymentFailureReason.OperationCancelled;
                case IAPThirdPayErrorCode.NetworkError: return ThirdPayPaymentFailureReason.ValidationNetworkError;
                case IAPThirdPayErrorCode.ServerValidationFailed: return ThirdPayPaymentFailureReason.ValidationServiceUnavailable;
                case IAPThirdPayErrorCode.StoreNotAvailable: return ThirdPayPaymentFailureReason.PaymentDisabledByServer;
                case IAPThirdPayErrorCode.WebViewClosed: return ThirdPayPaymentFailureReason.PaymentPageOpenFailed;
                case IAPThirdPayErrorCode.BillingNotReady: return ThirdPayPaymentFailureReason.GoogleAuthorizationConnectionFailed;
                case IAPThirdPayErrorCode.OrderPending: return ThirdPayPaymentFailureReason.ServerOrderProcessing;
                default: return ThirdPayPaymentFailureReason.Unknown;
            }
        }

        /// <summary>
        /// 解析第三方商品 ID，未配置时返回空字符串。
        /// </summary>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <returns>第三方商品 ID。</returns>
        private string ResolveThirdProductId(long tableId)
        {
            return Table?.FindByTableId(tableId)?.ThirdProductID ?? string.Empty;
        }

        /// <summary>
        /// 解析支付表配置价格，解析失败时返回 0。
        /// </summary>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <returns>支付表配置价格。</returns>
        private float ResolvePrice(long tableId)
        {
            string price = Table?.FindByTableId(tableId)?.Price;
            return float.TryParse(price, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }

        /// <summary>
        /// 判断当前运行环境是否为 Debug 开发模式。
        /// </summary>
        /// <returns>当前配置为 Debug 时返回 true。</returns>
        private bool IsTrackDebugMode()
        {
            return Context?.DevelopMode == DevelopMode.Debug;
        }
    }
}
