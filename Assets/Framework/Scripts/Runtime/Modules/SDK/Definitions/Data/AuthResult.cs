/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AuthResult.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   第三方登录结果数据类
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 第三方登录操作结果数据类，由 IAuthPlugin.LoginAsync 返回。
    /// 不变量：Success=false 时 ErrorMessage 非空；Token 仅成功时有效，不得在 Success=false 时使用。
    /// </summary>
    public sealed class AuthResult
    {
        /// <summary>
        /// 登录操作是否成功完成。
        /// </summary>
        public bool Success;

        /// <summary>
        /// 登录成功后第三方平台返回的用户唯一标识；失败时为 null。
        /// </summary>
        public string UserId;

        /// <summary>
        /// 登录成功后获取的访问令牌，用于后续接口鉴权；失败时为 null。
        /// </summary>
        public string Token;

        /// <summary>
        /// 本次登录所使用的第三方平台标识（如 "Apple"、"Google"、"Facebook"）。
        /// </summary>
        public string Provider;

        /// <summary>
        /// 登录失败时的错误描述；成功时为 null。
        /// </summary>
        public string ErrorMessage;
    }
}
