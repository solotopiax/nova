using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameBind.Runtime;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Runtime;
using NovaFramework.SDK.Facebook;
using UnityEngine;

namespace NovaFramework.Sdk.Facebook.Samples.Runtime
{
    public sealed partial class DemoFacebookView
    {
        /// <summary>
        /// 将预制体按钮连接到对应 SDK 操作，并在每一行展示使用到的公开接口。
        /// </summary>
        private void RegisterButtonHandlers()
        {
            if (m_LoginButton != null)
            {
                m_LoginButton.onClick.AddListener(() => LoginAsync("用户登录").Forget());
                SetButtonApiHint(m_LoginButton, "FacebookPlugin.LoginAsync(\"Facebook\")");
            }

            if (m_BindButton != null)
            {
                m_BindButton.onClick.AddListener(() => BindFacebookAccountAsync().Forget());
                SetButtonApiHint(m_BindButton, "facebookID -> Login.Async(...) -> Bind.BindAsync(Facebook, facebookID)");
            }

            if (m_UnbindButton != null)
            {
                m_UnbindButton.onClick.AddListener(() => UnbindAsync().Forget());
                SetButtonApiHint(m_UnbindButton, "FacebookPlugin.LogoutAsync()");
            }

            if (m_ShareButton != null)
            {
                m_ShareButton.onClick.AddListener(() => ShareAsync().Forget());
                SetButtonApiHint(m_ShareButton, "FacebookPlugin.Share.ShareLinkAsync(request)");
            }

            if (m_FriendsButton != null)
            {
                m_FriendsButton.onClick.AddListener(() => LoadFriendsAsync().Forget());
                SetButtonApiHint(m_FriendsButton, "EnsureFriendsPermissionAsync() / Friends.GetFriendsWithAvatarsAsync()");
            }
        }

        /// <summary>
        /// 执行 Facebook 登录；绑定按钮复用该流程，业务账号层可消费返回的用户 ID 和 Token。
        /// </summary>
        private async UniTaskVoid LoginAsync(string actionName)
        {
            await LoginFacebookAsync(actionName);
        }

