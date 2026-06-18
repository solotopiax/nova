/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Fsm.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   有限状态机
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 有限状态机。
    /// </summary>
    /// <typeparam name="T">状态机持有者类型。</typeparam>
    internal sealed partial class Fsm<T> : IFsm<T> where T : class
    {
        /// <summary>
        /// 初始化有限状态机的新实例（私有构造，通过 Create 静态工厂创建）。
        /// </summary>
        /// <param name="owner">状态机持有者。</param>
        private Fsm(T owner)
        {
            m_Owner = owner;
            m_States = new System.Collections.Generic.Dictionary<Type, FsmState<T>>();
            m_Blackboard = new System.Collections.Generic.Dictionary<string, object>();
            m_CurrentState = null;
            m_IsDestroyed = false;
            m_IsChangingState = false;
        }

        /// <summary>
        /// 创建有限状态机。
        /// </summary>
        /// <param name="owner">状态机持有者。</param>
        /// <param name="states">所有状态实例。</param>
        /// <returns>创建的有限状态机。</returns>
        public static Fsm<T> Create(T owner, params FsmState<T>[] states)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner), "状态机持有者无效。");
            }

            if (states == null || states.Length == 0)
            {
                throw new ArgumentException("状态集合不能为空。", nameof(states));
            }

            Fsm<T> fsm = new Fsm<T>(owner);
            for (int i = 0; i < states.Length; i++)
            {
                FsmState<T> state = states[i];
                if (state == null)
                {
                    throw new ArgumentException(Txt.Format("状态索引 {0} 为 null。", i));
                }

                Type stateType = state.GetType();
                if (fsm.m_States.ContainsKey(stateType))
                {
                    throw new ArgumentException(Txt.Format("重复的状态类型 {0}。", stateType.FullName));
                }

                fsm.AddStateInternal(state, out _);
            }

            for (int i = 0; i < states.Length; i++)
            {
                states[i].OnInit(fsm);
            }

            return fsm;
        }

        /// <summary>
        /// 以泛型方式启动状态机。
        /// </summary>
        /// <typeparam name="TState">初始状态类型。</typeparam>
        public void Start<TState>() where TState : FsmState<T>
        {
            Start(typeof(TState));
        }

        /// <summary>
        /// 以 Type 方式启动状态机。
        /// </summary>
        /// <param name="stateType">初始状态类型。</param>
        public void Start(Type stateType)
        {
            if (m_IsDestroyed)
            {
                throw new InvalidOperationException("状态机已销毁，不可启动。");
            }

            if (m_CurrentState != null)
            {
                throw new InvalidOperationException("状态机已启动，不可重复启动。");
            }

            if (stateType == null)
            {
                throw new ArgumentNullException(nameof(stateType), "初始状态类型无效。");
            }

            if (!m_States.TryGetValue(stateType, out FsmState<T> state))
            {
                throw new ArgumentException(Txt.Format("状态机中不存在状态 {0}。", stateType.FullName));
            }

            m_CurrentState = state;
            m_CurrentState.OnEnter(this);
        }

        /// <summary>
        /// 轮询状态机（驱动当前状态的 OnUpdate）。
        /// </summary>
        public void Update()
        {
            if (m_IsDestroyed)
            {
                throw new InvalidOperationException("状态机已销毁，不可继续轮询。");
            }

            m_CurrentState?.OnUpdate(this);
        }

        /// <summary>
        /// 切换到指定状态。
        /// </summary>
        /// <typeparam name="TState">目标状态类型。</typeparam>
        public void ChangeState<TState>() where TState : FsmState<T>
        {
            ChangeStateInternal(typeof(TState));
        }

        /// <summary>
        /// 以 Type 方式切换到指定状态。
        /// </summary>
        /// <param name="stateType">目标状态类型。</param>
        public void ChangeState(Type stateType)
        {
            if (stateType == null)
            {
                throw new ArgumentNullException(nameof(stateType), "目标状态类型无效。");
            }

            ChangeStateInternal(stateType);
        }

        /// <summary>
        /// 是否存在指定类型的状态。
        /// </summary>
        /// <typeparam name="TState">要检查的状态类型。</typeparam>
        /// <returns>存在返回 true，否则返回 false。</returns>
        public bool HasState<TState>() where TState : FsmState<T>
        {
            return m_States.ContainsKey(typeof(TState));
        }

        /// <summary>
        /// 获取指定类型的状态实例。
        /// </summary>
        /// <typeparam name="TState">要获取的状态类型。</typeparam>
        /// <returns>状态实例。</returns>
        public TState GetState<TState>() where TState : FsmState<T>
        {
            if (m_States.TryGetValue(typeof(TState), out FsmState<T> state))
            {
                return (TState)state;
            }

            throw new ArgumentException(Txt.Format("状态机中不存在状态 {0}。", typeof(TState).FullName));
        }

        /// <summary>
        /// 获取指定类型的状态实例。
        /// </summary>
        /// <param name="stateType">要获取的状态类型。</param>
        /// <returns>状态实例。</returns>
        public FsmState<T> GetState(Type stateType)
        {
            if (stateType == null)
            {
                throw new ArgumentNullException(nameof(stateType), "状态类型无效。");
            }

            if (m_States.TryGetValue(stateType, out FsmState<T> state))
            {
                return state;
            }

            throw new ArgumentException(Txt.Format("状态机中不存在状态 {0}。", stateType.FullName));
        }

        /// <summary>
        /// 向黑板写入数据。
        /// </summary>
        /// <param name="key">数据键名。</param>
        /// <param name="value">数据值。</param>
        public void SetData(string key, object value)
        {
            if (m_IsDestroyed)
            {
                throw new InvalidOperationException("状态机已销毁，不可写入数据。");
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("数据键名无效。", nameof(key));
            }

            m_Blackboard[key] = value;
        }

        /// <summary>
        /// 从黑板读取数据。
        /// </summary>
        /// <typeparam name="TData">数据值类型。</typeparam>
        /// <param name="key">数据键名。</param>
        /// <returns>数据值，不存在时返回类型默认值。</returns>
        public TData GetData<TData>(string key)
        {
            if (m_IsDestroyed)
            {
                throw new InvalidOperationException("状态机已销毁，不可读取数据。");
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("数据键名无效。", nameof(key));
            }

            if (m_Blackboard.TryGetValue(key, out object value))
            {
                if (value is TData typedValue)
                {
                    return typedValue;
                }

                throw new InvalidCastException(Txt.Format(
                    "黑板数据类型不匹配，键 '{0}' 的实际类型为 {1}，请求类型为 {2}。",
                    key, value?.GetType().FullName ?? "null", typeof(TData).FullName));
            }

            return default;
        }

        /// <summary>
        /// 从黑板移除数据。
        /// </summary>
        /// <param name="key">数据键名。</param>
        /// <returns>移除成功返回 true，键不存在返回 false。</returns>
        public bool RemoveData(string key)
        {
            if (m_IsDestroyed)
            {
                throw new InvalidOperationException("状态机已销毁，不可移除数据。");
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("数据键名无效。", nameof(key));
            }

            return m_Blackboard.Remove(key);
        }

        /// <summary>
        /// 关闭并销毁状态机。
        /// </summary>
        public void Shutdown()
        {
            if (m_CurrentState != null)
            {
                m_IsChangingState = true;
                try
                {
                    m_CurrentState.OnLeave(this, true);
                }
                finally
                {
                    m_IsChangingState = false;
                }
            }

            m_IsDestroyed = true;

            foreach (var pair in m_States)
            {
                pair.Value.OnDestroy(this);
            }

            m_States.Clear();
            m_Blackboard.Clear();
            m_CurrentState = null;
        }

        /// <summary>
        /// 向运行中的状态机动态追加状态，适用于热更 Assembly.Load 后注册业务状态。
        /// 全部校验通过后才执行写入，保证 m_States 原子更新。
        /// </summary>
        /// <param name="states">待追加的状态实例列表，不允许为空或包含 null。</param>
        public void AddStates(params FsmState<T>[] states)
        {
            if (m_IsDestroyed)
            {
                throw new InvalidOperationException("状态机已销毁，不可追加状态。");
            }

            if (m_IsChangingState)
            {
                throw new InvalidOperationException("状态机正在切换状态中，禁止在 OnLeave/OnEnter 期间追加状态。");
            }

            if (states == null || states.Length == 0)
            {
                throw new ArgumentNullException(nameof(states), "待追加的状态集合不能为空。");
            }

            // 第一遍：全量校验，确保所有项合法且无重复，校验失败不写入任何内容。
            var batchTypes = new System.Collections.Generic.HashSet<Type>();
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] == null)
                {
                    throw new ArgumentNullException(nameof(states), Txt.Format("待追加的状态索引 {0} 为 null。", i));
                }

                Type stateType = states[i].GetType();
                if (m_States.ContainsKey(stateType))
                {
                    throw new InvalidOperationException(Txt.Format("状态机中已存在类型 {0}，不可重复追加。", stateType.FullName));
                }

                if (!batchTypes.Add(stateType))
                {
                    throw new InvalidOperationException(Txt.Format("本次批量追加中出现重复的状态类型 {0}。", stateType.FullName));
                }
            }

            // 第二遍：正式写入，此时不再抛出异常，保证原子性。
            for (int i = 0; i < states.Length; i++)
            {
                AddStateInternal(states[i], out _);
            }

            // 第三遍：对所有新加入的状态执行 OnInit。
            for (int i = 0; i < states.Length; i++)
            {
                states[i].OnInit(this);
            }
        }
    }
}
