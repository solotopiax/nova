/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.WebGLCatalog.cs
 * author:    taoye
 * created:   2026/9/17
 * descrip:   WebGL 内置 Catalog 探测与文件系统选择依据
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using YooAsset;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager
    {
        // YooAsset 的 WebServerFileSystem 使用该官方文件判断 StreamingAssets 中实际包含的 Bundle。
        private const string c_WebGLBuiltinCatalogFileName = "BuiltinCatalog.bytes";

        /// <summary>
        /// 探测指定 Package 是否随 WebGL Player 部署了 YooAsset 官方 Catalog。
        /// Host 模式下，探测成功时启用 WebServer + WebNetwork；探测失败时使用纯 WebNetwork。
        /// </summary>
        /// <param name="packageName">待初始化的 YooAsset Package 名称。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTask ProbeWebGLBuiltinCatalogAsync(string packageName, CancellationToken ct)
        {
            if (m_WebGLBuiltinCatalogAvailability.ContainsKey(packageName))
            {
                return;
            }

            string url = BuildWebGLBuiltinCatalogUrl(packageName);
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = Math.Max(1, m_Config.CheckTimeout);
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                await UniTask.WaitUntil(() => operation.isDone, cancellationToken: ct);

                bool exists = request.result == UnityWebRequest.Result.Success;
                m_WebGLBuiltinCatalogAvailability[packageName] = exists;
                Log.Debug(LogTag.Asset,
                    "WebGL 内置 Catalog 探测完成：Package={0}, Exists={1}, Error={2}",
                    packageName,
                    exists,
                    request.error);
            }
        }

        /// <summary>
        /// 生成指定 Package 的 YooAsset 官方 Catalog URL；编辑器本地路径会转换为 file URL。
        /// </summary>
        /// <param name="packageName">YooAsset Package 名称。</param>
        /// <returns>可交给 UnityWebRequest 的 Catalog URL。</returns>
        private static string BuildWebGLBuiltinCatalogUrl(string packageName)
        {
            string root = Application.streamingAssetsPath.TrimEnd('/');
            if (root.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                root = new Uri(root).AbsoluteUri.TrimEnd('/');
            }

            return $"{root}/{Uri.EscapeDataString(YooAssetConfiguration.GetYooFolderName())}/" +
                   $"{Uri.EscapeDataString(packageName)}/{c_WebGLBuiltinCatalogFileName}";
        }

        /// <summary>
        /// 获取已完成的 WebGL 内置 Catalog 探测结果。
        /// </summary>
        /// <param name="packageName">YooAsset Package 名称。</param>
        /// <returns>Catalog 可访问时返回 true；纯 CDN 或探测失败时返回 false。</returns>
        private bool HasWebGLBuiltinCatalog(string packageName)
        {
            return m_WebGLBuiltinCatalogAvailability.TryGetValue(packageName, out bool exists) && exists;
        }
    }
}
