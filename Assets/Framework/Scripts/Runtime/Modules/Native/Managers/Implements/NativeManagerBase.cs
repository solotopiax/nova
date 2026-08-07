/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeManagerBase.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   NativeManager 抽象基类
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// NativeManager 抽象基类，收口框架原生能力契约。
    /// </summary>
    internal abstract class NativeManagerBase : FrameworkManager, INativeManager
    {
        /// <summary>
        /// Native 在 SDK 之前更新、SDK 之后关闭，保证 SDK 清理阶段仍可访问原生桥接。
        /// </summary>
        public override int Priority => 15;

        /// <summary>
        /// 初始化原生能力管理器。
        /// </summary>
        /// <param name="config">NativeManager 配置。</param>
        public abstract void Initialize(NativeManagerConfig config);

        /// <summary>
        /// 查询操作系统当前通知权限状态。
        /// </summary>
        /// <param name="ct">调用方取消令牌。</param>
        /// <returns>当前通知权限状态。</returns>
        public abstract UniTask<NotificationPermissionStatus> GetNotificationPermissionStatusAsync(CancellationToken ct = default);

        /// <summary>
        /// 请求通知权限。
        /// </summary>
        /// <param name="options">通知授权选项。</param>
        /// <param name="ct">调用方取消令牌。</param>
        /// <returns>通知权限请求结果。</returns>
        public abstract UniTask<NotificationPermissionResult> RequestNotificationPermissionAsync(
            NotificationAuthorizationOptions options = NotificationAuthorizationOptions.Alert |
                                                       NotificationAuthorizationOptions.Sound |
                                                       NotificationAuthorizationOptions.Badge,
            CancellationToken ct = default);

        /// <summary>
        /// 打开应用系统设置。
        /// </summary>
        /// <returns>是否成功发起跳转。</returns>
        public abstract UniTask<bool> OpenAppSettingsAsync();

        /// <summary>
        /// NativeManager 无周期任务。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭原生桥接并取消所有等待者。
        /// </summary>
        public abstract override void Shutdown();
    }
}

