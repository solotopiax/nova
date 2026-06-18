/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class ProcedureComponentInspector : BaseComponentInspector
    {
        // ── 原有字段 ──────────────────────────────────────────────

        /// <summary>
        /// 当前管理器类型名称。
        /// </summary>
        private SerializedProperty m_CurManagerTypeName;

        /// <summary>
        /// 管理器所有类型名称。
        /// </summary>
        private List<string> m_ManagerTypeNames;

        /// <summary>
        /// 入口流程类型名称。
        /// </summary>
        private SerializedProperty m_EntranceProcedureTypeName;

        /// <summary>
        /// 所有流程类型名称（自动扫描，含命名空间）。
        /// </summary>
        private List<string> m_ProcedureTypeNames;

        /// <summary>
        /// 启动阶段配置属性。
        /// </summary>
        private SerializedProperty m_LauncherSettings;

        /// <summary>
        /// Procedure 跳转历史 Foldout 展开状态（默认展开）。
        /// </summary>
        private bool m_RunHistoryFoldout = true;

        // ── 反射缓存（跨 assembly 访问 ProcedureLoadDll internal 属性） ────

        /// <summary>
        /// 通过反射获取 ProcedureLoadDll.LoadException 的 PropertyInfo 缓存。
        /// </summary>
        private System.Reflection.PropertyInfo m_LoadExceptionPropInfo;

        /// <summary>
        /// 通过反射获取 ProcedureLoadDll.LoadComplete 的 PropertyInfo 缓存。
        /// </summary>
        private System.Reflection.PropertyInfo m_LoadCompletePropInfo;

        /// <summary>
        /// 通过反射获取 ProcedureLoadDll.EntranceType 的 PropertyInfo 缓存。
        /// </summary>
        private System.Reflection.PropertyInfo m_EntranceTypePropInfo;

    }
}
