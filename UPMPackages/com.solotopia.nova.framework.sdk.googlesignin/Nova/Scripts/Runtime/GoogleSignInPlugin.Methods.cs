/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GoogleSignInPlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/6/25
 * descrip:   GoogleSignInPlugin 非公开方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.GoogleSignIn
{
    /// <summary>
    /// GoogleSignInPlugin 非公开方法分部类。
    /// </summary>
    public sealed partial class GoogleSignInPlugin
    {
        /// <summary>
        /// 异步初始化 Google 登录插件。
        /// </summary>
        /// <param name="config">SDK 配置。</param>
        /// <param name="ct">取消令牌。</param>
        protected override async UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            m_RuntimeConfig = config as GoogleSignInPluginConfig ?? new GoogleSignInPluginConfig();
            m_AuthService = new GoogleSignInAuthService(m_RuntimeConfig);

            if (!m_RuntimeConfig.AutoRestoreOnInitialize)
            {
                return;
            }

            try
            {
                GoogleSignInUserData userData = await m_AuthService.RestoreAsync(ct);
                SetLoginState(userData, c_ProviderName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.SDK, $"Google 登录状态恢复失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 异步释放 Google 登录插件。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        protected override UniTask OnDisposeAsync(CancellationToken ct)
        {
            SetLoginState(null);
            m_AuthService = null;
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
                Provider = string.IsNullOrEmpty(provider) ? c_ProviderName : provider,
                ErrorMessage = errorMessage,
            };
        }

        /// <summary>
        /// 更新登录状态。
        /// </summary>
        /// <param name="userData">用户数据。</param>
        /// <param name="provider">第三方登录渠道名。</param>
        private void SetLoginState(GoogleSignInUserData userData, string provider = null)
        {
            m_CurrentUserData = userData;
            if (!string.IsNullOrEmpty(userData?.UserId))
            {
                PublishData(SDKDataKeys.OpenId, userData.UserId);
                PublishData(SDKDataKeys.ThirdPlatform, string.IsNullOrEmpty(provider) ? c_ProviderName : provider);
            }
        }
    }
}
