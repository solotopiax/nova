/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoHybridClrGameDllView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   HybridCLR 3.2 — 业务 dll 已加载列表快照展示
 ***************************************************************/

using NovaFramework.Runtime;
using System.Collections.Generic;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// HybridCLR Demo 3.2：业务 dll 已加载列表快照。
    /// API 副标题：Util.HybridCLR.LoadGameAssemblyAsync(dlls)。
    /// 只读快照型：展示 ConfigManager.GameDlls 清单 + Namespace 卡片。
    /// MainDemo 场景下 dll 列表可能为空，届时显式标注。
    /// </summary>
    public sealed class DemoHybridClrGameDllView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Game DLL");
        }

        /// <summary>
        /// 视图打开钩子，读取 ConfigManager.GameDlls 并逐行写入反馈区。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            ClearFeedback();

            string ns = Nova.Config.Namespace;
            AppendFeedback(string.Format("Namespace -> \"{0}\"", string.IsNullOrEmpty(ns) ? "(empty)" : ns), FeedbackLevel.Info);

            IReadOnlyList<DllAssetEntry> dlls = Nova.Config.GameDlls;
            AppendFeedback(string.Format("GameDlls -> {0} entries", dlls.Count), FeedbackLevel.Info);

            for (int i = 0; i < dlls.Count; i++)
            {
                AppendFeedback(string.Format("  [{0}] {1} loaded", i, dlls[i].AssetLocation), FeedbackLevel.Success);
            }

            if (dlls.Count == 0)
            {
                AppendFeedback("(Empty — MainDemo 场景未启用业务 DLL 热更)", FeedbackLevel.Warn);
            }
        }
    }
}
