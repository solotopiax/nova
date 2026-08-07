/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NotificationAuthorizationOptions.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   通知授权请求选项定义
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 通知授权请求选项。Android 仅使用该参数保持统一接口，具体展示能力由通知渠道控制。
    /// </summary>
    [Flags]
    public enum NotificationAuthorizationOptions : ulong
    {
        /// <summary>
        /// 未指定任何授权能力，此值不能用于发起请求。
        /// </summary>
        None = 0,

        /// <summary>
        /// 请求通知横幅或提醒权限。
        /// </summary>
        Alert = 1UL << 0,

        /// <summary>
        /// 请求通知声音权限。
        /// </summary>
        Sound = 1UL << 1,

        /// <summary>
        /// 请求应用角标权限。
        /// </summary>
        Badge = 1UL << 2,

        /// <summary>
        /// 请求 iOS provisional 静默临时授权。
        /// </summary>
        Provisional = 1UL << 3,
    }
}

