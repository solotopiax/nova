/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeManager.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   NativeManager 主入口与请求并发控制
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// NativeManager 实现，负责平台分发、请求串行化与生命周期清理。
    /// </summary>
    internal sealed partial class NativeManager : NativeManagerBase
    {
        private const NotificationAuthorizationOptions c_AllOptions =
            NotificationAuthorizationOptions.Alert |
            NotificationAuthorizationOptions.Sound |
            NotificationAuthorizationOptions.Badge |
            NotificationAuthorizationOptions.Provisional;

        private readonly object m_RequestLock = new object();
        private NativeManagerConfig m_Config;
        private bool m_IsShutdown = true;
        private UniTaskCompletionSource<NotificationPermissionResult> m_InFlightPermissionRequest;
        private NotificationAuthorizationOptions m_InFlightPermissionOptions;

        /// <summary>
        /// 初始化平台桥接。初始化阶段不会查询或请求通知权限。
        /// </summary>
        /// <param name="config">NativeManager 配置。</param>
        public override void Initialize(NativeManagerConfig config)
        {
            m_Config = config ?? throw new ArgumentNullException(nameof(config));
            m_IsShutdown = false;
            InitializePlatform();
        }

        /// <summary>
        /// NativeManager 无周期任务，原生回调由平台事件驱动。
        /// </summary>
        public override void Update()
        {
            // 原生回调为事件驱动，无需每帧轮询。
        }

        /// <summary>
        /// 关闭平台桥接、取消 pending 请求并丢弃迟到回调。
        /// </summary>
        public override void Shutdown()
        {
            UniTaskCompletionSource<NotificationPermissionResult> pending;
            lock (m_RequestLock)
            {
                if (m_IsShutdown)
                {
                    return;
                }

                m_IsShutdown = true;
                m_Config = null;
                pending = m_InFlightPermissionRequest;
                m_InFlightPermissionRequest = null;
                m_InFlightPermissionOptions = NotificationAuthorizationOptions.None;
            }

            ShutdownPlatform();
            pending?.TrySetCanceled();
        }

        /// <summary>
        /// 查询当前通知权限，并允许调用方独立取消等待。
        /// </summary>
        /// <param name="ct">调用方取消令牌。</param>
        /// <returns>当前通知权限状态。</returns>
        public override UniTask<NotificationPermissionStatus> GetNotificationPermissionStatusAsync(CancellationToken ct = default)
        {
            EnsureInitialized();
            return GetNotificationPermissionStatusCoreAsync(ct);
        }

        /// <summary>
        /// 请求通知权限；相同选项合并，不同选项等待前一请求结束后再执行。
        /// </summary>
        /// <param name="options">通知授权选项。</param>
        /// <param name="ct">调用方取消令牌。</param>
        /// <returns>通知权限请求结果。</returns>
        public override UniTask<NotificationPermissionResult> RequestNotificationPermissionAsync(
            NotificationAuthorizationOptions options = NotificationAuthorizationOptions.Alert |
                                                       NotificationAuthorizationOptions.Sound |
                                                       NotificationAuthorizationOptions.Badge,
            CancellationToken ct = default)
        {
            EnsureInitialized();
            ValidateOptions(options);
            return RequestNotificationPermissionCoreAsync(options, ct);
        }

        /// <summary>
        /// 打开应用系统设置。返回 true 仅表示已成功发起跳转，不表示用户已看到页面或修改设置。
        /// </summary>
        /// <returns>是否成功发起跳转。</returns>
        public override UniTask<bool> OpenAppSettingsAsync()
        {
            EnsureInitialized();
            return OpenAppSettingsPlatformAsync();
        }

        /// <summary>
        /// 打开当前应用的系统通知设置。返回 true 仅表示已成功发起跳转，不表示用户已看到页面或修改设置。
        /// 无法精准跳转时返回 false，不回退到应用设置。
        /// </summary>
        /// <returns>是否成功发起精准通知设置页跳转。</returns>
        public override UniTask<bool> OpenNotificationSettingsAsync()
        {
            EnsureInitialized();
            return OpenNotificationSettingsPlatformAsync();
        }

        /// <summary>
        /// 查询平台状态并把取消限制在当前等待者。
        /// </summary>
        /// <param name="ct">调用方取消令牌。</param>
        /// <returns>当前通知权限状态。</returns>
        private async UniTask<NotificationPermissionStatus> GetNotificationPermissionStatusCoreAsync(CancellationToken ct)
        {
            return await GetNotificationPermissionStatusPlatformAsync().AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 协调并发通知权限请求，避免系统弹窗重入和参数覆盖。
        /// </summary>
        /// <param name="options">通知授权选项。</param>
        /// <param name="ct">调用方取消令牌。</param>
        /// <returns>通知权限请求结果。</returns>
        private async UniTask<NotificationPermissionResult> RequestNotificationPermissionCoreAsync(
            NotificationAuthorizationOptions options,
            CancellationToken ct)
        {
            UniTaskCompletionSource<NotificationPermissionResult> pending;
            bool waitsForDifferentOptions = false;

            lock (m_RequestLock)
            {
                EnsureRequestCanContinue();
                pending = m_InFlightPermissionRequest;
                if (pending == null)
                {
                    pending = new UniTaskCompletionSource<NotificationPermissionResult>();
                    m_InFlightPermissionRequest = pending;
                    m_InFlightPermissionOptions = options;
                    CompletePermissionRequestAsync(options, pending).Forget();
                }
                else
                {
                    waitsForDifferentOptions = m_InFlightPermissionOptions != options;
                }
            }

            NotificationPermissionResult completed = await pending.Task.AttachExternalCancellation(ct);
            if (!waitsForDifferentOptions)
            {
                return completed;
            }

            EnsureRequestCanContinue();
            return await RequestNotificationPermissionCoreAsync(options, ct);
        }

        /// <summary>
        /// 执行单个底层权限请求，并确保异常与清理都汇聚到共享 TCS。
        /// </summary>
        /// <param name="options">通知授权选项。</param>
        /// <param name="pending">本次共享请求完成源。</param>
        private async UniTaskVoid CompletePermissionRequestAsync(
            NotificationAuthorizationOptions options,
            UniTaskCompletionSource<NotificationPermissionResult> pending)
        {
            NotificationPermissionResult result = default;
            bool canceled = false;
            try
            {
                result = await RequestNotificationPermissionPlatformAsync(options);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            catch (Exception exception)
            {
                result = new NotificationPermissionResult(
                    false,
                    NotificationPermissionStatus.Unknown,
                    errorMessage: exception.Message);
            }
            finally
            {
                // 必须先清空 in-flight 标记再唤醒等待者，避免不同 options 的递归等待再次命中已完成请求。
                lock (m_RequestLock)
                {
                    if (ReferenceEquals(m_InFlightPermissionRequest, pending))
                    {
                        m_InFlightPermissionRequest = null;
                        m_InFlightPermissionOptions = NotificationAuthorizationOptions.None;
                    }
                }
            }

            if (canceled)
            {
                pending.TrySetCanceled();
            }
            else
            {
                pending.TrySetResult(result);
            }
        }

        /// <summary>
        /// 验证通知授权选项不为空且不包含未知位。
        /// </summary>
        /// <param name="options">待验证选项。</param>
        private static void ValidateOptions(NotificationAuthorizationOptions options)
        {
            if (options == NotificationAuthorizationOptions.None || (options & ~c_AllOptions) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), options, "通知授权选项为空或包含未知值。");
            }
        }

        /// <summary>
        /// 确保 Manager 已完成 Initialize 且尚未 Shutdown。
        /// </summary>
        private void EnsureInitialized()
        {
            if (m_IsShutdown || m_Config == null)
            {
                throw new InvalidOperationException("NativeManager 尚未初始化或已经关闭。");
            }
        }

        /// <summary>
        /// 在请求排队与异步续发边界重新检查生命周期，关闭后不得启动新的平台调用。
        /// </summary>
        private void EnsureRequestCanContinue()
        {
            lock (m_RequestLock)
            {
                EnsureInitialized();
            }
        }
    }
}
