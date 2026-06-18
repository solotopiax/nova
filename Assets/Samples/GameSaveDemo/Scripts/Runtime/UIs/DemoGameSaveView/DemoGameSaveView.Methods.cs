/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoGameSaveView.Methods.cs
 * author:    nova-create-sample
 * created:   2026/06/01
 * descrip:   GameSave Kit 演示 View — 私有方法
 *            演示 GameSave 存档读写 6 个公开接口，先登录取得 UID 再调用。
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Kit.Network.GameSave.Runtime;
using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameSave.Samples.Runtime
{
    /// <summary>
    /// GameSave Kit 演示 View 的私有方法。
    /// </summary>
    public sealed partial class DemoGameSaveView
    {
        /// <summary>
        /// 默认测试用 openId，当输入框为空时使用。
        /// </summary>
        private const string c_DefaultOpenId = "gamesave_demo_user";

        /// <summary>
        /// 默认存档 key，当输入框为空时使用。
        /// </summary>
        private const string c_DefaultKey = "player";

        /// <summary>
        /// 默认存档 value，当输入框为空时使用。
        /// </summary>
        private const string c_DefaultValue = "{\"level\":1,\"hp\":100}";

        /// <summary>
        /// 登录按钮点击回调，启动异步登录流程。
        /// </summary>
        private void OnLoginButtonClick()
        {
            LoginAsync().Forget();
        }

        /// <summary>
        /// 写入存档按钮点击回调，启动单 key 写入流程。
        /// </summary>
        private void OnSetButtonClick()
        {
            SetAsync().Forget();
        }

        /// <summary>
        /// 读取存档按钮点击回调，启动单 key 读取流程。
        /// </summary>
        private void OnGetButtonClick()
        {
            GetAsync().Forget();
        }

        /// <summary>
        /// 批量写入按钮点击回调，启动批量写入流程。
        /// </summary>
        private void OnSetBatchButtonClick()
        {
            SetBatchAsync().Forget();
        }

        /// <summary>
        /// 批量读取按钮点击回调，启动批量读取流程。
        /// </summary>
        private void OnGetBatchButtonClick()
        {
            GetBatchAsync().Forget();
        }

        /// <summary>
        /// 全量读取按钮点击回调，启动全量读取流程。
        /// </summary>
        private void OnGetFullButtonClick()
        {
            GetFullAsync().Forget();
        }

        /// <summary>
        /// 全量写入按钮点击回调，启动全量写入流程。
        /// </summary>
        private void OnSetFullButtonClick()
        {
            SetFullAsync().Forget();
        }

        /// <summary>
        /// 异步登录流程：调用 Login.Async 取得 UID，存档操作依赖该身份。
        /// </summary>
        private async UniTaskVoid LoginAsync()
        {
            string openId = ReadInput(m_OpenIdInput, c_DefaultOpenId);
            AppendFeedback($"Nova.Network.Kit<Login>().Async(\"\", \"{openId}\", false) → 登录中...");

            NetResponse<PbNetLoginResp> resp = await Nova.Network.Kit<Login>().Async(string.Empty, openId, false);

            if (resp.IsSuccess)
            {
                string uid = resp.Data != null ? resp.Data.Uid : string.Empty;
                AppendFeedback($"登录成功，UID={uid}，可继续演示存档读写。", FeedbackLevel.Success);
            }
            else
            {
                AppendFeedback($"登录失败，ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 单 key 写入存档：调用 GameSave.SetAsync(key, value)。
        /// </summary>
        private async UniTaskVoid SetAsync()
        {
            string key = ReadInput(m_KeyInput, c_DefaultKey);
            string value = ReadInput(m_ValueInput, c_DefaultValue);
            AppendFeedback($"SetAsync(\"{key}\", \"{value}\") → 写入中...");

            NetResponse<PbNetSetGameDataResp> resp = await Nova.Network.Kit<Save>().SetAsync(key, value);
            AppendSetResult("SetAsync", resp);
        }

        /// <summary>
        /// 单 key 读取存档：调用 GameSave.GetAsync(key)。
        /// </summary>
        private async UniTaskVoid GetAsync()
        {
            string key = ReadInput(m_KeyInput, c_DefaultKey);
            AppendFeedback($"GetAsync(\"{key}\") → 读取中...");

            NetResponse<PbNetGetGameDataResp> resp = await Nova.Network.Kit<Save>().GetAsync(key);
            AppendGetResult("GetAsync", resp);
        }

        /// <summary>
        /// 批量写入存档：调用 GameSave.SetAsync(keys, values) 写入固定示例数据。
        /// </summary>
        private async UniTaskVoid SetBatchAsync()
        {
            string[] keys = { "weapon", "skin" };
            string[] values = { "{\"id\":1001}", "{\"id\":2002}" };
            AppendFeedback("SetAsync([weapon, skin], [...]) → 批量写入中...");

            NetResponse<PbNetSetGameDataResp> resp = await Nova.Network.Kit<Save>().SetAsync(keys, values);
            AppendSetResult("SetAsync(批量)", resp);
        }

        /// <summary>
        /// 批量读取存档：调用 GameSave.GetAsync(keys) 读取固定示例 key。
        /// </summary>
        private async UniTaskVoid GetBatchAsync()
        {
            string[] keys = { "weapon", "skin" };
            AppendFeedback("GetAsync([weapon, skin]) → 批量读取中...");

            NetResponse<PbNetGetGameDataResp> resp = await Nova.Network.Kit<Save>().GetAsync(keys);
            AppendGetResult("GetAsync(批量)", resp);
        }

        /// <summary>
        /// 全量读取存档：调用 GameSave.GetFullAsync() 拉取全部存档节点。
        /// </summary>
        private async UniTaskVoid GetFullAsync()
        {
            AppendFeedback("GetFullAsync() → 全量读取中...");

            NetResponse<PbNetGetGameDataResp> resp = await Nova.Network.Kit<Save>().GetFullAsync();
            AppendGetResult("GetFullAsync", resp);
        }

        /// <summary>
        /// 全量写入存档：调用 GameSave.SetFullAsync(value) 以 value 作整包载荷写入。
        /// </summary>
        private async UniTaskVoid SetFullAsync()
        {
            string value = ReadInput(m_ValueInput, c_DefaultValue);
            AppendFeedback($"SetFullAsync(\"{value}\") → 全量写入中...");

            NetResponse<PbNetSetGameDataResp> resp = await Nova.Network.Kit<Save>().SetFullAsync(value);
            AppendSetResult("SetFullAsync", resp);
        }

        /// <summary>
        /// 按 Set 系响应的成功/失败分支打印反馈，成功时附受影响条数 Effect。
        /// </summary>
        /// <param name="api">触发的 API 名称，用于反馈前缀。</param>
        /// <param name="resp">Set 系接口响应。</param>
        private void AppendSetResult(string api, NetResponse<PbNetSetGameDataResp> resp)
        {
            if (resp.IsSuccess)
            {
                int effect = resp.Data != null ? resp.Data.Effect : 0;
                AppendFeedback($"{api} → IsSuccess=true, Effect={effect}", FeedbackLevel.Success);
            }
            else
            {
                AppendFeedback($"{api} → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 按 Get 系响应的成功/失败分支打印反馈，成功时逐条列出存档节点 Key=Value。
        /// </summary>
        /// <param name="api">触发的 API 名称，用于反馈前缀。</param>
        /// <param name="resp">Get 系接口响应。</param>
        private void AppendGetResult(string api, NetResponse<PbNetGetGameDataResp> resp)
        {
            if (!resp.IsSuccess)
            {
                AppendFeedback($"{api} → IsSuccess=false, ErrorCode={resp.ErrorCode}, ErrorMessage={resp.ErrorMessage}", FeedbackLevel.Error);
                return;
            }

            int count = resp.Data != null ? resp.Data.Datas.Count : 0;
            AppendFeedback($"{api} → IsSuccess=true, 节点数={count}", FeedbackLevel.Success);
            if (resp.Data == null)
            {
                return;
            }

            foreach (PbNetGameDataNode node in resp.Data.Datas)
            {
                AppendFeedback($"  {node.Key} = {node.Value}");
            }
        }

        /// <summary>
        /// 读取输入框文本，空白时回退到默认值。
        /// </summary>
        /// <param name="input">目标输入框。</param>
        /// <param name="fallback">空白时使用的默认值。</param>
        /// <returns>去除首尾空白后的输入文本或默认值。</returns>
        private string ReadInput(TMPro.TMP_InputField input, string fallback)
        {
            return (input != null && !string.IsNullOrWhiteSpace(input.text)) ? input.text.Trim() : fallback;
        }
    }
}
