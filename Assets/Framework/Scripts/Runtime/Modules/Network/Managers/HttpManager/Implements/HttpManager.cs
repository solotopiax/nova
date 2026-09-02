/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpManager.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   HTTP管理器
 ***************************************************************/

using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器，负责 HTTP 短连接请求发送、AES 加解密与 GZip 压缩处理。
    /// </summary>
    internal sealed partial class HttpManager : HttpManagerBase, IBusinessHttpManager, IPhysicalHttpManager
    {
        /// <summary>
        /// 初始化 HttpManager 的新实例。
        /// </summary>
        public HttpManager()
        {
        }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(HttpManagerConfig config)
        {
            ClearAllBusinessRoutePreferences();
            m_EnableUWRTracks = config.EnableUWRTracks;
            m_PreferLastSuccessfulHost = config.PreferLastSuccessfulHost;
            m_BusinessFallbackRoundCount = Math.Max(1, config.BusinessFallbackRoundCount);
            m_RetryRequestCount = Math.Max(0, config.RetryRequestCount);
            m_RequestTimeout = config.RequestTimeout;
            m_LastNetworkReachability = Application.internetReachability;
            m_Transport = new UnityWebRequestTransport();
            m_Transport.Initialize(m_RequestTimeout);
        }

        /// <summary>
        /// 异步发送 GET 请求。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public override UniTask<HttpResponse> GetAsync(string url, float requestTimeout = -1f, string headerInfos = null)
        {
            return ExecuteTrackedSingleRequestAsync(
                "http_get",
                "GET",
                url,
                requestTimeout,
                () => m_Transport.GetAsync(url, requestTimeout, headerInfos));
        }

        /// <summary>
        /// 发送一次不自动创建埋点链的可取消 GET，供 App 等模块自行包装完整主备链。
        /// </summary>
        public UniTask<HttpResponse> GetPhysicalAsync(
            string url,
            float requestTimeout,
            string headerInfos,
            CancellationToken cancellationToken)
        {
            return m_Transport.GetAsync(url, requestTimeout, headerInfos, cancellationToken);
        }

        /// <summary>
        /// 异步发送 POST 请求（字符串 body）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentString">请求体字符串。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public override UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout = -1f, string headerInfos = null)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(contentString ?? string.Empty);
            return ExecuteTrackedSingleRequestAsync(
                "http_post",
                "POST",
                url,
                requestTimeout,
                () => m_Transport.PostAsync(url, bodyBytes, requestTimeout, headerInfos));
        }

        /// <summary>
        /// 异步发送 POST 请求（字节流 body，明文）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentBytes">请求体字节数组。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public override UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout = -1f, string headerInfos = null)
        {
            return ExecuteTrackedSingleRequestAsync(
                "http_post_raw",
                "POST",
                url,
                requestTimeout,
                () => m_Transport.PostRawDataAsync(
                    url,
                    contentBytes,
                    requestTimeout,
                    headerInfos,
                    CancellationToken.None));
        }

        /// <summary>
        /// 异步发送 POST 请求（multipart 文件上传，明文）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="bodyJsonData">表单字段 JSON 数据，可为 null。</param>
        /// <param name="fileBytes">文件字节数组。</param>
        /// <param name="fileName">文件名。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public override UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout = -1f, string headerInfos = null)
        {
            return ExecuteTrackedSingleRequestAsync(
                "http_post_file",
                "POST",
                url,
                requestTimeout,
                () => m_Transport.PostFileAsync(
                    url,
                    bodyJsonData,
                    fileBytes,
                    fileName,
                    requestTimeout,
                    headerInfos));
        }

        /// <summary>
        /// 异步下载二进制数据，支持空闲超时与进度回调。
        /// </summary>
        /// <param name="url">下载地址。</param>
        /// <param name="idleTimeout">空闲超时时间（秒），连续 N 秒无新字节时中止，-1 使用默认请求超时。</param>
        /// <param name="progressCallback">下载进度回调，参数为包含已下载字节数与总字节数的 HttpResponse（中间态，IsSuccess 为 false）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含下载结果的 HttpResponse。</returns>
        public override UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default)
        {
            return ExecuteTrackedSingleRequestAsync(
                "http_download_binary",
                "GET",
                url,
                idleTimeout,
                () => m_Transport.DownloadBinaryAsync(url, idleTimeout, progressCallback, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// 异步下载文本内容，支持空闲超时与进度回调。
        /// </summary>
        /// <param name="url">下载地址。</param>
        /// <param name="idleTimeout">空闲超时时间（秒），连续 N 秒无新字节时中止，-1 使用默认请求超时。</param>
        /// <param name="progressCallback">下载进度回调，参数为包含已下载字节数与总字节数的 HttpResponse（中间态，IsSuccess 为 false）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含下载结果的 HttpResponse，文本内容存储在 Body 字段中。</returns>
        public override UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default)
        {
            return ExecuteTrackedSingleRequestAsync(
                "http_download_text",
                "GET",
                url,
                idleTimeout,
                () => m_Transport.DownloadTextAsync(url, idleTimeout, progressCallback, cancellationToken),
                cancellationToken);
        }

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public override void Update()
        {
            NetworkReachability currentReachability = Application.internetReachability;
            if (currentReachability == m_LastNetworkReachability)
            {
                return;
            }

            m_LastNetworkReachability = currentReachability;
            ClearAllBusinessRoutePreferences();
        }

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public override void Shutdown()
        {
            m_Transport?.Shutdown();
            m_Transport = null;
            ClearAllBusinessRoutePreferences();
        }
    }
}
