/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppleSignInPlugin.Methods.cs
 * author:    yingzheng
 * created:   2026/6/25
 * descrip:   AppleSignInPlugin 非公开方法
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AppleSignIn
{
    /// <summary>
    /// AppleSignInPlugin 非公开方法分部类。
    /// </summary>
    public sealed partial class AppleSignInPlugin
    {
        /// <summary>
        /// 异步初始化 Apple 登录插件。
        /// </summary>
        /// <param name="config">SDK 配置。</param>
        /// <param name="ct">取消令牌。</param>
        protected override UniTask OnInitializeAsync(ISDKPluginConfig config, CancellationToken ct)
        {
            m_RuntimeConfig = config as AppleSignInPluginConfig ?? new AppleSignInPluginConfig();
            m_AuthService = new AppleSignInAuthService(m_RuntimeConfig);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 异步释放 Apple 登录插件。
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
        /// 构建失败登录结果。
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
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 写入登录状态。
        /// </summary>
        /// <param name="userData">用户数据。</param>
        /// <param name="provider">第三方登录提供方名称。</param>
        private void SetLoginState(AppleSignInUserData userData, string provider = null)
        {
            m_CurrentUserData = userData;

            if (userData != null)
            {
                PublishData(nameof(AppleSignInUserData), userData);
                if (!string.IsNullOrEmpty(userData.UserId))
                {
                    PublishData(SDKDataKeys.OpenId, userData.UserId);
                    PublishData(SDKDataKeys.ThirdLoginProvider, string.IsNullOrEmpty(provider) ? c_ProviderName : provider);
                }
            }
        }
    }
}
