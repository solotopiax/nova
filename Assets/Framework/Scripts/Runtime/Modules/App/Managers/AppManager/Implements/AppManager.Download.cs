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
        /// 内部根据 AppManagerConfig 选择主备地址，
        /// 调用 IHttpManager 完成下载，写入 persistentDataPath。
        /// 待业务侧打通后补全写盘与进度回调。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>下载完成的 apk 文件本地绝对路径；失败时 throw。</returns>
        public override UniTask<string> DownloadAsync(CancellationToken ct)
        {
            throw new NotImplementedException("DownloadAsync 待业务侧打通后补全。");
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
