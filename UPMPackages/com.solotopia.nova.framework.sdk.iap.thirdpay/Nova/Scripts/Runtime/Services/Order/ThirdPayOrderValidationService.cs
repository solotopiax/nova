/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayOrderValidationService.cs
 * author:    yingzheng
 * created:   2026/9/17
 * descrip:   ThirdPay 订单验单与结果收敛服务
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 标识第三方订单验单的触发场景，用于保持各入口既有的重试与事件派发语义。
    /// </summary>
    internal enum ThirdPayValidationScene
    {
        /// <summary>
        /// 尚未进入验单链路的默认状态。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 支付页明确回调成功后的同步验单。
        /// </summary>
        DirectPay = 1,

        /// <summary>
        /// 应用内支付页关闭后结果不明确的同步验单。
        /// </summary>
        DirectPayAmbiguousReturn = 2,

        /// <summary>
        /// 发起新支付前对同业务键本地订单执行的验单。
        /// </summary>
        DirectPayPreflight = 3,

        /// <summary>
        /// 后台补单流程触发的验单。
        /// </summary>
        Recovered = 4,

        /// <summary>
        /// 外部浏览器返回应用后触发的验单。
        /// </summary>
        ExternalBrowserReturn = 5,

        /// <summary>
        /// 外部浏览器明确回调支付成功后触发的验单。
        /// </summary>
        ExternalBrowserCallback = 6,
    }

    /// <summary>
    /// 负责批量验单、重试、状态映射、订单持久化以及结果事件输出。
    /// </summary>
    internal sealed class ThirdPayOrderValidationService
    {
        /// <summary>
        /// 支付结果不明确场景允许的最大验单次数。
        /// </summary>
        private const int c_AmbiguousValidateMaxAttempts = 3;

        /// <summary>
        /// 服务端验单请求失败或订单状态尚未收敛后的重试间隔，单位为秒。
        /// </summary>
        private static readonly float[] s_ValidateRetryIntervals = { 0.2f, 0.5f, 1f, 2f, 4f, 8f };

        /// <summary>
        /// ThirdPay 强类型服务容器。
        /// </summary>
        private readonly ThirdPayServiceHub m_Hub;

        /// <summary>
        /// 创建第三方订单验单服务。
        /// </summary>
        /// <param name="hub">ThirdPay 强类型服务容器。</param>
        public ThirdPayOrderValidationService(ThirdPayServiceHub hub)
        {
            m_Hub = hub ?? throw new ArgumentNullException(nameof(hub));
        }

        /// <summary>
        /// 批量验证第三方支付订单，并按触发场景应用原有重试、事件和删除规则。
        /// </summary>
        /// <param name="orders">待验单的本地订单列表。</param>
        /// <param name="scene">本次验单触发场景。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>与输入订单对应的验单结果列表。</returns>
        public async UniTask<List<IAPResult>> ValidateAsync(List<ThirdPayOrderRecord> orders, ThirdPayValidationScene scene, CancellationToken ct)
        {
            if (orders == null || orders.Count == 0)
            {
                return new List<IAPResult>();
            }

            bool isRecovered = scene == ThirdPayValidationScene.Recovered;
            bool publishNonTerminalFailures = ShouldPublishNonTerminalFailure(scene);
            MarkOrdersValidating(orders);

            string verifyCmdName = m_Hub.Config?.VerifyIapCmdName;
            if (m_Hub.NetService == null || string.IsNullOrEmpty(verifyCmdName))
            {
                const string unavailableMessage = "验单服务未配置。";
                var unavailableContext = new ThirdPayValidationTrackContext(scene, 0, ThirdPayClientOrderStatus.ValidationServiceUnavailable, string.Empty, ThirdPayPaymentFailureReason.ValidationServiceUnavailable, 0, unavailableMessage);
                m_Hub.Store.TrackValidationFailureBatchInternal(orders, isRecovered, 0, false, 0, unavailableMessage, unavailableContext, true);
                return BuildValidationFailures(orders, unavailableMessage, publishNonTerminalFailures, isRecovered, unavailableContext);
            }

            var clientOrderIds = new List<string>(orders.Count);
            foreach (ThirdPayOrderRecord order in orders)
            {
                clientOrderIds.Add(order.ClientOrderId);
            }

            int maxAttempts = ResolveValidateMaxAttempts(scene);
            int lastProtocolCode = 0;
            string lastProtocolMessage = string.Empty;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                NetResponse<PbNetThirdVerifyIapResp> response = await m_Hub.NetService.VerifyIapAsync(verifyCmdName, clientOrderIds);
                bool isFinalAttempt = attempt + 1 >= maxAttempts;
                if (response.IsSuccess && response.Data != null)
                {
                    if (!isFinalAttempt && ShouldRetryValidationResponse(response.Data, scene))
                    {
                        TrackRetryableValidationResponse(orders, response.Data, scene, isRecovered, attempt + 1);
                        int intervalIndex = Math.Min(attempt, s_ValidateRetryIntervals.Length - 1);
                        await UniTask.Delay(TimeSpan.FromSeconds(s_ValidateRetryIntervals[intervalIndex]), cancellationToken: ct);
                        continue;
                    }

                    return ApplyValidationResponse(orders, response.Data, scene, attempt + 1);
                }

                lastProtocolCode = response.ErrorCode;
                lastProtocolMessage = response.ErrorMessage;
                var networkContext = new ThirdPayValidationTrackContext(scene, 0, ThirdPayClientOrderStatus.NetworkFailure, string.Empty, ThirdPayPaymentFailureReason.ValidationNetworkError, response.ErrorCode, response.ErrorMessage);
                m_Hub.Store.TrackValidationFailureBatchInternal(orders, isRecovered, attempt + 1, true, response.ErrorCode, response.ErrorMessage, networkContext, isFinalAttempt);
                if (IsConfirmedPaymentSuccessScene(scene) && attempt == 0)
                {
                    m_Hub.Store.TrackFirstValidationFailureBatchInternal(orders, attempt + 1, true);
                }

                if (!isFinalAttempt)
                {
                    int intervalIndex = Math.Min(attempt, s_ValidateRetryIntervals.Length - 1);
                    await UniTask.Delay(TimeSpan.FromSeconds(s_ValidateRetryIntervals[intervalIndex]), cancellationToken: ct);
                }
            }

            var finalNetworkContext = new ThirdPayValidationTrackContext(scene, 0, ThirdPayClientOrderStatus.NetworkFailure, string.Empty, ThirdPayPaymentFailureReason.ValidationNetworkError, lastProtocolCode, lastProtocolMessage);
            return BuildValidationFailures(orders, "验单网络请求失败，订单已保留。", publishNonTerminalFailures, isRecovered, finalNetworkContext);
        }

        /// <summary>
        /// 按验单场景解析本次最大验单次数。
        /// </summary>
        /// <param name="scene">本次验单触发场景。</param>
        /// <returns>当前场景允许的最大请求次数。</returns>
        private int ResolveValidateMaxAttempts(ThirdPayValidationScene scene)
        {
            if (IsSingleAttemptScene(scene))
            {
                return 1;
            }

            int configuredMaxAttempts = Math.Max(1, m_Hub.Store.ConfiguredMaxValidateAttempts);
            int confirmedMaxAttempts = s_ValidateRetryIntervals.Length + 1;
            return IsConfirmedPaymentSuccessScene(scene) ? Math.Max(confirmedMaxAttempts, configuredMaxAttempts) : Math.Min(c_AmbiguousValidateMaxAttempts, configuredMaxAttempts);
        }

        /// <summary>
        /// 判断验单场景是否已经由支付页明确回调支付成功。
        /// </summary>
        /// <param name="scene">本次验单触发场景。</param>
        /// <returns>明确收到支付成功回调时返回 true。</returns>
        private static bool IsConfirmedPaymentSuccessScene(ThirdPayValidationScene scene)
        {
            return scene == ThirdPayValidationScene.DirectPay || scene == ThirdPayValidationScene.ExternalBrowserCallback;
        }

        /// <summary>
        /// 判断验单场景是否只允许请求一次。
        /// </summary>
        /// <param name="scene">本次验单触发场景。</param>
        /// <returns>历史订单手动验单或后台补单场景返回 true。</returns>
        internal static bool IsSingleAttemptScene(ThirdPayValidationScene scene)
        {
            return scene == ThirdPayValidationScene.DirectPayPreflight || scene == ThirdPayValidationScene.Recovered;
        }

        /// <summary>
        /// 判断当前支付链中的服务端订单状态是否允许继续验单。
        /// </summary>
        /// <param name="scene">本次验单触发场景。</param>
        /// <param name="status">服务端订单状态。</param>
        /// <returns>当前支付仍在待支付或支付中状态且场景允许重试时返回 true。</returns>
        internal static bool ShouldRetryValidationStatus(ThirdPayValidationScene scene, PbNetThirdVerifyOrderStatus status)
        {
            if (status != PbNetThirdVerifyOrderStatus.PendingPayment && status != PbNetThirdVerifyOrderStatus.Processing)
            {
                return false;
            }

            return scene == ThirdPayValidationScene.DirectPay
                   || scene == ThirdPayValidationScene.ExternalBrowserCallback
                   || scene == ThirdPayValidationScene.DirectPayAmbiguousReturn
                   || scene == ThirdPayValidationScene.ExternalBrowserReturn;
        }

        /// <summary>
        /// 判断成功验单响应中是否存在当前场景允许继续验单的订单状态。
        /// </summary>
        /// <param name="response">服务端验单响应。</param>
        /// <param name="scene">本次验单触发场景。</param>
        /// <returns>存在可继续验单的订单状态时返回 true。</returns>
        private static bool ShouldRetryValidationResponse(PbNetThirdVerifyIapResp response, ThirdPayValidationScene scene)
        {
            if (response?.OrderList == null)
            {
                return false;
            }

            foreach (PbNetThirdVerifyOrderResult item in response.OrderList)
            {
                if (item != null && ShouldRetryValidationStatus(scene, item.Status))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 为服务端仍在收敛、即将进入下一次重试的订单上报本次可重试验单失败。
        /// 不改变订单状态、事件派发或重试决策，避免中间响应被统计为一次缺失的验单尝试。
        /// </summary>
        /// <param name="orders">本次验单请求中的本地订单。</param>
        /// <param name="response">服务端成功响应。</param>
        /// <param name="scene">本次验单触发场景。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">已经执行的验单次数（含本次）。</param>
        private void TrackRetryableValidationResponse(IReadOnlyList<ThirdPayOrderRecord> orders, PbNetThirdVerifyIapResp response, ThirdPayValidationScene scene, bool isRecovered, int validateCount)
        {
            if (orders == null || response?.OrderList == null)
            {
                return;
            }

            foreach (ThirdPayOrderRecord order in orders)
            {
                if (order == null)
                {
                    continue;
                }

                PbNetThirdVerifyOrderResult matched = FindResponse(response, order.ClientOrderId);
                if (matched == null || !ShouldRetryValidationStatus(scene, matched.Status))
                {
                    continue;
                }

                PbNetThirdVerifyOrderStatus status = matched.Status;
                var context = new ThirdPayValidationTrackContext(scene, (int)status, ThirdPayOrderResolution.ToClientOrderStatus(status), matched.ServerOrderId, ThirdPayOrderResolution.ToFailureReason(status));
                m_Hub.Store.TrackValidateFailInternal(
                    order,
                    isRecovered,
                    validateCount,
                    false,
                    0,
                    BuildStatusFailureMessage(status, false),
                    context);
            }
        }

        /// <summary>
        /// 将服务端响应转换为业务结果，并同步更新本地订单存档及删除打点。
        /// </summary>
        /// <param name="orders">本次请求中的本地订单列表。</param>
        /// <param name="response">服务端验单响应。</param>
        /// <param name="scene">本次验单触发场景。</param>
        /// <param name="validateCount">当前已执行的验单次数。</param>
        /// <returns>与输入订单对应的业务结果列表。</returns>
        private List<IAPResult> ApplyValidationResponse(List<ThirdPayOrderRecord> orders, PbNetThirdVerifyIapResp response, ThirdPayValidationScene scene, int validateCount)
        {
            var results = new List<IAPResult>(orders.Count);
            bool anyRemoved = false;
            bool anyChanged = false;
            bool isRecovered = scene == ThirdPayValidationScene.Recovered;
            bool publishNonTerminalFailures = ShouldPublishNonTerminalFailure(scene);
            bool publishTerminalFailures = ShouldPublishTerminalFailure(scene);
            foreach (ThirdPayOrderRecord order in orders)
            {
                PbNetThirdVerifyOrderResult matched = FindResponse(response, order.ClientOrderId);
                if (matched == null)
                {
                    const string missingMessage = "验单响应未包含该订单，订单已保留。";
                    var missingContext = new ThirdPayValidationTrackContext(scene, 0, ThirdPayClientOrderStatus.ResponseOrderMissing, string.Empty, ThirdPayPaymentFailureReason.ValidationResponseOrderMissing);
                    anyChanged |= MarkOrderPendingValidation(order);
                    m_Hub.Store.TrackValidateFailFinishInternal(order, isRecovered, validateCount, false, 0, missingMessage, missingContext);
                    var missing = new IAPResult(order.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, IAPErrorSource.ThirdPay, missingMessage, order.CustomData, order.ClientOrderId, isRecovered, order.ReceiptParam);
                    m_Hub.Store.AttachReturnedValidationFailureContext(missing, order, missingContext);
                    if (publishNonTerminalFailures)
                    {
                        m_Hub.Context?.EventBridge?.RaisePayFailed(missing);
                    }

                    results.Add(missing);
                    continue;
                }

                PbNetThirdVerifyOrderStatus status = matched.Status;
                ThirdPayOrderResolution resolution = ThirdPayOrderResolution.FromStatus(status);
                long tableId = matched.TableId != 0 ? matched.TableId : order.TableId;
                string receiptParam = string.IsNullOrEmpty(matched.ReceiptParam) ? order.ReceiptParam : matched.ReceiptParam;
                string serverOrderId = matched.ServerOrderId ?? string.Empty;
                var context = new ThirdPayValidationTrackContext(scene, (int)status, ThirdPayOrderResolution.ToClientOrderStatus(status), serverOrderId, ThirdPayOrderResolution.ToFailureReason(status));
                if (resolution.RemoveOrder)
                {
                    anyRemoved |= m_Hub.Store.RemoveLocalOrderAndTrack(order, isRecovered, validateCount, context, ThirdPayOrderResolution.ToDeleteReason(status), false);
                }
                else
                {
                    anyChanged |= MarkOrderPendingValidation(order);
                }

                if (resolution.IsSuccess)
                {
                    string orderId = string.IsNullOrEmpty(serverOrderId) ? order.ClientOrderId : serverOrderId;
                    m_Hub.Store.TrackValidateSuccessInternal(order, tableId, orderId, isRecovered, validateCount, context);
                    var success = new IAPResult(tableId, orderId, isRecovered, resolution.CanDeliver, order.CustomData, receiptParam, IAPStoreType.ThirdPay);
                    if (resolution.CanDeliver || ShouldPublishNonDeliverableSuccess(scene))
                    {
                        m_Hub.Context?.EventBridge?.RaisePaySuccess(success);
                    }

                    results.Add(success);
                    continue;
                }

                bool isTerminal = resolution.RaiseTerminalEvent;
                string failureMessage = BuildStatusFailureMessage(status, resolution.RemoveOrder);
                var failure = new IAPResult(tableId, (int)(isTerminal ? IAPThirdPayErrorCode.ServerValidationFailed : IAPThirdPayErrorCode.OrderPending), IAPErrorSource.ThirdPay, failureMessage, order.CustomData, order.ClientOrderId, isRecovered, receiptParam);
                m_Hub.Store.AttachReturnedValidationFailureContext(failure, order, context);
                if (isTerminal)
                {
                    m_Hub.Store.TrackValidateFailFinishInternal(order, isRecovered, validateCount, false, 0, failureMessage, context);
                    if (publishTerminalFailures)
                    {
                        m_Hub.Context?.EventBridge?.RaisePayFailed(failure);
                    }
                }
                else
                {
                    m_Hub.Store.TrackValidateFailInternal(order, isRecovered, validateCount, false, 0, failureMessage, context);
                    if (publishNonTerminalFailures)
                    {
                        m_Hub.Context?.EventBridge?.RaisePayFailed(failure);
                    }
                }

                results.Add(failure);
            }

            if (anyRemoved || anyChanged)
            {
                m_Hub.PersistContext.Save();
            }

            return results;
        }

        /// <summary>
        /// 构造服务端状态对应的业务失败描述，供 PayAsync 和事件调用方展示。
        /// </summary>
        /// <param name="status">服务端原始订单状态。</param>
        /// <param name="removed">本地订单是否已经移除。</param>
        /// <returns>与当前状态一致的失败描述。</returns>
        private static string BuildStatusFailureMessage(PbNetThirdVerifyOrderStatus status, bool removed)
        {
            switch (status)
            {
                case PbNetThirdVerifyOrderStatus.PendingPayment: return removed ? "第三方订单用户未支付，已移除本地订单。" : "第三方订单仍待支付，订单已保留。";
                case PbNetThirdVerifyOrderStatus.Processing: return "第三方订单仍在处理中，订单已保留。";
                case PbNetThirdVerifyOrderStatus.FailedOrExpired: return "第三方订单支付失败或已过期，已移除本地订单。";
                case PbNetThirdVerifyOrderStatus.NotFound: return "第三方订单不存在，已移除本地订单。";
                default: return $"第三方订单返回未知状态={status}，订单已保留。";
            }
        }

        /// <summary>
        /// 按客户端订单号查找对应的服务端验单条目。
        /// </summary>
        /// <param name="response">服务端验单响应。</param>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <returns>匹配的验单条目；未命中时返回 null。</returns>
        private static PbNetThirdVerifyOrderResult FindResponse(PbNetThirdVerifyIapResp response, string clientOrderId)
        {
            if (response?.OrderList == null)
            {
                return null;
            }

            foreach (PbNetThirdVerifyOrderResult item in response.OrderList)
            {
                if (string.Equals(item.ClientOrderId, clientOrderId, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// 判断非终态验单失败是否需要派发到全局事件。
        /// </summary>
        /// <param name="scene">本次验单触发场景。</param>
        /// <returns>需要发布失败事件时返回 true。</returns>
        internal static bool ShouldPublishNonTerminalFailure(ThirdPayValidationScene scene)
        {
            return scene == ThirdPayValidationScene.DirectPay
                   || scene == ThirdPayValidationScene.ExternalBrowserCallback
                   || scene == ThirdPayValidationScene.DirectPayAmbiguousReturn
                   || scene == ThirdPayValidationScene.DirectPayPreflight
                   || scene == ThirdPayValidationScene.ExternalBrowserReturn;
        }

        /// <summary>
        /// 判断终态失败是否需要派发到全局事件。
        /// </summary>
        /// <param name="scene">本次验单触发场景。</param>
        /// <returns>需要发布失败事件时返回 true。</returns>
        private static bool ShouldPublishTerminalFailure(ThirdPayValidationScene scene)
        {
            return scene != ThirdPayValidationScene.Recovered;
        }

        /// <summary>
        /// 判断不可发货成功结果是否需要派发到全局事件。
        /// </summary>
        /// <param name="scene">本次验单触发场景。</param>
        /// <returns>需要发布不可重复发货的成功事件时返回 true。</returns>
        internal static bool ShouldPublishNonDeliverableSuccess(ThirdPayValidationScene scene)
        {
            return scene == ThirdPayValidationScene.ExternalBrowserReturn;
        }

        /// <summary>
        /// 标记一批订单即将发起服务端验单。
        /// </summary>
        /// <param name="orders">待更新状态的本地订单列表。</param>
        private void MarkOrdersValidating(List<ThirdPayOrderRecord> orders)
        {
            bool anyChanged = false;
            foreach (ThirdPayOrderRecord order in orders)
            {
                if (order == null)
                {
                    continue;
                }

                bool changed = !string.Equals(order.State, ThirdPayLocalOrderState.Validating, StringComparison.Ordinal);
                order.State = ThirdPayLocalOrderState.Validating;
                anyChanged |= changed;
            }

            if (anyChanged)
            {
                m_Hub.PersistContext.Save();
            }
        }

        /// <summary>
        /// 标记订单验单尚未终结，供后续补单继续处理。
        /// </summary>
        /// <param name="order">待更新状态的本地订单。</param>
        /// <returns>订单状态发生变化时返回 true。</returns>
        private static bool MarkOrderPendingValidation(ThirdPayOrderRecord order)
        {
            if (order == null)
            {
                return false;
            }

            bool changed = !string.Equals(order.State, ThirdPayLocalOrderState.PendingValidation, StringComparison.Ordinal);
            order.State = ThirdPayLocalOrderState.PendingValidation;
            return changed;
        }

        /// <summary>
        /// 为整批订单构造同一原因的验单失败结果。
        /// </summary>
        /// <param name="orders">待构造结果的本地订单列表。</param>
        /// <param name="reason">统一失败原因。</param>
        /// <param name="publishFailures">是否发布支付失败事件。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="context">本次失败对应的验单上下文。</param>
        /// <returns>与输入订单对应的失败结果列表。</returns>
        private List<IAPResult> BuildValidationFailures(List<ThirdPayOrderRecord> orders, string reason, bool publishFailures, bool isRecovered, ThirdPayValidationTrackContext context)
        {
            var results = new List<IAPResult>(orders.Count);
            bool anyChanged = false;
            foreach (ThirdPayOrderRecord order in orders)
            {
                anyChanged |= MarkOrderPendingValidation(order);
                var failure = new IAPResult(order.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, IAPErrorSource.ThirdPay, reason, order.CustomData, order.ClientOrderId, isRecovered, order.ReceiptParam);
                m_Hub.Store.AttachReturnedValidationFailureContext(failure, order, context);
                if (publishFailures)
                {
                    m_Hub.Context?.EventBridge?.RaisePayFailed(failure);
                }

                results.Add(failure);
            }

            if (anyChanged)
            {
                m_Hub.PersistContext.Save();
            }

            return results;
        }
    }
}
