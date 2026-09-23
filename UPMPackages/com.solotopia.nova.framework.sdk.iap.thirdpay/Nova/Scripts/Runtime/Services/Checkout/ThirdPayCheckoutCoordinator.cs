/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayCheckoutCoordinator.cs
 * author:    yingzheng
 * created:   2026/9/17
 * descrip:   ThirdPay 支付创建、页面打开与验单编排
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 编排第三方支付配置、建单、Google 政策、支付页以及后续验单。
    /// </summary>
    internal sealed class ThirdPayCheckoutCoordinator : ThirdPayLogOwner
    {
        /// <summary>
        /// ThirdPay 强类型服务容器。
        /// </summary>
        private readonly ThirdPayServiceHub m_Hub;

        /// <summary>
        /// 创建第三方支付主流程协调器。
        /// </summary>
        /// <param name="hub">ThirdPay 强类型服务容器。</param>
        public ThirdPayCheckoutCoordinator(ThirdPayServiceHub hub)
        {
            m_Hub = hub ?? throw new ArgumentNullException(nameof(hub));
        }

        /// <summary>
        /// 执行第三方支付主链，保持原有配置、建单、页面打开及验单顺序。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>本次支付或验单的最终结果。</returns>
        public async UniTask<IAPResult> ExecuteAsync(IAPThirdPayRequest request, CancellationToken ct)
        {
            m_Hub.Store.MarkUserInteracted();

            bool useExternalBrowser = false;
            if (m_Hub.WebViewService == null && m_Hub.ExternalBrowserService == null)
            {
                m_Hub.Store.TrackCreateOrderFailInternal(request, ThirdPayCreateOrderFailureReason.PaymentPageServiceUnavailable);
                return CreateFailure(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方支付页服务尚未初始化。", ThirdPayPaymentFailureReason.StoreNotInitialized);
            }

            string userId = m_Hub.Store.CurrentUserId;
            if (string.IsNullOrEmpty(userId))
            {
                m_Hub.Store.TrackCreateOrderFailInternal(request, ThirdPayCreateOrderFailureReason.UserIdMissing);
                return CreateFailure(request, IAPThirdPayErrorCode.StoreInitFailed, "尚未设置用户 ID。", ThirdPayPaymentFailureReason.UserIdMissing);
            }

            ThirdPayOrderRecord order = null;
            ThirdPayPaymentConfigSnapshot paymentConfig = null;
            string paymentUrl = string.Empty;
            string googleToken = string.Empty;
            m_Hub.Store.AddWaitingRef();
            try
            {
                LogDebug($"第三方支付开始：TableId={request.TableId}，UserId={userId}，Country={m_Hub.Store.GetCountryCode()}");
                IAPResult localValidationResult = await TryValidateExistingOrderBeforePayAsync(request, ct);
                if (localValidationResult != null)
                {
                    return localValidationResult;
                }

                m_Hub.Store.TrackBuyInternal(request);
                LogDebug($"第三方支付准备阶段：开始确认统一支付配置，TableId={request.TableId}");
                if (!await m_Hub.Store.EnsurePaymentConfigAsync(ct))
                {
                    const string configReason = "第三方支付配置尚未就绪。";
                    m_Hub.Store.TrackCreateOrderFailInternal(request, ThirdPayCreateOrderFailureReason.PaymentConfigUnavailable, nativeErrorMessage: configReason);
                    return CreateFailure(request, IAPThirdPayErrorCode.StoreNotAvailable, configReason, ThirdPayPaymentFailureReason.PaymentConfigUnavailable);
                }

                paymentConfig = m_Hub.PaymentConfigService.Snapshot;
                if (paymentConfig == null || !paymentConfig.Enabled)
                {
                    string disabledReason = $"第三方支付服务器返回当前不可用：服务器错误码 {paymentConfig?.DisabledReason.ToString() ?? "Unknown"}。";
                    m_Hub.Store.TrackCreateOrderFailInternal(request, ThirdPayCreateOrderFailureReason.PaymentDisabledByServer, nativeErrorMessage: disabledReason);
                    return CreateFailure(request, IAPThirdPayErrorCode.StoreNotAvailable, disabledReason, ThirdPayPaymentFailureReason.PaymentDisabledByServer);
                }

                IAPProductEntry paymentEntry = m_Hub.Table?.FindByTableId(request.TableId);
                if (paymentEntry == null || paymentConfig.FindProduct(paymentEntry.ThirdProductID) == null)
                {
                    const string productReason = "第三方支付配置中不存在目标商品。";
                    m_Hub.Store.TrackCreateOrderFailInternal(request, ThirdPayCreateOrderFailureReason.ProductNotConfigured, nativeErrorMessage: productReason);
                    return CreateFailure(request, IAPThirdPayErrorCode.StoreNotAvailable, productReason, ThirdPayPaymentFailureReason.ProductNotConfigured);
                }

                if (string.IsNullOrEmpty(paymentConfig.PaymentCustomerIds))
                {
                    LogWarning("第三方支付渠道参数未就绪，将继续使用不含渠道客户号的支付 URL。");
                }

                useExternalBrowser = ShouldOpenPaymentPageExternally();
                LogDebug($"第三方支付准备阶段：支付页模式已确定，TableId={request.TableId}，ExternalBrowser={useExternalBrowser}，Country={m_Hub.Store.GetCountryCode()}");
                if (!useExternalBrowser && m_Hub.WebViewService == null)
                {
                    m_Hub.Store.TrackCreateOrderFailInternal(request, ThirdPayCreateOrderFailureReason.PaymentPageServiceUnavailable);
                    return CreateFailure(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方应用内 WebView 支付服务尚未初始化。", ThirdPayPaymentFailureReason.EmbeddedWebViewServiceUnavailable);
                }

                if (useExternalBrowser && m_Hub.ExternalBrowserService == null)
                {
                    m_Hub.Store.TrackCreateOrderFailInternal(request, ThirdPayCreateOrderFailureReason.PaymentPageServiceUnavailable);
                    return CreateFailure(request, IAPThirdPayErrorCode.StoreInitFailed, "第三方外部浏览器支付服务尚未初始化。", ThirdPayPaymentFailureReason.ExternalBrowserServiceUnavailable);
                }

                string clientOrderId = GenerateOrderId();
                order = new ThirdPayOrderRecord
                {
                    ClientOrderId = clientOrderId,
                    TableId = request.TableId,
                    UserId = userId,
                    CustomData = request.CustomData ?? string.Empty,
                    ReceiptParam = request.ReceiptParam ?? string.Empty,
                    State = ThirdPayLocalOrderState.Created,
                };

                m_Hub.PersistContext.UpsertOrder(order);
                LogDebug($"第三方支付本地订单已创建：OrderId={order.ClientOrderId}，TableId={order.TableId}，ExternalBrowser={useExternalBrowser}");

                ThirdPayGoogleAuthorization authorization;
                try
                {
                    LogDebug($"第三方支付授权和 URL 构建开始：OrderId={order.ClientOrderId}");
                    authorization = await AuthorizeAndBuildUrlAsync(order, paymentConfig, useExternalBrowser, ct);
                    LogDebug($"第三方支付授权和 URL 构建结束：OrderId={order.ClientOrderId}，Status={authorization.Status}，HasUrl={!string.IsNullOrEmpty(authorization.PaymentUrl)}，GoogleTokenLength={authorization.GoogleToken.Length}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogWarning($"第三方支付授权或 URL 构建异常：OrderId={clientOrderId}，Error={ex.Message}");
                    RemoveLocalOrderIfPaymentPageDidNotOpen(order, ThirdPayOrderDeleteReason.PaymentUrlBuildFailed);
                    m_Hub.Store.TrackCreateOrderFailInternal(request, ThirdPayCreateOrderFailureReason.PaymentUrlBuildException, clientOrderId, nativeErrorMessage: ex.Message);
                    return CreateFailure(request, IAPThirdPayErrorCode.StoreInitFailed, $"构造支付 URL 失败：{ex.Message}", ThirdPayPaymentFailureReason.PaymentUrlBuildFailed, clientOrderId);
                }

                if (authorization.Status != ThirdPayGoogleAuthorizationStatus.Authorized || string.IsNullOrEmpty(authorization.PaymentUrl))
                {
                    (IAPThirdPayErrorCode code, string reason) = MapGoogleAuthorizationFailure(authorization.Status);
                    LogWarning($"第三方支付授权未通过：OrderId={clientOrderId}，Status={authorization.Status}，HasUrl={!string.IsNullOrEmpty(authorization.PaymentUrl)}，Reason={reason}");
                    ThirdPayCreateOrderFailureReason createFailureReason = MapGoogleAuthorizationTrackReason(authorization.Status);
                    ThirdPayOrderDeleteReason deleteReason = authorization.Status == ThirdPayGoogleAuthorizationStatus.UrlBuildFailed || string.IsNullOrEmpty(authorization.PaymentUrl) ? ThirdPayOrderDeleteReason.PaymentUrlBuildFailed : ThirdPayOrderDeleteReason.AuthorizationFailed;
                    RemoveLocalOrderIfPaymentPageDidNotOpen(order, deleteReason);
                    m_Hub.Store.TrackCreateOrderFailInternal(request, createFailureReason, clientOrderId, nativeErrorMessage: reason);
                    ThirdPayPaymentFailureReason paymentFailureReason = MapGoogleAuthorizationPaymentFailureReason(authorization.Status, authorization.PaymentUrl);
                    return CreateFailure(request, code, reason, paymentFailureReason, clientOrderId);
                }

                paymentUrl = authorization.PaymentUrl;
                googleToken = authorization.GoogleToken;
                m_Hub.Store.TrackCreateOrderSuccessInternal(order);
                LogDebug($"第三方支付支付页已构建：OrderId={order.ClientOrderId}，TableId={order.TableId}，ExternalBrowser={useExternalBrowser}");
            }
            finally
            {
                m_Hub.Store.SubWaitingRef();
            }

            if (useExternalBrowser)
            {
                return await m_Hub.Store.OpenExternalBrowserPaymentAsync(request, order, paymentConfig, googleToken, paymentUrl, ct);
            }

            ThirdPayPaymentPageResult pageResult;
            try
            {
                LogDebug($"第三方支付准备打开 WebView：OrderId={order.ClientOrderId}");
                pageResult = await m_Hub.WebViewService.OpenWithResultAsync(paymentUrl, order.ClientOrderId, ct);
                LogDebug($"第三方支付 WebView 返回：OrderId={order.ClientOrderId}，Result={pageResult.Result}，Mode={pageResult.OpenMode}，CallbackStatus={(int)pageResult.CallbackStatus}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogWarning($"第三方支付 WebView 打开异常：OrderId={order.ClientOrderId}，Error={ex.Message}");
                RemoveLocalOrderIfPaymentPageDidNotOpen(order, ThirdPayOrderDeleteReason.PaymentPageNeverOpened);
                ThirdPayPaymentPageResult exceptionResult = ThirdPayPaymentPageResult.Completed(ThirdPayOpenResult.Failed, ThirdPayExternalBrowserLaunchMode.EmbeddedWebView, ThirdPayWebViewCallbackStatus.Unknown, ThirdPayCloseReason.PaymentPageException, ThirdPayPaymentFailureReason.PaymentPageOpenException, 0, ex.Message, false);
                return CreateFailure(request, IAPThirdPayErrorCode.WebViewClosed, $"打开支付页异常：{ex.Message}", ThirdPayPaymentFailureReason.PaymentPageOpenException, order.ClientOrderId, exceptionResult);
            }

            ThirdPayValidationScene validationScene;
            if (pageResult.HasSuccessfulPayCallback)
            {
                m_Hub.Store.TrackLocalPaySuccessInternal(order, false, pageResult);
                validationScene = ThirdPayValidationScene.DirectPay;
                LogDebug($"第三方支付 WebView 收到成功 pay_callback，开始验单：OrderId={order.ClientOrderId}，Status={(int)pageResult.CallbackStatus}");
            }
            else if (pageResult.IsSessionEstablished)
            {
                m_Hub.Store.TrackThirdPayCloseOrderInternal(order, pageResult);
                validationScene = ThirdPayValidationScene.DirectPayAmbiguousReturn;
                LogDebug($"第三方支付 WebView 未收到成功 pay_callback，按关闭场景开始验单：OrderId={order.ClientOrderId}，CloseReason={pageResult.CloseReason}");
            }
            else
            {
                LogWarning($"第三方支付 WebView 未建立支付会话：OrderId={order.ClientOrderId}，FailureReason={pageResult.FailureReason}");
                RemoveLocalOrderIfPaymentPageDidNotOpen(order, ThirdPayOrderDeleteReason.PaymentPageNeverOpened);
                return CreateFailure(request, IAPThirdPayErrorCode.WebViewClosed, ThirdPayTrackReasonDescriptions.GetPaymentFailureDetail(pageResult.FailureReason), pageResult.FailureReason, order.ClientOrderId, pageResult);
            }

            m_Hub.Store.AddWaitingRef();
            try
            {
                return await m_Hub.Store.ValidateOrderAsync(order, validationScene, ct);
            }
            finally
            {
                m_Hub.Store.SubWaitingRef();
            }
        }

        /// <summary>
        /// 使用固定支付配置快照与动态 AES 构造支付 URL。
        /// </summary>
        /// <param name="order">当前本地订单。</param>
        /// <param name="paymentConfig">本次支付固定使用的配置快照。</param>
        /// <param name="googleToken">Google 外链上报令牌；无令牌时为空。</param>
        /// <param name="isExternalBrowser">是否使用外部浏览器打开。</param>
        /// <param name="showBackButton">是否要求支付页显示返回按钮。</param>
        /// <param name="isCustomTab">是否由 Auth Tab 或 Custom Tabs 承载。</param>
        /// <returns>包含加密支付参数的完整支付 URL。</returns>
        internal string BuildPaymentUrl(ThirdPayOrderRecord order, ThirdPayPaymentConfigSnapshot paymentConfig, string googleToken, bool isExternalBrowser, bool showBackButton = false, bool isCustomTab = false)
        {
            if (paymentConfig == null || !paymentConfig.Enabled || string.IsNullOrEmpty(paymentConfig.PaymentPageUrl))
            {
                throw new InvalidOperationException("第三方支付配置快照不可用。");
            }

            IAPProductEntry productEntry = m_Hub.Table?.FindByTableId(order.TableId);
            if (productEntry == null)
            {
                throw new InvalidOperationException($"支付商品表中不存在 TableId={order.TableId} 的条目。");
            }

            PbNetThirdProductInfo productInfo = paymentConfig.FindProduct(productEntry.ThirdProductID);
            if (productInfo == null)
            {
                throw new InvalidOperationException($"第三方商品列表中不存在 TableId={order.TableId} 对应的商品。");
            }

            if (productInfo.Id <= 0 || string.IsNullOrEmpty(productInfo.LocalCurrency) || string.IsNullOrEmpty(productInfo.LocalPrice) || string.IsNullOrEmpty(productEntry.Name))
            {
                throw new InvalidOperationException($"TableId={order.TableId} 的第三方商品 ID、货币、价格或名称不完整。");
            }

            var builder = new ThirdPayUrlBuilder(ThirdPayDynamicAesEncryptor.EncodeToBase64);
            PbNetReqHeader header = NetBuilder.BuildHeader();
            if (header.Appid <= 0)
            {
                throw new InvalidOperationException("公共请求头 AppId 未配置或不是有效正整数。");
            }

            var payload = new ThirdPayUrlPayload
            {
                ProductId = productInfo.Id,
                UserId = order.UserId,
                TableId = order.TableId,
                Currency = productInfo.LocalCurrency,
                Price = productInfo.LocalPrice,
                ProductName = productEntry.Name,
                CountryCode = paymentConfig.CountryCode,
                ClientOrderId = order.ClientOrderId,
                Platform = header.Platform.ToString(),
                Channel = header.Channel.ToString(),
                AppId = header.Appid,
                ChannelParams = paymentConfig.PaymentCustomerIds,
                GoogleToken = googleToken,
                ReceiptParam = order.ReceiptParam,
                IsExternalBrowser = isExternalBrowser,
                IsCustomTab = isCustomTab,
                ShowBackButton = showBackButton,
            };
            return builder.Build(paymentConfig.PaymentPageUrl, header.Language, payload);
        }

        /// <summary>
        /// 支付页明确未打开成功时清理本地订单。
        /// </summary>
        /// <param name="order">需要清理的本地订单。</param>
        /// <param name="deleteReason">本次清理对应的稳定删除原因。</param>
        internal void RemoveLocalOrderIfPaymentPageDidNotOpen(ThirdPayOrderRecord order, ThirdPayOrderDeleteReason deleteReason = ThirdPayOrderDeleteReason.PaymentPageNeverOpened)
        {
            if (order == null || string.IsNullOrEmpty(order.ClientOrderId))
            {
                return;
            }

            var context = new ThirdPayValidationTrackContext(default, 0, ThirdPayClientOrderStatus.Unknown, string.Empty, ThirdPayPaymentFailureReason.Unknown);
            if (m_Hub.Store.RemoveLocalOrderAndTrack(order, false, 0, context, deleteReason, true))
            {
                LogDebug($"第三方支付页未打开成功，本地订单已清理：OrderId={order.ClientOrderId}，OrderKey={ThirdPayOrderKey.Build(order)}");
            }
        }

        /// <summary>
        /// 构造并发布第三方支付失败结果。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="code">第三方支付错误码。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>已发布的失败结果。</returns>
        internal IAPResult CreateFailure(IAPThirdPayRequest request, IAPThirdPayErrorCode code, string reason)
        {
            var result = new IAPResult(request.TableId, (int)code, IAPErrorSource.ThirdPay, reason, request.CustomData, request.ReceiptParam);
            m_Hub.Context?.EventBridge?.RaisePayFailed(result);
            return result;
        }

        /// <summary>
        /// 构造并发布第三方支付失败结果，同时保留 PayAsync 返回边界所需的精确埋点上下文。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="code">对外兼容的 ThirdPay 粗粒度错误码。</param>
        /// <param name="reason">业务失败描述。</param>
        /// <param name="trackReason">写入专属属性的细分失败原因。</param>
        /// <param name="clientOrderId">已经生成的客户端订单号；尚未建单时为空。</param>
        /// <param name="pageResult">支付页结构化结果；尚未打开支付页时为空。</param>
        /// <returns>已发布且绑定精确失败埋点上下文的结果。</returns>
        private IAPResult CreateFailure(IAPThirdPayRequest request, IAPThirdPayErrorCode code, string reason, ThirdPayPaymentFailureReason trackReason, string clientOrderId = "", ThirdPayPaymentPageResult? pageResult = null)
        {
            IAPResult result = CreateFailure(request, code, reason);
            return m_Hub.Store.AttachReturnedPayFailureContext(result, trackReason, clientOrderId, pageResult);
        }

        /// <summary>
        /// 发起新支付前复用相同业务键的本地未完成订单并直接验单。
        /// </summary>
        /// <param name="request">当前第三方支付请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>命中本地订单时返回验单结果；未命中时返回 null。</returns>
        private async UniTask<IAPResult> TryValidateExistingOrderBeforePayAsync(IAPThirdPayRequest request, CancellationToken ct)
        {
            if (!m_Hub.PersistContext.TryFindOrderByKey(request.TableId, request.ReceiptParam, out ThirdPayOrderRecord order))
            {
                return null;
            }

            RefreshOrderContextForRequest(order, request);
            m_Hub.PersistContext.UpsertOrder(order);
            LogDebug($"第三方支付命中本地未完成订单，直接验单：OrderId={order.ClientOrderId}，OrderKey={ThirdPayOrderKey.Build(order)}");
            return await m_Hub.Store.ValidateOrderAsync(order, ThirdPayValidationScene.DirectPayPreflight, ct);
        }

        /// <summary>
        /// 用当前支付请求补齐旧本地订单缺失的业务上下文。
        /// </summary>
        /// <param name="order">待补齐的本地订单。</param>
        /// <param name="request">当前第三方支付请求。</param>
        private void RefreshOrderContextForRequest(ThirdPayOrderRecord order, IAPThirdPayRequest request)
        {
            if (order == null || request == null)
            {
                return;
            }

            order.TableId = request.TableId;
            if (string.IsNullOrEmpty(order.UserId))
            {
                order.UserId = m_Hub.Store.CurrentUserId;
            }

            if (string.IsNullOrEmpty(order.CustomData))
            {
                order.CustomData = request.CustomData ?? string.Empty;
            }

            if (string.IsNullOrEmpty(order.ReceiptParam))
            {
                order.ReceiptParam = request.ReceiptParam ?? string.Empty;
            }

            if (string.IsNullOrEmpty(order.State))
            {
                order.State = ThirdPayLocalOrderState.PendingValidation;
            }
        }

        /// <summary>
        /// 完成 Android Google 外链政策流程，并构造最终支付 URL。
        /// </summary>
        /// <param name="order">当前本地订单。</param>
        /// <param name="paymentConfig">本次支付固定使用的配置快照。</param>
        /// <param name="isExternalBrowser">是否使用外部浏览器打开。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>Google 授权状态、令牌与支付 URL。</returns>
        private async UniTask<ThirdPayGoogleAuthorization> AuthorizeAndBuildUrlAsync(ThirdPayOrderRecord order, ThirdPayPaymentConfigSnapshot paymentConfig, bool isExternalBrowser, CancellationToken ct)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            ThirdPayGooglePolicyService googlePolicy = m_Hub.GooglePolicy;
            bool skipInformationScreen = m_Hub.SkipPaymentInformationScreen;
            LogDebug($"第三方支付 Android Google 授权开始：OrderId={order.ClientOrderId}，ExternalBrowser={isExternalBrowser}，HasGooglePolicy={googlePolicy != null}，SkipInfo={skipInformationScreen}");
            if (googlePolicy == null)
            {
                LogWarning($"第三方支付 Android Google 授权服务为空，使用无 Google token 支付 URL 兜底：OrderId={order.ClientOrderId}");
                return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.Authorized, string.Empty, BuildPaymentUrl(order, paymentConfig, string.Empty, isExternalBrowser, isCustomTab: isExternalBrowser));
            }

            ThirdPayGoogleAuthorization authorization = await googlePolicy.AuthorizeAsync(token => BuildPaymentUrl(order, paymentConfig, token, isExternalBrowser, isCustomTab: isExternalBrowser), skipInformationScreen, ct);
            LogDebug($"第三方支付 Android Google 授权结果：OrderId={order.ClientOrderId}，Status={authorization.Status}，HasUrl={!string.IsNullOrEmpty(authorization.PaymentUrl)}");
            return authorization;
#else
            LogDebug($"第三方支付非 Android 环境直接构建 URL：OrderId={order.ClientOrderId}，ExternalBrowser={isExternalBrowser}");
            await UniTask.CompletedTask;
            return new ThirdPayGoogleAuthorization(ThirdPayGoogleAuthorizationStatus.Authorized, string.Empty, BuildPaymentUrl(order, paymentConfig, string.Empty, isExternalBrowser, isCustomTab: false));
#endif
        }

        /// <summary>
        /// 将 Google 外链授权失败状态映射为第三方支付错误码与失败原因。
        /// </summary>
        /// <param name="status">Google 外链授权状态。</param>
        /// <returns>对应的第三方支付错误码与失败原因。</returns>
        private static (IAPThirdPayErrorCode Code, string Reason) MapGoogleAuthorizationFailure(ThirdPayGoogleAuthorizationStatus status)
        {
            switch (status)
            {
                case ThirdPayGoogleAuthorizationStatus.ConnectionFailed:
                    return (IAPThirdPayErrorCode.BillingNotReady, "无法连接 Google 外链结算服务。");
                case ThirdPayGoogleAuthorizationStatus.TokenCreationFailed:
                    return (IAPThirdPayErrorCode.BillingNotReady, "创建 Google 外链上报 token 失败。");
                case ThirdPayGoogleAuthorizationStatus.UrlBuildFailed:
                    return (IAPThirdPayErrorCode.StoreInitFailed, "构造第三方支付 URL 失败。");
                case ThirdPayGoogleAuthorizationStatus.UserCancelled:
                    return (IAPThirdPayErrorCode.UserCancelled, "用户在 Google 外链信息页取消支付。");
                default:
                    return (IAPThirdPayErrorCode.BillingNotReady, "Google 外链政策校验未通过。");
            }
        }

        /// <summary>
        /// 将 Google 外链授权结果映射为稳定的创建订单失败原因。
        /// </summary>
        /// <param name="status">Google 外链授权状态。</param>
        /// <returns>对应的创建订单失败枚举。</returns>
        private static ThirdPayCreateOrderFailureReason MapGoogleAuthorizationTrackReason(ThirdPayGoogleAuthorizationStatus status)
        {
            switch (status)
            {
                case ThirdPayGoogleAuthorizationStatus.ConnectionFailed:
                    return ThirdPayCreateOrderFailureReason.GoogleAuthorizationConnectionFailed;
                case ThirdPayGoogleAuthorizationStatus.TokenCreationFailed:
                    return ThirdPayCreateOrderFailureReason.GoogleTokenCreationFailed;
                case ThirdPayGoogleAuthorizationStatus.UrlBuildFailed:
                    return ThirdPayCreateOrderFailureReason.GooglePaymentUrlBuildFailed;
                case ThirdPayGoogleAuthorizationStatus.UserCancelled:
                    return ThirdPayCreateOrderFailureReason.GoogleUserCancelled;
                default:
                    return ThirdPayCreateOrderFailureReason.Unknown;
            }
        }

        /// <summary>
        /// 将 Google 外链授权结果映射为 PayAsync 失败事件使用的细分原因。
        /// </summary>
        /// <param name="status">Google 外链授权状态。</param>
        /// <param name="paymentUrl">授权流程返回的支付 URL。</param>
        /// <returns>对应的支付失败枚举。</returns>
        private static ThirdPayPaymentFailureReason MapGoogleAuthorizationPaymentFailureReason(ThirdPayGoogleAuthorizationStatus status, string paymentUrl)
        {
            if (status == ThirdPayGoogleAuthorizationStatus.Authorized && string.IsNullOrEmpty(paymentUrl))
            {
                return ThirdPayPaymentFailureReason.PaymentUrlMissing;
            }

            switch (status)
            {
                case ThirdPayGoogleAuthorizationStatus.ConnectionFailed:
                    return ThirdPayPaymentFailureReason.GoogleAuthorizationConnectionFailed;
                case ThirdPayGoogleAuthorizationStatus.TokenCreationFailed:
                    return ThirdPayPaymentFailureReason.GoogleTokenCreationFailed;
                case ThirdPayGoogleAuthorizationStatus.UrlBuildFailed:
                    return ThirdPayPaymentFailureReason.GooglePaymentUrlBuildFailed;
                case ThirdPayGoogleAuthorizationStatus.UserCancelled:
                    return ThirdPayPaymentFailureReason.GoogleUserCancelled;
                default:
                    return ThirdPayPaymentFailureReason.UnexpectedException;
            }
        }

        /// <summary>
        /// 按当前平台固定支付页打开方式。
        /// </summary>
        /// <returns>Android 真机返回 true，其他环境返回 false。</returns>
        private static bool ShouldOpenPaymentPageExternally()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// 生成当前客户端唯一的第三方支付订单号。
        /// </summary>
        /// <returns>UTC 毫秒时间戳与 GUID 片段组成的订单号。</returns>
        private static string GenerateOrderId()
        {
            return DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }
}
