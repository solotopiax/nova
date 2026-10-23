/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeManagerConfig.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   NativeManager 启动配置
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// NativeManager 启动配置；平台安装能力默认关闭，权限与系统确认只能显式发起。
    /// </summary>
    [Serializable]
    public sealed class NativeManagerConfig
    {
        /// <summary>允许业务显式安装自身 APK；默认 false，开启后 Android 构建声明来源安装权限，API28 以下不支持。</summary>
        public bool EnableAndroidAppInstall;
    }
}

