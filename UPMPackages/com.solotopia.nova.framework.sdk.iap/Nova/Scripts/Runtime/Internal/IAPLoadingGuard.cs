/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPLoadingGuard.cs
 * author:    yingzheng
 * created:   2026/5/27
 * descrip:   IAP 引用计数式 Loading 管理器，通过回调解耦 UI 实现
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP Loading 引用计数管理器。
    /// 通过注入的 Push/Pop 回调控制 Loading 显示，不依赖任何具体 UI 接口。
    /// 创建时不依赖回调，需在商店初始化完成后调用 Bind 注入。
    /// </summary>
    public sealed class IAPLoadingGuard
    {
        /// <summary>
        /// 显示 Loading 的回调，由业务层在 Bind 时注入。
        /// </summary>
        private Action m_OnPush;

        /// <summary>
        /// 隐藏 Loading 的回调，由业务层在 Bind 时注入。
        /// </summary>
        private Action m_OnPop;

        /// <summary>
        /// 待执行的 Pop 回调队列，与 Push 一一对应，由 Pop 按序消费。
        /// </summary>
        private readonly Queue<Action> m_PendingRelease = new Queue<Action>();

        /// <summary>
        /// 用户主动发起支付或 Restore 后是否显示通用 Loading（默认 true）。
        /// </summary>
        public bool UseCommonLoading { get; set; } = true;

        /// <summary>
        /// 游戏启动阶段（补单/订阅等协议）是否显示 Loading。
        /// </summary>
        public bool GameStartShowCommonLoading { get; set; }

        /// <summary>
        /// 本次游戏是否已发生过支付或 Restore 行为，影响 ShouldShow 的判断结果。
        /// </summary>
        public bool HasUserInteracted { get; set; }

        /// <summary>
        /// 注入 Loading 显示/隐藏回调；在 store 初始化完成后调用一次。
        /// </summary>
        /// <param name="onPush">显示 Loading 的回调。</param>
        /// <param name="onPop">隐藏 Loading 的回调。</param>
        public void Bind(Action onPush, Action onPop)
        {
            m_OnPush = onPush;
            m_OnPop = onPop;
        }

        /// <summary>
        /// 根据当前交互状态决定是否应显示 Loading：
        /// 用户已交互时取 UseCommonLoading，否则取 GameStartShowCommonLoading。
        /// </summary>
        /// <returns>当前是否应显示 Loading。</returns>
        public bool ShouldShow() => HasUserInteracted ? UseCommonLoading : GameStartShowCommonLoading;

        /// <summary>
        /// 显示 Loading，是否显示受 ShouldShow 控制。
        /// </summary>
        public void Push() => Push(ShouldShow());

        /// <summary>
        /// 显示 Loading，force 直接决定是否显示，忽略 ShouldShow。
        /// </summary>
        /// <param name="force">为 true 时强制显示，为 false 时跳过。</param>
        public void Push(bool force)
        {
            if (!force || m_OnPush == null)
            {
                return;
            }

            m_OnPush.Invoke();
            Action onPop = m_OnPop;
            m_PendingRelease.Enqueue(() => onPop?.Invoke());
        }

        /// <summary>
        /// 隐藏一层 Loading，是否执行受 ShouldShow 控制。
        /// </summary>
        public void Pop() => Pop(ShouldShow());

        /// <summary>
        /// 隐藏一层 Loading，force 直接决定是否执行，忽略 ShouldShow。
        /// </summary>
        /// <param name="force">为 true 时强制执行，为 false 时跳过。</param>
        public void Pop(bool force)
        {
            if (!force || m_PendingRelease.Count <= 0)
            {
                return;
            }

            m_PendingRelease.Dequeue().Invoke();
        }

        /// <summary>
        /// 清空所有挂起的 Pop 回调，不触发任何 onPop，用于 store Dispose 时强制复位。
        /// </summary>
        public void Clear() => m_PendingRelease.Clear();
    }
}
