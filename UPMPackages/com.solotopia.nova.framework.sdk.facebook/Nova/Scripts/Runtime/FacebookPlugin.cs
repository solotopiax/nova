/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookPlugin.cs
 * author:    yingzheng
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
    [SDKPluginConfigType(typeof(FacebookPluginConfig))]
    public sealed partial class FacebookPlugin : SDKPluginBase, IAuthPlugin, IAcquisitionTrackPlugin, ISDKLoginReceiver
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
                Log.Warning(LogTag.Facebook, "登录未执行：Facebook SDK 未初始化。");
                return BuildFailedAuthResult(provider, "Facebook SDK 未初始化。");
            }

            AuthResult result = await m_AuthService.LoginAsync(provider, ct);
            if (!result.Success)
            {
                return result;
            }

            SetLoginState(new FacebookUserData(result.UserId, result.Token), result.Provider);
            Log.Debug(LogTag.Facebook, $"登录态已更新：自动下载头像={m_RuntimeConfig == null || m_RuntimeConfig.AutoDownloadAvatarOnLogin}。");
            if (m_RuntimeConfig == null || m_RuntimeConfig.AutoDownloadAvatarOnLogin)
            {
                await TryDownloadCurrentAvatarAsync(ct);
            }

            Log.Debug(LogTag.Facebook, $"登录流程完成：头像缓存可用={!string.IsNullOrEmpty(m_CurrentUserData?.AvatarPath)}。");
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
            Log.Debug(LogTag.Facebook, "本地 Facebook 登录态已清除。");
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
                Log.Warning(LogTag.Facebook, "好友授权未执行：Facebook SDK 未初始化。");
                return BuildFailedAuthResult(ProviderName, "Facebook SDK 未初始化。");
            }

            AuthResult result = await m_AuthService.EnsureFriendsPermissionAsync(ProviderName, ct);
            if (result.Success)
            {
                SetLoginState(new FacebookUserData(result.UserId, result.Token, m_CurrentUserData?.AvatarPath), result.Provider);
            }

            return result;
        }
    }
}
