/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventComponentInspector.EventListRuntimeDrawer.cs
 * author:    taoye
 * created:   2026/4/3
 * descrip:   Event组件编辑器面板定制 —— 已注册事件列表运行时绘制器
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class EventComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 已注册事件列表运行时绘制器。
        /// </summary>
        internal sealed class EventListRuntimeDrawer : IEditorRuntimeDrawer
        {
            /// <summary>
            /// 列表名称。
            /// </summary>
            private readonly string m_ListName;

            /// <summary>
            /// 事件 ID 到类名的映射。
            /// </summary>
            private readonly Dictionary<int, string> m_EventIDToClassName;

            /// <summary>
            /// 初始化已注册事件列表运行时绘制器的新实例。
            /// </summary>
            public EventListRuntimeDrawer()
            {
                m_ListName = "已注册事件列表";
                m_EventIDToClassName = new Dictionary<int, string>();
                BuildEventIDMap();
            }

            /// <summary>
            /// 释放资源，清理 Foldout 状态。
            /// </summary>
            public void Dispose()
            {
                EditorUtil.Draw.CleanFoldout(m_ListName);
            }

            /// <summary>
            /// 构建事件 ID 到类名的映射表。
            /// </summary>
            private void BuildEventIDMap()
            {
                var types = UnityEditor.TypeCache.GetTypesDerivedFrom<EventData>();
                foreach (Type type in types)
                {
                    if (type.IsAbstract)
                    {
                        continue;
                    }

                    m_EventIDToClassName[EventTypeID.Get(type)] = type.Name;
                }
            }

            /// <summary>
            /// 获取事件类名。
            /// </summary>
            /// <param name="eventID">事件 ID。</param>
            /// <returns>事件类名。</returns>
            private string GetEventClassName(int eventID)
            {
                return m_EventIDToClassName.TryGetValue(eventID, out string name) ? name : Txt.Format("Unknown({0})", eventID);
            }

            /// <summary>
            /// 绘制。
            /// </summary>
            /// <param name="target">目标对象。</param>
            public void Draw(UnityEngine.Object target)
            {
                EventComponent t = target as EventComponent;
                if (t == null)
                {
                    return;
                }

                IReadOnlyCollection<int> eventIDs = t.GetRegisteredEventIDs();
                if (eventIDs == null || eventIDs.Count <= 0)
                {
                    return;
                }

                string title = Txt.Format("{0}({1})", m_ListName, eventIDs.Count);
                if (!EditorUtil.Draw.Foldout(title, m_ListName))
                {
                    return;
                }

                EditorUtil.Draw.Layout.Vertical("box", () =>
                {
                    foreach (int id in eventIDs)
                    {
                        string className = GetEventClassName(id);
                        string itemKey = Txt.Format("{0}_{1}", m_ListName, id);

                        if (EditorUtil.Draw.Foldout(className, itemKey))
                        {
                            EditorUtil.Draw.IncreaseIndentLevel();

                            EditorUtil.Draw.Label("ID", id.ToString(), false);

                            List<EventHandler<EventData>> handlers = t.GetHandlers(id);
                            int handlerCount = handlers != null ? handlers.Count : 0;
                            EditorUtil.Draw.Label("Handler 数", handlerCount.ToString(), false);

                            if (handlers != null)
                            {
                                for (int i = 0; i < handlers.Count; i++)
                                {
                                    EventHandler<EventData> h = handlers[i];
                                    string handlerInfo;
                                    if (h.Target != null)
                                    {
                                        handlerInfo = Txt.Format("{0}.{1}", h.Target.GetType().Name, h.Method.Name);
                                    }
                                    else
                                    {
                                        handlerInfo = Txt.Format("(static) {0}.{1}",
                                            h.Method.DeclaringType != null ? h.Method.DeclaringType.Name : "?",
                                            h.Method.Name);
                                    }

                                    EditorUtil.Draw.Label(Txt.Format("[{0}]", i), handlerInfo, false);
                                }
                            }

                            EditorUtil.Draw.DecreaseIndentLevel();
                        }
                    }
                });

                EditorUtil.Draw.Separator();
            }
        }
    }
}