        /// <summary>
        /// 执行 Facebook 登录并返回第三方授权结果；调用方可继续消费 UserId/Token。
        /// </summary>
        private async UniTask<AuthResult> LoginFacebookAsync(string actionName)
        {
            if (!TryGetFacebookPlugin(out FacebookPlugin plugin))
            {
                return null;
            }

            try
            {
                AppendFeedback(actionName + "请求中...");
                AuthResult result = await plugin.LoginAsync("Facebook");
                if (result == null || !result.Success)
                {
                    AppendFeedback(actionName + "失败：" + (result?.ErrorMessage ?? "null"), FeedbackLevel.Error);
                    return result;
                }

                AppendFeedback(actionName + "成功：fbID=" + result.UserId, FeedbackLevel.Success);
                m_CurrentFacebookId = result.UserId;
                await RefreshProfileAsync(plugin, result.UserId);
                return result;
            }
            catch (OperationCanceledException)
            {
                AppendFeedback(actionName + "已取消。", FeedbackLevel.Warn);
                return null;
            }
            catch (Exception ex)
            {
                AppendFeedback(actionName + "异常：" + ex.Message, FeedbackLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Facebook 授权成功后，确保当前已有游戏账号登录态，再自动调用 GameBind 绑定服务。
        /// </summary>
        private async UniTaskVoid BindFacebookAccountAsync()
        {
            if (string.IsNullOrEmpty(m_CurrentFacebookId))
            {
                AppendFeedback("请先完成 Facebook 登录，绑定流程会使用登录成功后缓存的 facebookID。", FeedbackLevel.Warn);
                return;
            }

            if (!await EnsureGameLoginAsync())
            {
                return;
            }

            try
            {
                AppendFeedback("正在请求 Nova.Network.Kit<Bind>().BindAsync(Facebook, \"" + m_CurrentFacebookId + "\")...");
                NetResponse<PbNetBindResp> resp = await Nova.Network.Kit<Bind>().BindAsync((int)PbNetChannel.Facebook, m_CurrentFacebookId);
                if (resp.IsSuccess)
                {
                    AppendFeedback("Facebook 账号绑定成功。", FeedbackLevel.Success);
                }
                else if (resp.ErrorCode == BindErrorCode.ErrBindConflict)
                {
                    string existingUid = resp.Data != null ? resp.Data.ExistingUid : string.Empty;
                    AppendFeedback("Facebook 账号绑定冲突：existing_uid=" + existingUid, FeedbackLevel.Warn);
                    AppendFeedback("业务层应继续调用 QueryConflictAsync + ResolveAsync，让玩家选择保留游客账号或使用已有账号。", FeedbackLevel.Info);
                }
                else
                {
                    AppendFeedback("Facebook 账号绑定失败：ErrorCode=" + resp.ErrorCode + ", ErrorMessage=" + resp.ErrorMessage, FeedbackLevel.Error);
                }
            }
            catch (Exception ex)
            {
                AppendFeedback("Facebook 账号绑定异常：" + ex.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// GameBind 依赖 Header.Uid；若尚未登录游戏账号，先走设备/游客登录获取 UID。
        /// </summary>
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

        /// <summary>
        /// 退出 Facebook 会话，并清理当前示例展示的个人信息和好友列表状态。
        /// </summary>
        private async UniTaskVoid UnbindAsync()
        {
            if (!TryGetFacebookPlugin(out FacebookPlugin plugin))
            {
                return;
            }

            try
            {
                await plugin.LogoutAsync();
                m_CurrentFacebookId = null;
                RefreshProfile(null);
                if (m_FriendsListText != null)
                {
                    m_FriendsListText.text = "暂无好友数据";
                }

                AppendFeedback("已解绑/登出当前 Facebook 会话。", FeedbackLevel.Success);
            }
            catch (Exception ex)
            {
                AppendFeedback("解绑异常：" + ex.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 使用固定示例链接演示 Facebook 分享能力。
        /// </summary>
        private async UniTaskVoid ShareAsync()
        {
            if (!TryGetFacebookPlugin(out FacebookPlugin plugin))
            {
                return;
            }

            if (plugin.Share == null)
            {
                AppendFeedback("Facebook 分享服务不可用。", FeedbackLevel.Error);
                return;
            }

            try
            {
                AppendFeedback("正在打开 Facebook 分享...");
                FacebookShareResult result = await plugin.Share.ShareLinkAsync(new FacebookShareRequest
                {
                    ContentUrl = new Uri("https://solotopia.com"),
                    Title = "Nova Facebook Demo",
                    Description = "Nova Framework Facebook SDK sample."
                });

                if (result == null || !result.Success)
                {
                    AppendFeedback("分享未完成：已取消=" + (result?.Cancelled ?? false) + "，错误=" + (result?.ErrorMessage ?? "null"), FeedbackLevel.Warn);
                    return;
                }

                AppendFeedback("分享完成。", FeedbackLevel.Success);
            }
            catch (Exception ex)
            {
                AppendFeedback("分享异常：" + ex.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 先请求 user_friends 权限，再读取同样授权过当前应用的好友列表。
        /// </summary>
        private async UniTaskVoid LoadFriendsAsync()
        {
            if (!TryGetFacebookPlugin(out FacebookPlugin plugin))
            {
                return;
            }

            if (plugin.Friends == null)
            {
                AppendFeedback("Facebook 好友服务不可用。", FeedbackLevel.Error);
                return;
            }

            try
            {
                AppendFeedback("正在请求 user_friends 权限并读取好友列表...");
                AuthResult permission = await plugin.EnsureFriendsPermissionAsync();
                if (permission == null || !permission.Success)
                {
                    AppendFeedback("好友权限获取失败：" + (permission?.ErrorMessage ?? "null"), FeedbackLevel.Error);
                    return;
                }

                IReadOnlyList<FacebookFriend> friends = await plugin.Friends.GetFriendsWithAvatarsAsync();
                RefreshFriends(friends);
                AppendFeedback("好友列表读取完成，数量=" + (friends?.Count ?? 0), FeedbackLevel.Success);
            }
            catch (Exception ex)
            {
                AppendFeedback("好友列表读取异常：" + ex.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 获取当前 Facebook 个人信息，并交由 RefreshProfile 统一刷新界面。
        /// </summary>
        private async UniTask RefreshProfileAsync(FacebookPlugin plugin, string fallbackFacebookId = null)
        {
            if (plugin == null || plugin.Profile == null)
            {
                RefreshProfile(null, fallbackFacebookId);
                return;
            }

            FacebookProfile profile = await plugin.Profile.GetCurrentProfileAsync();
            RefreshProfile(profile, fallbackFacebookId);
        }

        /// <summary>
        /// 刷新个人信息文本，并在资料可用时替换头像纹理。
        /// </summary>
        private void RefreshProfile(FacebookProfile profile, string fallbackFacebookId = null)
        {
            string facebookId = string.IsNullOrEmpty(profile?.FacebookId) ? fallbackFacebookId : profile.FacebookId;

            if (m_NameValueText != null)
            {
                m_NameValueText.text = string.IsNullOrEmpty(profile?.Name)
                    ? (string.IsNullOrEmpty(facebookId) ? "未登录" : "未获取昵称")
                    : profile.Name;
            }

            if (m_FacebookIdValueText != null)
            {
                m_FacebookIdValueText.text = string.IsNullOrEmpty(facebookId) ? "-" : facebookId;
            }

            if (m_AvatarImage != null)
            {
                if (m_AvatarTexture != null)
                {
                    Destroy(m_AvatarTexture);
                    m_AvatarTexture = null;
                }

                m_AvatarImage.texture = null;
                m_AvatarImage.color = new Color(1f, 1f, 1f, profile == null ? 0.24f : 1f);
                if (!string.IsNullOrEmpty(profile?.AvatarPath))
                {
                    LoadAvatarTexture(profile.AvatarPath);
                }
            }
        }

        /// <summary>
        /// 将返回的好友列表渲染为简单文本，便于示例界面直接查看。
        /// </summary>
        private void RefreshFriends(IReadOnlyList<FacebookFriend> friends)
        {
            if (m_FriendsListText == null)
            {
                return;
            }

            if (friends == null || friends.Count == 0)
            {
                m_FriendsListText.text = "暂无好友数据。Facebook 只返回同样授权过本 App 的好友。";
                return;
            }

            var lines = new List<string>();
            for (int i = 0; i < friends.Count; i++)
            {
                FacebookFriend friend = friends[i];
                lines.Add((i + 1) + ". " + friend.Name + " / " + friend.FacebookId + "\n头像：" + (string.IsNullOrEmpty(friend.AvatarPath) ? friend.AvatarUrl : friend.AvatarPath));
            }

            m_FriendsListText.text = string.Join("\n", lines);
        }

        /// <summary>
        /// 从本地磁盘读取已下载的头像图片，并显示到 RawImage。
        /// </summary>
        private void LoadAvatarTexture(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                return;
            }

            byte[] bytes = System.IO.File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return;
            }

            m_AvatarTexture = texture;
            m_AvatarImage.texture = texture;
        }

        /// <summary>
        /// 获取已注册的 Facebook 插件，并将配置或初始化失败信息输出到反馈区域。
        /// </summary>
        private bool TryGetFacebookPlugin(out FacebookPlugin plugin)
        {
            plugin = null;
            if (Nova.SDK == null)
            {
                AppendFeedback("Nova.SDK 不可用。", FeedbackLevel.Error);
                return false;
            }

            if (!Nova.SDK.TryGet(out plugin) || plugin == null)
            {
                AppendFeedback("FacebookPlugin 不可用，请确认 SDK 配置已启用并初始化完成。", FeedbackLevel.Error);
                return false;
            }

            return true;
        }
    }
}
