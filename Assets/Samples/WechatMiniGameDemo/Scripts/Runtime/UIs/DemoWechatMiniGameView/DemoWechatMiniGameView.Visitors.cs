/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoWechatMiniGameView.Visitors.cs
 * author:    nova-create-sample
 * created:   2026/09/20
 * descrip:   DemoWechatMiniGameView 演示 View — 字段与属性
 ***************************************************************/

using UnityEngine;
using UnityEngine.UI;
using System.Threading;
using NovaFramework.SDK.WeChatMiniGame.Runtime;

namespace NovaFramework.Sdk.Wechat.Minigame.Samples.Runtime
{
    /// <summary>
    /// DemoWechatMiniGameView 演示 View 的字段声明。
    /// </summary>
    public sealed partial class DemoWechatMiniGameView
    {
        /// <summary>仅供本 Demo 使用的购买、广告和通知物料，不进入正式 SDK 配置。</summary>
        [SerializeField] private DemoWechatMiniGameMaterials m_DemoMaterials = new DemoWechatMiniGameMaterials();

        /// <summary>当前 View 会话使用的微信插件。</summary>
        private WeChatMiniGamePlugin m_Wechat;

        /// <summary>关闭 View 时统一取消仍在等待的微信异步调用。</summary>
        private CancellationTokenSource m_SessionCancellation;

        /// <summary>当前 Demo 会话已由业务服务端校验的微信 OpenID。</summary>
        private string m_WechatOpenId;

        /// <summary>当前 Demo 会话已由业务服务端校验的微信 UnionID，可为空。</summary>
        private string m_WechatUnionId;

        /// <summary>保持已经登录的业务 UID，供页面重开后的已发货埋点重试。</summary>
        private string m_DemoGameUid;

        /// <summary>
        /// 查看 SDK 运行环境信息。
        /// </summary>
        [SerializeField] private Button m_RuntimeInfoButton;

        /// <summary>获取微信一次性登录 code，并交由微信业务后端完成校验。</summary>
        [SerializeField] private Button m_LoginButton;

        /// <summary>查询微信隐私授权状态。</summary>
        [SerializeField] private Button m_PrivacyStatusButton;

        /// <summary>在当前用户手势中请求微信隐私授权。</summary>
        [SerializeField] private Button m_PrivacyAuthorizeButton;

        /// <summary>读取系统剪贴板。</summary>
        [SerializeField] private Button m_ReadClipboardButton;

        /// <summary>在当前用户手势中写入系统剪贴板。</summary>
        [SerializeField] private Button m_WriteClipboardButton;

        /// <summary>触发一次短振动。</summary>
        [SerializeField] private Button m_VibrateButton;

        /// <summary>在当前用户手势中拉起主动分享。</summary>
        [SerializeField] private Button m_ShareButton;

        /// <summary>检查当前设备是否支持微信虚拟支付。</summary>
        [SerializeField] private Button m_PaymentSupportButton;

        /// <summary>通过业务服务端订单购买游戏币。</summary>
        [SerializeField] private Button m_PayCurrencyButton;

        /// <summary>通过业务服务端订单直购道具。</summary>
        [SerializeField] private Button m_PayItemButton;

        /// <summary>查询当前会话最近一笔服务端订单。</summary>
        [SerializeField] private Button m_QueryOrderButton;

        /// <summary>主动验证当前会话最近一笔订单并刷新服务端入账结果。</summary>
        [SerializeField] private Button m_RecoverOrderButton;

        /// <summary>验证当前账号全部订单并刷新服务端入账结果。</summary>
        [SerializeField] private Button m_RecoverPendingButton;

        /// <summary>切换订单列表的支付类型筛选。</summary>
        [SerializeField] private Button m_FilterOrdersButton;
        /// <summary>预加载激励视频广告。</summary>
        [SerializeField] private Button m_LoadRewardedButton;
        /// <summary>显示已经预加载的激励视频广告。</summary>
        [SerializeField] private Button m_ShowRewardedButton;
        /// <summary>预加载插屏广告。</summary>
        [SerializeField] private Button m_LoadInterstitialButton;
        /// <summary>显示已经预加载的插屏广告。</summary>
        [SerializeField] private Button m_ShowInterstitialButton;
        /// <summary>显示底部居中 Banner。</summary>
        [SerializeField] private Button m_ShowBannerButton;
        /// <summary>隐藏已创建 Banner。</summary>
        [SerializeField] private Button m_HideBannerButton;

        /// <summary>请求用户订阅消息授权并把逐模板结果登记到服务端。</summary>
        [SerializeField] private Button m_SubscribeMessageButton;

        /// <summary>微信订阅模板 ID 输入框。</summary>
        [SerializeField] private TMPro.TMP_InputField m_SubscriptionTemplateIdInput;

        /// <summary>服务端通知配置 ID 输入框。</summary>
        [SerializeField] private TMPro.TMP_InputField m_NoticeConfigIdInput;

        /// <summary>通知触发 Unix 秒时间戳输入框；0 表示立即。</summary>
        [SerializeField] private TMPro.TMP_InputField m_NoticeTriggerTimeInput;

        /// <summary>通知任务稳定幂等键输入框。</summary>
        [SerializeField] private TMPro.TMP_InputField m_NoticeClientTaskIdInput;

        /// <summary>待做内容安全检查的文本输入框。</summary>
        [SerializeField] private TMPro.TMP_InputField m_TextSecurityContentInput;

        /// <summary>运行时创建的文本安全检查按钮。</summary>
        [SerializeField] private Button m_CheckTextSecurityButton;
    }
}
