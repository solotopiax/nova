/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppleSignInUserData.cs
 * author:    Codex
 * created:   2026/6/25
 * descrip:   Apple 登录用户数据
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.AppleSignIn
{
    /// <summary>
    /// Apple 登录用户数据。
    /// </summary>
    public sealed class AppleSignInUserData
    {
        /// <summary>
        /// 获取 Apple 用户 ID。
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// 获取姓名。Apple 只会在用户首次授权且请求姓名时返回。
        /// </summary>
        public string FullName { get; }

        /// <summary>
        /// 创建 Apple 用户数据。
        /// </summary>
        /// <param name="userId">Apple 用户 ID。</param>
        /// <param name="fullName">姓名。</param>
        public AppleSignInUserData(string userId, string fullName)
        {
            UserId = userId;
            FullName = fullName;
        }

        /// <summary>
        /// 转换为 Nova 登录结果。
        /// </summary>
        /// <param name="provider">登录提供方名称。</param>
        /// <returns>Nova 登录结果。</returns>
        public AuthResult ToAuthResult(string provider)
        {
            string resolvedProvider = string.IsNullOrEmpty(provider) ? "Apple" : provider;
            if (string.IsNullOrEmpty(UserId))
            {
                return new AuthResult
                {
                    Success = false,
                    Provider = resolvedProvider,
                    ErrorMessage = "Apple 登录结果缺少 UserId。"
                };
            }

            return new AuthResult
            {
                Success = true,
                UserId = UserId,
                Provider = resolvedProvider,
            };
        }
    }
}
