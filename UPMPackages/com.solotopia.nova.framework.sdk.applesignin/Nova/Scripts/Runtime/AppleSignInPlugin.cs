/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppleSignInPlugin.cs
 * author:    yingzheng
 * created:   2026/6/25
 * descrip:   Apple 登录 SDK 插件
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AppleSignIn
{
    /// <summary>
    /// Apple 登录 SDK 插件。
    /// </summary>
    public sealed partial class AppleSignInPlugin : SDKPluginBase, IAuthPlugin
    {
        /// <summary>
        /// 异步发起 Apple 登录。
        /// </summary>
        /// <param name="provider">登录提供方。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>登录结果。</returns>
        public async UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct = default)
        {
            if (m_AuthService == null)
            {
                return BuildFailedAuthResult(provider, "Apple 登录 SDK 未初始化。");
            }

            AppleSignInUserData userData;
            try
            {
                userData = await m_AuthService.LoginAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return BuildFailedAuthResult(provider, ex.Message);
            }

            AuthResult result = userData.ToAuthResult(string.IsNullOrEmpty(provider) ? c_ProviderName : provider);
            if (result.Success)
            {
                SetLoginState(userData);
            }

            return result;
        }

        /// <summary>
        /// 异步登出当前 Apple 账号。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>登出任务。</returns>
        public UniTask LogoutAsync(CancellationToken ct = default)
        {
            SetLoginState(null);
            return UniTask.CompletedTask;
        }
    }
}
