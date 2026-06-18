/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoHybridClrProcedureRegisterView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   HybridCLR 3.3 — 业务 Procedure 注册时序快照展示
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// HybridCLR Demo 3.3：业务 Procedure 注册时序快照。
    /// API 副标题：ProcedureLoadDll -> RegisterAdditionalProcedures(...)。
    /// 只读快照型：展示启动链路各阶段（Manifest -> Config -> AOT -> DLL -> Register -> Jump）
    /// 以及当前已到达的 Procedure 快照。
    /// </summary>
    public sealed class DemoHybridClrProcedureRegisterView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Procedure 注册时序");
        }

        /// <summary>
        /// 视图打开钩子，展示启动链路时序节点与当前 Procedure 快照。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            ClearFeedback();

            AppendFeedback("启动链路时序（框架侧）：", FeedbackLevel.Info);
            AppendFeedback("  [1] Manifest 加载", FeedbackLevel.Info);
            AppendFeedback("  [2] Config 加载", FeedbackLevel.Info);
            AppendFeedback("  [3] AOT metadata 并行加载", FeedbackLevel.Info);
            AppendFeedback("  [4] 业务 DLL 串行加载", FeedbackLevel.Info);
            AppendFeedback("  [5] RegisterAdditionalProcedures -> 业务 Procedure 注入 FSM", FeedbackLevel.Info);
            AppendFeedback("  [6] 跳转业务入口 Procedure", FeedbackLevel.Info);

            ProcedureBase current = Nova.Procedure.CurrentProcedure;
            string currentName = current != null ? current.GetType().Name : "(null)";
            AppendFeedback(string.Format("CurrentProcedure -> {0}", currentName), FeedbackLevel.Success);
        }
    }
}
