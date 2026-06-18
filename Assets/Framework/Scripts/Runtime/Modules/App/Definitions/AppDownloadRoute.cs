/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppDownloadRoute.cs
 * author:    taoye
 * created:   2024/8/28
 * descrip:   大版本强制更新路由方式
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 大版本强制更新路由方式。
    /// </summary>
    public enum AppDownloadRoute : byte
    {
        /// <summary>
        /// 跳转商店。
        /// </summary>
        Store = 0,

        /// <summary>
        /// 内部下载 APK。
        /// </summary>
        Apk,
    }
}
