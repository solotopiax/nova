/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeAppInstallResult.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   独立 Android APK 安装能力
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>显式 APK 安装最终结果，不把系统确认界面或元数据解析解释为安装成功。</summary>
    public readonly struct NativeAppInstallResult
    {
        /// <summary>创建平台安装最终结果。</summary>
        /// <param name="status">平台或能力门的最终状态。</param>
        /// <param name="errorMessage">技术失败信息；不含下载 URL 或签名密钥。</param>
        public NativeAppInstallResult(NativeAppInstallStatus status, string errorMessage = null)
        { Status = status; ErrorMessage = errorMessage ?? string.Empty; }
        /// <summary>最终状态。</summary>
        public NativeAppInstallStatus Status { get; }
        /// <summary>技术失败信息。</summary>
        public string ErrorMessage { get; }
        /// <summary>仅系统明确报告安装成功时为 true。</summary>
        public bool IsInstalled => Status == NativeAppInstallStatus.Installed;
    }
}
