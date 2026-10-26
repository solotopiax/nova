/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentSkills.ToolchainSetup.cs
 * author:    taoye
 * created:   2026/10/26
 * descrip:   安装升级后的工具链自动补齐、绑定和复核
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Mcp.Editor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AgentSkills
        {
            /// <summary>
            /// 自动补齐主机工具、同步 Nova Skills、绑定本项目 MCP 并重新核对安装结果。
            /// 固定使用官方安装入口和已注册 Provider，不要求项目组执行补装命令。
            /// </summary>
            /// <param name="cancellationToken">域重载或退出时终止尚在运行的安装进程。</param>
            /// <returns>自动处理后的结果；网络、权限或登录限制保持未完成，由调度器重试。</returns>
            public static async UniTask<ToolchainReport> EnsureToolchainAsync(CancellationToken cancellationToken = default)
            {
                await UniTask.SwitchToMainThread(cancellationToken);
                string projectRoot = ResolveProjectRoot();
                var provisioning = new List<ToolchainItem>();
                try
                {
                    ReconcileResult reconciliation = Reconcile();
                    if (reconciliation.Conflicts.Count > 0)
                    {
                        provisioning.Add(new ToolchainItem("nova-skills", "Nova Skills 自动同步", "blocked",
                            "已自动同步可安全更新的 Skills；本地冲突仍保留，未覆盖项目已有内容。"));
                    }
                }
                catch (Exception)
                {
                    provisioning.Add(new ToolchainItem("nova-skills", "Nova Skills 自动同步", "blocked",
                        "自动同步暂未完成，将随工具链处理自动重试；未覆盖本地内容。"));
                }

                try { provisioning.AddRange(await ProvisionHostToolsAsync(cancellationToken)); }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    provisioning.Add(new ToolchainItem("host-provisioning", "主机工具自动安装", "blocked",
                        "自动安装暂未完成，将随工具链处理自动重试。"));
                }
                await UniTask.SwitchToMainThread(cancellationToken);
                if (provisioning.Any(item => item.Id == "unity-pipeline" && item.Status == "partial")
                    && !UnityEditor.EditorApplication.isCompiling && !UnityEditor.EditorApplication.isUpdating)
                {
                    // CLI 的声明写入不等于 UPM 完成；主动 Resolve，重载后由 pending 标记接力。
                    UnityEditor.PackageManager.Client.Resolve();
                }
                ToolchainItem connection;
                try
                {
                    NovaToolchainBinding.BindingResult binding = await NovaToolchainBinding.EnsureAsync(projectRoot, cancellationToken);
                    connection = new ToolchainItem("mcp-connection", "MCP 服务与本项目自动绑定", binding.Status, binding.Detail);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    connection = new ToolchainItem("mcp-connection", "MCP 服务与本项目自动绑定", "blocked",
                        "本项目 MCP 自动绑定暂未完成，将自动重试；未修改其他项目的服务。" );
                }
                await UniTask.SwitchToMainThread(cancellationToken);
                ToolchainReport validation = await ValidateToolchainAsync();
                var items = new List<ToolchainItem>();
                foreach (ToolchainItem item in validation.Items)
                {
                    ToolchainItem provisioned = provisioning.FirstOrDefault(step => step.Id == item.Id && step.Status == "success");
                    if (item.Id == "mcp-connection")
                    {
                        items.Add(connection);
                    }
                    else if (item.Status == "partial" && provisioned != null)
                    {
                        // 安装器有本轮明确的安装启用回执时，后续目录查询超时不能抹去该事实。
                        items.Add(provisioned);
                    }
                    else
                    {
                        items.Add(new ToolchainItem(item.Id, item.Name, item.Status, item.Detail));
                    }
                }

                foreach (ToolchainItem step in provisioning)
                {
                    if (!items.Any(item => item.Id == step.Id)) items.Add(step);
                    else if (step.Status == "blocked")
                    {
                        int index = items.FindIndex(item => item.Id == step.Id);
                        if (items[index].Status != "success") items[index] = step;
                    }
                }
                return new ToolchainReport(items);
            }
        }
    }
}
