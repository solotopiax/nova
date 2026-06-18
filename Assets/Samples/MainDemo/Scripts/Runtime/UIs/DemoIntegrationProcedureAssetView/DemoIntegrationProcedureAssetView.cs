/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIntegrationProcedureAssetView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Integration 4.3 — Procedure + Asset 热更链路只读快照
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Integration Demo 4.3：Procedure + Asset 热更链路只读快照展示。
    /// API 副标题：ProcedureCheckVersion -> ProcedureHotfix -> ProcedureLoadDll。
    /// 只读快照型：展示链路节点（5 个）+ EnableHotfix 总开关卡片，不实际跑下载。
    /// </summary>
    public sealed class DemoIntegrationProcedureAssetView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Procedure + Asset");
        }

        /// <summary>
        /// 视图打开钩子，展示热更链路节点状态与 EnableHotfix 总开关快照。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            ClearFeedback();

            bool enableHotfix = Nova.Asset.EnableHotfix;
            AppendFeedback(string.Format("EnableHotfix -> {0}", enableHotfix), enableHotfix ? FeedbackLevel.Info : FeedbackLevel.Warn);

            if (!enableHotfix)
            {
                AppendFeedback("EnableHotfix=false -> Hotfix procedures skipped", FeedbackLevel.Warn);
            }

            AppendFeedback("热更链路节点（快照）：", FeedbackLevel.Info);
            AppendFeedback(string.Format("  [1] ProcedureCheckVersion -> {0}", enableHotfix ? "active" : "skipped"), enableHotfix ? FeedbackLevel.Success : FeedbackLevel.Warn);
            AppendFeedback(string.Format("  [2] ProcedureHotfix       -> {0}", enableHotfix ? "active" : "skipped"), enableHotfix ? FeedbackLevel.Success : FeedbackLevel.Warn);
            AppendFeedback(string.Format("  [3] ProcedureAppDownload  -> {0}", enableHotfix ? "active" : "skipped"), enableHotfix ? FeedbackLevel.Success : FeedbackLevel.Warn);
            AppendFeedback("  [4] ProcedureLoadDll      -> active", FeedbackLevel.Success);
            AppendFeedback("  [5] 业务入口 Procedure    -> active", FeedbackLevel.Success);

            ProcedureBase current = Nova.Procedure.CurrentProcedure;
            string currentName = current != null ? current.GetType().Name : "(null)";
            AppendFeedback(string.Format("CurrentProcedure -> {0}", currentName), FeedbackLevel.Info);
        }
    }
}
