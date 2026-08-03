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
using NovaFramework.Kit.Network.GameBind.Runtime;
using NovaFramework.Kit.Network.GameLogin.Runtime;
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

        private void OnBindButtonClick()
        {
            BindGoogleAccountAsync().Forget();
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

            m_CurrentGoogleId = userData.UserId;
            AppendFeedback($"用户 ID：{Fallback(userData.UserId)}");
            AppendFeedback($"邮箱：{Fallback(userData.Email)}");
            AppendFeedback($"昵称：{Fallback(userData.DisplayName)}");
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
                if (result == null || !result.Success)
                {
                    AppendFeedback($"Google 登录失败：{result?.ErrorMessage ?? "null"}", FeedbackLevel.Error);
                    return;
                }

                m_CurrentGoogleId = result.UserId;
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

        private async UniTaskVoid BindGoogleAccountAsync()
        {
            if (string.IsNullOrEmpty(m_CurrentGoogleId))
            {
                AppendFeedback("请先完成 Google 登录，绑定流程会使用登录成功后缓存的 googleID。", FeedbackLevel.Warn);
                return;
            }

            if (!await EnsureGameLoginAsync())
            {
                return;
            }

            try
            {
                AppendFeedback($"正在请求 Nova.Network.Kit<Bind>().BindAsync(Google, \"{m_CurrentGoogleId}\")...");
                NetResponse<PbNetBindResp> resp = await Nova.Network.Kit<Bind>().BindAsync(ThirdLoginProvider.Google, m_CurrentGoogleId);
                if (resp.IsSuccess)
                {
                    AppendFeedback("Google 账号绑定成功。", FeedbackLevel.Success);
                }
                else if (resp.ErrorCode == BindErrorCode.ErrBindConflict)
                {
                    string existingUid = resp.Data != null ? resp.Data.ExistingUid : string.Empty;
                    AppendFeedback("Google 账号绑定冲突：existing_uid=" + existingUid, FeedbackLevel.Warn);
                    AppendFeedback("业务层应继续调用 QueryConflictAsync + ResolveAsync，让玩家选择保留游客账号或使用已有账号。", FeedbackLevel.Info);
                }
                else
                {
                    AppendFeedback("Google 账号绑定失败：ErrorCode=" + resp.ErrorCode + ", ErrorMessage=" + resp.ErrorMessage, FeedbackLevel.Error);
                }
            }
            catch (Exception ex)
            {
                AppendFeedback("Google 账号绑定异常：" + ex.Message, FeedbackLevel.Error);
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
            if (!TryGetGoogleSignInPlugin(out GoogleSignInPlugin plugin))
            {
                return;
            }

            try
            {
                await plugin.LogoutAsync(CancellationToken.None);
                m_CurrentGoogleId = null;
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
                AppendFeedback("GoogleSignInPlugin 不可用，请确认 Google SDK 配置已启用并完成初始化。", FeedbackLevel.Error);
                return false;
            }

            m_Plugin = plugin;
            return true;
        }

        private void ClearPluginReference()
        {
            m_Plugin = null;
            m_CurrentGoogleId = null;
        }

        private static string Fallback(string value)
        {
            return string.IsNullOrEmpty(value) ? "未返回" : value;
        }
    }
}
