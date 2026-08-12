/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeManager.Platform.Android.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   Android 通知权限与应用设置桥接
 ***************************************************************/

#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;

namespace NovaFramework.Runtime
{
    internal sealed partial class NativeManager
    {
        private const string c_PostNotificationsPermission = "android.permission.POST_NOTIFICATIONS";
        private const string c_NotificationPermissionRequestedKey = "Nova.Native.NotificationPermissionRequested";
        private const string c_ApplicationNotificationSettingsAction = "android.settings.APP_NOTIFICATION_SETTINGS";
        private const string c_ApplicationPackageExtra = "android.provider.extra.APP_PACKAGE";

        private PermissionCallbacks m_AndroidPermissionCallbacks;
        private UniTaskCompletionSource<NotificationPermissionResult> m_AndroidPermissionRequest;

        /// <summary>
        /// Android 桥接无需预初始化，权限仅在业务显式调用时请求。
        /// </summary>
        private void InitializePlatform()
        {
        }

        /// <summary>
        /// 查询 Android 通知总开关与 Android 13 运行时权限的组合状态。
        /// </summary>
        /// <returns>当前通知权限状态。</returns>
        private UniTask<NotificationPermissionStatus> GetNotificationPermissionStatusPlatformAsync()
        {
            try
            {
                return UniTask.FromResult(GetAndroidNotificationPermissionStatus());
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Base, "查询 Android 通知权限失败：{0}。", exception);
                return UniTask.FromResult(NotificationPermissionStatus.Unknown);
            }
        }

        /// <summary>
        /// Android 13 及以上请求 POST_NOTIFICATIONS；低版本仅返回当前通知总开关状态。
        /// </summary>
        /// <param name="options">统一接口选项，Android 的展示能力由通知渠道控制。</param>
        /// <returns>通知权限请求结果。</returns>
        private UniTask<NotificationPermissionResult> RequestNotificationPermissionPlatformAsync(
            NotificationAuthorizationOptions options)
        {
            try
            {
                if (GetAndroidSdkInt() < 33 || Permission.HasUserAuthorizedPermission(c_PostNotificationsPermission))
                {
                    NotificationPermissionStatus status = GetAndroidNotificationPermissionStatus();
                    return UniTask.FromResult(new NotificationPermissionResult(true, status));
                }

                m_AndroidPermissionRequest = new UniTaskCompletionSource<NotificationPermissionResult>();
                m_AndroidPermissionCallbacks = new PermissionCallbacks();
                m_AndroidPermissionCallbacks.PermissionGranted += CompleteAndroidPermissionRequest;
                m_AndroidPermissionCallbacks.PermissionDenied += CompleteAndroidPermissionRequest;
                m_AndroidPermissionCallbacks.PermissionDeniedAndDontAskAgain += CompleteAndroidPermissionRequest;
                Permission.RequestUserPermission(c_PostNotificationsPermission, m_AndroidPermissionCallbacks);
                return m_AndroidPermissionRequest.Task;
            }
            catch (Exception exception)
            {
                return UniTask.FromResult(new NotificationPermissionResult(
                    false,
                    NotificationPermissionStatus.Unknown,
                    errorMessage: exception.Message));
            }
        }

