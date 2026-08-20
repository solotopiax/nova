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
    /// 只读快照型：展示启动链路各阶段、业务入口注册结果与当前 Procedure。
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
        /// 视图打开钩子，展示真实启动链路、业务入口注册结果与当前 Procedure 快照。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            ClearFeedback();

            AppendFeedback("ProcedureLoadDll 启动链路：", FeedbackLevel.Info);
            AppendFeedback("  [1] UniTask.Yield -> 脱离 FSM 切换栈", FeedbackLevel.Info);
            AppendFeedback("  [2] Manifest -> Config", FeedbackLevel.Info);
            AppendFeedback("  [3] AOT Metadata 并行加载", FeedbackLevel.Info);
            AppendFeedback("  [4] StartupGameDlls 串行加载 -> RefreshAssemblies", FeedbackLevel.Info);
            AppendFeedback("  [5] 扫描启动程序集 -> RegisterAdditionalProcedures", FeedbackLevel.Info);
            AppendFeedback("  [6] 定位业务入口 -> OnUpdate 跳转", FeedbackLevel.Info);

            string entranceName = Nova.Config.HybridConfigs?.GameEntranceProcedureName;
            string entranceFullName = string.IsNullOrWhiteSpace(Nova.Config.Namespace) || string.IsNullOrWhiteSpace(entranceName)
                ? string.Empty
                : string.Format("{0}.{1}", Nova.Config.Namespace, entranceName);
            System.Type entranceType = string.IsNullOrEmpty(entranceFullName)
                ? null
                : Util.Assembly.GetType(entranceFullName);
            ProcedureBase registeredEntrance = entranceType == null ? null : Nova.Procedure.GetProcedure(entranceType);
            AppendFeedback(
                string.Format("入口注册 -> {0}", registeredEntrance != null ? entranceFullName : "未找到"),
                registeredEntrance != null ? FeedbackLevel.Success : FeedbackLevel.Error);

            ProcedureBase current = Nova.Procedure.CurrentProcedure;
            string currentName = current != null ? current.GetType().Name : "(null)";
            AppendFeedback(
                string.Format("CurrentProcedure -> {0}", currentName),
                current != null ? FeedbackLevel.Success : FeedbackLevel.Error);
        }
    }
}
