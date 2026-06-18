/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileRuntimeContext.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   MobileStore 初始化阶段状态机，封装 StoreController 与连接状态
 ***************************************************************/

using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// MobileStore 运行时上下文，统一管理 StoreController、连接态与初始化状态。
    /// </summary>
    internal sealed class MobileRuntimeContext
    {
        /// <summary>
        /// Unity IAP 商店控制器。
        /// </summary>
        internal StoreController Controller { get; private set; }

        /// <summary>
        /// 最近一次初始化失败原因。
        /// </summary>
        internal MobileStoreInitFailureReason LastInitFailureReason { get; private set; } = MobileStoreInitFailureReason.None;

        /// <summary>
        /// 最近一次初始化失败的详情描述。
        /// </summary>
        internal string LastInitFailureMessage { get; private set; } = string.Empty;

        /// <summary>
        /// 当前 Unity IAP 商店连接是否已建立。
        /// </summary>
        private bool m_Connected;

        /// <summary>
        /// 当前 MobileStore 初始化阶段。
        /// </summary>
        private MobileStoreInitState m_InitState = MobileStoreInitState.None;

        /// <summary>
        /// 商店是否已就绪（初始化完成且已连接）。
        /// </summary>
        internal bool IsReady => m_InitState == MobileStoreInitState.Ready && m_Connected;

        /// <summary>
        /// 商店是否正在初始化中。
        /// </summary>
        internal bool IsInitializing => m_InitState == MobileStoreInitState.Initializing;

        /// <summary>
        /// 商店初始化是否已失败。
        /// </summary>
        internal bool IsFailed => m_InitState == MobileStoreInitState.Failed;

        /// <summary>
        /// 开始一次新的初始化流程，设置控制器并切换到 Initializing 状态。
        /// </summary>
        /// <param name="controller">Unity IAP 商店控制器。</param>
        internal void BeginInitialization(StoreController controller)
        {
            Controller = controller;
            m_Connected = false;
            m_InitState = MobileStoreInitState.Initializing;
            LastInitFailureReason = MobileStoreInitFailureReason.None;
            LastInitFailureMessage = string.Empty;
        }

        /// <summary>
        /// 标记商店连接成功。
        /// </summary>
        internal void MarkConnected()
        {
            m_Connected = true;
        }

        /// <summary>
        /// 标记商店连接断开。
        /// </summary>
        internal void MarkDisconnected()
        {
            m_Connected = false;
        }

        /// <summary>
        /// 标记初始化完成（Ready 状态）。
        /// </summary>
        internal void MarkReady()
        {
            m_InitState = MobileStoreInitState.Ready;
        }

        /// <summary>
        /// 幂等地标记初始化失败；已处于 Ready/Failed 时返回 false。
        /// </summary>
        /// <param name="reason">失败原因。</param>
        /// <param name="detail">附加说明。</param>
        /// <returns>标记成功返回 true，已处于 Ready/Failed 时返回 false。</returns>
        internal bool TryMarkFailed(MobileStoreInitFailureReason reason, string detail)
        {
            if (m_InitState == MobileStoreInitState.Ready || m_InitState == MobileStoreInitState.Failed)
            {
                return false;
            }

            m_InitState = MobileStoreInitState.Failed;
            m_Connected = false;
            LastInitFailureReason = reason;
            LastInitFailureMessage = detail;
            return true;
        }
    }
}
