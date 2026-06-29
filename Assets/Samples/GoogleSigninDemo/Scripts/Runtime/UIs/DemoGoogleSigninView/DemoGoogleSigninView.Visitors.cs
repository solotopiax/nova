/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGoogleSigninView.Visitors.cs
 * author:    Codex
 * created:   2026/06/25
 * descrip:   Google 登录演示字段
 ***************************************************************/

using NovaFramework.SDK.GoogleSignIn;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Googlesignin.Samples.Runtime
{
    public sealed partial class DemoGoogleSigninView
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
        /// Google 插件。
        /// </summary>
        private GoogleSignInPlugin m_Plugin;
    }
}
