/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoWechatMiniGameView.cs
 * author:    nova-create-sample
 * created:   2026/09/20
 * descrip:   DemoWechatMiniGameView 演示 View — 生命周期与公开接口
 ***************************************************************/

using NovaFramework.SDK.WeChatMiniGame.Runtime;

namespace NovaFramework.Sdk.Wechat.Minigame.Samples.Runtime
{
    /// <summary>
    /// DemoWechatMiniGameView 演示 View，派生自 BaseDemoView，遵循三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// 覆盖运行环境、登录、隐私、剪贴板、振动与主动分享等常用微信能力。
    /// </summary>
    public sealed partial class DemoWechatMiniGameView : BaseDemoView, IWeChatMiniGameFulfillmentHandler
    {
        /// <summary>由消费项目配置的订阅消息模板 ID，最多三个。</summary>
        public static string[] SubscriptionTemplateIds { get; set; } = System.Array.Empty<string>();

        /// <summary>Demo 游戏币商品 ID。</summary>
        public static string CurrencyProductId { get; set; } = "demo_currency";

        /// <summary>Demo 道具商品 ID。</summary>
        public static string ItemProductId { get; set; } = "demo_item";

        /// <summary>
        /// 视图初始化钩子，仅在首次创建实例时触发。
        /// 注册示例按钮事件并设置标题与 API 副标题。
        /// 子类重写须调用 base.OnInit(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("微信小游戏接口演示");

            // 先取得并校验微信 OpenID，再用它查询游戏账号绑定关系。
            m_LoginButton?.transform.SetAsFirstSibling();
            m_GameLoginButton?.transform.SetSiblingIndex(1);
            BindButton(m_LoginButton, OnWechatLoginClick, "微信登录校验", "WeChatMiniGamePlugin.LoginAndVerifyAsync()");
            BindButton(m_GameLoginButton, OnGameLoginClick, "登录游戏服务器", "Login.Async(openid) -> Login.Async() -> Bind.BindAsync(Wechat, openid)");
            BindButton(m_RuntimeInfoButton, OnRuntimeInfoClick, "运行环境信息", "RuntimeInfo");
            BindButton(m_PrivacyStatusButton, OnPrivacyStatusClick, "查询隐私状态", "GetPrivacyStatusAsync()");
            BindButton(m_PrivacyAuthorizeButton, OnPrivacyAuthorizeClick, "请求隐私授权", "RequirePrivacyAuthorizationAsync()");
            BindButton(m_ReadClipboardButton, OnReadClipboardClick, "读取剪贴板", "GetClipboardTextAsync()");
            BindButton(m_WriteClipboardButton, OnWriteClipboardClick, "写入剪贴板", "SetClipboardTextAsync()");
            BindButton(m_VibrateButton, OnVibrateClick, "短振动", "VibrateShortAsync()");
            BindButton(m_ShareButton, OnShareClick, "主动分享", "ShareAppMessage()");
            BindButton(m_PaymentSupportButton, OnPaymentSupportClick, "检查支付能力", "CheckPaymentSupportAsync()");
            BindButton(m_PayCurrencyButton, OnPayCurrencyClick, "购买游戏币", "WeChatMiniGamePlugin.PurchaseAndVerifyAsync()");
            BindButton(m_PayItemButton, OnPayItemClick, "购买道具", "WeChatMiniGamePlugin.PurchaseAndVerifyAsync()");
            BindButton(m_QueryOrderButton, OnQueryOrderClick, "查询服务端订单", "WeChatMiniGamePlugin.QueryCurrentUserPaymentOrdersAsync()");
            BindButton(m_RecoverOrderButton, OnVerifyOrderClick, "验证最近订单", "WeChatMiniGamePlugin.VerifyPaymentOrderAsync()");
            BindButton(m_RecoverPendingButton, OnVerifyAllOrdersClick, "验证全部订单", "WeChatMiniGamePlugin.VerifyAllPaymentOrdersAsync()");
            BindButton(m_SubscribeMessageButton, OnSubscribeMessageClick, "开启消息提醒", "WeChatMiniGamePlugin.RequestSubscribeMessagesAsync()");
        }

        /// <summary>
        /// 视图打开钩子，每次 OpenUIViewAsync 调用时触发。
        /// 子类重写须调用 base.OnOpen(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            BeginSession();
            AppendFeedback("订单由客户端持久化；服务端验单允许后，由项目侧按订单号幂等发货。", FeedbackLevel.Info);
        }

        /// <summary>
        /// 视图关闭钩子，关闭时由基类清空反馈区。
        /// 子类重写须调用 base.OnClose(isShutdown, userData)。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            EndSession();
            base.OnClose(isShutdown, userData);
        }
    }
}
