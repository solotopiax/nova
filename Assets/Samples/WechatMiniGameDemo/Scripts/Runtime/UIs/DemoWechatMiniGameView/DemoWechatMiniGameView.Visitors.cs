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
        /// <summary>当前 View 会话使用的微信插件。</summary>
        private WeChatMiniGamePlugin m_Wechat;

        /// <summary>关闭 View 时统一取消仍在等待的微信异步调用。</summary>
        private CancellationTokenSource m_SessionCancellation;

        /// <summary>当前 Demo 会话最近一笔客户端订单号，仅用于按钮定位。</summary>
        private string m_LastOrderId;

        /// <summary>当前 Demo 会话已由业务服务端校验的微信 OpenID。</summary>
        private string m_WechatOpenId;

        /// <summary>当前 Demo 会话已由业务服务端校验的微信 UnionID，可为空。</summary>
        private string m_WechatUnionId;

        /// <summary>
        /// 查看 SDK 运行环境信息。
        /// </summary>
        [SerializeField] private Button m_RuntimeInfoButton;

        /// <summary>用已校验的微信 OpenID 登录游戏服务器，未绑定时注册游客并绑定。</summary>
        [SerializeField] private Button m_GameLoginButton;

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

        /// <summary>请求用户订阅消息授权并把逐模板结果登记到服务端。</summary>
        [SerializeField] private Button m_SubscribeMessageButton;
    }
}
