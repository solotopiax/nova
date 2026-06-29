/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGoogleSigninView.Methods.cs
 * author:    Codex
 * created:   2026/06/25
 * descrip:   Google 登录演示方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.GoogleSignIn;

namespace NovaFramework.Sdk.Googlesignin.Samples.Runtime
{
    public sealed partial class DemoGoogleSigninView
    {
        private void OnLoginButtonClick()
        {
            LoginAsync().Forget();
        }

        private void OnLogoutButtonClick()
        {
            LogoutAsync().Forget();
        }

        private void OnCurrentUserButtonClick()
        {
            if (!TryGetGoogleSignInPlugin(out GoogleSignInPlugin plugin))
            {
                return;
            }

            GoogleSignInUserData userData = plugin.CurrentUserData;
            if (userData == null)
            {
                AppendFeedback("当前没有 Google 用户。", FeedbackLevel.Warn);
                return;
            }

            AppendFeedback($"用户 ID：{Fallback(userData.UserId)}");
            AppendFeedback($"邮箱：{Fallback(userData.Email)}");
            AppendFeedback($"名称：{Fallback(userData.DisplayName)}");
            AppendFeedback($"头像：{Fallback(userData.AvatarUrl)}");
        }

        private async UniTaskVoid LoginAsync()
        {
            if (!TryGetGoogleSignInPlugin(out GoogleSignInPlugin plugin))
            {
                return;
            }

            try
            {
                AuthResult result = await plugin.LoginAsync("Google", CancellationToken.None);
                if (!result.Success)
                {
                    AppendFeedback($"Google 登录失败：{result.ErrorMessage}", FeedbackLevel.Error);
                    return;
                }

                AppendFeedback($"Google 登录成功：{result.UserId}", FeedbackLevel.Success);
                OnCurrentUserButtonClick();
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("Google 登录已取消。", FeedbackLevel.Warn);
            }
            catch (Exception ex)
            {
                AppendFeedback($"Google 登录异常：{ex.Message}", FeedbackLevel.Error);
            }
        }

        private async UniTaskVoid LogoutAsync()
        {
            if (!TryGetGoogleSignInPlugin(out GoogleSignInPlugin plugin))
            {
                return;
            }

            try
            {
                await plugin.LogoutAsync(CancellationToken.None);
                AppendFeedback("Google 已登出。", FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("Google 登出已取消。", FeedbackLevel.Warn);
            }
            catch (Exception ex)
            {
                AppendFeedback($"Google 登出异常：{ex.Message}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 获取 Google 插件。
        /// </summary>
        private bool TryGetGoogleSignInPlugin(out GoogleSignInPlugin plugin)
        {
            plugin = null;
            if (Nova.SDK == null)
            {
                AppendFeedback("Nova.SDK 不可用。", FeedbackLevel.Error);
                return false;
            }

            if (!Nova.SDK.TryGet(out plugin) || plugin == null)
            {
                AppendFeedback("GoogleSignInPlugin 不可用，请确认 Google SDK 配置已启用并初始化完成。", FeedbackLevel.Error);
                return false;
            }

            m_Plugin = plugin;
            return true;
        }

        private void ClearPluginReference()
        {
            m_Plugin = null;
        }

        private static string Fallback(string value)
        {
            return string.IsNullOrEmpty(value) ? "未返回" : value;
        }
    }
}
