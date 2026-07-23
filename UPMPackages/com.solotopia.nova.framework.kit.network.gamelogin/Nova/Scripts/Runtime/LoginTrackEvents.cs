/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LoginTrackEvents.cs
 * author:    taoye
 * created:   2026/7/21
 * descrip:   GameLogin 埋点事件名与字段名常量
 ***************************************************************/

namespace NovaFramework.Kit.Network.GameLogin.Runtime
{
    internal static class LoginTrackEvents
    {
        public const string Login = "nova_gamelogin_login";
        public const string DeleteAccount = "nova_gamelogin_delete_account";
    }

    internal static class LoginTrackFields
    {
        public const string LoginType = "nova_gamelogin_login_type";
        public const string LoginResult = "nova_gamelogin_login_result";
        public const string LoginErrorCode = "nova_gamelogin_login_error_code";
        public const string LoginRegisterTime = "nova_gamelogin_login_register_time";
        public const string LoginTime = "nova_gamelogin_login_time";
        public const string LoginIsNewAccount = "nova_gamelogin_login_is_new_account";
        public const string LoginDurationMs = "nova_gamelogin_login_duration_ms";
        public const string DeleteAccountResult = "nova_gamelogin_delete_account_result";
        public const string DeleteAccountErrorCode = "nova_gamelogin_delete_account_error_code";
        public const string DeleteAccountDurationMs = "nova_gamelogin_delete_account_duration_ms";
        public const string OpenId = "nova_openid";
    }
}
