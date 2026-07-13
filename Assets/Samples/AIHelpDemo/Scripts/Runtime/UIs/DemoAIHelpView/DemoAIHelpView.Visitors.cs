/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAIHelpView.Visitors.cs
 * author:    nova-create-sample
 * created:   2026/07/09
 * descrip:   DemoAIHelpView 演示 View — 字段与属性
 ***************************************************************/

using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Aihelp.Samples.Runtime
{
    /// <summary>
    /// DemoAIHelpView 演示 View 的字段声明。
    /// 每个按钮对应 AIHelpPlugin 一个常用接口，点击后就近打印反馈。
    /// </summary>
    public sealed partial class DemoAIHelpView
    {
        /// <summary>
        /// 查看 SDK 版本与可用状态按钮（GetSDKVersion() / IsAvailable）。
        /// </summary>
        [SerializeField] private Button m_InfoButton;

        /// <summary>
        /// 拉起帮助中心页面按钮（Show("E001")，无欢迎语）。
        /// </summary>
        [SerializeField] private Button m_HelpCenterButton;

        /// <summary>
        /// 拉起在线客服页面按钮（Show("E002", welcomeMessage)，带欢迎语）。
        /// </summary>
        [SerializeField] private Button m_CustomerServiceButton;

        /// <summary>
        /// 同步登录用户按钮（Login）。
        /// </summary>
        [SerializeField] private Button m_LoginButton;

        /// <summary>
        /// 查询未读消息数按钮（FetchUnreadMessageCount / FetchUnreadTaskCount），结果经事件异步回显。
        /// </summary>
        [SerializeField] private Button m_FetchUnreadButton;

        /// <summary>
        /// 关闭当前 AIHelp 页面按钮（Close），与基类的关闭本 View 按钮（m_CloseButton）区分。
        /// </summary>
        [SerializeField] private Button m_CloseAIHelpButton;
    }
}
