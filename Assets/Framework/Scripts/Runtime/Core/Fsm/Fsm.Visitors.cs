/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Fsm.Visitors.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   有限状态机——字段与属性
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    internal sealed partial class Fsm<T> : IFsm<T> where T : class
    {
        /// <summary>
        /// 状态机持有者。
        /// </summary>
        private readonly T m_Owner;
        /// <summary>
        /// 获取状态机持有者。
        /// </summary>
        public T Owner => m_Owner;

        /// <summary>
        /// 所有状态，<状态类型, 状态实例>。
        /// </summary>
        private readonly Dictionary<Type, FsmState<T>> m_States;

        /// <summary>
        /// 黑板数据，<键名, 数据值>，用于状态间传递参数。
        /// </summary>
        private readonly Dictionary<string, object> m_Blackboard;

        /// <summary>
        /// 当前状态。
        /// </summary>
        private FsmState<T> m_CurrentState;
        /// <summary>
        /// 获取当前状态。
        /// </summary>
        public FsmState<T> CurrentState => m_CurrentState;

        /// <summary>
        /// 是否已销毁。
        /// </summary>
        private bool m_IsDestroyed;
        /// <summary>
        /// 获取状态机是否已销毁。
        /// </summary>
        public bool IsDestroyed => m_IsDestroyed;

        /// <summary>
        /// 是否正在切换状态（重入保护标志）。
        /// </summary>
        private bool m_IsChangingState;
    }
}
