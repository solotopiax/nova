/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookPlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   FacebookPlugin 非公开方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// FacebookPlugin 非公开方法分部类。
    /// </summary>
    public sealed partial class FacebookPlugin
    {
        /// <summary>
        /// 异步初始化 Facebook SDK。
        /// </summary>
        /// <param name="config">SDK 配置。</param>
        /// <param name="ct">取消令牌。</param>
        protected override async UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            m_RuntimeConfig = config as FacebookPluginConfig ?? new FacebookPluginConfig();
            int avatarSize = m_RuntimeConfig.AvatarSize > 0 ? m_RuntimeConfig.AvatarSize : FacebookPluginConfig.DefaultAvatarSize;

            m_GraphService = new FacebookGraphService();
            m_ProfileService = new FacebookProfileService(this, m_GraphService, avatarSize);
            m_FriendsService = new FacebookFriendsService(m_ProfileService, m_GraphService);
            m_ShareService = new FacebookShareService();
            m_AuthService = new FacebookAuthService();

            await m_AuthService.InitializeAsync(ct);
            SetLoginState(new FacebookUserData(m_AuthService.CurrentUserId, m_AuthService.CurrentAccessToken));
        }

        /// <summary>
        /// 异步释放 Facebook SDK 插件。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        protected override UniTask OnDisposeAsync(CancellationToken ct)
        {
            SetLoginState(null);
            m_AuthService = null;
            m_ProfileService = null;
            m_FriendsService = null;
            m_ShareService = null;
            m_GraphService = null;
            m_RuntimeConfig = null;
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 构造失败登录结果。
        /// </summary>
        /// <param name="provider">登录提供方。</param>
        /// <param name="errorMessage">错误信息。</param>
        /// <returns>失败结果。</returns>
        private static AuthResult BuildFailedAuthResult(string provider, string errorMessage)
        {
            return new AuthResult
            {
                Success = false,
                Provider = string.IsNullOrEmpty(provider) ? ProviderName : provider,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 更新登录状态。
        /// </summary>
        /// <param name="userData">用户数据。</param>
        private void SetLoginState(FacebookUserData userData)
        {
            m_CurrentUserData = userData;
        }

        /// <summary>
        /// 尝试下载头像。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        private async UniTask TryDownloadCurrentAvatarAsync(CancellationToken ct)
        {
            if (m_ProfileService == null || string.IsNullOrEmpty(m_CurrentUserData?.UserId))
            {
                return;
            }

            try
            {
                string avatarPath = await m_ProfileService.DownloadAvatarAsync(m_CurrentUserData.UserId, ct);
                m_CurrentUserData = m_CurrentUserData?.WithAvatarPath(avatarPath);
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.SDK, $"Facebook 头像自动下载失败：{ex.Message}");
            }
        }
    }
}
