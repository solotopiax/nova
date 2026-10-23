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
    /// HTTP 管理器，固定使用 UnityWebRequest 发送普通请求、业务主备请求、上传与下载；本层不自动 AES 或 GZip。
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

        /// <summary>发送可取消单URL GET，向物理传输和唯一埋点链透传同一令牌。</summary>
        /// <param name="url">单一请求 URL。</param>
        /// <param name="requestTimeout">单次总超时（秒），负数使用当前默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 不附加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌；传输取消返回失败响应。</param>
        /// <returns>取消返回失败响应，调用方仍须finally归池。</returns>
        public override UniTask<HttpResponse> GetAsync(string url, float requestTimeout, string headerInfos, CancellationToken cancellationToken)
        {
            return ExecuteCancellableSingleRequestAsync("http_get", "GET", url, requestTimeout,
                () => m_Transport.GetAsync(url, requestTimeout, headerInfos, cancellationToken), cancellationToken);
        }

        /// <summary>发送可取消字符串POST，沿用原UTF8空字符串与二进制Content-Type语义。</summary>
        /// <param name="url">单一请求 URL。</param>
        /// <param name="contentString">按 UTF-8 编码的请求体，null 按旧语义为空。</param>
        /// <param name="requestTimeout">单次总超时（秒），负数使用当前默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 不附加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌；传输取消返回失败响应。</param>
        /// <returns>成功或取消失败响应均由调用方归池。</returns>
        public override UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout, string headerInfos, CancellationToken cancellationToken)
        {
            return ExecuteCancellableSingleRequestAsync("http_post", "POST", url, requestTimeout,
                () => m_Transport.PostAsync(url, Encoding.UTF8.GetBytes(contentString ?? string.Empty), requestTimeout, headerInfos, cancellationToken), cancellationToken);
        }

        /// <summary>发送可取消原始字节POST；单URL不进入业务主备或重试链。</summary>
        /// <param name="url">单一请求 URL。</param>
        /// <param name="contentBytes">原始请求体字节。</param>
        /// <param name="requestTimeout">单次总超时（秒），负数使用当前默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 不附加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌；传输取消返回失败响应。</param>
        /// <returns>调用方负责归池的响应，取消不抛OperationCanceledException。</returns>
        public override UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout, string headerInfos, CancellationToken cancellationToken)
        {
            return ExecuteCancellableSingleRequestAsync("http_post_raw", "POST", url, requestTimeout,
                () => m_Transport.PostRawDataAsync(url, contentBytes, requestTimeout, headerInfos, cancellationToken), cancellationToken);
        }

        /// <summary>发送可取消multipart上传，保留原表单/文件编码与请求Dispose边界。</summary>
        /// <param name="url">单一请求 URL。</param>
        /// <param name="bodyJsonData">一级表单字段 JSON，可为 null。</param>
        /// <param name="fileBytes">文件内容字节。</param>
        /// <param name="fileName">上传文件名。</param>
        /// <param name="requestTimeout">单次总超时（秒），负数使用当前默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 不附加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌；传输取消返回失败响应。</param>
        /// <returns>调用方负责归池的成功或失败响应。</returns>
        public override UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout, string headerInfos, CancellationToken cancellationToken)
        {
            return ExecuteCancellableSingleRequestAsync("http_post_file", "POST", url, requestTimeout,
                () => m_Transport.PostFileAsync(url, bodyJsonData, fileBytes, fileName, requestTimeout, headerInfos, cancellationToken), cancellationToken);
        }

        /// <summary>新可取消入口在发送前拒绝已取消请求，不触发请求工厂或UWR埋点；已开始请求复用唯一TrackedSingle链。</summary>
        /// <param name="operationName">既有稳定埋点操作名。</param>
        /// <param name="method">HTTP方法。</param>
        /// <param name="url">单URL。</param>
        /// <param name="requestTimeout">原请求总超时。</param>
        /// <param name="requestFactory">只有未取消时调用的物理请求工厂。</param>
        /// <param name="cancellationToken">物理传输及埋点共用的令牌。</param>
        /// <returns>未发送取消为NotReachedServer失败响应；已开始请求沿用原响应及池所有权。</returns>
        private UniTask<HttpResponse> ExecuteCancellableSingleRequestAsync(string operationName, string method, string url, float requestTimeout,
            Func<UniTask<HttpResponse>> requestFactory, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromResult(HttpResponse.Create(0, null, null, null, "Request cancelled.", false, 0, -1L, HttpDeliveryState.NotReachedServer));
            return ExecuteTrackedSingleRequestAsync(operationName, method, url, requestTimeout, requestFactory, cancellationToken);
        }

        /// <summary>
        /// 异步下载二进制数据，支持空闲超时与进度回调。
        /// </summary>
        /// <param name="url">下载地址。</param>
        /// <param name="idleTimeout">空闲超时时间（秒），连续 N 秒无新字节时中止，-1 使用默认请求超时。</param>
        /// <param name="progressCallback">下载进度回调；中间响应 IsSuccess 为 false，仅在本次回调内借用，由框架归池，调用方不得自行归池。</param>
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
        /// <param name="progressCallback">下载进度回调；中间响应 IsSuccess 为 false，仅在本次回调内借用，由框架归池，调用方不得自行归池。</param>
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
