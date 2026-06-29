/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAppleSigninView.Visitors.cs
 * author:    Codex
 * created:   2026/06/25
 * descrip:   Apple 登录演示字段
 ***************************************************************/

using NovaFramework.SDK.AppleSignIn;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Applesignin.Samples.Runtime
{
    public sealed partial class DemoAppleSigninView
    {
        /// <summary>
        /// 登录按钮。
        /// </summary>
        [SerializeField] private Button m_LoginButton = null;

        /// <summary>
        /// 登出按钮。
        /// </summary>
        [SerializeField] private Button m_LogoutButton = null;

        /// <summary>
        /// 当前用户按钮。
        /// </summary>
        [SerializeField] private Button m_CurrentUserButton = null;

        /// <summary>
        /// Apple 插件。
        /// </summary>
        private AppleSignInPlugin m_Plugin;
    }
}
