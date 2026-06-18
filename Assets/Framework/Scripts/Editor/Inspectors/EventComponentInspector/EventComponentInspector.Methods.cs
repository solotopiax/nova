/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   Event组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class EventComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置信息。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("Event 管理器", m_ManagerTypeNames, m_CurManagerTypeName, true, null, UnityEngine.GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IEventManager 接口后，该类型将自动出现在此列表中。", });
            EditorUtil.Draw.Line();

            EditorUtil.Draw.EnumFlagsSelector<NovaFramework.Runtime.EventPoolMode>("事件池模式", m_EventPoolMode, true, null, UnityEngine.GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)AllowNoHandler 允许事件无处理函数",
                "(2)AllowMultiHandler 允许同一事件注册多个处理函数",
                "(3)AllowDuplicateHandler 允许重复注册同一处理函数"
            });
            EditorUtil.Draw.Property("每帧最大事件分发数量", m_MaxDispatchPerFrame, true, UnityEngine.GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)取值 0 表示无上限",
                "(2)取值大于 0 时可防止事件风暴导致的帧卡顿",
                "(3)时间敏感的事件可使用 FireNow 立即分发"
            });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制运行时信息。
        /// </summary>
        private void DrawRuntimeInfos()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            EventComponent t = (EventComponent)target;

            EditorUtil.Draw.Label("事件处理器数量", t.EventHandlerCount.ToString(), false);
            EditorUtil.Draw.Label("待派发事件数量", t.EventCount.ToString(), false);
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制运行时列表。
        /// </summary>
        private void DrawRuntimeLists()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            foreach (IEditorRuntimeDrawer drawer in m_ListRuntimeDrawers)
            {
                drawer.Draw(target);
            }
        }

    }
}
