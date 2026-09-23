/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayTrackTypes.cs
 * author:    yingzheng
 * created:   2026/9/20
 * descrip:   ThirdPay 支付埋点稳定枚举与原因说明
 ***************************************************************/

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付页的实际关闭原因；数值会写入 nova_third_pay_close_reason，新增项只能追加。
    /// </summary>
    internal enum ThirdPayCloseReason
    {
        /// <summary>
        /// 未知或尚未确定关闭原因。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 支付页通过 close_callback 主动关闭。
        /// </summary>
        CloseCallback = 1,

        /// <summary>
        /// 嵌入式 UniWebView 被用户关闭。
        /// </summary>
        EmbeddedWebViewUserClosed = 2,

        /// <summary>
        /// iOS Safe Browsing 被用户关闭。
        /// </summary>
        IOSSafeBrowsingDismissed = 3,

        /// <summary>
        /// pay_callback 明确返回支付失败状态。
        /// </summary>
        PayCallbackFailed = 4,

        /// <summary>
        /// pay_callback 返回未定义的支付状态。
        /// </summary>
        PayCallbackUnknownStatus = 5,

        /// <summary>
        /// pay_callback 缺少订单号、状态或包含无法解析的参数。
        /// </summary>
        PayCallbackInvalidPayload = 6,

        /// <summary>
        /// 外部浏览器返回应用后未收到成功 pay_callback。
        /// </summary>
        ExternalBrowserReturnedWithoutCallback = 7,

        /// <summary>
        /// 外部浏览器回调携带的订单号不匹配当前会话。
        /// </summary>
        ExternalBrowserCallbackOrderMismatch = 8,

        /// <summary>
        /// 外部浏览器回调到达时当前会话已经开始验单。
        /// </summary>
        ExternalBrowserCallbackIgnoredAfterValidationStarted = 9,

        /// <summary>
        /// 支付页加载失败。
        /// </summary>
        PaymentPageLoadingError = 10,

        /// <summary>
        /// 支付页 Web 内容进程终止。
        /// </summary>
        WebContentProcessTerminated = 11,

        /// <summary>
        /// 支付页会话发生未分类异常。
        /// </summary>
        PaymentPageException = 12,

        /// <summary>
        /// 支付操作被取消。
        /// </summary>
        OperationCancelled = 13,

        /// <summary>
        /// 当前支付会话被后续支付会话替换。
        /// </summary>
        SessionReplaced = 14,

        /// <summary>
        /// Store 释放导致支付会话结束。
        /// </summary>
        StoreDisposed = 15,
    }

    /// <summary>
    /// ThirdPay 支付流程失败原因；数值会写入 nova_third_pay_failure_reason，新增项只能追加。
    /// </summary>
    internal enum ThirdPayPaymentFailureReason
    {
        /// <summary>
        /// 未知失败原因。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Store 被禁用。
        /// </summary>
        StoreDisabled = 1,

        /// <summary>
        /// Store 尚未初始化完成。
        /// </summary>
        StoreNotInitialized = 2,

        /// <summary>
        /// 已有支付流程进行中。
        /// </summary>
        AlreadyPurchasing = 3,

        /// <summary>
        /// 商品表中不存在目标商品。
        /// </summary>
        ProductNotFound = 4,

        /// <summary>
        /// 用户 UID 为空。
        /// </summary>
        UserIdMissing = 5,

        /// <summary>
        /// 统一支付配置不可用。
        /// </summary>
        PaymentConfigUnavailable = 6,

        /// <summary>
        /// 服务端关闭当前第三方支付能力。
        /// </summary>
        PaymentDisabledByServer = 7,

        /// <summary>
        /// 统一支付配置中不存在目标商品。
        /// </summary>
        ProductNotConfigured = 8,

        /// <summary>
        /// 嵌入式支付页服务不可用。
        /// </summary>
        EmbeddedWebViewServiceUnavailable = 9,

        /// <summary>
        /// 外部浏览器支付页服务不可用。
        /// </summary>
        ExternalBrowserServiceUnavailable = 10,

        /// <summary>
        /// Google 外链服务连接失败。
        /// </summary>
        GoogleAuthorizationConnectionFailed = 11,

        /// <summary>
        /// Google 外链令牌创建失败。
        /// </summary>
        GoogleTokenCreationFailed = 12,

        /// <summary>
        /// Google 外链支付 URL 构建失败。
        /// </summary>
        GooglePaymentUrlBuildFailed = 13,

        /// <summary>
        /// 用户在 Google 信息页取消。
        /// </summary>
        GoogleUserCancelled = 14,

        /// <summary>
        /// 支付页打开失败。
        /// </summary>
        PaymentPageOpenFailed = 15,

        /// <summary>
        /// 支付页打开过程抛出异常。
        /// </summary>
        PaymentPageOpenException = 16,

        /// <summary>
        /// Application.OpenURL 回退地址为空。
        /// </summary>
        SystemBrowserFallbackUrlEmpty = 17,

        /// <summary>
        /// pay_callback 明确返回支付失败。
        /// </summary>
        PaymentCallbackFailed = 18,

        /// <summary>
        /// pay_callback 参数无效。
        /// </summary>
        PaymentCallbackInvalidPayload = 19,

        /// <summary>
        /// pay_callback 返回未知状态。
        /// </summary>
        PaymentCallbackUnknownStatus = 20,

        /// <summary>
        /// pay_callback 的订单号与活动会话不一致。
        /// </summary>
        CallbackOrderMismatch = 21,

        /// <summary>
        /// pay_callback 到达时已进入验单。
        /// </summary>
        CallbackIgnoredAfterValidationStarted = 22,

        /// <summary>
        /// 外部浏览器返回后没有收到成功 pay_callback。
        /// </summary>
        ExternalBrowserReturnedWithoutCallback = 23,

        /// <summary>
        /// 验单服务不可用。
        /// </summary>
        ValidationServiceUnavailable = 24,

        /// <summary>
        /// 验单网络请求失败。
        /// </summary>
        ValidationNetworkError = 25,

        /// <summary>
        /// 验单响应没有目标订单。
        /// </summary>
        ValidationResponseOrderMissing = 26,

        /// <summary>
        /// 服务端订单仍待支付。
        /// </summary>
        ServerOrderPendingPayment = 27,

        /// <summary>
        /// 服务端订单仍在处理中。
        /// </summary>
        ServerOrderProcessing = 28,

        /// <summary>
        /// 服务端订单支付失败或已过期。
        /// </summary>
        ServerOrderFailedOrExpired = 29,

        /// <summary>
        /// 服务端订单不存在。
        /// </summary>
        ServerOrderNotFound = 30,

        /// <summary>
        /// 服务端返回无法识别的订单状态。
        /// </summary>
        UnknownServerOrderStatus = 31,

        /// <summary>
        /// 支付操作被取消。
        /// </summary>
        OperationCancelled = 32,

        /// <summary>
        /// 支付会话被新会话替换。
        /// </summary>
        SessionReplaced = 33,

        /// <summary>
        /// Store 被释放。
        /// </summary>
        StoreDisposed = 34,

        /// <summary>
        /// 支付流程发生未分类异常。
        /// </summary>
        UnexpectedException = 35,

        /// <summary>
        /// 构建第三方支付 URL 失败。
        /// </summary>
        PaymentUrlBuildFailed = 36,

        /// <summary>
        /// 支付授权完成但未取得可打开的支付 URL。
        /// </summary>
        PaymentUrlMissing = 37,
    }

    /// <summary>
    /// ThirdPay 创建本地订单、授权或支付 URL 的失败原因；数值会写入 nova_third_pay_create_failure_reason，新增项只能追加。
    /// </summary>
    internal enum ThirdPayCreateOrderFailureReason
    {
        /// <summary>
        /// 未知创建订单失败原因。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 用户 UID 为空。
        /// </summary>
        UserIdMissing = 1,

        /// <summary>
        /// 统一支付配置不可用。
        /// </summary>
        PaymentConfigUnavailable = 2,

        /// <summary>
        /// 服务端关闭当前第三方支付能力。
        /// </summary>
        PaymentDisabledByServer = 3,

        /// <summary>
        /// 统一支付配置中不存在目标商品。
        /// </summary>
        ProductNotConfigured = 4,

        /// <summary>
        /// 支付页服务不可用。
        /// </summary>
        PaymentPageServiceUnavailable = 5,

        /// <summary>
        /// Google 外链服务连接失败。
        /// </summary>
        GoogleAuthorizationConnectionFailed = 6,

        /// <summary>
        /// Google 外链令牌创建失败。
        /// </summary>
        GoogleTokenCreationFailed = 7,

        /// <summary>
        /// Google 外链支付 URL 构建失败。
        /// </summary>
        GooglePaymentUrlBuildFailed = 8,

        /// <summary>
        /// 用户在 Google 信息页取消。
        /// </summary>
        GoogleUserCancelled = 9,

        /// <summary>
        /// 支付 URL 构建抛出异常。
        /// </summary>
        PaymentUrlBuildException = 10,

        /// <summary>
        /// 授权成功但未返回支付 URL。
        /// </summary>
        PaymentUrlEmpty = 11,
    }

    /// <summary>
    /// 客户端归一化的 ThirdPay 订单状态；数值会写入 nova_client_order_status，新增项只能追加。
    /// </summary>
    internal enum ThirdPayClientOrderStatus
    {
        /// <summary>
        /// 尚未取得可归类的订单状态。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 支付成功且允许业务发货。
        /// </summary>
        SuccessDeliverable = 1,

        /// <summary>
        /// 已发货，不允许重复发货。
        /// </summary>
        SuccessAlreadyDelivered = 2,

        /// <summary>
        /// 服务端确认订单仍待支付。
        /// </summary>
        PaymentPending = 3,

        /// <summary>
        /// 服务端确认订单仍在处理。
        /// </summary>
        Processing = 4,

        /// <summary>
        /// 服务端确认订单失败或过期。
        /// </summary>
        FailedOrExpired = 5,

        /// <summary>
        /// 服务端确认订单不存在。
        /// </summary>
        NotFound = 6,

        /// <summary>
        /// 验单响应未返回目标订单。
        /// </summary>
        ResponseOrderMissing = 7,

        /// <summary>
        /// 验单请求发生网络错误。
        /// </summary>
        NetworkFailure = 8,

        /// <summary>
        /// 验单服务不可用。
        /// </summary>
        ValidationServiceUnavailable = 9,

        /// <summary>
        /// 服务端返回未知订单状态。
        /// </summary>
        UnknownServerStatus = 10,
    }

    /// <summary>
    /// ThirdPay 本地订单删除原因；数值会写入 nova_order_delete_reason，新增项只能追加。
    /// </summary>
    internal enum ThirdPayOrderDeleteReason
    {
        /// <summary>
        /// 未知删除原因。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 服务端确认支付成功。
        /// </summary>
        Paid = 1,

        /// <summary>
        /// 服务端确认订单已经发货。
        /// </summary>
        AlreadyDelivered = 2,

        /// <summary>
        /// 待支付状态达到当前场景验单上限。
        /// </summary>
        PendingPaymentExhausted = 3,

        /// <summary>
        /// 服务端确认订单失败或过期。
        /// </summary>
        FailedOrExpired = 4,

        /// <summary>
        /// 服务端确认订单不存在。
        /// </summary>
        NotFound = 5,

        /// <summary>
        /// 支付页未成功打开。
        /// </summary>
        PaymentPageNeverOpened = 6,

        /// <summary>
        /// Google 授权失败或用户取消授权。
        /// </summary>
        AuthorizationFailed = 7,

        /// <summary>
        /// 支付 URL 构建失败。
        /// </summary>
        PaymentUrlBuildFailed = 8,

        /// <summary>
        /// 用户或账号切换后清理旧订单。
        /// </summary>
        UserOrAccountChanged = 9,

        /// <summary>
        /// Store 释放或显式清理订单。
        /// </summary>
        ExplicitCleanup = 10,

        /// <summary>
        /// 本地订单数据非法。
        /// </summary>
        LocalOrderInvalid = 11,
    }

    /// <summary>
    /// 支付回调解析结果，区分非支付 Deep Link、无效参数与未知状态。
    /// </summary>
    internal enum ThirdPayCallbackResolveResult
    {
        /// <summary>
        /// 当前 Deep Link 不是支付回调。
        /// </summary>
        NotPaymentCallback = 0,

        /// <summary>
        /// 已解析到字段完整的支付或关闭回调。
        /// </summary>
        Valid = 1,

        /// <summary>
        /// 回调路径正确但参数缺失或无法解析。
        /// </summary>
        InvalidPayload = 2,

        /// <summary>
        /// 回调路径正确但状态码不受支持。
        /// </summary>
        UnknownStatus = 3,
    }

    /// <summary>
    /// 单次 ThirdPay 验单埋点需要携带的服务端、客户端状态和失败上下文。
    /// </summary>
    internal readonly struct ThirdPayValidationTrackContext
    {
        /// <summary>
        /// 创建验单埋点上下文。
        /// </summary>
        /// <param name="scene">验单触发场景。</param>
        /// <param name="serverOrderStatus">服务端原始订单状态；无有效响应时为 0。</param>
        /// <param name="clientOrderStatus">客户端归一化订单状态。</param>
        /// <param name="serverOrderId">服务端订单号。</param>
        /// <param name="failureReason">当前验单失败原因。</param>
        /// <param name="protocolCode">本次验单协议错误码；成功响应或无协议响应时为 0。</param>
        /// <param name="protocolMessage">本次验单协议错误描述；无错误时为空。</param>
        public ThirdPayValidationTrackContext(ThirdPayValidationScene scene, int serverOrderStatus, ThirdPayClientOrderStatus clientOrderStatus, string serverOrderId, ThirdPayPaymentFailureReason failureReason, int protocolCode = 0, string protocolMessage = "")
        {
            Scene = scene;
            ServerOrderStatus = serverOrderStatus;
            ClientOrderStatus = clientOrderStatus;
            ServerOrderId = serverOrderId ?? string.Empty;
            FailureReason = failureReason;
            ProtocolCode = protocolCode;
            ProtocolMessage = protocolMessage ?? string.Empty;
        }

        /// <summary>
        /// 验单触发场景。
        /// </summary>
        public ThirdPayValidationScene Scene { get; }

        /// <summary>
        /// 服务端原始订单状态；无有效响应时为 0。
        /// </summary>
        public int ServerOrderStatus { get; }

        /// <summary>
        /// 客户端归一化订单状态。
        /// </summary>
        public ThirdPayClientOrderStatus ClientOrderStatus { get; }

        /// <summary>
        /// 服务端订单号。
        /// </summary>
        public string ServerOrderId { get; }

        /// <summary>
        /// 当前验单失败原因。
        /// </summary>
        public ThirdPayPaymentFailureReason FailureReason { get; }

        /// <summary>
        /// 本次验单协议错误码；成功响应或无协议响应时为 0。
        /// </summary>
        public int ProtocolCode { get; }

        /// <summary>
        /// 本次验单协议错误描述；无错误时为空。
        /// </summary>
        public string ProtocolMessage { get; }
    }

    /// <summary>
    /// 统一维护 ThirdPay 失败和关闭枚举对应的稳定中文描述，供 nova_reason_detail 使用。
    /// </summary>
    internal static class ThirdPayTrackReasonDescriptions
    {
        /// <summary>
        /// 获取支付页关闭原因的稳定中文描述。
        /// </summary>
        /// <param name="reason">支付页关闭原因。</param>
        /// <returns>与枚举值一一对应的中文描述。</returns>
        internal static string GetCloseReasonDetail(ThirdPayCloseReason reason)
        {
            switch (reason)
            {
                case ThirdPayCloseReason.CloseCallback: return "支付页通过 close_callback 关闭。";
                case ThirdPayCloseReason.EmbeddedWebViewUserClosed: return "用户关闭了嵌入式第三方支付页。";
                case ThirdPayCloseReason.IOSSafeBrowsingDismissed: return "用户关闭了 iOS Safe Browsing 第三方支付页。";
                case ThirdPayCloseReason.PayCallbackFailed: return "pay_callback 明确返回支付失败状态。";
                case ThirdPayCloseReason.PayCallbackUnknownStatus: return "pay_callback 返回了未定义的支付状态。";
                case ThirdPayCloseReason.PayCallbackInvalidPayload: return "pay_callback 缺少订单号、状态或参数无法解析。";
                case ThirdPayCloseReason.ExternalBrowserReturnedWithoutCallback: return "外部浏览器返回应用后未收到成功 pay_callback。";
                case ThirdPayCloseReason.ExternalBrowserCallbackOrderMismatch: return "外部浏览器 pay_callback 的订单号与当前会话不匹配。";
                case ThirdPayCloseReason.ExternalBrowserCallbackIgnoredAfterValidationStarted: return "外部浏览器 pay_callback 到达时当前会话已经开始验单。";
                case ThirdPayCloseReason.PaymentPageLoadingError: return "第三方支付页加载失败。";
                case ThirdPayCloseReason.WebContentProcessTerminated: return "第三方支付页 Web 内容进程终止。";
                case ThirdPayCloseReason.PaymentPageException: return "第三方支付页会话发生异常。";
                case ThirdPayCloseReason.OperationCancelled: return "第三方支付操作被取消。";
                case ThirdPayCloseReason.SessionReplaced: return "当前第三方支付会话被后续会话替换。";
                case ThirdPayCloseReason.StoreDisposed: return "ThirdPay Store 释放导致支付会话结束。";
                default: return "第三方支付页以未知原因结束。";
            }
        }

        /// <summary>
        /// 获取支付流程失败原因的稳定中文描述。
        /// </summary>
        /// <param name="reason">支付流程失败原因。</param>
        /// <returns>与枚举值一一对应的中文描述。</returns>
        internal static string GetPaymentFailureDetail(ThirdPayPaymentFailureReason reason)
        {
            switch (reason)
            {
                case ThirdPayPaymentFailureReason.StoreDisabled: return "ThirdPay Store 已被禁用。";
                case ThirdPayPaymentFailureReason.StoreNotInitialized: return "ThirdPay Store 尚未初始化完成。";
                case ThirdPayPaymentFailureReason.AlreadyPurchasing: return "当前已有第三方支付流程进行中。";
                case ThirdPayPaymentFailureReason.ProductNotFound: return "支付商品表中不存在目标商品。";
                case ThirdPayPaymentFailureReason.UserIdMissing: return "当前用户 UID 为空。";
                case ThirdPayPaymentFailureReason.PaymentConfigUnavailable: return "未取得可用的第三方支付配置。";
                case ThirdPayPaymentFailureReason.PaymentDisabledByServer: return "服务端关闭了当前第三方支付能力。";
                case ThirdPayPaymentFailureReason.ProductNotConfigured: return "第三方支付配置中不存在目标商品。";
                case ThirdPayPaymentFailureReason.EmbeddedWebViewServiceUnavailable: return "嵌入式第三方支付页服务不可用。";
                case ThirdPayPaymentFailureReason.ExternalBrowserServiceUnavailable: return "外部浏览器第三方支付页服务不可用。";
                case ThirdPayPaymentFailureReason.GoogleAuthorizationConnectionFailed: return "无法连接 Google 外链结算服务。";
                case ThirdPayPaymentFailureReason.GoogleTokenCreationFailed: return "创建 Google 外链上报令牌失败。";
                case ThirdPayPaymentFailureReason.GooglePaymentUrlBuildFailed: return "Google 外链支付 URL 构建失败。";
                case ThirdPayPaymentFailureReason.GoogleUserCancelled: return "用户在 Google 外链信息页取消支付。";
                case ThirdPayPaymentFailureReason.PaymentPageOpenFailed: return "第三方支付页打开失败。";
                case ThirdPayPaymentFailureReason.PaymentPageOpenException: return "第三方支付页打开过程发生异常。";
                case ThirdPayPaymentFailureReason.SystemBrowserFallbackUrlEmpty: return "系统浏览器回退支付 URL 为空。";
                case ThirdPayPaymentFailureReason.PaymentCallbackFailed: return "pay_callback 明确返回支付失败。";
                case ThirdPayPaymentFailureReason.PaymentCallbackInvalidPayload: return "pay_callback 参数无效。";
                case ThirdPayPaymentFailureReason.PaymentCallbackUnknownStatus: return "pay_callback 返回未知状态。";
                case ThirdPayPaymentFailureReason.CallbackOrderMismatch: return "pay_callback 的订单号与活动会话不一致。";
                case ThirdPayPaymentFailureReason.CallbackIgnoredAfterValidationStarted: return "pay_callback 到达时支付会话已经开始验单。";
                case ThirdPayPaymentFailureReason.ExternalBrowserReturnedWithoutCallback: return "外部浏览器返回应用后未收到成功 pay_callback。";
                case ThirdPayPaymentFailureReason.ValidationServiceUnavailable: return "第三方支付验单服务不可用。";
                case ThirdPayPaymentFailureReason.ValidationNetworkError: return "第三方支付验单网络请求失败。";
                case ThirdPayPaymentFailureReason.ValidationResponseOrderMissing: return "第三方支付验单响应没有目标订单。";
                case ThirdPayPaymentFailureReason.ServerOrderPendingPayment: return "服务端订单状态为待支付。";
                case ThirdPayPaymentFailureReason.ServerOrderProcessing: return "服务端订单状态为处理中。";
                case ThirdPayPaymentFailureReason.ServerOrderFailedOrExpired: return "服务端订单支付失败或已过期。";
                case ThirdPayPaymentFailureReason.ServerOrderNotFound: return "服务端订单不存在。";
                case ThirdPayPaymentFailureReason.UnknownServerOrderStatus: return "服务端返回未知第三方订单状态。";
                case ThirdPayPaymentFailureReason.OperationCancelled: return "第三方支付操作被取消。";
                case ThirdPayPaymentFailureReason.SessionReplaced: return "当前第三方支付会话被后续会话替换。";
                case ThirdPayPaymentFailureReason.StoreDisposed: return "ThirdPay Store 已释放。";
                case ThirdPayPaymentFailureReason.UnexpectedException: return "第三方支付流程发生未分类异常。";
                case ThirdPayPaymentFailureReason.PaymentUrlBuildFailed: return "构建第三方支付 URL 失败。";
                case ThirdPayPaymentFailureReason.PaymentUrlMissing: return "支付授权完成但未取得可打开的支付 URL。";
                default: return "第三方支付失败原因未知。";
            }
        }

        /// <summary>
        /// 获取创建订单失败原因的稳定中文描述。
        /// </summary>
        /// <param name="reason">创建订单失败原因。</param>
        /// <returns>与枚举值一一对应的中文描述。</returns>
        internal static string GetCreateOrderFailureDetail(ThirdPayCreateOrderFailureReason reason)
        {
            switch (reason)
            {
                case ThirdPayCreateOrderFailureReason.UserIdMissing: return "当前用户 UID 为空。";
                case ThirdPayCreateOrderFailureReason.PaymentConfigUnavailable: return "未取得可用的第三方支付配置。";
                case ThirdPayCreateOrderFailureReason.PaymentDisabledByServer: return "服务端关闭了当前第三方支付能力。";
                case ThirdPayCreateOrderFailureReason.ProductNotConfigured: return "第三方支付配置中不存在目标商品。";
                case ThirdPayCreateOrderFailureReason.PaymentPageServiceUnavailable: return "第三方支付页服务不可用。";
                case ThirdPayCreateOrderFailureReason.GoogleAuthorizationConnectionFailed: return "无法连接 Google 外链结算服务。";
                case ThirdPayCreateOrderFailureReason.GoogleTokenCreationFailed: return "创建 Google 外链上报令牌失败。";
                case ThirdPayCreateOrderFailureReason.GooglePaymentUrlBuildFailed: return "Google 外链支付 URL 构建失败。";
                case ThirdPayCreateOrderFailureReason.GoogleUserCancelled: return "用户在 Google 外链信息页取消支付。";
                case ThirdPayCreateOrderFailureReason.PaymentUrlBuildException: return "第三方支付 URL 构建过程发生异常。";
                case ThirdPayCreateOrderFailureReason.PaymentUrlEmpty: return "第三方支付授权未返回支付 URL。";
                default: return "第三方创建订单失败原因未知。";
            }
        }

        /// <summary>
        /// 获取本地订单删除原因的稳定中文描述。
        /// </summary>
        /// <param name="reason">本地订单删除原因。</param>
        /// <returns>与枚举值一一对应的中文描述。</returns>
        internal static string GetOrderDeleteReasonDetail(ThirdPayOrderDeleteReason reason)
        {
            switch (reason)
            {
                case ThirdPayOrderDeleteReason.Paid: return "服务端确认订单已支付，已移除本地订单。";
                case ThirdPayOrderDeleteReason.AlreadyDelivered: return "服务端确认订单已经发货，已移除本地订单。";
                case ThirdPayOrderDeleteReason.PendingPaymentExhausted: return "待支付订单达到当前场景验单上限；预检和补单场景均为单次上限，已移除本地订单。";
                case ThirdPayOrderDeleteReason.FailedOrExpired: return "服务端确认订单支付失败或已过期，已移除本地订单。";
                case ThirdPayOrderDeleteReason.NotFound: return "服务端确认订单不存在，已移除本地订单。";
                case ThirdPayOrderDeleteReason.PaymentPageNeverOpened: return "支付页未成功打开，已移除本地订单。";
                case ThirdPayOrderDeleteReason.AuthorizationFailed: return "支付授权失败或被取消，已移除本地订单。";
                case ThirdPayOrderDeleteReason.PaymentUrlBuildFailed: return "支付 URL 构建失败，已移除本地订单。";
                case ThirdPayOrderDeleteReason.UserOrAccountChanged: return "用户或账号发生变化，已移除本地订单。";
                case ThirdPayOrderDeleteReason.ExplicitCleanup: return "执行显式清理，已移除本地订单。";
                case ThirdPayOrderDeleteReason.LocalOrderInvalid: return "本地订单数据非法，已移除本地订单。";
                default: return "以未知原因移除了第三方本地订单。";
            }
        }
    }
}
