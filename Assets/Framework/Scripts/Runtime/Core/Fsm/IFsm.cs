/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IFsm.cs
 * author:    taoye
 * created:   2026/3/12
 * descrip:   有限状态机接口
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 有限状态机接口。
    /// </summary>
    /// <typeparam name="T">状态机持有者类型。</typeparam>
    public interface IFsm<T> where T : class
    {
        /// <summary>
        /// 获取状态机持有者。
        /// </summary>
        T Owner { get; }

        /// <summary>
        /// 获取当前状态。
        /// </summary>
        FsmState<T> CurrentState { get; }

        /// <summary>
        /// 获取状态机是否已销毁。
        /// </summary>
        bool IsDestroyed { get; }

        /// <summary>
        /// 以泛型方式启动状态机。
        /// </summary>
        /// <typeparam name="TState">初始状态类型。</typeparam>
        void Start<TState>() where TState : FsmState<T>;

        /// <summary>
        /// 以 Type 方式启动状态机。
        /// </summary>
        /// <param name="stateType">初始状态类型。</param>
        void Start(Type stateType);

        /// <summary>
        /// 是否存在指定类型的状态。
        /// </summary>
        /// <typeparam name="TState">要检查的状态类型。</typeparam>
        /// <returns>存在返回 true，否则返回 false。</returns>
        bool HasState<TState>() where TState : FsmState<T>;

        /// <summary>
        /// 获取指定类型的状态实例。
        /// </summary>
        /// <typeparam name="TState">要获取的状态类型。</typeparam>
        /// <returns>状态实例。</returns>
        TState GetState<TState>() where TState : FsmState<T>;

        /// <summary>
        /// 获取指定类型的状态实例。
        /// </summary>
        /// <param name="stateType">要获取的状态类型。</param>
        /// <returns>状态实例。</returns>
        FsmState<T> GetState(Type stateType);

        /// <summary>
        /// 切换到指定状态（由 FsmState.ChangeState 内部调用）。
        /// </summary>
        /// <typeparam name="TState">目标状态类型。</typeparam>
        void ChangeState<TState>() where TState : FsmState<T>;

        /// <summary>
        /// 以 Type 方式切换到指定状态（由 FsmState.ChangeState 内部调用）。
        /// </summary>
        /// <param name="stateType">目标状态类型。</param>
        void ChangeState(Type stateType);

        /// <summary>
        /// 向黑板写入数据。
        /// </summary>
        /// <param name="key">数据键名。</param>
        /// <param name="value">数据值。</param>
        void SetData(string key, object value);

        /// <summary>
        /// 从黑板读取数据。
        /// </summary>
        /// <typeparam name="TData">数据值类型。</typeparam>
        /// <param name="key">数据键名。</param>
        /// <returns>数据值，不存在时返回类型默认值。</returns>
        TData GetData<TData>(string key);

        /// <summary>
        /// 从黑板移除数据。
        /// </summary>
        /// <param name="key">数据键名。</param>
        /// <returns>移除成功返回 true，键不存在返回 false。</returns>
        bool RemoveData(string key);

        /// <summary>
        /// 向运行中的状态机动态追加状态，适用于热更 Assembly.Load 后注册业务状态。
        /// </summary>
        /// <param name="states">待追加的状态实例列表，不允许为空或包含 null。</param>
        void AddStates(params FsmState<T>[] states);
    }
}
