/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGameLoginView.Methods.cs
 * author:    taoye
 * created:   2026/06/01
 * descrip:   Login Kit 演示 View — 私有方法
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameLogin.Samples.Runtime
{
    /// <summary>
    /// Login Kit 演示 View，展示登录、登出与删除账号 API 的调用方式。
    /// </summary>
    public sealed partial class DemoGameLoginView
    {
        /// <summary>
        /// 登录按钮点击回调，启动异步登录流程。
        /// </summary>
        private void OnLoginButtonClick()
        {
            LoginAsync().Forget();
        }

        /// <summary>
        /// 清空按钮点击回调，调用 Nova.Network.Kit<Login>().Clear() 清空本地 UID。
        /// </summary>
        private void OnClearButtonClick()
        {
            Nova.Network.Kit<Login>().Clear();
            AppendFeedback("Nova.Network.Kit<Login>().Clear() → UID 已清空", FeedbackLevel.Success);
        }

        /// <summary>
        /// 删除账号按钮点击回调，启动异步删除账号流程。
        /// </summary>
        private void OnDeleteButtonClick()
        {
            DeleteAsync().Forget();
        }

        /// <summary>
        /// 异步删除账号流程：调用 Nova.Network.Kit<Login>().DeleteAsync()，
        /// 删除当前登录账号，成功后本地 UID 自动清空；按响应 IsSuccess 打印 Success/Error 级别反馈。
        /// </summary>
        private async UniTaskVoid DeleteAsync()
        {
            AppendFeedback("Nova.Network.Kit<Login>().DeleteAsync() → 请求中...");
            NetResponse<PbNetDeleteResp> resp = await Nova.Network.Kit<Login>().DeleteAsync();
            if (resp.IsSuccess)
            {
                AppendFeedback("Nova.Network.Kit<Login>().DeleteAsync() → IsSuccess=true, 账号已删除，本地 UID 已清空", FeedbackLevel.Success);
            }
            else
            {
                AppendFeedback($"Nova.Network.Kit<Login>().DeleteAsync() → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 异步登录流程：读取 uid、openId 与 forceNewAccount 参数，调用 Login.Async，
        /// 按响应 IsSuccess 打印 Success/Error 级别反馈并附 UID 或错误信息。
        /// </summary>
        private async UniTaskVoid LoginAsync()
        {
            bool requestedForceNewAccount = m_ForceNewAccountToggle != null && m_ForceNewAccountToggle.isOn;
            ResolveLoginParameters(
                m_UidInput != null ? m_UidInput.text : null,
                requestedForceNewAccount,
                out string uid,
                out bool forceNewAccount);

            // openId 以页面输入为准：有则用输入值，为空则传空串（游客/设备登录），不做默认回退
            string openId = (m_OpenIdInput != null && !string.IsNullOrWhiteSpace(m_OpenIdInput.text))
                ? m_OpenIdInput.text.Trim()
                : string.Empty;

            AppendFeedback($"Nova.Network.Kit<Login>().Async(\"{uid}\", \"{openId}\", forceNewAccount={forceNewAccount}) → 请求中...");

            NetResponse<PbNetLoginResp> resp = await Nova.Network.Kit<Login>().Async(uid, openId, forceNewAccount);

            if (resp.IsSuccess)
            {
                string responseUid = resp.Data != null ? resp.Data.Uid : string.Empty;
                AppendFeedback($"Nova.Network.Kit<Login>().Async(\"{uid}\", \"{openId}\", {forceNewAccount}) → IsSuccess=true, UID={responseUid}", FeedbackLevel.Success);
            }
            else
            {
                AppendFeedback($"Nova.Network.Kit<Login>().Async(\"{uid}\", \"{openId}\", {forceNewAccount}) → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 归一化登录 UID，并保证显式 UID 优先于强制新账号。
        /// </summary>
        private static void ResolveLoginParameters(
            string rawUid,
            bool requestedForceNewAccount,
            out string uid,
            out bool forceNewAccount)
        {
            uid = string.IsNullOrWhiteSpace(rawUid) ? string.Empty : rawUid.Trim();
            forceNewAccount = string.IsNullOrEmpty(uid) && requestedForceNewAccount;
        }
    }
}
