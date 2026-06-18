/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventComponentInspector.cs
 * author:    taoye
 * created:   2026/1/16
 * descrip:   Event组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Event 组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(EventComponent))]
    internal sealed partial class EventComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            // m_FeishuDocumentUrl = EditorPath.Readme.Uri.SolarVibrate;

            m_CurManagerTypeName = serializedObject.FindProperty("m_CurManagerTypeName");
            m_EventPoolMode = serializedObject.FindProperty("m_EventPoolMode");
            m_MaxDispatchPerFrame = serializedObject.FindProperty("m_MaxDispatchPerFrame");
            m_ManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IEventManager)));

            m_ListRuntimeDrawers = new List<IEditorRuntimeDrawer>()
            {
                new EventListRuntimeDrawer(),
            };
        }

        /// <summary>
        /// 禁用时释放运行时绘制器资源。
        /// </summary>
        private void OnDisable()
        {
            if (m_ListRuntimeDrawers != null)
            {
                foreach (var drawer in m_ListRuntimeDrawers)
                {
                    drawer.Dispose();
                }
                m_ListRuntimeDrawers = null;
            }
        }

        /// <summary>
        /// 绘制。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            DrawRuntimeInfos();
            DrawRuntimeLists();
            FinalRefreshInspectorGUI();
        }

    }
}
