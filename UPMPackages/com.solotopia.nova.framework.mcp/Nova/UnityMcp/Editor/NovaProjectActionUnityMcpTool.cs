/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaProjectActionUnityMcpTool.cs
 * author:    taoye
 * created:   2026/8/20
 * descrip:   Unity MCP 到 Nova Project Action 中立网关的薄适配层
 ***************************************************************/

using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using NovaFramework.Mcp.Editor;

namespace NovaFramework.Mcp.UnityMcp.Editor
{
    /// <summary>
    /// Nova MCP 包当前默认提供的 Unity MCP Tool 适配器。
    /// 所有准入、安全门和 Action 分发均由中立 Gateway 负责。
    /// </summary>
    [McpForUnityTool(
        "nova_project_action",
        Description = "调用已注册并明确开放的 Nova Project Action。仅支持 describe、plan、execute、verify。",
        StructuredOutput = true,
        AutoRegister = true,
        Group = "core")]
    public static class NovaProjectActionUnityMcpTool
    {
        public sealed class Parameters
        {
            [ToolParameter("操作：describe、plan、execute 或 verify。")]
            public string operation { get; set; }

            [ToolParameter("Nova Project Action ID；describe 列表时可省略。", Required = false)]
            public string action_id { get; set; }

            [ToolParameter("plan 的强类型 Action 请求对象。", Required = false)]
            public object request { get; set; }

            /// <summary>登记、读取或释放同一 Plan 的传输记录；省略保持同步计划。</summary>
            [ToolParameter("plan 的可选传输子模式：start 登记、status 读取、forget 释放终态记录；省略保持同步计划。", Required = false)]
            public string plan_mode { get; set; }

            /// <summary>登记、读取或释放同一 Verify 的传输记录；省略保持同步核验。</summary>
            [ToolParameter("verify 的可选传输子模式：start 登记、status 读取、forget 释放终态记录；省略保持同步核验。", Required = false)]
            public string verify_mode { get; set; }

            /// <summary>将同一请求的断线重试绑定到调用方预先生成的精确 GUID。</summary>
            [ToolParameter("plan/verify 各自传输记录的调用方 N 格式 GUID；重试复用同号同请求，forget 后不复用。", Required = false)]
            public string request_id { get; set; }

            [ToolParameter("execute 的一次性计划 ID。", Required = false)]
            public string plan_id { get; set; }

            [ToolParameter("execute 的计划绑定令牌；不代表传输层已验证人类授权。", Required = false)]
            public string confirmation_token { get; set; }

            [ToolParameter("verify 的 Core RecoveryToken 或兼容 Receipt 字符串。", Required = false)]
            public string receipt { get; set; }
        }

        public static async Task<object> HandleCommand(JObject parameters)
        {
            NovaProjectActionGatewayResponse response = await NovaProjectActionGateway.HandleCommand(parameters);
            return response.success
                ? new SuccessResponse(response.message, response.data)
                : new ErrorResponse(response.code, response.data);
        }
    }
}
