/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeAppInstallStatus.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   独立 Android APK 安装能力
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>自身 APK 显式安装的最终状态；系统确认界面发起不是安装成功。</summary>
    public enum NativeAppInstallStatus
    {
        /// <summary>Editor、非 Android 或 Android API 28 以下不支持。</summary>
        Unsupported = 0,
        /// <summary>NativeManagerConfig.EnableAndroidAppInstall 未开启。</summary>
        Disabled = 1,
        /// <summary>系统未允许此应用请求安装；业务可显式打开来源许可设置后重试。</summary>
        PermissionRequired = 2,
        /// <summary>PackageInstaller 报告 STATUS_SUCCESS；自身更新可能先被系统终止进程。</summary>
        Installed = 3,
        /// <summary>系统安装被用户取消或报告主动放弃；调用者令牌取消另抛取消异常。</summary>
        Cancelled = 4,
        /// <summary>验证、准备、系统安装或原生桥接失败，详情见结果错误文本。</summary>
        Failed = 5,
    }
}
