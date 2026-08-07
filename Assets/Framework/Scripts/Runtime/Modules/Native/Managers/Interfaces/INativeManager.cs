/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  INativeManager.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   框架原生能力管理接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架层访问操作系统原生能力的统一接口。
    /// </summary>
    public interface INativeManager
    {
        /// <summary>
        /// 初始化原生能力管理器。
        /// </summary>
        /// <param name="config">NativeManager 配置。</param>
        void Initialize(NativeManagerConfig config);

        /// <summary>
        /// 查询操作系统当前通知权限状态。
        /// </summary>
        /// <param name="ct">只取消当前调用方等待，不取消底层系统查询。</param>
        /// <returns>当前通知权限状态。</returns>
        UniTask<NotificationPermissionStatus> GetNotificationPermissionStatusAsync(CancellationToken ct = default);

        /// <summary>
        /// 请求通知权限。相同选项并发调用共享一个系统请求，不同选项串行执行。
        /// </summary>
        /// <param name="options">需要请求的通知能力。</param>
        /// <param name="ct">只取消当前调用方等待，不取消共享的系统请求。</param>
        /// <returns>请求流程与请求后系统状态。</returns>
        UniTask<NotificationPermissionResult> RequestNotificationPermissionAsync(
            NotificationAuthorizationOptions options = NotificationAuthorizationOptions.Alert |
                                                       NotificationAuthorizationOptions.Sound |
                                                       NotificationAuthorizationOptions.Badge,
            CancellationToken ct = default);

        /// <summary>
        /// 打开应用系统设置。返回 true 只表示成功发起跳转，不表示用户修改了设置。
        /// </summary>
        /// <returns>是否成功发起设置页跳转。</returns>
        UniTask<bool> OpenAppSettingsAsync();
    }
}

