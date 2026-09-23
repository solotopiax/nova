/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.ExternalBrowser.cs
 * author:    yingzheng
 * created:   2026/8/27
 * descrip:   ThirdPay 外部浏览器支付会话
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 承载 ThirdPayStore 的外部浏览器支付会话实现。
    /// </summary>
    public sealed partial class ThirdPayStore
    {
        /// <summary>
        /// 处理当前外部浏览器支付会话的暂停/恢复事件。
        /// </summary>
        /// <param name="isPaused">true 表示进入后台或暂停，false 表示恢复前台。</param>
        void IIAPStorePauseListener.OnPause(bool isPaused)
        {
            OnStorePause(isPaused);
        }

        /// <summary>
        /// 处理当前外部浏览器支付会话的暂停/恢复事件。
        /// </summary>
        /// <param name="isPaused">true 表示进入后台或暂停，false 表示恢复前台。</param>
        private void OnStorePause(bool isPaused)
        {
            if (isPaused)
            {
                MarkExternalBrowserPaymentLeftApp();
                return;
            }

            ScheduleExternalBrowserReturnValidation();
        }

        /// <summary>
        /// 处理当前外部浏览器支付会话的焦点变化事件。
        /// </summary>
        /// <param name="hasFocus">true 表示获得焦点，false 表示失去焦点。</param>
        void IIAPStoreFocusListener.OnFocus(bool hasFocus)
        {
            OnStoreFocus(hasFocus);
        }

        /// <summary>
        /// 处理当前外部浏览器支付会话的焦点变化事件。
        /// </summary>
        /// <param name="hasFocus">true 表示获得焦点，false 表示失去焦点。</param>
        private void OnStoreFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                MarkExternalBrowserPaymentLeftApp();
                return;
            }

            ScheduleExternalBrowserReturnValidation();
        }

        /// <summary>
        /// 使用外部浏览器打开支付 URL，优先响应 Unity Deep Link，并保留返回 App 后的延迟验单兜底。
        /// </summary>
        /// <param name="request">ThirdPay 支付请求。</param>
        /// <param name="order">已保存的本地订单。</param>
        /// <param name="paymentConfig">本次支付固定使用的配置快照。</param>
        /// <param name="googleToken">Google 外部结算上报 token。</param>
        /// <param name="paymentUrl">最终支付 URL。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>Deep Link 成功回调或浏览器返回 App 后的验单结果。</returns>
        internal async UniTask<IAPResult> OpenExternalBrowserPaymentAsync(IAPThirdPayRequest request, ThirdPayOrderRecord order, ThirdPayPaymentConfigSnapshot paymentConfig, string googleToken, string paymentUrl, CancellationToken ct)
        {
            if (m_Hub.ExternalBrowserService == null)
            {
                LogWarning($"第三方支付外部浏览器服务未初始化：OrderId={order?.ClientOrderId}");
                RemoveLocalOrderIfPaymentPageDidNotOpen(order, ThirdPayOrderDeleteReason.PaymentPageNeverOpened);
                return AttachReturnedPayFailureContext(Fail(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方外部浏览器支付服务尚未初始化。"), ThirdPayPaymentFailureReason.ExternalBrowserServiceUnavailable, order?.ClientOrderId);
            }

            BeginExternalBrowserPaySession(order);
            ThirdPayExternalBrowserPaySession session = m_Hub.ExternalBrowserSessionState.Current;
            if (session == null)
            {
                RemoveLocalOrderIfPaymentPageDidNotOpen(order, ThirdPayOrderDeleteReason.PaymentPageNeverOpened);
                return AttachReturnedPayFailureContext(Fail(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方外部浏览器支付会话创建失败。"), ThirdPayPaymentFailureReason.PaymentPageOpenFailed, order?.ClientOrderId);
            }

            ThirdPayPaymentPageResult pageResult;
            try
            {
                LogDebug($"第三方支付准备打开外部支付页：OrderId={order.ClientOrderId}");
                pageResult = await m_Hub.ExternalBrowserService.OpenWithResultAsync(paymentUrl, () => BuildPaymentUrl(order, paymentConfig, googleToken, true, showBackButton: true), ct);
                session.RecordPaymentPageResult(pageResult);
                LogDebug($"第三方支付外部支付页打开返回：OrderId={order.ClientOrderId}，Result={pageResult.Result}，Mode={pageResult.OpenMode}，Established={pageResult.IsSessionEstablished}");
            }
            catch (OperationCanceledException)
            {
                FinalizeExternalBrowserClose(session, ThirdPayCloseReason.OperationCancelled, ThirdPayPaymentFailureReason.OperationCancelled);
                ClearExternalBrowserPaySession();
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"第三方支付外部支付页打开异常：OrderId={order.ClientOrderId}，Error={ex.Message}");
                ClearExternalBrowserPaySession();
                RemoveLocalOrderIfPaymentPageDidNotOpen(order, ThirdPayOrderDeleteReason.PaymentPageNeverOpened);
                ThirdPayPaymentPageResult exceptionResult = ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, session.OpenMode, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.PaymentPageException, ThirdPayPaymentFailureReason.PaymentPageOpenException, 0, ex.Message, false);
                return AttachReturnedPayFailureContext(Fail(request, IAPThirdPayErrorCode.WebViewClosed, $"打开外部浏览器支付页异常：{ex.Message}"), ThirdPayPaymentFailureReason.PaymentPageOpenException, order.ClientOrderId, exceptionResult);
            }

            if (!pageResult.IsSessionEstablished)
            {
                LogWarning($"第三方支付外部支付页打开失败：OrderId={order.ClientOrderId}");
                ClearExternalBrowserPaySession();
                RemoveLocalOrderIfPaymentPageDidNotOpen(order, ThirdPayOrderDeleteReason.PaymentPageNeverOpened);
                return AttachReturnedPayFailureContext(Fail(request, IAPThirdPayErrorCode.WebViewClosed, "外部浏览器支付页打开失败。"), pageResult.FailureReason, order.ClientOrderId, pageResult);
            }

            return await session.PayTcs.Task;
        }

        /// <summary>
        /// 为已保存的本地订单创建新的浏览器支付会话。
        /// </summary>
        /// <param name="order">已保存的本地订单。</param>
        private void BeginExternalBrowserPaySession(ThirdPayOrderRecord order)
        {
            ThirdPayExternalBrowserPaySession previous = m_Hub.ExternalBrowserSessionState.Current;
            if (previous != null)
            {
                FinalizeExternalBrowserClose(previous, ThirdPayCloseReason.SessionReplaced, ThirdPayPaymentFailureReason.SessionReplaced);
            }

            ClearExternalBrowserPaySession();
            if (order == null || string.IsNullOrEmpty(order.ClientOrderId))
            {
                return;
            }

            m_Hub.ExternalBrowserSessionState.Begin(order);
            Application.deepLinkActivated += OnExternalBrowserDeepLinkActivated;
        }

        /// <summary>
        /// 处理 Auth Tab、Custom Tabs 或系统浏览器送回的支付 Deep Link。
        /// </summary>
        /// <param name="url">完整支付回调 URL。</param>
        private void OnExternalBrowserDeepLinkActivated(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                LogDebug("第三方支付外部浏览器收到空 DeepLink，继续等待返回兜底验单。");
                return;
            }

            ThirdPayWebViewCallback callback;
            ThirdPayCallbackResolveResult resolveResult;
            try
            {
                var message = new UniWebViewMessage(url);
                resolveResult = ThirdPayWebViewCallbackResolver.ResolveCallback(message.Path, message.Args, out callback);
            }
            catch (Exception ex)
            {
                LogWarning($"第三方支付外部浏览器 DeepLink 解析失败：Error={ex.Message}");
                return;
            }

            if (resolveResult == ThirdPayCallbackResolveResult.NotPaymentCallback)
            {
                LogDebug($"第三方支付外部浏览器未识别 DeepLink：URL={url}，继续等待返回兜底验单。");
                return;
            }

            ThirdPayExternalBrowserPaySession activeSession = m_Hub.ExternalBrowserSessionState.Current;
            if (activeSession == null || activeSession.Completed)
            {
                LogDebug($"第三方支付外部浏览器收到支付回调但没有活动会话：URL={url}");
                return;
            }

            if (resolveResult == ThirdPayCallbackResolveResult.InvalidPayload)
            {
                LogWarning($"第三方支付外部浏览器 pay_callback 参数无效，无法确认所属订单并继续等待：OrderId={activeSession.ClientOrderId}");
                return;
            }

            if (resolveResult == ThirdPayCallbackResolveResult.UnknownStatus)
            {
                if (!string.Equals(activeSession.ClientOrderId, callback.OrderId, StringComparison.Ordinal))
                {
                    LogWarning($"第三方支付外部浏览器忽略非当前订单的未知状态回调：CallbackOrderId={callback.OrderId}，ActiveOrderId={activeSession.ClientOrderId}，Status={callback.RawStatus}");
                    return;
                }

                activeSession.RecordCloseCandidate(ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.PayCallbackUnknownStatus, ThirdPayPaymentFailureReason.PaymentCallbackUnknownStatus, callback.RawStatus);
                LogWarning($"第三方支付外部浏览器 pay_callback 状态未知：CallbackOrderId={callback.OrderId}，Status={callback.RawStatus}");
                ScheduleExternalBrowserReturnValidationFromCallback(activeSession);
                return;
            }

            if (string.Equals(callback.Path, "pay_callback", StringComparison.Ordinal) && !string.Equals(activeSession.ClientOrderId, callback.OrderId, StringComparison.Ordinal))
            {
                LogWarning($"第三方支付外部浏览器忽略非当前订单回调：CallbackOrderId={callback.OrderId}，ActiveOrderId={activeSession.ClientOrderId}，Status={callback.RawStatus}");
                return;
            }

            LogDebug($"第三方支付外部浏览器收到支付回调：Path={callback.Path}，OrderId={callback.OrderId}，Status={(int)callback.Status}，Result={callback.Result}");
            if (callback.Result != ThirdPayOpenResult.Success)
            {
                ThirdPayCloseReason closeReason = callback.Result == ThirdPayOpenResult.Cancel ? ThirdPayCloseReason.CloseCallback : ThirdPayCloseReason.PayCallbackFailed;
                ThirdPayPaymentFailureReason failureReason = callback.Result == ThirdPayOpenResult.Cancel ? ThirdPayPaymentFailureReason.OperationCancelled : ThirdPayPaymentFailureReason.PaymentCallbackFailed;
                activeSession.RecordCloseCandidate(callback.Status, closeReason, failureReason, callback.RawStatus);
                LogDebug($"第三方支付外部浏览器回调未成功，等待返回兜底验单：OrderId={callback.OrderId}，Status={(int)callback.Status}");
                ScheduleExternalBrowserReturnValidationFromCallback(activeSession);
                return;
            }

            if (!TryBeginExternalBrowserCallbackValidation(callback.OrderId, out ThirdPayExternalBrowserPaySession session))
            {
                string activeOrderId = activeSession.ClientOrderId;
                if (activeSession.IsValidating)
                {
                    activeSession.RecordPaymentPageResult(ThirdPayPaymentPageResult.CallbackSuccess(activeSession.OpenMode, callback.Status));
                    FinalizeExternalBrowserSuccess(activeSession, FindLocalOrder(activeOrderId) ?? BuildOrderFromExternalBrowserSession(activeSession));
                    LogDebug($"第三方支付外部浏览器成功回调在兜底验单期间到达，已补记本地支付成功且不重复发起验单：OrderId={activeOrderId}");
                    return;
                }

                LogDebug($"第三方支付外部浏览器成功回调未取得验单权，保持当前会话等待：OrderId={activeOrderId}");
                return;
            }

            session.RecordPaymentPageResult(ThirdPayPaymentPageResult.CallbackSuccess(session.OpenMode, callback.Status));
            int version = session.ReturnVersion;
            PushExternalBrowserReturnWaiting(session);
            LogDebug($"第三方支付外部浏览器成功回调已取消返回倒计时，立即开始验单：OrderId={session.ClientOrderId}，Status={(int)callback.Status}，Version={version}");
            m_Hub.RunBackgroundTask(_ => RunExternalBrowserCallbackValidationAsync(session.ClientOrderId, version), "外部浏览器成功回调验单");
        }

        /// <summary>
        /// 在订单号匹配且尚未开始验单时，将活动会话推进到成功回调验单状态。
        /// </summary>
        /// <param name="clientOrderId">回调携带的客户端订单号。</param>
        /// <param name="session">命中的活动会话。</param>
        /// <returns>当前回调取得本会话唯一验单权时返回 true。</returns>
        private bool TryBeginExternalBrowserCallbackValidation(string clientOrderId, out ThirdPayExternalBrowserPaySession session)
        {
            session = m_Hub.ExternalBrowserSessionState.Current;
            if (session == null
                || session.Completed
                || session.IsValidating
                || !string.Equals(session.ClientOrderId, clientOrderId, StringComparison.Ordinal))
            {
                return false;
            }

            session.IsValidating = true;
            session.ReturnVersion++;
            session.CancelReturnDelay();
            return true;
        }

        /// <summary>
        /// 对已命中活动会话的外部浏览器成功回调立即执行验单。
        /// </summary>
        /// <param name="clientOrderId">回调匹配的客户端订单号。</param>
        /// <param name="version">成功回调取得验单权后的会话版本。</param>
        private async UniTask RunExternalBrowserCallbackValidationAsync(string clientOrderId, int version)
        {
            ThirdPayExternalBrowserPaySession session = null;
            try
            {
                if (!m_Hub.ExternalBrowserSessionState.TryGetMatching(clientOrderId, version, out session) || session.Completed || !session.IsValidating)
                {
                    return;
                }

                ThirdPayOrderRecord order = FindLocalOrder(clientOrderId);
                FinalizeExternalBrowserSuccess(session, order ?? BuildOrderFromExternalBrowserSession(session));
                if (order == null)
                {
                    IAPResult missing = BuildExternalBrowserMissingOrderResult(session);
                    AttachReturnedValidationFailureContext(missing, BuildOrderFromExternalBrowserSession(session), new ThirdPayValidationTrackContext(ThirdPayValidationScene.ExternalBrowserCallback, 0, ThirdPayClientOrderStatus.ResponseOrderMissing, string.Empty, ThirdPayPaymentFailureReason.ValidationResponseOrderMissing));
                    Context?.EventBridge?.RaisePayFailed(missing);
                    CompleteExternalBrowserReturnPayResult(session, missing);
                    return;
                }

                IAPResult result = await ValidateOrderAsync(order, ThirdPayValidationScene.ExternalBrowserCallback, session.SessionCts.Token);
                CompleteExternalBrowserReturnPayResult(session, result);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogWarning($"外部浏览器支付成功回调验单异常：{ex.Message}");
                if (session != null)
                {
                    IAPResult failure = BuildExternalBrowserValidationExceptionResult(session, ex);
                    AttachReturnedValidationFailureContext(failure, BuildOrderFromExternalBrowserSession(session), new ThirdPayValidationTrackContext(ThirdPayValidationScene.ExternalBrowserCallback, 0, ThirdPayClientOrderStatus.Unknown, string.Empty, ThirdPayPaymentFailureReason.UnexpectedException));
                    Context?.EventBridge?.RaisePayFailed(failure);
                    CompleteExternalBrowserReturnPayResult(session, failure);
                }
            }
            finally
            {
                if (session != null)
                {
                    CompleteExternalBrowserPaySession(clientOrderId, version);
                }
            }
        }

        /// <summary>
        /// 标记外部浏览器支付期间 App 已离开前台。
        /// </summary>
        private void MarkExternalBrowserPaymentLeftApp()
        {
            ThirdPayExternalBrowserPaySession session = m_Hub.ExternalBrowserSessionState.Current;
            if (session == null || session.Completed)
            {
                return;
            }

            session.HasLeftApp = true;
            if (!session.IsValidating)
            {
                PopExternalBrowserReturnWaiting(session);
                session.CancelReturnDelay();
            }
        }

        /// <summary>
        /// App 回到前台后启动或重启延迟验单。
        /// </summary>
        private void ScheduleExternalBrowserReturnValidation()
        {
            ThirdPayExternalBrowserPaySession session = m_Hub.ExternalBrowserSessionState.Current;
            if (session == null || session.Completed || session.IsValidating || !session.HasLeftApp)
            {
                return;
            }

            session.ReturnVersion++;
            int version = session.ReturnVersion;
            float delaySeconds = GetExternalBrowserReturnValidateDelaySeconds();
            session.CancelReturnDelay();
            session.DelayCts = CancellationTokenSource.CreateLinkedTokenSource(session.SessionCts.Token, m_Hub.RuntimeTaskToken);
            PushExternalBrowserReturnWaiting(session);
            // 记录倒计时启动，便于真机确认从外部浏览器返回后的自动验单触发点。
            LogDebug($"第三方支付外部浏览器返回验单倒计时开始：OrderId={session.ClientOrderId}，Version={version}，DelaySeconds={delaySeconds:0.###}");
            m_Hub.RunBackgroundTask(_ => RunExternalBrowserReturnValidationAfterDelayAsync(session.ClientOrderId, version, delaySeconds, session.DelayCts.Token), "外部浏览器返回延迟验单");
        }

        /// <summary>
        /// pay_callback 已将应用带回前台时，直接复用外部浏览器返回倒计时启动兜底验单。
        /// </summary>
        /// <param name="session">当前活动支付会话。</param>
        private void ScheduleExternalBrowserReturnValidationFromCallback(ThirdPayExternalBrowserPaySession session)
        {
            if (session == null || session.Completed || session.IsValidating)
            {
                return;
            }

            session.HasLeftApp = true;
            ScheduleExternalBrowserReturnValidation();
        }

        /// <summary>
        /// 对当前外部浏览器支付会话执行一次延迟验单。
        /// </summary>
        /// <param name="clientOrderId">期望的客户端订单号。</param>
        /// <param name="version">期望的返回版本号。</param>
        /// <param name="delaySeconds">本次返回验单倒计时秒数。</param>
        /// <param name="delayToken">延迟取消令牌。</param>
        private async UniTask RunExternalBrowserReturnValidationAfterDelayAsync(string clientOrderId, int version, float delaySeconds, CancellationToken delayToken)
        {
            bool validationStarted = false;
            ThirdPayExternalBrowserPaySession session = null;
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: delayToken);
                if (!TryBeginExternalBrowserReturnValidation(clientOrderId, version, out session))
                {
                    return;
                }

                validationStarted = true;
                if (session.CloseReason == ThirdPayCloseReason.Unknown)
                {
                    session.RecordCloseCandidate(ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.ExternalBrowserReturnedWithoutCallback, ThirdPayPaymentFailureReason.ExternalBrowserReturnedWithoutCallback);
                }

                ThirdPayOrderRecord order = FindLocalOrder(clientOrderId);
                if (order == null)
                {
                    FinalizeExternalBrowserClose(session);
                    IAPResult missing = BuildExternalBrowserMissingOrderResult(session);
                    AttachReturnedValidationFailureContext(missing, BuildOrderFromExternalBrowserSession(session), new ThirdPayValidationTrackContext(ThirdPayValidationScene.ExternalBrowserReturn, 0, ThirdPayClientOrderStatus.ResponseOrderMissing, string.Empty, ThirdPayPaymentFailureReason.ValidationResponseOrderMissing));
                    Context?.EventBridge?.RaisePayFailed(missing);
                    CompleteExternalBrowserReturnPayResult(session, missing);
                    return;
                }

                // 只在当前版本倒计时完成且订单仍存在时，记录即将发起的验单。
                LogDebug($"第三方支付外部浏览器返回验单倒计时结束，开始验证订单：OrderId={clientOrderId}，Version={version}，TableId={order.TableId}，UserId={order.UserId}");
                IAPResult result = await ValidateOrderAsync(order, ThirdPayValidationScene.ExternalBrowserReturn, session.SessionCts.Token);
                FinalizeExternalBrowserClose(session);
                CompleteExternalBrowserReturnPayResult(session, result);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogWarning($"外部浏览器支付返回验单异常：{ex.Message}");
                if (session != null)
                {
                    FinalizeExternalBrowserClose(session);
                    IAPResult failure = BuildExternalBrowserValidationExceptionResult(session, ex);
                    AttachReturnedValidationFailureContext(failure, BuildOrderFromExternalBrowserSession(session), new ThirdPayValidationTrackContext(ThirdPayValidationScene.ExternalBrowserReturn, 0, ThirdPayClientOrderStatus.Unknown, string.Empty, ThirdPayPaymentFailureReason.UnexpectedException));
                    Context?.EventBridge?.RaisePayFailed(failure);
                    CompleteExternalBrowserReturnPayResult(session, failure);
                }
            }
            finally
            {
                if (validationStarted)
                {
                    CompleteExternalBrowserPaySession(clientOrderId, version);
                }
            }
        }

        /// <summary>
        /// 延迟任务仍为当前版本时，将活跃会话推进到验单中状态。
        /// </summary>
        /// <param name="clientOrderId">期望的客户端订单号。</param>
        /// <param name="version">期望的返回版本号。</param>
        /// <param name="session">活跃会话。</param>
        /// <returns>可开始验单时返回 true。</returns>
        private bool TryBeginExternalBrowserReturnValidation(string clientOrderId, int version, out ThirdPayExternalBrowserPaySession session)
        {
            if (!m_Hub.ExternalBrowserSessionState.TryGetMatching(clientOrderId, version, out session)
                || session.Completed
                || session.IsValidating)
            {
                return false;
            }

            session.IsValidating = true;
            session.CancelReturnDelay();
            return true;
        }

        /// <summary>
        /// 外部浏览器返回 App 后显示 Loading，覆盖倒计时等待期，避免玩家误触底层 UI。
        /// </summary>
        /// <param name="session">当前外部浏览器支付会话。</param>
        private void PushExternalBrowserReturnWaiting(ThirdPayExternalBrowserPaySession session)
        {
            if (session == null || session.Completed || session.ReturnWaitingRefAdded)
            {
                return;
            }

            bool shouldShow = m_LoadingGuard.ShouldShow();
            AddWaitingRef(shouldShow);
            session.ReturnWaitingRefAdded = shouldShow;
        }

        /// <summary>
        /// 释放外部浏览器返回等待期持有的 Loading 引用。
        /// </summary>
        /// <param name="session">当前外部浏览器支付会话。</param>
        private void PopExternalBrowserReturnWaiting(ThirdPayExternalBrowserPaySession session)
        {
            if (session == null || !session.ReturnWaitingRefAdded)
            {
                return;
            }

            session.ReturnWaitingRefAdded = false;
            SubWaitingRef(true);
        }

        /// <summary>
        /// 当前会话仍匹配预期订单和版本时完成并清理会话。
        /// </summary>
        /// <param name="clientOrderId">期望的客户端订单号。</param>
        /// <param name="version">期望的返回版本号。</param>
        private void CompleteExternalBrowserPaySession(string clientOrderId, int version)
        {
            ThirdPayExternalBrowserPaySession session = m_Hub.ExternalBrowserSessionState.Current;
            if (session == null)
            {
                return;
            }

            if (!string.Equals(session.ClientOrderId, clientOrderId, StringComparison.Ordinal) || session.ReturnVersion != version)
            {
                return;
            }

            ClearExternalBrowserPaySession();
        }

        /// <summary>
        /// 在当前外部浏览器会话首次收到有效成功 pay_callback 时上报本地支付成功。
        /// </summary>
        /// <param name="session">当前活动支付会话。</param>
        /// <param name="order">用于关联埋点的本地订单。</param>
        private void FinalizeExternalBrowserSuccess(ThirdPayExternalBrowserPaySession session, ThirdPayOrderRecord order)
        {
            if (session == null || order == null || !session.TryFinalizePaymentPageTrack())
            {
                return;
            }

            ThirdPayPaymentPageResult pageResult = ThirdPayPaymentPageResult.CallbackSuccess(session.OpenMode, session.CallbackStatus);
            session.RecordPaymentPageResult(pageResult);
            TrackLocalPaySuccessInternal(order, false, pageResult);
        }

        /// <summary>
        /// 在外部支付页没有成功 pay_callback 时上报唯一关闭终态。
        /// </summary>
        /// <param name="session">当前活动支付会话。</param>
        /// <param name="closeReason">需要覆盖记录的关闭原因；Unknown 表示使用会话已有候选。</param>
        /// <param name="failureReason">与关闭原因对应的支付失败原因。</param>
        private void FinalizeExternalBrowserClose(ThirdPayExternalBrowserPaySession session, ThirdPayCloseReason closeReason = ThirdPayCloseReason.Unknown, ThirdPayPaymentFailureReason failureReason = ThirdPayPaymentFailureReason.Unknown)
        {
            if (session == null || !session.IsPaymentPageEstablished)
            {
                return;
            }

            if (closeReason != ThirdPayCloseReason.Unknown)
            {
                session.RecordCloseCandidate(session.CallbackStatus, closeReason, failureReason);
            }

            if (!session.TryFinalizePaymentPageTrack())
            {
                return;
            }

            if (session.CloseReason == ThirdPayCloseReason.Unknown)
            {
                session.RecordCloseCandidate(session.CallbackStatus, ThirdPayCloseReason.ExternalBrowserReturnedWithoutCallback, ThirdPayPaymentFailureReason.ExternalBrowserReturnedWithoutCallback);
            }

            TrackThirdPayCloseOrderInternal(BuildOrderFromExternalBrowserSession(session), session.BuildPaymentPageResult(ThirdPayOpenResult.Cancel));
        }

        /// <summary>
        /// 从外部浏览器会话恢复仅用于埋点关联的订单快照。
        /// </summary>
        /// <param name="session">当前外部浏览器会话。</param>
        /// <returns>包含稳定客户端订单号和业务字段的订单快照。</returns>
        private static ThirdPayOrderRecord BuildOrderFromExternalBrowserSession(ThirdPayExternalBrowserPaySession session)
        {
            return new ThirdPayOrderRecord
            {
                ClientOrderId = session.ClientOrderId,
                TableId = session.TableId,
                UserId = session.UserId,
                CustomData = session.CustomData,
                ReceiptParam = session.ReceiptParam,
                State = ThirdPayLocalOrderState.PendingValidation,
            };
        }

        /// <summary>
        /// 清理当前外部浏览器支付会话并取消未完成计时器。
        /// </summary>
        internal void ClearExternalBrowserPaySession()
        {
            Application.deepLinkActivated -= OnExternalBrowserDeepLinkActivated;
            ThirdPayExternalBrowserPaySession session = m_Hub.ExternalBrowserSessionState.Detach();
            if (session == null)
            {
                return;
            }

            PopExternalBrowserReturnWaiting(session);
            session.Completed = true;
            IAPResult closedResult = BuildExternalBrowserSessionClosedResult(session);
            ThirdPayPaymentFailureReason failureReason = session.FailureReason == ThirdPayPaymentFailureReason.Unknown ? ThirdPayPaymentFailureReason.UnexpectedException : session.FailureReason;
            AttachReturnedPayFailureContext(closedResult, failureReason, session.ClientOrderId, session.BuildPaymentPageResult(ThirdPayOpenResult.Failed));
            session.PayTcs.TrySetResult(closedResult);
            session.Dispose();
        }

        /// <summary>
        /// 以明确原因完成当前外部浏览器支付页终态埋点后清理会话。
        /// </summary>
        /// <param name="closeReason">支付页关闭原因。</param>
        /// <param name="failureReason">支付流程失败原因。</param>
        internal void CloseAndClearExternalBrowserPaySession(ThirdPayCloseReason closeReason, ThirdPayPaymentFailureReason failureReason)
        {
            FinalizeExternalBrowserClose(m_Hub.ExternalBrowserSessionState.Current, closeReason, failureReason);
            ClearExternalBrowserPaySession();
        }

        /// <summary>
        /// 将外部浏览器返回验单结果回传给仍在等待的 PayAsync 调用方。
        /// </summary>
        /// <param name="session">当前外部浏览器支付会话。</param>
        /// <param name="result">返回 App 后取得的验单结果。</param>
        private static void CompleteExternalBrowserReturnPayResult(ThirdPayExternalBrowserPaySession session, IAPResult result)
        {
            if (session == null || result == null)
            {
                return;
            }

            session.PayTcs.TrySetResult(result);
        }

        /// <summary>
        /// 按客户端订单号查找已保存的本地订单。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <returns>本地订单；未找到时返回 null。</returns>
        private ThirdPayOrderRecord FindLocalOrder(string clientOrderId)
        {
            if (string.IsNullOrEmpty(clientOrderId))
            {
                return null;
            }

            return m_Hub.PersistContext.TryGetOrder(clientOrderId, out ThirdPayOrderRecord order) ? order : null;
        }

        /// <summary>
        /// 构造外部浏览器会话被清理但尚无验单结果时返回给调用方的失败结果。
        /// </summary>
        /// <param name="session">当前外部浏览器支付会话。</param>
        /// <returns>携带稳定客户端订单号的失败结果。</returns>
        private static IAPResult BuildExternalBrowserSessionClosedResult(ThirdPayExternalBrowserPaySession session)
        {
            return new IAPResult(session.TableId, (int)IAPThirdPayErrorCode.StoreNotAvailable, IAPErrorSource.ThirdPay, "外部浏览器支付会话已结束，订单保留等待后续验单。", session.CustomData, session.ClientOrderId, false, session.ReceiptParam);
        }

        /// <summary>
        /// 构造外部浏览器返回验单时本地订单缺失的失败结果。
        /// </summary>
        /// <param name="session">当前外部浏览器支付会话。</param>
        /// <returns>携带稳定客户端订单号的失败结果。</returns>
        private static IAPResult BuildExternalBrowserMissingOrderResult(ThirdPayExternalBrowserPaySession session)
        {
            return new IAPResult(session.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, IAPErrorSource.ThirdPay, "外部浏览器返回验单未找到本地订单。", session.CustomData, session.ClientOrderId, false, session.ReceiptParam);
        }

        /// <summary>
        /// 构造外部浏览器返回验单异常时返回给调用方的失败结果。
        /// </summary>
        /// <param name="session">当前外部浏览器支付会话。</param>
        /// <param name="exception">验单异常。</param>
        /// <returns>携带稳定客户端订单号的失败结果。</returns>
        private static IAPResult BuildExternalBrowserValidationExceptionResult(ThirdPayExternalBrowserPaySession session, Exception exception)
        {
            return new IAPResult(session.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, IAPErrorSource.ThirdPay, $"外部浏览器返回验单异常：{exception.Message}", session.CustomData, session.ClientOrderId, false, session.ReceiptParam);
        }

        /// <summary>
        /// 获取外部浏览器返回后的验单延迟秒数。
        /// </summary>
        /// <returns>正数延迟秒数。</returns>
        private float GetExternalBrowserReturnValidateDelaySeconds()
        {
            return m_Hub.Config?.ExternalBrowserReturnValidateDelaySeconds ?? c_DefaultExternalBrowserReturnValidateDelaySeconds;
        }
    }
}
