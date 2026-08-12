/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeComponent.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   框架原生能力组件
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架层访问操作系统原生能力的唯一 Component 门面。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class NativeComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒时创建 NativeManager，但不触发任何系统权限请求。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_NativeManager = Util.TypeCreator.Create<INativeManager>(m_CurNativeManagerTypeName);
            if (m_NativeManager == null)
            {
                throw new InvalidOperationException("NativeManager 无效。");
            }
        }

        /// <summary>
        /// 开始时初始化 NativeManager，不自动查询或请求通知权限。
        /// </summary>
        private void Start()
        {
            m_NativeManager.Initialize(m_NativeManagerConfig);
        }

        /// <summary>
        /// 销毁时只断开 Component 引用，统一清理由 Nova 的 Manager Shutdown 链负责。
        /// </summary>
        private void OnDestroy()
        {
            m_NativeManager = null;
        }

        /// <summary>
        /// 查询操作系统当前通知权限状态。
        /// </summary>
        /// <param name="ct">只取消当前调用方等待。</param>
        /// <returns>当前通知权限状态。</returns>
        public UniTask<NotificationPermissionStatus> GetNotificationPermissionStatusAsync(CancellationToken ct = default)
        {
            return m_NativeManager.GetNotificationPermissionStatusAsync(ct);
        }

        /// <summary>
        /// 请求通知权限。框架不会在启动阶段自动调用此方法。
        /// </summary>
        /// <param name="options">需要请求的通知能力。</param>
        /// <param name="ct">只取消当前调用方等待，不取消共享系统请求。</param>
        /// <returns>请求流程与请求后的系统权威状态。</returns>
        public UniTask<NotificationPermissionResult> RequestNotificationPermissionAsync(
            NotificationAuthorizationOptions options = NotificationAuthorizationOptions.Alert |
                                                       NotificationAuthorizationOptions.Sound |
                                                       NotificationAuthorizationOptions.Badge,
            CancellationToken ct = default)
        {
            return m_NativeManager.RequestNotificationPermissionAsync(options, ct);
        }

        /// <summary>
        /// 打开应用系统设置。返回 true 仅表示已成功发起跳转，不表示用户已看到页面或修改设置。
        /// </summary>
        /// <returns>是否成功发起设置页跳转。</returns>
        public UniTask<bool> OpenAppSettingsAsync()
        {
            return m_NativeManager.OpenAppSettingsAsync();
        }

        /// <summary>
        /// 打开当前应用的系统通知设置。返回 true 仅表示已成功发起跳转，不表示用户已看到页面或修改设置。
        /// 无法精准跳转时返回 false，不回退到应用设置。
        /// </summary>
        /// <returns>是否成功发起精准通知设置页跳转。</returns>
        public UniTask<bool> OpenNotificationSettingsAsync()
        {
            return m_NativeManager.OpenNotificationSettingsAsync();
        }
    }
}
