/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EventComponent.Visitors.cs
 * author:    taoye
 * created:   2026/1/16
 * descrip:   事件组件-访问器
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 事件组件。
    /// </summary>
    public sealed partial class EventComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前事件管理器类型名称。
        /// </summary>
        [SerializeField]
        private string m_CurManagerTypeName = "NovaFramework.Runtime.EventManager";
        public string CurManagerTypeName => m_CurManagerTypeName;

        /// <summary>
        /// 事件池模式。
        /// </summary>
        [SerializeField]
        private EventPoolMode m_EventPoolMode = EventPoolMode.AllowNoHandler | EventPoolMode.AllowMultiHandler;

        /// <summary>
        /// 每帧最大事件分发数量（0 表示无限制）。
        /// </summary>
        [SerializeField]
        private int m_MaxDispatchPerFrame = 0;

        /// <summary>
        /// 事件管理器实例。
        /// </summary>
        private IEventManager m_EventManager;
        
        /// <summary>
        /// 获取事件处理函数的数量。
        /// </summary>
        public int EventHandlerCount => m_EventManager?.HandlerCount ?? 0;

        /// <summary>
        /// 获取事件数量。
        /// </summary>
        public int EventCount => m_EventManager?.Count ?? 0;

    }
}
