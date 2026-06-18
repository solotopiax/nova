/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CheckUpdate.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   CheckUpdate 工具 —— 公有接口与启动钩子
 ***************************************************************/

using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// UPM 包版本检查工具，提供启动时自动检查与手动触发检查两种入口。
        /// 通过 SkipConfig 持久化记录用户选择跳过的版本，避免重复弹窗打扰。
        /// </summary>
        public static partial class CheckUpdate
        {
            /// <summary>
            /// Editor 启动钩子：延迟一帧后 fire-and-forget 执行启动时版本检查。
            /// </summary>
            [InitializeOnLoad]
            private static class Initializer
            {
                static Initializer()
                {
                    // 延迟一帧等待 Editor 完全就绪后再触发检查，避免阻塞域重载
                    EditorApplication.delayCall += OnDelayedStart;
                }

                private static void OnDelayedStart()
                {
                    EditorApplication.delayCall -= OnDelayedStart;
                    // fire-and-forget：异常已在 CheckOnStartupAsync 内部静默处理
                    _ = CheckOnStartupAsync();
                }
            }
        }
    }
}
