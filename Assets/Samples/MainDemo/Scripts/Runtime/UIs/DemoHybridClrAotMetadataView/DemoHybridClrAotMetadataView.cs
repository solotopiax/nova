/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoHybridClrAotMetadataView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   HybridCLR 3.1 — AOT metadata 已加载列表快照展示
 ***************************************************************/

using NovaFramework.Runtime;
using System.Collections.Generic;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// HybridCLR Demo 3.1：AOT metadata 已加载列表快照。
    /// API 副标题：Util.HybridCLR.LoadAotMetadataAsync(dlls)（启动期已调，本 demo 仅展示快照）。
    /// 只读快照型：展示 ConfigManager.AotMetadataDlls 清单 + HomologousImageMode 卡片。
    /// </summary>
    public sealed class DemoHybridClrAotMetadataView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("AOT Metadata");
        }

        /// <summary>
        /// 视图打开钩子，读取 ConfigManager.AotMetadataDlls 并逐行写入反馈区。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            ClearFeedback();

            IReadOnlyList<DllAssetEntry> dlls = Nova.Config.AotMetadataDlls;

            AppendFeedback(string.Format("AotMetadataDlls -> {0} entries", dlls.Count), FeedbackLevel.Info);

            for (int i = 0; i < dlls.Count; i++)
            {
                AppendFeedback(string.Format("  [{0}] {1}", i, dlls[i].AssetLocation), FeedbackLevel.Info);
            }

            if (dlls.Count == 0)
            {
                AppendFeedback("(Empty — Editor 模式下跳过 AOT metadata 加载)", FeedbackLevel.Warn);
            }

            AppendFeedback("HomologousImageMode -> SuperSet", FeedbackLevel.Info);
        }
    }
}
