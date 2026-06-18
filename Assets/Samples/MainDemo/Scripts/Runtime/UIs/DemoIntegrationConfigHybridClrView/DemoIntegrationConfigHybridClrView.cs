/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIntegrationConfigHybridClrView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Integration 4.5 — Config + HybridCLR Namespace 注入快照
 ***************************************************************/

using NovaFramework.Runtime;
using System;
using System.Reflection;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Integration Demo 4.5：Namespace 注入 -> 程序集解析只读快照。
    /// API 副标题：Nova.Config.ConfigManager.Namespace -> Util.Assembly.GetAssembly(ns)。
    /// 只读快照型：展示 Namespace 卡片 + 解析得到的 Assembly 卡片 + 已注册 ProcedureBase 子类计数。
    /// MainDemo 场景下业务 DLL 未加载时子类数为 0，显式标注。
    /// </summary>
    public sealed class DemoIntegrationConfigHybridClrView : BaseDemoView
    {
        /// <summary>
        /// 视图初始化钩子，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Config + HybridCLR");
        }

        /// <summary>
        /// 视图打开钩子，读取 Config.Namespace 并尝试解析对应程序集，统计 ProcedureBase 子类数。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            ClearFeedback();

            string ns = Nova.Config.Namespace;
            AppendFeedback(string.Format("Nova.Config.Namespace -> \"{0}\"", string.IsNullOrEmpty(ns) ? "(empty)" : ns), FeedbackLevel.Info);

            if (string.IsNullOrEmpty(ns))
            {
                AppendFeedback("Namespace 为空，跳过程序集解析", FeedbackLevel.Warn);
                return;
            }

            Assembly assembly = Util.Assembly.GetAssembly(ns);

            if (assembly == null)
            {
                AppendFeedback(string.Format("Util.Assembly.GetAssembly(\"{0}\") -> null (未加载或未找到)", ns), FeedbackLevel.Warn);
                AppendFeedback("(MainDemo 场景下业务 DLL 未加载，程序集不存在)", FeedbackLevel.Warn);
                return;
            }

            AppendFeedback(string.Format("Util.Assembly.GetAssembly(\"{0}\") -> Assembly resolved", ns), FeedbackLevel.Success);

            int procedureCount = CountProcedureSubclasses(assembly);
            AppendFeedback(string.Format("ProcedureBase 子类计数 -> {0}{1}", procedureCount, procedureCount == 0 ? " (MainDemo)" : ""), procedureCount > 0 ? FeedbackLevel.Success : FeedbackLevel.Warn);
        }

        /// <summary>
        /// 统计给定程序集中 ProcedureBase 的非抽象子类数量。
        /// </summary>
        /// <param name="assembly">目标程序集。</param>
        /// <returns>ProcedureBase 子类数量。</returns>
        private static int CountProcedureSubclasses(Assembly assembly)
        {
            if (assembly == null)
            {
                return 0;
            }

            Type procedureBaseType = typeof(ProcedureBase);
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            int count = 0;
            for (int i = 0; i < types.Length; i++)
            {
                Type t = types[i];
                if (t != null && !t.IsAbstract && procedureBaseType.IsAssignableFrom(t))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
