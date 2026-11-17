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
            /// Editor 启动钩子：重载后恢复待展示结果，再安排安全时机的启动检查。
            /// </summary>
            [InitializeOnLoad]
            private static class Initializer
            {
                static Initializer()
                {
                    // 等初始化钩子注册完毕，再由 update 核对安装和重要声明状态。
                    EditorApplication.delayCall += OnDelayedStart;
                }

                /// <summary>只安排自动检查；已查询的待展示结果跨重载复用，不直接打开窗口。</summary>
                private static void OnDelayedStart()
                {
                    EditorApplication.delayCall -= OnDelayedStart;
                    QueueAutomaticStartupCheck();
                }
            }
        }
    }
}
