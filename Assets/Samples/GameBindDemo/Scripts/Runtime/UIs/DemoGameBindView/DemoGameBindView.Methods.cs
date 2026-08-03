/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGameBindView.Methods.cs
 * author:    taoye
 * created:   2026/07/02
 * descrip:   GameBind Kit 演示 View — 私有方法
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameBind.Runtime;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Kit.Network.GameSave.Runtime;
using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameBind.Samples.Runtime
{
    /// <summary>
    /// GameBind Kit 演示 View，展示绑定、冲突查询、裁决三段 API 的调用方式。
    /// </summary>
    public sealed partial class DemoGameBindView
    {
        /// <summary>
        /// 登录按钮点击回调，启动异步登录流程（绑定前提）。
        /// </summary>
        private void OnLoginButtonClick()
        {
            LoginAsync().Forget();
        }

        /// <summary>
        /// 绑定按钮点击回调，启动异步绑定流程。
        /// </summary>
        private void OnBindButtonClick()
        {
            BindAsync().Forget();
        }

        /// <summary>
        /// 上传存档按钮点击回调，启动异步全量上传根存档流程。
        /// </summary>
        private void OnUploadSaveButtonClick()
        {
            UploadSaveAsync().Forget();
        }

        /// <summary>
        /// 获取存档按钮点击回调，启动异步全量拉取当前用户根存档流程。
        /// </summary>
        private void OnGetSaveButtonClick()
        {
            GetSaveAsync().Forget();
        }

        /// <summary>
        /// 冲突查询按钮点击回调，启动异步冲突详情查询流程。
        /// </summary>
        private void OnQueryConflictButtonClick()
        {
            QueryConflictAsync().Forget();
        }

        /// <summary>
        /// 查询指定 uid 存档按钮点击回调，启动异步拉取指定用户存档流程。
        /// </summary>
        private void OnQuerySaveByUidButtonClick()
        {
            QuerySaveByUidAsync().Forget();
        }

        /// <summary>
        /// 裁决按钮点击回调，启动异步二选一裁决流程。
        /// </summary>
        private void OnResolveButtonClick()
        {
            ResolveAsync().Forget();
        }

        /// <summary>
        /// 异步全量上传根存档流程：读取 JSON 输入框，调用 Nova.Network.Kit<Save>().SetFullAsync(json) 上传用户根存档。
        /// 身份靠 Header.Uid（当前登录态）识别，故需先登录；成功后 existing 账号即可被冲突查询关联到进度摘要。
        /// </summary>
        private async UniTaskVoid UploadSaveAsync()
        {
            string json = (m_SaveJsonInput != null && m_SaveJsonInput.text != null)
                ? m_SaveJsonInput.text
                : string.Empty;

            AppendFeedback($"Nova.Network.Kit<Save>().SetFullAsync(\"{json}\") → 请求中...");
            NetResponse<PbNetSetGameDataResp> resp = await Nova.Network.Kit<Save>().SetFullAsync(json);
            if (resp.IsSuccess)
            {
                int effect = resp.Data != null ? resp.Data.Effect : 0;
                AppendFeedback($"SetFullAsync → IsSuccess=true, effect={effect}，根存档已上传", FeedbackLevel.Success);
            }
            else
            {
                AppendFeedback($"SetFullAsync → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 异步全量拉取当前用户根存档流程：调用 Nova.Network.Kit<Save>().GetFullAsync()，把返回的存档节点打印到日志。
        /// </summary>
        private async UniTaskVoid GetSaveAsync()
        {
            AppendFeedback("Nova.Network.Kit<Save>().GetFullAsync() → 请求中...");
            NetResponse<PbNetGetGameDataResp> resp = await Nova.Network.Kit<Save>().GetFullAsync();
            LogGetSaveResult("GetFullAsync", resp);
        }

        /// <summary>
        /// 异步拉取指定 uid 存档流程：读取目标 uid 输入框，调用 Nova.Network.Kit<Save>().GetFullAsync(targetUid)，把返回的存档节点打印到日志。
        /// target_uid 为空时等价于查当前用户；跨用户查询由服务端做权限校验。
        /// </summary>
        private async UniTaskVoid QuerySaveByUidAsync()
        {
            string targetUid = (m_TargetUidInput != null && !string.IsNullOrWhiteSpace(m_TargetUidInput.text))
                ? m_TargetUidInput.text.Trim()
                : string.Empty;

            AppendFeedback($"Nova.Network.Kit<Save>().GetFullAsync(\"{targetUid}\") → 请求中...");
            NetResponse<PbNetGetGameDataResp> resp = await Nova.Network.Kit<Save>().GetFullAsync(targetUid);
            LogGetSaveResult($"GetFullAsync(\"{targetUid}\")", resp);
        }

        /// <summary>
        /// 打印获取存档结果到日志：成功时逐条列出存档节点的 key/value 及元数据，失败时打错误码。
        /// </summary>
        /// <param name="label">调用标识，用于日志前缀。</param>
        /// <param name="resp">获取存档响应。</param>
        private void LogGetSaveResult(string label, NetResponse<PbNetGetGameDataResp> resp)
        {
            if (resp.IsSuccess)
            {
                var data = resp.Data;
                int count = data != null && data.Datas != null ? data.Datas.Count : 0;
                AppendFeedback($"{label} → IsSuccess=true, 节点数={count}, last_timestamp={(data != null ? data.LastTimestamp : 0)}", FeedbackLevel.Success);
                if (data != null && data.Datas != null)
                {
                    foreach (var node in data.Datas)
                    {
                        AppendFeedback($"  [{node.Key}] = {node.Value}", FeedbackLevel.Info);
                    }
                }
            }
            else
            {
                AppendFeedback($"{label} → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 读取输入框 openId，以页面输入为准：有则用输入值，为空则返回空串，不做默认回退。
        /// </summary>
        /// <returns>用于绑定流程的三方 openId。</returns>
        private string ReadOpenId()
        {
            return (m_OpenIdInput != null && !string.IsNullOrWhiteSpace(m_OpenIdInput.text))
                ? m_OpenIdInput.text.Trim()
                : string.Empty;
        }

        /// <summary>
        /// 异步登录流程：绑定的前提是已登录。调用 Nova.Network.Kit<Login>().Async 取得当前账号 UID。
        /// </summary>
        private async UniTaskVoid LoginAsync()
        {
            // openId 以页面输入为准：有值则传入登录，为空则走游客/设备登录
            string openId = ReadOpenId();
            bool forceNewAccount = m_ForceNewAccountToggle != null && m_ForceNewAccountToggle.isOn;

            AppendFeedback($"Nova.Network.Kit<Login>().Async(string.Empty, \"{openId}\", forceNewAccount={forceNewAccount}) → 请求中...");
            NetResponse<PbNetLoginResp> resp = await Nova.Network.Kit<Login>().Async(string.Empty, openId, forceNewAccount);
            if (resp.IsSuccess)
            {
                string uid = resp.Data != null ? resp.Data.Uid : string.Empty;
                AppendFeedback($"登录成功，当前账号 UID={uid}，可继续绑定三方号", FeedbackLevel.Success);
            }
            else
            {
                AppendFeedback($"登录失败，ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 异步绑定流程：调用 Nova.Network.Kit<Bind>().BindAsync，provider 演示写死 Google。
        /// 命中 ErrBindConflict(10402) 时提示走冲突查询 + 裁决二选一。
        /// </summary>
        private async UniTaskVoid BindAsync()
        {
            string openId = ReadOpenId();
            AppendFeedback($"Nova.Network.Kit<Bind>().BindAsync(Google, \"{openId}\") → 请求中...");

            NetResponse<PbNetBindResp> resp = await Nova.Network.Kit<Bind>().BindAsync(
                ThirdLoginProvider.Google, openId);

            if (resp.IsSuccess)
            {
                AppendFeedback("BindAsync → IsSuccess=true, 绑定成功", FeedbackLevel.Success);
            }
            else if (resp.ErrorCode == BindErrorCode.ErrBindConflict)
            {
                string existingUid = resp.Data != null ? resp.Data.ExistingUid : string.Empty;
                AppendFeedback($"→ ErrBindConflict(10402) 冲突，existing_uid={existingUid}", FeedbackLevel.Warn);
                AppendFeedback("→ 点击 QueryConflictButton 拉对方账号进度摘要，再调整 Choice 开关点击 ResolveButton 裁决", FeedbackLevel.Info);
            }
            else
            {
                AppendFeedback($"BindAsync → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 异步冲突查询流程：调用 Nova.Network.Kit<Bind>().QueryConflictAsync 拉对方账号进度摘要。
        /// guest 侧进度由业务侧从本地存档取，此处仅演示 existing 侧服务端返回。
        /// </summary>
        private async UniTaskVoid QueryConflictAsync()
        {
            string openId = ReadOpenId();
            AppendFeedback($"Nova.Network.Kit<Bind>().QueryConflictAsync(\"{openId}\") → 请求中...");

            NetResponse<PbNetBindConflictResp> resp = await Nova.Network.Kit<Bind>().QueryConflictAsync(openId);

            if (resp.IsSuccess)
            {
                BindSummary existing = resp.Data != null ? resp.Data.ExistingSummary : null;
                AppendFeedback($"QueryConflictAsync → IsSuccess=true, {FormatSummary("existing", existing)}", FeedbackLevel.Success);
                AppendFeedback("→ 展示 guest（本地存档取）与 existing 进度供玩家二选一", FeedbackLevel.Info);
            }
            else
            {
                AppendFeedback($"QueryConflictAsync → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 异步裁决流程：读取 choice / verifyCode，调用 Nova.Network.Kit<Bind>().ResolveAsync 做账号归属裁决。
        /// 裁决只返回账号归属；数据覆盖方向由 choice 决定，需业务层配合 GameSave 编排（此处仅日志提示）。
        /// </summary>
        private async UniTaskVoid ResolveAsync()
        {
            string openId = ReadOpenId();
            // 勾选=existing（保留云端进度），不勾=guest（保留当前进度）
            string choice = (m_ChoiceToggle != null && m_ChoiceToggle.isOn) ? "existing" : "guest";
            string verifyCode = (m_VerifyCodeInput != null && !string.IsNullOrWhiteSpace(m_VerifyCodeInput.text))
                ? m_VerifyCodeInput.text.Trim()
                : string.Empty;

            AppendFeedback($"Nova.Network.Kit<Bind>().ResolveAsync(\"{openId}\", choice=\"{choice}\", verifyCode=\"{verifyCode}\") → 请求中...");

            NetResponse<PbNetBindResolveResp> resp = await Nova.Network.Kit<Bind>().ResolveAsync(openId, choice, verifyCode);

            if (resp.IsSuccess)
            {
                string finalUid = resp.Data != null ? resp.Data.FinalUid : string.Empty;
                string abandonedUid = resp.Data != null ? resp.Data.AbandonedUid : string.Empty;
                AppendFeedback($"ResolveAsync → IsSuccess=true, final_uid={finalUid}, abandoned_uid={abandonedUid}", FeedbackLevel.Success);
                if (choice == "guest")
                {
                    AppendFeedback("→ 保留本地进度：业务层调 Nova.Network.Kit<Save>().SetFullAsync(localPayload) 本地覆盖云端", FeedbackLevel.Info);
                }
                else
                {
                    AppendFeedback("→ 保留云端进度：业务层切登录态到 final_uid 后调 Nova.Network.Kit<Save>().GetFullAsync() 云端覆盖本地", FeedbackLevel.Info);
                }
            }
            else if (resp.ErrorCode == BindErrorCode.ErrBindBusy || resp.ErrorCode == BindErrorCode.ErrBindConflict)
            {
                AppendFeedback($"ResolveAsync → 操作繁忙/归属复核变化（ErrorCode={resp.ErrorCode}），稍后原样重试即可", FeedbackLevel.Warn);
            }
            else
            {
                AppendFeedback($"ResolveAsync → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 格式化 BindSummary 为可读字符串，用于冲突详情反馈展示。
        /// </summary>
        /// <param name="role">账号角色（guest / existing）。</param>
        /// <param name="summary">账号进度摘要，可为 null。</param>
        /// <returns>可读摘要字符串。</returns>
        private static string FormatSummary(string role, BindSummary summary)
        {
            if (summary == null)
            {
                return $"{role}=(无摘要)";
            }
            return $"{role}: uid={summary.Uid}, timestamp={summary.Timestamp}";
        }
    }
}
