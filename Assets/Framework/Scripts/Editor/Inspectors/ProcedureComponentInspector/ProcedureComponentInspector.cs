/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureComponentInspector.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using System.Reflection;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Procedure组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(ProcedureComponent))]
    internal sealed partial class ProcedureComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_ManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IProcedureManager)));
            m_EntranceProcedureTypeName = serializedObject.FindProperty("m_EntranceProcedureTypeName");
            m_LauncherSettings = serializedObject.FindProperty("m_LauncherSettings");

            // 反射缓存：ProcedureLoadDll internal 属性（跨 assembly 只能反射访问）
            System.Type loadDllType = typeof(ProcedureBase).Assembly.GetType("NovaFramework.Runtime.ProcedureLoadDll");
            if (loadDllType != null)
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                m_LoadExceptionPropInfo = loadDllType.GetProperty("LoadException", flags);
                m_LoadCompletePropInfo = loadDllType.GetProperty("LoadComplete", flags);
                m_EntranceTypePropInfo = loadDllType.GetProperty("EntranceType", flags);
            }

            RefreshProcedureTypeNames();
        }

        /// <summary>
        /// 是否需要持续重绘（Play Mode 下每帧刷新跳转历史与运行时状态）。
        /// </summary>
        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        /// <summary>
        /// 绘制。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            DrawProcedureSettings();
            DrawLauncherSettings();
            DrawRuntimeInfo();
            DrawProcedureHistory();
            FinalRefreshInspectorGUI();
        }

        /// <summary>
        /// 编译完成回调。
        /// </summary>
        protected override void OnCompileComplete()
        {
            base.OnCompileComplete();
            RefreshProcedureTypeNames();
        }
    }
}
