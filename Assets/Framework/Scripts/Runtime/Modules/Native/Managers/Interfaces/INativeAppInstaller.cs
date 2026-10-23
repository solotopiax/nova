/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  INativeAppInstaller.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   独立 Android APK 安装能力
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>可选 Android 自身应用安装能力；旧 INativeManager 实现无需新增必实现成员。</summary>
    public interface INativeAppInstaller
    {
        /// <summary>查询当前操作系统是否允许此应用请求安装；不打开设置或系统安装界面。</summary>
        /// <param name="ct">取消当前查询。</param>
        /// <returns>只有安装能力已启用、平台支持且系统来源许可有效时为 true。</returns>
        UniTask<bool> CanRequestPackageInstallsAsync(CancellationToken ct = default);

        /// <summary>业务显式打开当前应用的来源安装许可设置，不自动启动安装。</summary>
        /// <returns>是否成功发起精准设置跳转，不代表用户已授权。</returns>
        UniTask<bool> OpenPackageInstallSettingsAsync();

        /// <summary>显式安装 persistentDataPath 内的完整 APK；只接受自身包名、严格有效签名及相同当前 signer 集合、递增 versionCode。</summary>
        /// <param name="apkPath">受控目录内无符号链接的已完成文件绝对路径，不接受 URL 或外部目录。</param>
        /// <param name="ct">取消会停止准备、放弃未完成 session 并清理临时文件；不能回滚已完成安装。</param>
        /// <returns>系统最终状态；不承诺静默安装或自动重启，不绑定远端 CheckAsync 命中版本。</returns>
        UniTask<NativeAppInstallResult> InstallApkAsync(string apkPath, CancellationToken ct = default);
    }
}
