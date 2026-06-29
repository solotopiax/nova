/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GoogleSignInUserData.cs
 * author:    Codex
 * created:   2026/6/25
 * descrip:   Google 登录用户数据
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.GoogleSignIn
{
    /// <summary>
    /// Google 登录用户数据。
    /// </summary>
    public sealed class GoogleSignInUserData
    {
        /// <summary>
        /// 获取 Google 用户 ID。
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// 获取 ID Token。
        /// </summary>
        public string IdToken { get; }

        /// <summary>
        /// 获取邮箱。
        /// </summary>
        public string Email { get; }

        /// <summary>
        /// 获取显示名称。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 获取头像地址。
        /// </summary>
        public string AvatarUrl { get; }

        /// <summary>
        /// 创建 Google 用户数据。
        /// </summary>
        /// <param name="userId">Google 用户 ID。</param>
        /// <param name="idToken">ID Token。</param>
        /// <param name="email">邮箱。</param>
        /// <param name="displayName">显示名称。</param>
        /// <param name="avatarUrl">头像地址。</param>
        public GoogleSignInUserData(string userId, string idToken, string email, string displayName, string avatarUrl)
        {
            UserId = userId;
            IdToken = idToken;
            Email = email;
            DisplayName = displayName;
            AvatarUrl = avatarUrl;
        }

        /// <summary>
        /// 转换为 Nova 登录结果。
        /// </summary>
        /// <param name="provider">登录提供方名称。</param>
        /// <returns>Nova 登录结果。</returns>
        public AuthResult ToAuthResult(string provider)
        {
            if (string.IsNullOrEmpty(UserId) || string.IsNullOrEmpty(IdToken))
            {
                return new AuthResult
                {
                    Success = false,
                    Provider = string.IsNullOrEmpty(provider) ? "Google" : provider,
                    ErrorMessage = "Google 登录结果缺少 UserId 或 ID Token。"
                };
            }

            return new AuthResult
            {
                Success = true,
                UserId = UserId,
                Token = IdToken,
                Provider = string.IsNullOrEmpty(provider) ? "Google" : provider,
            };
        }
    }
}
