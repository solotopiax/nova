/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookUserData.cs
 * author:    Codex
 * created:   2026/6/25
 * descrip:   Facebook 登录用户数据
 ***************************************************************/

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook 登录用户数据。
    /// </summary>
    public sealed class FacebookUserData
    {
        /// <summary>
        /// 用户 ID。
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// 访问令牌。
        /// </summary>
        public string AccessToken { get; }

        /// <summary>
        /// 头像路径。
        /// </summary>
        public string AvatarPath { get; }

        /// <summary>
        /// 创建用户数据。
        /// </summary>
        /// <param name="userId">用户 ID。</param>
        /// <param name="accessToken">访问令牌。</param>
        /// <param name="avatarPath">头像路径。</param>
        public FacebookUserData(string userId, string accessToken, string avatarPath = null)
        {
            UserId = userId;
            AccessToken = accessToken;
            AvatarPath = avatarPath;
        }

        /// <summary>
        /// 替换头像路径。
        /// </summary>
        /// <param name="avatarPath">头像路径。</param>
        /// <returns>用户数据。</returns>
        public FacebookUserData WithAvatarPath(string avatarPath)
        {
            return new FacebookUserData(UserId, AccessToken, avatarPath);
        }
    }
}
