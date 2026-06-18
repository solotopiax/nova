/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventPool.Event.cs
 * author:    taoye
 * created:   2025/12/5
 * descrip:   事件定义
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 事件池。
    /// </summary>
    /// <typeparam name="T">事件类型。</typeparam>
    internal sealed partial class EventPool<T> where T : EventData
    {
        /// <summary>
        /// 事件结点。
        /// </summary>
        private sealed class Event : IReference
        {
            /// <summary>
            /// 事件派发者。
            /// </summary>
            private object m_Sender;
            public object Sender => m_Sender;
            
            /// <summary>
            /// 事件参数。
            /// </summary>
            private T m_EventArgs;
            public T EventArgs => m_EventArgs;

            /// <summary>
            /// 实例化。
            /// </summary>
            public Event()
            {
                m_Sender = null;
                m_EventArgs = null;
            }

            /// <summary>
            /// 创建事件。
            /// </summary>
            /// <param name="sender">事件派发者。</param>
            /// <param name="e">事件参数。</param>
            /// <returns>事件对象。</returns>
            public static Event Create(object sender, T e)
            {
                Event eventNode = ReferencePool.Get<Event>();
                eventNode.m_Sender = sender;
                eventNode.m_EventArgs = e;
                return eventNode;
            }

            /// <summary>
            /// 清理。
            /// </summary>
            public void Clear()
            {
                m_Sender = null;
                m_EventArgs = null;
            }
            
        }
    }
}
