/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeComponent.AppInstall.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   独立 Android APK 安装能力
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    public sealed partial class NativeComponent
    {
        /// <summary>当前 Manager 提供可选安装接口且序列化总门已开启；不表示当前平台支持或系统已授权。</summary>
        public bool IsAppInstallEnabled => m_NativeManager is INativeAppInstaller && m_NativeManagerConfig != null &&
                                          m_NativeManagerConfig.EnableAndroidAppInstall;

        /// <summary>薄转发系统来源安装许可查询；不支持可选接口的自定义 Manager 返回 false。</summary>
        /// <param name="ct">当前调用的取消令牌。</param>
        /// <returns>系统是否允许安装请求。</returns>
        public UniTask<bool> CanRequestPackageInstallsAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return m_NativeManager is INativeAppInstaller installer
                ? installer.CanRequestPackageInstallsAsync(ct) : UniTask.FromResult(false);
        }

        /// <summary>薄转发显式来源安装许可设置跳转；不支持可选接口时返回 false。</summary>
        /// <returns>是否成功发起系统设置跳转，不表示已授权。</returns>
        public UniTask<bool> OpenPackageInstallSettingsAsync() => m_NativeManager is INativeAppInstaller installer
            ? installer.OpenPackageInstallSettingsAsync() : UniTask.FromResult(false);

        /// <summary>薄转发显式自身 APK 安装；旧自定义 Manager 未实现可选接口时返回 Unsupported。</summary>
        /// <param name="apkPath">persistentDataPath 内已完成 APK 文件绝对路径。</param>
        /// <param name="ct">取消当前安装准备与未完成 session，不回滚已成功安装。</param>
        /// <returns>系统或能力门的最终状态。</returns>
        public UniTask<NativeAppInstallResult> InstallApkAsync(string apkPath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return m_NativeManager is INativeAppInstaller installer ? installer.InstallApkAsync(apkPath, ct)
                : UniTask.FromResult(new NativeAppInstallResult(NativeAppInstallStatus.Unsupported));
        }
    }
}
