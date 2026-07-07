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
using NovaFramework.Kit.Network.GameBind.Runtime;
using NovaFramework.Kit.Network.GameLogin.Runtime;
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

        private void OnBindButtonClick()
        {
            BindAppleAccountAsync().Forget();
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

            m_CurrentAppleId = userData.UserId;
            AppendFeedback($"用户 ID：{Fallback(userData.UserId)}");
            AppendFeedback($"全名：{Fallback(userData.FullName)}");
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
                if (result == null || !result.Success)
                {
                    AppendFeedback($"Apple 登录失败：{result?.ErrorMessage ?? "null"}", FeedbackLevel.Error);
                    return;
                }

                m_CurrentAppleId = result.UserId;
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

        private async UniTaskVoid BindAppleAccountAsync()
        {
            if (string.IsNullOrEmpty(m_CurrentAppleId))
            {
                AppendFeedback("请先完成 Apple 登录，绑定流程会使用登录成功后缓存的 appleID。", FeedbackLevel.Warn);
                return;
            }

            if (!await EnsureGameLoginAsync())
            {
                return;
            }

            try
            {
                AppendFeedback($"正在请求 Nova.Network.Kit<Bind>().BindAsync(Apple, \"{m_CurrentAppleId}\")...");
                NetResponse<PbNetBindResp> resp = await Nova.Network.Kit<Bind>().BindAsync((int)PbNetChannel.Apple, m_CurrentAppleId);
                if (resp.IsSuccess)
                {
                    AppendFeedback("Apple 账号绑定成功。", FeedbackLevel.Success);
                }
                else if (resp.ErrorCode == BindErrorCode.ErrBindConflict)
                {
                    string existingUid = resp.Data != null ? resp.Data.ExistingUid : string.Empty;
                    AppendFeedback("Apple 账号绑定冲突：existing_uid=" + existingUid, FeedbackLevel.Warn);
                    AppendFeedback("业务层应继续调用 QueryConflictAsync + ResolveAsync，让玩家选择保留游客账号或使用已有账号。", FeedbackLevel.Info);
                }
                else
                {
                    AppendFeedback("Apple 账号绑定失败：ErrorCode=" + resp.ErrorCode + ", ErrorMessage=" + resp.ErrorMessage, FeedbackLevel.Error);
                }
            }
            catch (Exception ex)
            {
                AppendFeedback("Apple 账号绑定异常：" + ex.Message, FeedbackLevel.Error);
            }
        }

        private async UniTask<bool> EnsureGameLoginAsync()
        {
            Login login = Nova.Network.Kit<Login>();
            if (login.IsLoggedIn)
            {
                return true;
            }

            AppendFeedback("当前没有游戏账号登录态，先执行 Nova.Network.Kit<Login>().Async(string.Empty, string.Empty, false)...");
            NetResponse<PbNetLoginResp> resp = await login.Async(string.Empty, string.Empty, false);
            if (resp.IsSuccess)
            {
                string uid = resp.Data != null ? resp.Data.Uid : string.Empty;
                AppendFeedback("游戏账号登录成功：UID=" + uid, FeedbackLevel.Success);
                return true;
            }

            AppendFeedback("游戏账号登录失败：ErrorCode=" + resp.ErrorCode + ", ErrorMessage=" + resp.ErrorMessage, FeedbackLevel.Error);
            return false;
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
                m_CurrentAppleId = null;
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
                AppendFeedback("AppleSignInPlugin 不可用，请确认 Apple SDK 配置已启用并完成初始化。", FeedbackLevel.Error);
                return false;
            }

            m_Plugin = plugin;
            return true;
        }

        private void ClearPluginReference()
        {
            m_Plugin = null;
            m_CurrentAppleId = null;
        }

        private static string Fallback(string value)
        {
            return string.IsNullOrEmpty(value) ? "未返回" : value;
        }
    }
}
