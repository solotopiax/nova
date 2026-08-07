/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeManager.Platform.iOS.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   iOS 通知权限与应用设置桥接
 ***************************************************************/

#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    internal sealed partial class NativeManager
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationStatusCallback(ulong requestId, int authorizationStatus);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NotificationRequestCallback(
            ulong requestId,
            int authorizationStatus,
            long errorCode,
            IntPtr errorDomainUtf8,
            IntPtr errorMessageUtf8);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void OpenSettingsCallback(ulong requestId, int opened);

        private static readonly NotificationStatusCallback s_NotificationStatusCallback = OnNotificationStatus;
        private static readonly NotificationRequestCallback s_NotificationRequestCallback = OnNotificationRequest;
        private static readonly OpenSettingsCallback s_OpenSettingsCallback = OnOpenSettings;
        private static NativeManager s_ActiveManager;
        private static long s_NextIosRequestId;

        private readonly Dictionary<ulong, UniTaskCompletionSource<NotificationPermissionStatus>> m_IosStatusRequests = new();
        private readonly Dictionary<ulong, UniTaskCompletionSource<NotificationPermissionResult>> m_IosPermissionRequests = new();
        private readonly Dictionary<ulong, UniTaskCompletionSource<bool>> m_IosOpenSettingsRequests = new();

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NovaNative_GetNotificationPermissionStatus(
            ulong requestId,
            NotificationStatusCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NovaNative_RequestNotificationPermission(
            ulong requestId,
            ulong options,
            NotificationRequestCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NovaNative_OpenAppSettings(
            ulong requestId,
            OpenSettingsCallback callback);

        /// <summary>
        /// 注册当前 NativeManager 作为静态 AOT 回调接收者。
        /// </summary>
        private void InitializePlatform()
        {
            if (s_ActiveManager != null && !ReferenceEquals(s_ActiveManager, this))
            {
                throw new InvalidOperationException("iOS NativeManager 已存在活动实例。");
            }
            s_ActiveManager = this;
        }

        /// <summary>
        /// 发起 iOS 通知状态查询，并按 requestId 保存独立完成源。
        /// </summary>
        /// <returns>当前通知权限状态。</returns>
        private UniTask<NotificationPermissionStatus> GetNotificationPermissionStatusPlatformAsync()
        {
            ulong requestId = NextIosRequestId();
            var pending = new UniTaskCompletionSource<NotificationPermissionStatus>();
            m_IosStatusRequests.Add(requestId, pending);
            try
            {
                NovaNative_GetNotificationPermissionStatus(requestId, s_NotificationStatusCallback);
            }
            catch (Exception exception)
            {
                m_IosStatusRequests.Remove(requestId);
                Log.Error(LogTag.Base, "查询 iOS 通知权限失败：{0}。", exception);
                pending.TrySetResult(NotificationPermissionStatus.Unknown);
            }
            return pending.Task;
        }

        /// <summary>
        /// 发起 iOS 通知授权请求；原生层会在请求后重新读取 UNNotificationSettings。
        /// </summary>
        /// <param name="options">通知授权选项。</param>
        /// <returns>通知权限请求结果。</returns>
        private UniTask<NotificationPermissionResult> RequestNotificationPermissionPlatformAsync(
            NotificationAuthorizationOptions options)
        {
            ulong requestId = NextIosRequestId();
            var pending = new UniTaskCompletionSource<NotificationPermissionResult>();
            m_IosPermissionRequests.Add(requestId, pending);
            try
            {
                NovaNative_RequestNotificationPermission(requestId, (ulong)options, s_NotificationRequestCallback);
            }
            catch (Exception exception)
            {
                m_IosPermissionRequests.Remove(requestId);
                pending.TrySetResult(new NotificationPermissionResult(
                    false,
                    NotificationPermissionStatus.Unknown,
                    errorMessage: exception.Message));
            }
            return pending.Task;
        }

        /// <summary>
        /// 打开 iOS 通知设置页，低版本由原生层回退到应用设置页。
        /// </summary>
        /// <returns>是否成功发起跳转。</returns>
        private UniTask<bool> OpenAppSettingsPlatformAsync()
        {
            ulong requestId = NextIosRequestId();
            var pending = new UniTaskCompletionSource<bool>();
            m_IosOpenSettingsRequests.Add(requestId, pending);
            try
            {
                NovaNative_OpenAppSettings(requestId, s_OpenSettingsCallback);
            }
            catch (Exception exception)
            {
                m_IosOpenSettingsRequests.Remove(requestId);
                Log.Error(LogTag.Base, "打开 iOS 应用设置失败：{0}。", exception);
                pending.TrySetResult(false);
            }
            return pending.Task;
        }

        /// <summary>
        /// 取消全部 iOS pending 请求并解除静态回调接收者。
        /// </summary>
        private void ShutdownPlatform()
        {
            CancelAndClear(m_IosStatusRequests);
            CancelAndClear(m_IosPermissionRequests);
            CancelAndClear(m_IosOpenSettingsRequests);
            if (ReferenceEquals(s_ActiveManager, this))
            {
                s_ActiveManager = null;
            }
        }

        /// <summary>
        /// 生成非零递增 requestId，避免异步回调相互覆盖。
        /// </summary>
        /// <returns>新的 requestId。</returns>
        private ulong NextIosRequestId()
        {
            long next = Interlocked.Increment(ref s_NextIosRequestId);
            if (next == 0)
            {
                next = Interlocked.Increment(ref s_NextIosRequestId);
            }
            return unchecked((ulong)next);
        }

        /// <summary>
        /// 取消并清空一组 iOS pending 完成源。
        /// </summary>
        /// <typeparam name="T">完成结果类型。</typeparam>
        /// <param name="requests">待清理字典。</param>
        private static void CancelAndClear<T>(Dictionary<ulong, UniTaskCompletionSource<T>> requests)
        {
            foreach (UniTaskCompletionSource<T> pending in requests.Values)
            {
                pending.TrySetCanceled();
            }
            requests.Clear();
        }

        /// <summary>
        /// 接收 iOS 状态查询回调。原生层保证在主队列调用。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="authorizationStatus">UNAuthorizationStatus 原始值。</param>
        [MonoPInvokeCallback(typeof(NotificationStatusCallback))]
        private static void OnNotificationStatus(ulong requestId, int authorizationStatus)
        {
            NativeManager manager = s_ActiveManager;
            if (manager == null || !manager.m_IosStatusRequests.Remove(requestId, out var pending))
            {
                return;
            }
            pending.TrySetResult(MapIosAuthorizationStatus(authorizationStatus));
        }

        /// <summary>
        /// 接收 iOS 授权请求回调，立即复制仅在回调期有效的 UTF-8 字符串。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="authorizationStatus">请求后的 UNAuthorizationStatus。</param>
        /// <param name="errorCode">NSError code。</param>
        /// <param name="errorDomainUtf8">NSError domain UTF-8 指针。</param>
        /// <param name="errorMessageUtf8">NSError 描述 UTF-8 指针。</param>
        [MonoPInvokeCallback(typeof(NotificationRequestCallback))]
        private static void OnNotificationRequest(
            ulong requestId,
            int authorizationStatus,
            long errorCode,
            IntPtr errorDomainUtf8,
            IntPtr errorMessageUtf8)
        {
            NativeManager manager = s_ActiveManager;
            if (manager == null || !manager.m_IosPermissionRequests.Remove(requestId, out var pending))
            {
                return;
            }

            string errorDomain = Marshal.PtrToStringAnsi(errorDomainUtf8) ?? string.Empty;
            string errorMessage = Marshal.PtrToStringAnsi(errorMessageUtf8) ?? string.Empty;
            bool success = errorCode == 0 && string.IsNullOrEmpty(errorDomain);
            pending.TrySetResult(new NotificationPermissionResult(
                success,
                MapIosAuthorizationStatus(authorizationStatus),
                errorCode,
                errorDomain,
                errorMessage));
        }

        /// <summary>
        /// 接收 iOS 设置页打开结果。true 只表示 URL 成功打开。
        /// </summary>
        /// <param name="requestId">请求标识。</param>
        /// <param name="opened">是否成功打开 URL。</param>
        [MonoPInvokeCallback(typeof(OpenSettingsCallback))]
        private static void OnOpenSettings(ulong requestId, int opened)
        {
            NativeManager manager = s_ActiveManager;
            if (manager == null || !manager.m_IosOpenSettingsRequests.Remove(requestId, out var pending))
            {
                return;
            }
            pending.TrySetResult(opened != 0);
        }

        /// <summary>
        /// 将 UNAuthorizationStatus 映射为框架状态，未知未来值保持 Unknown。
        /// </summary>
        /// <param name="authorizationStatus">UNAuthorizationStatus 原始值。</param>
        /// <returns>框架通知权限状态。</returns>
        private static NotificationPermissionStatus MapIosAuthorizationStatus(int authorizationStatus)
        {
            return authorizationStatus switch
            {
                0 => NotificationPermissionStatus.NotDetermined,
                1 => NotificationPermissionStatus.Denied,
                2 => NotificationPermissionStatus.Authorized,
                3 => NotificationPermissionStatus.Provisional,
                4 => NotificationPermissionStatus.Ephemeral,
                _ => NotificationPermissionStatus.Unknown,
            };
        }
    }
}
#endif
