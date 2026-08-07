/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeManager.Platform.Default.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   非移动平台 Native 能力回退
 ***************************************************************/

#if (!UNITY_ANDROID && !UNITY_IOS) || UNITY_EDITOR
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    internal sealed partial class NativeManager
    {
        /// <summary>
        /// 非移动平台无需初始化原生桥接。
        /// </summary>
        private void InitializePlatform()
        {
        }

        /// <summary>
        /// 非移动平台返回不支持。
        /// </summary>
        /// <returns>不支持状态。</returns>
        private UniTask<NotificationPermissionStatus> GetNotificationPermissionStatusPlatformAsync()
        {
            return UniTask.FromResult(NotificationPermissionStatus.Unsupported);
        }

        /// <summary>
        /// 非移动平台以成功完成且不支持的结果结束，不制造系统错误。
        /// </summary>
        /// <param name="options">通知授权选项。</param>
        /// <returns>不支持结果。</returns>
        private UniTask<NotificationPermissionResult> RequestNotificationPermissionPlatformAsync(
            NotificationAuthorizationOptions options)
        {
            return UniTask.FromResult(new NotificationPermissionResult(true, NotificationPermissionStatus.Unsupported));
        }

        /// <summary>
        /// 非移动平台没有应用设置页入口。
        /// </summary>
        /// <returns>固定返回 false。</returns>
        private UniTask<bool> OpenAppSettingsPlatformAsync()
        {
            return UniTask.FromResult(false);
        }

        /// <summary>
        /// 非移动平台无需清理原生桥接。
        /// </summary>
        private void ShutdownPlatform()
        {
        }
    }
}
#endif

