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
        /// <returns>浏览器打开请求提交后的待确认结果。</returns>
        private async UniTask<IAPResult> OpenExternalBrowserPaymentAsync(IAPThirdPayRequest request, ThirdPayOrderRecord order, string googleToken, string paymentUrl, CancellationToken ct)
        {
            if (m_ExternalBrowserService == null)
            {
                LogWarning($"第三方支付外部浏览器服务未初始化：OrderId={order?.ClientOrderId}");
                return Fail(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方外部浏览器支付服务尚未初始化。");
            }

            BeginExternalBrowserPaySession(order);
            ThirdPayOpenResult openResult;
            try
            {
                LogDebug($"第三方支付准备打开外部支付页：OrderId={order.ClientOrderId}");
                openResult = await m_ExternalBrowserService.OpenAsync(paymentUrl, () => BuildPaymentUrl(order, googleToken, true, true), ct);
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
                TrackLocalPayFailInternal(request, IAPThirdPayErrorCode.WebViewClosed, ex.Message);
                return Fail(request, IAPThirdPayErrorCode.WebViewClosed, $"打开外部浏览器支付页异常：{ex.Message}");
            }

            if (openResult == ThirdPayOpenResult.Failed)
            {
                LogWarning($"第三方支付外部支付页打开失败：OrderId={order.ClientOrderId}");
                ClearExternalBrowserPaySession();
                TrackLocalPayFailInternal(request, IAPThirdPayErrorCode.WebViewClosed, "外部浏览器支付页打开失败。");
                return Fail(request, IAPThirdPayErrorCode.WebViewClosed, "外部浏览器支付页打开失败，订单保留等待后续验单。");
            }

            return BuildExternalBrowserPendingResult(order);
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

            m_ExternalBrowserPaySession = new ThirdPayExternalBrowserPaySession(order.ClientOrderId, order.TableId, order.UserId);
        }

        /// <summary>
        /// 标记外部浏览器支付期间 App 已离开前台。
        /// </summary>
        private void MarkExternalBrowserPaymentLeftApp()
        {
            ThirdPayExternalBrowserPaySession session = m_ExternalBrowserPaySession;
            if (session == null || session.Completed)
            {
                return;
            }

            session.HasLeftApp = true;
            session.CancelReturnDelay();
        }

        /// <summary>
        /// App 回到前台后启动或重启延迟验单。
        /// </summary>
        private void ScheduleExternalBrowserReturnValidation()
        {
            ThirdPayExternalBrowserPaySession session = m_ExternalBrowserPaySession;
            if (session == null || session.Completed || session.IsValidating || !session.HasLeftApp)
            {
                return;
            }

            session.ReturnVersion++;
            int version = session.ReturnVersion;
            session.CancelReturnDelay();
            session.DelayCts = CancellationTokenSource.CreateLinkedTokenSource(session.SessionCts.Token);
            RunExternalBrowserReturnValidationAfterDelayAsync(session.ClientOrderId, version, session.DelayCts.Token).Forget();
        }

        /// <summary>
        /// 对当前外部浏览器支付会话执行一次延迟验单。
        /// </summary>
        /// <param name="clientOrderId">期望的客户端订单号。</param>
        /// <param name="version">期望的返回版本号。</param>
        /// <param name="delayToken">延迟取消令牌。</param>
        private async UniTaskVoid RunExternalBrowserReturnValidationAfterDelayAsync(string clientOrderId, int version, CancellationToken delayToken)
        {
            bool validationStarted = false;
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(GetExternalBrowserReturnValidateDelaySeconds()), cancellationToken: delayToken);
                if (!TryBeginExternalBrowserReturnValidation(clientOrderId, version, out ThirdPayExternalBrowserPaySession session))
                {
                    return;
                }

                validationStarted = true;
                ThirdPayOrderRecord order = FindLocalOrder(clientOrderId);
                if (order == null)
                {
                    return;
                }

                AddWaitingRef();
                try
                {
                    await ValidateOrderAsync(order, ThirdPayValidationScene.ExternalBrowserReturn, session.SessionCts.Token);
                }
                finally
                {
                    SubWaitingRef();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                LogWarning($"外部浏览器支付返回验单异常：{ex.Message}");
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
            session = m_ExternalBrowserPaySession;
            if (session == null || session.Completed || session.IsValidating)
            {
                return false;
            }

            if (!string.Equals(session.ClientOrderId, clientOrderId, StringComparison.Ordinal) || session.ReturnVersion != version)
            {
                return false;
            }

            session.IsValidating = true;
            session.CancelReturnDelay();
            return true;
        }

        /// <summary>
        /// 当前会话仍匹配预期订单和版本时完成并清理会话。
        /// </summary>
        /// <param name="clientOrderId">期望的客户端订单号。</param>
        /// <param name="version">期望的返回版本号。</param>
        private void CompleteExternalBrowserPaySession(string clientOrderId, int version)
        {
            ThirdPayExternalBrowserPaySession session = m_ExternalBrowserPaySession;
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
            ThirdPayExternalBrowserPaySession session = m_ExternalBrowserPaySession;
            if (session == null)
            {
                return;
            }

            m_ExternalBrowserPaySession = null;
            session.Completed = true;
            session.Dispose();
        }

        /// <summary>
        /// 按客户端订单号查找已保存的本地订单。
        /// </summary>
        /// <param name="clientOrderId">客户端订单号。</param>
        /// <returns>本地订单；未找到时返回 null。</returns>
        private ThirdPayOrderRecord FindLocalOrder(string clientOrderId)
        {
            if (m_OrderRepository == null || string.IsNullOrEmpty(clientOrderId))
            {
                return null;
            }

            return m_OrderRepository.TryGet(clientOrderId, out ThirdPayOrderRecord order) ? order : null;
        }

        /// <summary>
        /// 构造外部浏览器打开后立即返回给调用方的待确认结果。
        /// </summary>
        /// <param name="order">已保存的本地订单。</param>
        /// <returns>携带稳定客户端订单号的待确认结果。</returns>
        private static IAPResult BuildExternalBrowserPendingResult(ThirdPayOrderRecord order)
        {
            return new IAPResult(order.TableId, (int)IAPThirdPayErrorCode.OrderPending, IAPErrorSource.ThirdPay, "外部浏览器支付已打开，订单等待返回 App 后验单。", order.CustomData, order.ClientOrderId, false, order.ReceiptParam);
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
