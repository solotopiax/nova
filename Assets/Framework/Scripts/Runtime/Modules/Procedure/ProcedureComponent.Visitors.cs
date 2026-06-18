/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureComponent.Visitors.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure组件 —— 属性与字段
 ***************************************************************/

using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
#endif

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Procedure组件。
    /// </summary>
    public sealed partial class ProcedureComponent : FrameworkComponent
    {
#if UNITY_EDITOR
        /// <summary>
        /// 跳转历史容量上限常量。
        /// </summary>
        private const int c_RunHistoryCapacity = 128;
#endif

        /// <summary>
        /// 当前Procedure管理器类型名称。
        /// </summary>
        [Tooltip("Procedure 管理器实现类全名")]
        [SerializeField]
        private string m_CurManagerTypeName = "NovaFramework.Runtime.ProcedureManager";
        public string CurManagerTypeName => m_CurManagerTypeName;

        /// <summary>
        /// 入口流程类型名称。
        /// 默认指向 Framework 内置的 ProcedureSplash（ProcedureLaunch 已合并入 ProcedureSplash）。
        /// 若现有 Prefab 仍保留旧值 NovaFramework.Runtime.ProcedureLaunch，
        /// 必须在 Inspector 中手动更新为 NovaFramework.Runtime.ProcedureSplash，
        /// 否则 Start() 将在类型查找阶段抛出 InvalidOperationException。
        /// </summary>
        [Tooltip("入口流程类型名称，程序启动后首先执行")]
        [SerializeField]
        private string m_EntranceProcedureTypeName = "NovaFramework.Runtime.ProcedureSplash";
        public string EntranceProcedureTypeName => m_EntranceProcedureTypeName;

        /// <summary>
        /// 启动阶段配置（闪屏时长、Prefab 名称等）。
        /// </summary>
        [Tooltip("启动阶段配置（闪屏时长、Prefab 名称等）")]
        [SerializeField]
        private LauncherSettings m_LauncherSettings = new LauncherSettings();
        public LauncherSettings LauncherSettings => m_LauncherSettings;

        /// <summary>
        /// Procedure管理器实例。
        /// </summary>
        private IProcedureManager m_ProcedureManager;

        /// <summary>
        /// 获取当前流程。
        /// </summary>
        public ProcedureBase CurrentProcedure => m_ProcedureManager?.CurrentProcedure;

#if UNITY_EDITOR
        /// <summary>
        /// 运行时跳转历史容器（调试用，容量上限 c_RunHistoryCapacity）。
        /// </summary>
        private readonly List<ProcedureRunInfo> m_RunHistory = new List<ProcedureRunInfo>(16);
        /// <summary>
        /// 上一次采样到的 Procedure 类型全名，用于变更检测去重。
        /// </summary>
        private string m_LastProcedureTypeFullName;
        /// <summary>
        /// 对外只读的 Procedure 跳转历史视图。
        /// </summary>
        public IReadOnlyList<ProcedureRunInfo> RunHistory => m_RunHistory;
#endif
    }
}
