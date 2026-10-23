/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppManager.Download.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   App 管理器 —— APK 下载与跳商店
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 管理器。
    /// </summary>
    internal sealed partial class AppManager : AppManagerBase
    {
        /// <summary>
        /// 触发 App 下载（DownloadRoute=Apk 时）。
        /// 仅 Android Player 在已命中更新后按冻结的 HTTPS 主备地址流式下载。
        /// 完整文件经大小与 APK 归档结构检查后原子提交；取消或失败保留此前正式文件。
        /// 返回路径不表示签名、目标版本或安装成功，安装仍由独立能力或业务层决定。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>下载完成的 apk 文件本地绝对路径；失败时 throw。</returns>
        public override UniTask<string> DownloadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (m_Config == null || !m_Config.EnableAppUpdate || m_Config.DownloadRoute != AppDownloadRoute.Apk ||
                (m_MatchedRule != AppDownloadRule.Forced && m_MatchedRule != AppDownloadRule.Recommended))
                throw new InvalidOperationException("APK 下载需要已初始化、启用更新且已命中的 Apk 路由。");
            string[] candidates = AppApkDownloader.BuildCandidates(m_TargetDownloadUrl, m_Config.FallbackDownloadUrl);
            return m_ApkDownloader.DownloadAsync(candidates, Path.Hotfix.ApkDownloadedFullPath, ct);
#else
            throw new PlatformNotSupportedException("内置 APK 文件下载仅支持 Android Player；不会在 Editor、iOS 或 WebGL 执行。");
#endif
        }

        /// <summary>
        /// 跳转到应用商店（DownloadRoute=Store 时）。
        /// 调用 Util.AppStore.OpenAsync 触发平台商店链接。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示已成功触发跳转；false 表示目标地址无效或跳转异常。</returns>
        public override async UniTask<bool> OpenStoreAsync(CancellationToken ct)
        {
            string url = ResolveStoreUrl();
            if (!IsValidUrl(url))
            {
                Log.Error(LogTag.App, "OpenStoreAsync 失败：目标商店地址为空。");
                return false;
            }

            try
            {
                await Util.AppStore.OpenAsync(url);
                Log.Debug(LogTag.App, "已触发商店跳转：{0}", url);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(LogTag.App, "OpenStoreAsync 异常：{0}", ex.Message);
                return false;
            }
        }
    }
}
