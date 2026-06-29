/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAppleSigninView.Methods.cs
 * author:    Codex
 * created:   2026/06/25
 * descrip:   Apple 登录演示方法
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AppleSignIn;

namespace NovaFramework.Sdk.Applesignin.Samples.Runtime
{
    public sealed partial class DemoAppleSigninView
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
            if (!TryGetAppleSignInPlugin(out AppleSignInPlugin plugin))
            {
                return;
            }

            AppleSignInUserData userData = plugin.CurrentUserData;
            if (userData == null)
            {
                AppendFeedback("当前没有 Apple 用户。", FeedbackLevel.Warn);
                return;
            }

            AppendFeedback($"用户 ID：{Fallback(userData.UserId)}");
            AppendFeedback($"姓名：{Fallback(userData.FullName)}");
        }

        private async UniTaskVoid LoginAsync()
        {
            if (!TryGetAppleSignInPlugin(out AppleSignInPlugin plugin))
            {
                return;
            }

            try
            {
                AuthResult result = await plugin.LoginAsync("Apple", CancellationToken.None);
                if (!result.Success)
                {
                    AppendFeedback($"Apple 登录失败：{result.ErrorMessage}", FeedbackLevel.Error);
                    return;
                }

                AppendFeedback($"Apple 登录成功：{result.UserId}", FeedbackLevel.Success);
                OnCurrentUserButtonClick();
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("Apple 登录已取消。", FeedbackLevel.Warn);
            }
            catch (Exception ex)
            {
                AppendFeedback($"Apple 登录异常：{ex.Message}", FeedbackLevel.Error);
            }
        }

        private async UniTaskVoid LogoutAsync()
        {
            if (!TryGetAppleSignInPlugin(out AppleSignInPlugin plugin))
            {
                return;
            }

            try
            {
                await plugin.LogoutAsync(CancellationToken.None);
                AppendFeedback("Apple 已登出。", FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("Apple 登出已取消。", FeedbackLevel.Warn);
            }
            catch (Exception ex)
            {
                AppendFeedback($"Apple 登出异常：{ex.Message}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 获取 Apple 插件。
        /// </summary>
        private bool TryGetAppleSignInPlugin(out AppleSignInPlugin plugin)
        {
            plugin = null;
            if (Nova.SDK == null)
            {
                AppendFeedback("Nova.SDK 不可用。", FeedbackLevel.Error);
                return false;
            }

            if (!Nova.SDK.TryGet(out plugin) || plugin == null)
            {
                AppendFeedback("AppleSignInPlugin 不可用，请确认 Apple SDK 配置已启用并初始化完成。", FeedbackLevel.Error);
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

