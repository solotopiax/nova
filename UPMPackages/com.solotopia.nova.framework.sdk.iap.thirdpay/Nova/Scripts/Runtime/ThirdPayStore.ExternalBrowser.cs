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

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    public sealed partial class ThirdPayStore
    {
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
        /// 使用外部浏览器打开支付 URL，并将验单交给返回 App 后的生命周期触发。
        /// </summary>
        /// <param name="request">ThirdPay 支付请求。</param>
        /// <param name="order">已保存的本地订单。</param>
        /// <param name="googleToken">Google 外部结算上报 token。</param>
        /// <param name="paymentUrl">最终支付 URL。</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>浏览器返回 App 后的验单结果。</returns>
        private async UniTask<IAPResult> OpenExternalBrowserPaymentAsync(IAPThirdPayRequest request, ThirdPayOrderRecord order, string googleToken, string paymentUrl, CancellationToken ct)
        {
            if (m_ExternalBrowserService == null)
            {
                LogWarning($"第三方支付外部浏览器服务未初始化：OrderId={order?.ClientOrderId}");
                RemoveLocalOrderIfPaymentPageDidNotOpen(order);
                return Fail(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方外部浏览器支付服务尚未初始化。");
            }

            BeginExternalBrowserPaySession(order);
            ThirdPayExternalBrowserPaySession session = m_ExternalBrowserSessionState.Current;
            if (session == null)
            {
                RemoveLocalOrderIfPaymentPageDidNotOpen(order);
                return Fail(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方外部浏览器支付会话创建失败。");
            }

            ThirdPayOpenResult openResult;
            try
            {
                LogDebug($"第三方支付准备打开外部支付页：OrderId={order.ClientOrderId}");
                openResult = await m_ExternalBrowserService.OpenAsync(paymentUrl, () => BuildPaymentUrl(order, googleToken, true, showBackButton: true), ct);
                LogDebug($"第三方支付外部支付页打开返回：OrderId={order.ClientOrderId}，Result={openResult}");
            }
            catch (OperationCanceledException)
            {
                ClearExternalBrowserPaySession();
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"第三方支付外部支付页打开异常：OrderId={order.ClientOrderId}，Error={ex.Message}");
                ClearExternalBrowserPaySession();
                RemoveLocalOrderIfPaymentPageDidNotOpen(order);
                TrackLocalPayFailInternal(request, IAPThirdPayErrorCode.WebViewClosed, ex.Message);
                return Fail(request, IAPThirdPayErrorCode.WebViewClosed, $"打开外部浏览器支付页异常：{ex.Message}");
            }

            if (openResult == ThirdPayOpenResult.Failed)
            {
                LogWarning($"第三方支付外部支付页打开失败：OrderId={order.ClientOrderId}");
                ClearExternalBrowserPaySession();
                RemoveLocalOrderIfPaymentPageDidNotOpen(order);
                TrackLocalPayFailInternal(request, IAPThirdPayErrorCode.WebViewClosed, "外部浏览器支付页打开失败。");
                return Fail(request, IAPThirdPayErrorCode.WebViewClosed, "外部浏览器支付页打开失败。");
            }

            return await session.PayTcs.Task;
        }

        /// <summary>
        /// 为已保存的本地订单创建新的浏览器支付会话。
        /// </summary>
        /// <param name="order">已保存的本地订单。</param>
        private void BeginExternalBrowserPaySession(ThirdPayOrderRecord order)
        {
            ClearExternalBrowserPaySession();
            if (order == null || string.IsNullOrEmpty(order.ClientOrderId))
            {
                return;
            }

            m_ExternalBrowserSessionState.Begin(order);
        }

        /// <summary>
        /// 标记外部浏览器支付期间 App 已离开前台。
        /// </summary>
        private void MarkExternalBrowserPaymentLeftApp()
        {
            ThirdPayExternalBrowserPaySession session = m_ExternalBrowserSessionState.Current;
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
            ThirdPayExternalBrowserPaySession session = m_ExternalBrowserSessionState.Current;
            if (session == null || session.Completed || session.IsValidating || !session.HasLeftApp)
            {
                return;
            }

            session.ReturnVersion++;
            int version = session.ReturnVersion;
            float delaySeconds = GetExternalBrowserReturnValidateDelaySeconds();
            session.CancelReturnDelay();
            session.DelayCts = CancellationTokenSource.CreateLinkedTokenSource(session.SessionCts.Token);
            PushExternalBrowserReturnWaiting(session);
            // 记录倒计时启动，便于真机确认从外部浏览器返回后的自动验单触发点。
            LogDebug($"第三方支付外部浏览器返回验单倒计时开始：OrderId={session.ClientOrderId}，Version={version}，DelaySeconds={delaySeconds:0.###}");
            RunExternalBrowserReturnValidationAfterDelayAsync(session.ClientOrderId, version, delaySeconds, session.DelayCts.Token).Forget();
        }

        /// <summary>
        /// 对当前外部浏览器支付会话执行一次延迟验单。
        /// </summary>
        /// <param name="clientOrderId">期望的客户端订单号。</param>
        /// <param name="version">期望的返回版本号。</param>
        /// <param name="delaySeconds">本次返回验单倒计时秒数。</param>
        /// <param name="delayToken">延迟取消令牌。</param>
        private async UniTaskVoid RunExternalBrowserReturnValidationAfterDelayAsync(string clientOrderId, int version, float delaySeconds, CancellationToken delayToken)
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
                ThirdPayOrderRecord order = FindLocalOrder(clientOrderId);
                if (order == null)
                {
                    IAPResult missing = BuildExternalBrowserMissingOrderResult(session);
                    Context?.EventBridge?.RaisePayFailed(missing);
                    CompleteExternalBrowserReturnPayResult(session, missing);
                    return;
                }

                // 只在当前版本倒计时完成且订单仍存在时，记录即将发起的验单。
                LogDebug($"第三方支付外部浏览器返回验单倒计时结束，开始验证订单：OrderId={clientOrderId}，Version={version}，TableId={order.TableId}，UserId={order.UserId}");
                IAPResult result = await ValidateOrderAsync(order, ThirdPayValidationScene.ExternalBrowserReturn, session.SessionCts.Token);
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
                    IAPResult failure = BuildExternalBrowserValidationExceptionResult(session, ex);
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
            if (!m_ExternalBrowserSessionState.TryGetMatching(clientOrderId, version, out session)
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
            ThirdPayExternalBrowserPaySession session = m_ExternalBrowserSessionState.Current;
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
        /// 清理当前外部浏览器支付会话并取消未完成计时器。
        /// </summary>
        private void ClearExternalBrowserPaySession()
        {
            ThirdPayExternalBrowserPaySession session = m_ExternalBrowserSessionState.Detach();
            if (session == null)
            {
                return;
            }

            PopExternalBrowserReturnWaiting(session);
            session.Completed = true;
            session.PayTcs.TrySetResult(BuildExternalBrowserSessionClosedResult(session));
            session.Dispose();
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

            return m_PersistContext.TryGetOrder(clientOrderId, out ThirdPayOrderRecord order) ? order : null;
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
            return m_Config?.ExternalBrowserReturnValidateDelaySeconds ?? c_DefaultExternalBrowserReturnValidateDelaySeconds;
        }
    }
}
