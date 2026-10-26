/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaToolchainBinding.cs
 * author:    taoye
 * created:   2026/10/26
 * descrip:   工具链安装后的 Provider 中立服务与宿主绑定入口
 ***************************************************************/

using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Mcp.Editor
{
    /// <summary>通过显式注册的 Provider 准备当前项目服务与宿主绑定，不执行任何 AI 任务。</summary>
    public static class NovaToolchainBinding
    {
        private static string s_ProviderId;
        private static Func<string, CancellationToken, UniTask<BindingResult>> s_Ensure;
        private static bool s_IsRunning;

        /// <summary>本次自动绑定结果；服务连通和当前聊天已加载工具是不同证据。</summary>
        public sealed class BindingResult
        {
            /// <summary>建立脱敏结果，不包含凭据、原始命令输出或可执行请求。</summary>
            /// <param name="projectRoot">实际处理的项目根。</param>
            /// <param name="status">success、partial 或 blocked。</param>
            /// <param name="detail">实际完成项及尚存的外部条件。</param>
            public BindingResult(string projectRoot, string status, string detail)
            {
                if (status != "success" && status != "partial" && status != "blocked") throw new ArgumentException("未知绑定结果状态。", nameof(status));
                ProjectRoot = projectRoot ?? string.Empty;
                Status = status;
                Detail = detail ?? string.Empty;
            }

            /// <summary>本次处理的项目根。</summary>
            public string ProjectRoot { get; }
            /// <summary>本次实际达到的状态。</summary>
            public string Status { get; }
            /// <summary>面向项目组的完成情况及阻碍。</summary>
            public string Detail { get; }
        }

        /// <summary>当前域最近一次绑定状态；域重载后重新建立，不作为长期验收凭据。</summary>
        public static BindingResult LastResult { get; private set; } = new BindingResult("", "partial", "自动服务与绑定尚未运行。");

        /// <summary>注册唯一 Provider；中立层不引用 Framework 或具体传输实现。</summary>
        /// <param name="providerId">稳定 Provider 身份。</param>
        /// <param name="ensure">只为当前项目准备服务与宿主绑定的明确入口。</param>
        public static void RegisterProvider(string providerId, Func<string, CancellationToken, UniTask<BindingResult>> ensure)
        {
            if (string.IsNullOrWhiteSpace(providerId)) throw new ArgumentException("Provider 身份不能为空。", nameof(providerId));
            if (ensure == null) throw new ArgumentNullException(nameof(ensure));
            if (s_ProviderId != null && s_ProviderId != providerId) throw new InvalidOperationException("已有不同的工具链绑定 Provider，不能静默替换。");
            s_ProviderId = providerId;
            s_Ensure = ensure;
        }

        /// <summary>串行准备当前 Editor 项目的服务与绑定；异常准确返回 blocked，由调用方有界重试。</summary>
        /// <param name="projectRoot">必须与当前 Editor 对应的完整项目根一致。</param>
        /// <param name="cancellationToken">调用方中断信号；已启动的正式服务不会被当作临时进程杀死。</param>
        /// <returns>本地实际部署结果；success 不代表当前聊天已经加载工具，宿主现场证据独立核对。</returns>
        public static async UniTask<BindingResult> EnsureAsync(string projectRoot, CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            string normalizedRoot;
            try
            {
                normalizedRoot = Path.GetFullPath(projectRoot ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string editorRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                StringComparison comparison = Application.platform == RuntimePlatform.WindowsEditor ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (!string.Equals(normalizedRoot, editorRoot, comparison)) return LastResult = new BindingResult(normalizedRoot, "blocked", "请求的项目与当前 Unity Editor 不一致，未修改任何绑定。");
            }
            catch
            {
                return LastResult = new BindingResult("", "blocked", "无法确定当前项目根，自动绑定未执行。");
            }

            await UniTask.WaitUntil(() => !s_IsRunning, cancellationToken: cancellationToken);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return LastResult = new BindingResult(normalizedRoot, "partial", "Unity 正在编译或更新包，程序将在稳定后继续自动绑定。");
            if (s_Ensure == null) return LastResult = new BindingResult(normalizedRoot, "blocked", "当前 MCP Provider 尚未注册自动绑定能力，等待包编译完成后自动重试。");
            s_IsRunning = true;
            LastResult = new BindingResult(normalizedRoot, "partial", "正在自动准备本机 MCP 服务和当前项目的 Codex 绑定。");
            try
            {
                BindingResult result = await s_Ensure(normalizedRoot, cancellationToken);
                if (result == null || !string.Equals(result.ProjectRoot, normalizedRoot, StringComparison.Ordinal))
                    return LastResult = new BindingResult(normalizedRoot, "blocked", "Provider 未返回当前项目的有效绑定结果，不能确认自动部署完成。");
                return LastResult = result;
            }
            catch (OperationCanceledException)
            {
                return LastResult = new BindingResult(normalizedRoot, "partial", "自动绑定被重载或取消中断，已完成配置保留，程序稍后继续核验。");
            }
            catch
            {
                return LastResult = new BindingResult(normalizedRoot, "blocked", "自动服务或绑定未完成，可能受网络、文件权限或现有配置限制；程序将有界重试，未覆盖其他项目配置。");
            }
            finally
            {
                s_IsRunning = false;
            }
        }
    }
}
