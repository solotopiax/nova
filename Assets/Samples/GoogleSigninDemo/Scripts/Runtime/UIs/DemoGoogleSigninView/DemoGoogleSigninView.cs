/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGoogleSigninView.cs
 * author:    Codex
 * created:   2026/06/25
 * descrip:   Google 登录演示入口
 ***************************************************************/

namespace NovaFramework.Sdk.Googlesignin.Samples.Runtime
{
    public sealed partial class DemoGoogleSigninView : BaseDemoView
    {
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Google 登录");

            if (m_LoginButton != null)
            {
                m_LoginButton.onClick.AddListener(OnLoginButtonClick);
                SetButtonApiHint(m_LoginButton, "GoogleSignInPlugin.LoginAsync(\"Google\")");
            }

            if (m_LogoutButton != null)
            {
                m_LogoutButton.onClick.AddListener(OnLogoutButtonClick);
                SetButtonApiHint(m_LogoutButton, "GoogleSignInPlugin.LogoutAsync()");
            }

            if (m_CurrentUserButton != null)
            {
                m_CurrentUserButton.onClick.AddListener(OnCurrentUserButtonClick);
                SetButtonApiHint(m_CurrentUserButton, "GoogleSignInPlugin.CurrentUserData");
            }
        }

        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            AppendFeedback("选择按钮调用 Google 登录接口。");
        }

        public override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);

            ClearPluginReference();
        }
    }
}
