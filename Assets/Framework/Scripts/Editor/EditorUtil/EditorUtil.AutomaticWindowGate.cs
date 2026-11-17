/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AutomaticWindowGate.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   Nova 自动提示窗口的只读等待判断
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>只协调 Nova 自动提示的展示时机，不关闭窗口、不替代用户确认，也不识别所有第三方原生模态。</summary>
        internal static class AutomaticWindowGate
        {
            /// <summary>在真正展示前读取当前状态；普通更新额外等待重要声明检查及人工确认。</summary>
            /// <param name="waitForImportantChecks">普通更新为 true，重要声明自身为 false，避免检查相互等待。</param>
            /// <returns>中文等待原因；空字符串表示本门没有阻止展示。</returns>
            internal static string ReadWaitReason(bool waitForImportantChecks)
            {
                return EvaluateWaitReason(Application.isBatchMode, EditorApplication.isCompiling, EditorApplication.isUpdating,
                    EditorApplication.isPlayingOrWillChangePlaymode, AgentSkills.IsToolchainInstallationActive,
                    ImportantUpdates.IsPresentationShowing, waitForImportantChecks && ImportantUpdates.IsPresentationPending);
            }

            /// <summary>按稳定优先级区分编译、安装和人工等待；工具链的延时重试本身不阻止窗口。</summary>
            /// <param name="batchMode">是否以无界面模式运行。</param>
            /// <param name="compiling">是否正在编译。</param>
            /// <param name="updating">是否正在导入或更新。</param>
            /// <param name="playing">是否运行或切换运行模式。</param>
            /// <param name="toolchainActive">是否有立即排队或正在运行的工具链安装。</param>
            /// <param name="importantShowing">是否正在确认重要声明。</param>
            /// <param name="importantPending">调用方是否还需等待重要声明检查。</param>
            /// <returns>可读等待原因；不会把本次没有阻止展示解释为环境验收成功。</returns>
            internal static string EvaluateWaitReason(bool batchMode, bool compiling, bool updating, bool playing,
                bool toolchainActive, bool importantShowing, bool importantPending)
            {
                if (batchMode) return "无界面模式不自动打开提示窗口。";
                if (compiling) return "等待 Unity 编译完成。";
                if (updating) return "等待 Unity 导入和包更新完成。";
                if (playing) return "等待退出运行模式。";
                if (importantShowing) return "等待当前重要升级说明的人工确认。";
                if (toolchainActive) return "等待当前一轮 AI 工具链安装和核验结束。";
                if (importantPending) return "等待重要升级说明检查和确认结束。";
                return string.Empty;
            }
        }
    }
}
