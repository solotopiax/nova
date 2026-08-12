/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPBridge.Visitors.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   IAP Demo Core 桥接层字段与可选模块内部访问入口
 ***************************************************************/

using System;
using System.Threading;
using NovaFramework.SDK.IAP.Runtime;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// IAP Demo Core 桥接层字段声明。
    /// </summary>
    internal sealed partial class DemoIAPBridge
    {
        /// <summary>
        /// 支付透传数据使用的演示场景名。
        /// </summary>
        private const string c_SceneName = "DemoIAPView";

        /// <summary>
        /// 向 View 反馈区追加消息的回调。
        /// </summary>
        private readonly Action<string, FeedbackLevel> m_Feedback;

        /// <summary>
        /// 支付按钮交互状态变化回调。
        /// </summary>
        private readonly Action<bool> m_PayInteractableChanged;

        /// <summary>
        /// 当前 View 生命周期内的异步取消源。
        /// </summary>
        private readonly CancellationTokenSource m_Cancellation = new CancellationTokenSource();

        /// <summary>
        /// 当前基础 IAP 插件实例。
        /// </summary>
        private IAPPlugin m_IAP;

        /// <summary>
        /// 是否已订阅 IAP 全局事件。
        /// </summary>
        private bool m_EventsSubscribed;

        /// <summary>
        /// 当前桥接层是否已释放。
        /// </summary>
        private bool m_Disposed;

        /// <summary>
        /// 获取可选商店模块共用的基础 IAP 插件。
        /// </summary>
        internal IAPPlugin IAP => m_IAP;

        /// <summary>
        /// 获取绑定当前 View 生命周期的取消令牌。
        /// </summary>
        internal CancellationToken CancellationToken => m_Cancellation.Token;

        /// <summary>
        /// 获取当前桥接层是否已经释放。
        /// </summary>
        internal bool IsDisposed => m_Disposed;

        /// <summary>
        /// 支付请求的基础演示透传模型。
        /// </summary>
        [Serializable]
        private sealed class PayPayload
        {
            /// <summary>
            /// 商品表行 ID。
            /// </summary>
            public long TableId;

            /// <summary>
            /// 发起支付的演示场景。
            /// </summary>
            public string Scene;
        }
    }
}
