/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetManager.WebGLCatalog.cs
 * author:    taoye
 * created:   2026/9/17
 * descrip:   WebGL 内置 Catalog 布局与文件系统选择依据
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using YooAsset;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace NovaFramework.Runtime
{
    internal sealed partial class AssetManager
    {
        // YooAsset 的 WebServerFileSystem 使用该官方文件判断 StreamingAssets 中实际包含的 Bundle。
        private const string c_WebGLBuiltinCatalogFileName = "BuiltinCatalog.bytes";

        // Player 构建后处理器始终生成该清单，避免纯 CDN 布局通过预期 404 判断首包状态。
        private const string c_WebGLAssetLayoutFileName = "nova-webgl-layout.txt";
        private const string c_WebGLAssetLayoutHeader = "NOVA_WEBGL_LAYOUT_V1";

        /// <summary>
        /// 读取 WebGL Player 构建期生成的布局清单，并确定指定 Package 是否包含 YooAsset 官方 Catalog。
        /// Editor 直接检查本地文件；旧 Player 缺少清单时回退原 Catalog 探测以保持兼容。
        /// </summary>
        /// <param name="packageName">待初始化的 YooAsset Package 名称。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTask LoadWebGLBuiltinCatalogLayoutAsync(string packageName, CancellationToken ct)
        {
            if (m_WebGLBuiltinCatalogAvailability.ContainsKey(packageName))
            {
                return;
            }

#if UNITY_EDITOR
            string localCatalogPath = IOPath.Combine(
                Application.streamingAssetsPath,
                YooAssetConfiguration.GetYooFolderName(),
                packageName,
                c_WebGLBuiltinCatalogFileName);
            m_WebGLBuiltinCatalogAvailability[packageName] = IOFile.Exists(localCatalogPath);
            await UniTask.CompletedTask;
#else
            if (!m_WebGLBuiltinCatalogLayoutResolved)
            {
                m_WebGLBuiltinCatalogLayoutAvailable = await TryLoadWebGLAssetLayoutAsync(ct);
                m_WebGLBuiltinCatalogLayoutResolved = true;
            }

            if (m_WebGLBuiltinCatalogLayoutAvailable)
            {
                return;
            }

            await ProbeLegacyWebGLBuiltinCatalogAsync(packageName, ct);
#endif
        }

        /// <summary>
        /// 从 StreamingAssets 读取构建期布局清单；成功时记录所有带官方 Catalog 的 Package。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>清单存在且格式有效时返回 true。</returns>
        private async UniTask<bool> TryLoadWebGLAssetLayoutAsync(CancellationToken ct)
        {
            string buildId = Uri.EscapeDataString(Application.buildGUID ?? string.Empty);
            string url = $"{Application.streamingAssetsPath.TrimEnd('/')}/{c_WebGLAssetLayoutFileName}?build={buildId}";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = Math.Max(1, m_Config.CheckTimeout);
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                await UniTask.WaitUntil(() => operation.isDone, cancellationToken: ct);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Log.Warning(LogTag.Asset,
                        "WebGL 布局清单读取失败，将按旧版方式探测内置 Catalog：HTTP={0}, Detail={1}",
                        request.responseCode,
                        request.error);
                    return false;
                }

                string[] lines = request.downloadHandler.text.Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0 ||
                    !string.Equals(lines[0].Trim(), c_WebGLAssetLayoutHeader, StringComparison.Ordinal))
                {
                    Log.Warning(LogTag.Asset,
                        "WebGL 布局清单格式无效，将按旧版方式探测内置 Catalog：URL={0}",
                        url);
                    return false;
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    string package = lines[i].Trim();
                    if (!string.IsNullOrEmpty(package))
                    {
                        m_WebGLBuiltinCatalogAvailability[package] = true;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// 兼容没有布局清单的旧 WebGL 产物，通过请求官方 Catalog 判断指定 Package 是否带首包。
        /// </summary>
        /// <param name="packageName">待初始化的 YooAsset Package 名称。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTask ProbeLegacyWebGLBuiltinCatalogAsync(string packageName, CancellationToken ct)
        {
            string url = $"{Application.streamingAssetsPath.TrimEnd('/')}/" +
                         $"{Uri.EscapeDataString(YooAssetConfiguration.GetYooFolderName())}/" +
                         $"{Uri.EscapeDataString(packageName)}/{c_WebGLBuiltinCatalogFileName}";
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = Math.Max(1, m_Config.CheckTimeout);
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                await UniTask.WaitUntil(() => operation.isDone, cancellationToken: ct);

                bool exists = request.result == UnityWebRequest.Result.Success;
                m_WebGLBuiltinCatalogAvailability[packageName] = exists;
                if (exists)
                {
                    Log.Warning(LogTag.Asset,
                        "旧版 WebGL 产物未包含布局清单，已探测到内置 Catalog：Package={0}",
                        packageName);
                }
                else
                {
                    Log.Warning(LogTag.Asset,
                        "旧版 WebGL 产物未包含布局清单且内置 Catalog 未命中，将使用纯 CDN：Package={0}, HTTP={1}, Detail={2}",
                        packageName,
                        request.responseCode,
                        request.error);
                }
            }
        }

        /// <summary>
        /// 获取 WebGL 内置 Catalog 布局结果。
        /// </summary>
        /// <param name="packageName">YooAsset Package 名称。</param>
        /// <returns>Catalog 随 Player 发布时返回 true；纯 CDN 或探测失败时返回 false。</returns>
        private bool HasWebGLBuiltinCatalog(string packageName)
        {
            return m_WebGLBuiltinCatalogAvailability.TryGetValue(packageName, out bool exists) && exists;
        }
    }
}
