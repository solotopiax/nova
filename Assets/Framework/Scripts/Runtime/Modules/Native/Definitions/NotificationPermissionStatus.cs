/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NotificationPermissionStatus.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   通知权限状态定义
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 操作系统当前的通知授权状态。
    /// </summary>
    public enum NotificationPermissionStatus
    {
        /// <summary>
        /// 原生层返回未知值或查询失败。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 当前平台不支持通知权限能力。
        /// </summary>
        Unsupported = 1,

        /// <summary>
        /// 用户尚未对通知权限作出选择。
        /// </summary>
        NotDetermined = 2,

        /// <summary>
        /// 用户拒绝或系统通知总开关已关闭。
        /// </summary>
        Denied = 3,

        /// <summary>
        /// 已获得完整通知授权。
        /// </summary>
        Authorized = 4,

        /// <summary>
        /// iOS 临时静默授权，通知默认仅进入通知中心。
        /// </summary>
        Provisional = 5,

        /// <summary>
        /// iOS App Clip 临时授权。
        /// </summary>
        Ephemeral = 6,
    }
}

