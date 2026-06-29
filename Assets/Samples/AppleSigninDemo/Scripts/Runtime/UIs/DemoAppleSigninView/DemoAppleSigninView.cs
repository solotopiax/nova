/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAppleSigninView.cs
 * author:    Codex
 * created:   2026/06/25
 * descrip:   Apple 登录演示入口
 ***************************************************************/

namespace NovaFramework.Sdk.Applesignin.Samples.Runtime
{
    public sealed partial class DemoAppleSigninView : BaseDemoView
    {
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Apple 登录");

            if (m_LoginButton != null)
            {
                m_LoginButton.onClick.AddListener(OnLoginButtonClick);
                SetButtonApiHint(m_LoginButton, "AppleSignInPlugin.LoginAsync(\"Apple\")");
            }

            if (m_LogoutButton != null)
            {
                m_LogoutButton.onClick.AddListener(OnLogoutButtonClick);
                SetButtonApiHint(m_LogoutButton, "AppleSignInPlugin.LogoutAsync()");
            }

            if (m_CurrentUserButton != null)
            {
                m_CurrentUserButton.onClick.AddListener(OnCurrentUserButtonClick);
                SetButtonApiHint(m_CurrentUserButton, "AppleSignInPlugin.CurrentUserData");
            }
        }

        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            AppendFeedback("选择按钮调用 Apple 登录接口。");
        }

        public override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);

            ClearPluginReference();
        }
    }
}
