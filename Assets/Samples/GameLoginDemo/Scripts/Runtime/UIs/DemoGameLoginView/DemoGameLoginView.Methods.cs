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
        /// 默认测试用 openId，当输入框为空时使用，对应测试服匿名访客标识。
        /// </summary>
        private const string c_DefaultOpenId = "test_openid_guest";

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
        /// 异步登录流程：读取 openId 与 forceNewAccount 参数，调用 Login.Async，
        /// 按响应 IsSuccess 打印 Success/Error 级别反馈并附 UID 或错误信息。
        /// </summary>
        private async UniTaskVoid LoginAsync()
        {
            string openId = (m_OpenIdInput != null && !string.IsNullOrWhiteSpace(m_OpenIdInput.text))
                ? m_OpenIdInput.text.Trim()
                : c_DefaultOpenId;

            bool forceNewAccount = m_ForceNewAccountToggle != null && m_ForceNewAccountToggle.isOn;

            AppendFeedback($"Nova.Network.Kit<Login>().Async(string.Empty, \"{openId}\", forceNewAccount={forceNewAccount}) → 请求中...");

            NetResponse<PbNetLoginResp> resp = await Nova.Network.Kit<Login>().Async(string.Empty, openId, forceNewAccount);

            if (resp.IsSuccess)
            {
                string uid = resp.Data != null ? resp.Data.Uid : string.Empty;
                AppendFeedback($"Nova.Network.Kit<Login>().Async(string.Empty, \"{openId}\", {forceNewAccount}) → IsSuccess=true, UID={uid}", FeedbackLevel.Success);
            }
            else
            {
                AppendFeedback($"Nova.Network.Kit<Login>().Async(string.Empty, \"{openId}\", {forceNewAccount}) → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
                // 命中绑定冲突：提示读取摘要后走 BindResolve 二选一
                if (resp.ErrorCode == LoginErrorCode.ErrBindConflict && resp.Data != null)
                {
                    string guestInfo = FormatBindSummary("guest", resp.Data.GuestSummary);
                    string existingInfo = FormatBindSummary("existing", resp.Data.ExistingSummary);
                    AppendFeedback($"→ ErrBindConflict(10402) 需二选一：{guestInfo} | {existingInfo}", FeedbackLevel.Warn);
                    AppendFeedback("→ 调整 Choice 开关后点击 BindResolveButton 发起二选一", FeedbackLevel.Info);
                }
            }
        }

        /// <summary>
        /// 绑定冲突二选一按钮点击回调，启动异步二选一流。
        /// </summary>
        private void OnBindResolveButtonClick()
        {
            BindResolveAsync().Forget();
        }

        /// <summary>
        /// 异步绑定冲突二选一流：读取 openId / choice / verifyCode，调用 Login.BindResolveAsync，
        /// provider 写死 (int)PbNetChannel.Google(2)；按响应 IsSuccess 打印 Success/Error 级别反馈并附最终 UID 或错误信息。
        /// </summary>
        private async UniTaskVoid BindResolveAsync()
        {
            string openId = (m_OpenIdInput != null && !string.IsNullOrWhiteSpace(m_OpenIdInput.text))
                ? m_OpenIdInput.text.Trim()
                : c_DefaultOpenId;

            // 勾选=existing（保留云端进度），不勾=guest（保留当前进度）
            string choice = (m_BindChoiceToggle != null && m_BindChoiceToggle.isOn) ? "existing" : "guest";
            string verifyCode = (m_VerifyCodeInput != null && !string.IsNullOrWhiteSpace(m_VerifyCodeInput.text))
                ? m_VerifyCodeInput.text.Trim()
                : string.Empty;

            AppendFeedback($"Nova.Network.Kit<Login>().BindResolveAsync(Google, \"{openId}\", choice=\"{choice}\", verifyCode=\"{verifyCode}\") → 请求中...");

            NetResponse<PbNetBindResolveResp> resp = await Nova.Network.Kit<Login>().BindResolveAsync(
                (int)PbNetChannel.Google, openId, choice, verifyCode);

            if (resp.IsSuccess)
            {
                string finalUid = Nova.Network.Kit<Login>().UID;
                string abandonedUid = resp.Data != null ? resp.Data.AbandonedUid : string.Empty;
                AppendFeedback($"Nova.Network.Kit<Login>().BindResolveAsync(...) → IsSuccess=true, 最终 UID={finalUid}, abandoned_uid={abandonedUid}", FeedbackLevel.Success);
            }
            else
            {
                AppendFeedback($"Nova.Network.Kit<Login>().BindResolveAsync(...) → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 格式化 BindSummary 为可读字符串，用于二选一反馈展示。
        /// </summary>
        /// <param name="role">账号角色（guest / existing）。</param>
        /// <param name="summary">账号进度摘要，可为 null。</param>
        /// <returns>可读摘要字符串。</returns>
        private static string FormatBindSummary(string role, BindSummary summary)
        {
            if (summary == null)
            {
                return $"{role}=(无摘要)";
            }
            return $"{role}: uid={summary.Uid}, lv={summary.Level}, vip={summary.VipLevel}, gold={summary.Gold}, diamond={summary.Diamond}, exp={summary.Exp}";
        }
    }
}
