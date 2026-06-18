/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   Event组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class EventComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 当前 Event 管理器类型名称。
        /// </summary>
        private SerializedProperty m_CurManagerTypeName;

        /// <summary>
        /// 事件池模式。
        /// </summary>
        private SerializedProperty m_EventPoolMode;

        /// <summary>
        /// 每帧最大事件分发数量。
        /// </summary>
        private SerializedProperty m_MaxDispatchPerFrame;

        /// <summary>
        /// Event 管理器所有类型名称。
        /// </summary>
        private List<string> m_ManagerTypeNames;

        /// <summary>
        /// 运行时信息绘制器列表。
        /// </summary>
        private List<IEditorRuntimeDrawer> m_ListRuntimeDrawers;
    }
}
