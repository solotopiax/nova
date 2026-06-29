/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookPlugin.cs
 * author:    taoye
 * created:   2026/4/20
 * descrip:   Facebook SDK 插件
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook SDK 插件。
    /// </summary>
    public sealed partial class FacebookPlugin : SDKPluginBase, IAuthPlugin
    {
        /// <summary>
        /// 异步发起 Facebook 登录流程。
        /// </summary>
        /// <param name="provider">第三方平台标识。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>登录结果。</returns>
        public async UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct = default)
        {
            if (m_AuthService == null)
            {
                return BuildFailedAuthResult(provider, "Facebook SDK 未初始化。");
            }

            AuthResult result = await m_AuthService.LoginAsync(provider, ct);
            if (!result.Success)
            {
                return result;
            }

            SetLoginState(new FacebookUserData(result.UserId, result.Token));
            if (m_RuntimeConfig == null || m_RuntimeConfig.AutoDownloadAvatarOnLogin)
            {
                await TryDownloadCurrentAvatarAsync(ct);
            }

            return result;
        }

        /// <summary>
        /// 异步登出当前 Facebook 账号。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>登出任务。</returns>
        public UniTask LogoutAsync(CancellationToken ct = default)
        {
            m_AuthService?.Logout();
            SetLoginState(null);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 确保好友列表权限。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>授权结果。</returns>
        public async UniTask<AuthResult> EnsureFriendsPermissionAsync(CancellationToken ct = default)
        {
            if (m_AuthService == null)
            {
                return BuildFailedAuthResult(ProviderName, "Facebook SDK 未初始化。");
            }

            AuthResult result = await m_AuthService.EnsureFriendsPermissionAsync(ProviderName, ct);
            if (result.Success)
            {
                SetLoginState(new FacebookUserData(result.UserId, result.Token, m_CurrentUserData?.AvatarPath));
            }

            return result;
        }
    }
}
