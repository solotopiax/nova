/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PushToken.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   推送令牌数据类
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 推送令牌数据类，由 IPushPlugin.GetTokenAsync 返回或 OnTokenRefreshed 事件携带。
    /// 不变量：Value 非空，代表设备在当前推送平台的唯一注册令牌；令牌可能因系统重置而变更，需监听 OnTokenRefreshed 保持同步。
    /// </summary>
    public sealed class PushToken
    {
        /// <summary>
        /// 令牌原始字符串值，由推送平台（FCM/APNs 等）分配，非空。
        /// </summary>
        public string Value;

        /// <summary>
        /// 令牌来源平台标识（如 "FCM"、"APNs"），便于多推送平台并存时区分。
        /// </summary>
        public string Provider;
    }
}