        /// <summary>
        /// 打开 Android 应用详情设置页。
        /// </summary>
        /// <returns>是否成功发起 Activity 跳转。</returns>
        private UniTask<bool> OpenAppSettingsPlatformAsync()
        {
            try
            {
                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject intent = new AndroidJavaObject(
                    "android.content.Intent",
                    "android.settings.APPLICATION_DETAILS_SETTINGS");
                using AndroidJavaClass uriClass = new AndroidJavaClass("android.net.Uri");
                using AndroidJavaObject uri = uriClass.CallStatic<AndroidJavaObject>(
                    "parse",
                    "package:" + Application.identifier);
                using AndroidJavaObject configuredIntent = intent.Call<AndroidJavaObject>("setData", uri);
                activity.Call("startActivity", configuredIntent);
                return UniTask.FromResult(true);
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Base, "打开 Android 应用设置失败：{0}。", exception);
                return UniTask.FromResult(false);
            }
        }

        /// <summary>
        /// Android 8.0 及以上打开当前应用的通知设置页；低版本不回退到应用详情设置页。
        /// </summary>
        /// <returns>是否成功发起精准通知设置页跳转。</returns>
        private UniTask<bool> OpenNotificationSettingsPlatformAsync()
        {
            try
            {
                if (GetAndroidSdkInt() < 26)
                {
                    return UniTask.FromResult(false);
                }

                using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using AndroidJavaObject intent = new AndroidJavaObject(
                    "android.content.Intent",
                    c_ApplicationNotificationSettingsAction);
                using AndroidJavaObject configuredIntent = intent.Call<AndroidJavaObject>(
                    "putExtra",
                    c_ApplicationPackageExtra,
                    Application.identifier);
                activity.Call("startActivity", configuredIntent);
                return UniTask.FromResult(true);
            }
            catch (Exception exception)
            {
                Log.Error(LogTag.Base, "打开 Android 通知设置失败：{0}。", exception);
                return UniTask.FromResult(false);
            }
        }

        /// <summary>
        /// 取消 Android pending 请求并释放回调引用。
        /// </summary>
        private void ShutdownPlatform()
        {
            m_AndroidPermissionRequest?.TrySetCanceled();
            m_AndroidPermissionRequest = null;
            m_AndroidPermissionCallbacks = null;
        }

        /// <summary>
        /// 处理 Android 权限请求的三类完成回调，并以系统实时状态生成结果。
        /// </summary>
        /// <param name="permissionName">完成回调对应的权限名称。</param>
        private void CompleteAndroidPermissionRequest(string permissionName)
        {
            UniTaskCompletionSource<NotificationPermissionResult> pending = m_AndroidPermissionRequest;
            m_AndroidPermissionRequest = null;
            m_AndroidPermissionCallbacks = null;

            if (pending == null || m_IsShutdown)
            {
                return;
            }

            PlayerPrefs.SetInt(c_NotificationPermissionRequestedKey, 1);
            try
            {
                pending.TrySetResult(new NotificationPermissionResult(
                    true,
                    GetAndroidNotificationPermissionStatus()));
            }
            catch (Exception exception)
            {
                pending.TrySetResult(new NotificationPermissionResult(
                    false,
                    NotificationPermissionStatus.Unknown,
                    errorMessage: exception.Message));
            }
        }

        /// <summary>
        /// 综合 Android 通知总开关、运行时权限与首次请求历史生成跨平台状态。
        /// </summary>
        /// <returns>当前通知权限状态。</returns>
        private static NotificationPermissionStatus GetAndroidNotificationPermissionStatus()
        {
            bool notificationsEnabled = AreAndroidNotificationsEnabled();
            if (GetAndroidSdkInt() < 33)
            {
                return notificationsEnabled
                    ? NotificationPermissionStatus.Authorized
                    : NotificationPermissionStatus.Denied;
            }

            bool permissionGranted = Permission.HasUserAuthorizedPermission(c_PostNotificationsPermission);
            if (permissionGranted && notificationsEnabled)
            {
                return NotificationPermissionStatus.Authorized;
            }

            return PlayerPrefs.GetInt(c_NotificationPermissionRequestedKey, 0) == 0
                ? NotificationPermissionStatus.NotDetermined
                : NotificationPermissionStatus.Denied;
        }

        /// <summary>
        /// 获取 Android API Level，不依赖任何 SDK 编译宏。
        /// </summary>
        /// <returns>Android API Level。</returns>
        private static int GetAndroidSdkInt()
        {
            using AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION");
            return versionClass.GetStatic<int>("SDK_INT");
        }

        /// <summary>
        /// 查询 Android 7.0 及以上 NotificationManager 的应用通知总开关。
        /// </summary>
        /// <returns>应用通知总开关是否启用。</returns>
        private static bool AreAndroidNotificationsEnabled()
        {
            if (GetAndroidSdkInt() < 24)
            {
                return true;
            }

            using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using AndroidJavaObject manager = activity.Call<AndroidJavaObject>("getSystemService", "notification");
            return manager != null && manager.Call<bool>("areNotificationsEnabled");
        }
    }
}
#endif
