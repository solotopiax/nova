/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UnityWebRequestTransport.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   UnityWebRequest 默认 HTTP 传输后端
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 基于 UnityWebRequest 的内置 HTTP 传输，在没有更高优先级可选后端时提供默认网络能力。
    /// </summary>
    internal sealed class UnityWebRequestTransport : IHttpTransport
    {
        private const string c_BinaryContentType = "application/octet-stream";

        private float m_RequestTimeout = 60f;

        /// <summary>
        /// 初始化默认请求超时。UnityWebRequest 不支持独立连接超时，因此连接阶段受总请求超时约束。
        /// </summary>
        /// <param name="requestTimeout">默认请求总超时时间（秒）。</param>
        /// <param name="connectTimeout">默认连接超时时间（秒）；UnityWebRequest 无独立映射能力。</param>
        public void Initialize(float requestTimeout, float connectTimeout)
        {
            m_RequestTimeout = requestTimeout;
        }

        /// <summary>
        /// UnityWebRequest 不声明 IP 候选能力，避免 HTTPS Host、SNI 与证书校验语义被破坏。
        /// </summary>
        /// <param name="uri">原始请求 URI。</param>
        /// <returns>始终返回 false。</returns>
        public bool CanUseIpCandidate(Uri uri)
        {
            return false;
        }

        /// <summary>
        /// 异步发送 GET 请求并禁用业务不可控的缓存复用。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="requestTimeout">请求总超时时间（秒），负数使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒）；UnityWebRequest 无独立映射能力。</param>
        /// <param name="headerInfos">请求头 JSON 键值对。</param>
        /// <param name="hostHeader">IP 候选使用的 Host；当前后端不会请求 IP 候选。</param>
        /// <returns>HTTP 响应。</returns>
        public async UniTask<HttpResponse> GetAsync(string url, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
            using UnityWebRequest request = UnityWebRequest.Get(url);
            ApplyHeaderInfos(request, headerInfos, hostHeader);
            if (string.IsNullOrEmpty(request.GetRequestHeader("Cache-Control")))
            {
                request.SetRequestHeader("Cache-Control", "no-cache");
            }

            return await ExecuteRequestAsync(request, ResolveRequestTimeout(requestTimeout), "GET");
        }

        /// <summary>
        /// 异步发送字符串 POST 已编码后的 UTF-8 字节。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="bodyBytes">请求体字节。</param>
        /// <param name="requestTimeout">请求总超时时间（秒），负数使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒）；UnityWebRequest 无独立映射能力。</param>
        /// <param name="headerInfos">请求头 JSON 键值对。</param>
        /// <param name="hostHeader">IP 候选使用的 Host；当前后端不会请求 IP 候选。</param>
        /// <returns>HTTP 响应。</returns>
        public UniTask<HttpResponse> PostAsync(string url, byte[] bodyBytes, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
            return PostBytesAsync(url, bodyBytes, requestTimeout, headerInfos, hostHeader, "POST");
        }

        /// <summary>
        /// 异步发送原始二进制 POST 请求。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentBytes">原始请求体字节。</param>
        /// <param name="requestTimeout">请求总超时时间（秒），负数使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒）；UnityWebRequest 无独立映射能力。</param>
        /// <param name="headerInfos">请求头 JSON 键值对。</param>
        /// <param name="hostHeader">IP 候选使用的 Host；当前后端不会请求 IP 候选。</param>
        /// <returns>HTTP 响应。</returns>
        public UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
            return PostBytesAsync(url, contentBytes, requestTimeout, headerInfos, hostHeader, "POST RawData");
        }

        /// <summary>
        /// 异步发送 multipart 文件上传请求，并把 JSON 对象的一级属性转换为表单字段。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="bodyJsonData">表单字段 JSON 对象。</param>
        /// <param name="fileBytes">文件字节。</param>
        /// <param name="fileName">文件名。</param>
        /// <param name="requestTimeout">请求总超时时间（秒），负数使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒）；UnityWebRequest 无独立映射能力。</param>
        /// <param name="headerInfos">请求头 JSON 键值对。</param>
        /// <param name="hostHeader">IP 候选使用的 Host；当前后端不会请求 IP 候选。</param>
        /// <returns>HTTP 响应。</returns>
        public async UniTask<HttpResponse> PostFileAsync(
            string url,
            string bodyJsonData,
            byte[] fileBytes,
            string fileName,
            float requestTimeout,
            float connectTimeout,
            string headerInfos,
            string hostHeader)
        {
            var sections = new List<IMultipartFormSection>();
            if (!string.IsNullOrEmpty(bodyJsonData))
            {
                JObject bodyJson = JObject.Parse(bodyJsonData);
                foreach (KeyValuePair<string, JToken> field in bodyJson)
                {
                    sections.Add(new MultipartFormDataSection(field.Key, field.Value?.ToString() ?? string.Empty));
                }
            }

            sections.Add(new MultipartFormFileSection(
                "file",
                fileBytes ?? Array.Empty<byte>(),
                fileName ?? string.Empty,
                c_BinaryContentType));

            using UnityWebRequest request = UnityWebRequest.Post(url, sections);
            ApplyHeaderInfos(request, headerInfos, hostHeader);
            return await ExecuteRequestAsync(request, ResolveRequestTimeout(requestTimeout), "POST File");
        }

        /// <summary>
        /// 异步下载二进制数据，支持取消、下载进度与空闲超时。
        /// </summary>
        /// <param name="url">下载 URL。</param>
        /// <param name="idleTimeout">连续无新增字节的超时时间（秒），负数使用默认请求超时。</param>
        /// <param name="progressCallback">下载进度回调。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="hostHeader">IP 候选使用的 Host；当前后端不会请求 IP 候选。</param>
        /// <returns>HTTP 响应。</returns>
        public UniTask<HttpResponse> DownloadBinaryAsync(
            string url,
            int idleTimeout,
            Action<HttpResponse> progressCallback,
            CancellationToken cancellationToken,
            string hostHeader)
        {
            return DownloadAsync(url, idleTimeout, progressCallback, cancellationToken, hostHeader);
        }

        /// <summary>
        /// 异步下载文本数据，支持取消、下载进度与空闲超时。
        /// </summary>
        /// <param name="url">下载 URL。</param>
        /// <param name="idleTimeout">连续无新增字节的超时时间（秒），负数使用默认请求超时。</param>
        /// <param name="progressCallback">下载进度回调。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="hostHeader">IP 候选使用的 Host；当前后端不会请求 IP 候选。</param>
        /// <returns>HTTP 响应。</returns>
        public UniTask<HttpResponse> DownloadTextAsync(
            string url,
            int idleTimeout,
            Action<HttpResponse> progressCallback,
            CancellationToken cancellationToken,
            string hostHeader)
        {
            return DownloadAsync(url, idleTimeout, progressCallback, cancellationToken, hostHeader);
        }

        /// <summary>
        /// 关闭传输后端。当前实现不持有跨请求状态，无需额外释放。
        /// </summary>
        public void Shutdown()
        {
        }

        /// <summary>
        /// 创建并发送原始字节 POST 请求。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="bodyBytes">请求体字节。</param>
        /// <param name="requestTimeout">请求总超时时间（秒）。</param>
        /// <param name="headerInfos">请求头 JSON 键值对。</param>
        /// <param name="hostHeader">IP 候选使用的 Host。</param>
        /// <param name="requestTag">日志使用的请求标签。</param>
        /// <returns>HTTP 响应。</returns>
        private async UniTask<HttpResponse> PostBytesAsync(
            string url,
            byte[] bodyBytes,
            float requestTimeout,
            string headerInfos,
            string hostHeader,
            string requestTag)
        {
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes ?? Array.Empty<byte>()),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", c_BinaryContentType);
            ApplyHeaderInfos(request, headerInfos, hostHeader);
            return await ExecuteRequestAsync(request, ResolveRequestTimeout(requestTimeout), requestTag);
        }

        /// <summary>
        /// 执行普通请求，并用实时钟补足 UnityWebRequest 秒级超时的零值语义。
        /// </summary>
        /// <param name="request">已配置的请求。</param>
        /// <param name="timeoutSeconds">请求总超时时间（秒）。</param>
        /// <param name="requestTag">日志使用的请求标签。</param>
        /// <returns>HTTP 响应。</returns>
        private static async UniTask<HttpResponse> ExecuteRequestAsync(UnityWebRequest request, float timeoutSeconds, string requestTag)
        {
            float startTime = Time.realtimeSinceStartup;
            if (timeoutSeconds > 0f)
            {
                request.timeout = Mathf.Max(1, Mathf.CeilToInt(timeoutSeconds));
            }

            try
            {
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (timeoutSeconds <= 0f || Time.realtimeSinceStartup - startTime >= timeoutSeconds)
                    {
                        request.Abort();
                        return CreateFailureResponse(
                            request,
                            Txt.Format("{0} 请求超时（{1}s）。", requestTag, timeoutSeconds),
                            ToLong(request.downloadedBytes));
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                return BuildHttpResponse(request);
            }
            catch (Exception e)
            {
                request.Abort();
                Log.Warning(LogTag.Http, "{0} 请求异常：{1}，URL：{2}。", requestTag, e.Message, request.url);
                return CreateFailureResponse(request, e.Message, ToLong(request.downloadedBytes));
            }
        }

        /// <summary>
        /// 执行下载请求并监视取消、进度与空闲超时。
        /// </summary>
        /// <param name="url">下载 URL。</param>
        /// <param name="idleTimeout">空闲超时时间（秒）。</param>
        /// <param name="progressCallback">下载进度回调。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="hostHeader">IP 候选使用的 Host。</param>
        /// <returns>HTTP 响应。</returns>
        private async UniTask<HttpResponse> DownloadAsync(
            string url,
            int idleTimeout,
            Action<HttpResponse> progressCallback,
            CancellationToken cancellationToken,
            string hostHeader)
        {
            int effectiveIdleTimeout = idleTimeout < 0 ? Mathf.CeilToInt(m_RequestTimeout) : idleTimeout;
            using UnityWebRequest request = UnityWebRequest.Get(url);
            ApplyHeaderInfos(request, null, hostHeader);
            request.SetRequestHeader("Cache-Control", "no-cache");

            long lastDownloadedBytes = 0;
            float lastProgressTime = Time.realtimeSinceStartup;
            try
            {
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        request.Abort();
                        return CreateFailureResponse(request, "Download cancelled.", lastDownloadedBytes);
                    }

                    long downloadedBytes = ToLong(request.downloadedBytes);
                    if (downloadedBytes != lastDownloadedBytes)
                    {
                        lastDownloadedBytes = downloadedBytes;
                        lastProgressTime = Time.realtimeSinceStartup;
                        ReportProgress(request, progressCallback, downloadedBytes);
                    }

                    if (effectiveIdleTimeout <= 0 || Time.realtimeSinceStartup - lastProgressTime > effectiveIdleTimeout)
                    {
                        request.Abort();
                        return CreateFailureResponse(
                            request,
                            Txt.Format("Idle timeout ({0}s) exceeded for URL: {1}。", effectiveIdleTimeout, url),
                            lastDownloadedBytes);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                long completedBytes = ToLong(request.downloadedBytes);
                if (completedBytes != lastDownloadedBytes)
                {
                    ReportProgress(request, progressCallback, completedBytes);
                }

                return BuildHttpResponse(request);
            }
            catch (Exception e)
            {
                request.Abort();
                Log.Warning(LogTag.Http, "下载请求异常：{0}，URL：{1}。", e.Message, url);
                return CreateFailureResponse(request, e.Message, lastDownloadedBytes);
            }
        }

        /// <summary>
        /// 应用 JSON 请求头，并在调用方未显式提供 Host 时补充候选 Host。
        /// </summary>
        /// <param name="request">目标请求。</param>
        /// <param name="headerInfos">请求头 JSON 键值对。</param>
        /// <param name="hostHeader">候选 Host。</param>
        private static void ApplyHeaderInfos(UnityWebRequest request, string headerInfos, string hostHeader)
        {
            bool hasHostHeader = false;
            if (!string.IsNullOrEmpty(headerInfos))
            {
                JObject headerJson = JObject.Parse(headerInfos);
                foreach (KeyValuePair<string, JToken> header in headerJson)
                {
                    if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
                    {
                        hasHostHeader = true;
                    }

                    request.SetRequestHeader(header.Key, header.Value?.ToString() ?? string.Empty);
                }
            }

            if (!hasHostHeader && !string.IsNullOrEmpty(hostHeader))
            {
                request.SetRequestHeader("Host", hostHeader);
            }
        }

        /// <summary>
        /// 把完成的 UnityWebRequest 转换为框架池化响应。
        /// </summary>
        /// <param name="request">已完成请求。</param>
        /// <returns>框架 HTTP 响应。</returns>
        private static HttpResponse BuildHttpResponse(UnityWebRequest request)
        {
            byte[] rawData = request.downloadHandler?.data;
            string body = request.downloadHandler?.text;
            bool isSuccess = request.result == UnityWebRequest.Result.Success;
            Dictionary<string, string> headers = CopyResponseHeaders(request.GetResponseHeaders());
            long downloadedBytes = rawData?.LongLength ?? ToLong(request.downloadedBytes);
            long totalBytes = ReadTotalBytes(headers, downloadedBytes);
            return HttpResponse.Create(
                ToStatusCode(request.responseCode),
                body,
                rawData,
                headers,
                isSuccess ? null : request.error,
                isSuccess,
                downloadedBytes,
                totalBytes,
                DetermineDeliveryState(request, request.error));
        }

        /// <summary>
        /// 创建失败响应，并保留服务端已返回的状态、正文、字节与响应头。
        /// </summary>
        /// <param name="request">当前请求。</param>
        /// <param name="error">失败说明。</param>
        /// <param name="downloadedBytes">已下载字节数。</param>
        /// <returns>失败的框架 HTTP 响应。</returns>
        private static HttpResponse CreateFailureResponse(UnityWebRequest request, string error, long downloadedBytes)
        {
            byte[] rawData = request.downloadHandler?.data;
            Dictionary<string, string> headers = CopyResponseHeaders(request.GetResponseHeaders());
            return HttpResponse.Create(
                ToStatusCode(request.responseCode),
                request.downloadHandler?.text,
                rawData,
                headers,
                error,
                false,
                downloadedBytes,
                ReadTotalBytes(headers, rawData?.LongLength ?? -1L),
                DetermineDeliveryState(request, error));
        }

        /// <summary>
        /// 只把能够明确定位在 DNS、TLS 或 TCP 建连阶段的错误标记为未到达服务器，超时与连接中断保持无法确认。
        /// </summary>
        /// <param name="request">当前 UnityWebRequest。</param>
        /// <param name="error">Unity 返回或框架补充的错误描述。</param>
        /// <returns>单次请求的到达状态。</returns>
        private static HttpDeliveryState DetermineDeliveryState(UnityWebRequest request, string error)
        {
            if (request != null && request.responseCode > 0)
            {
                return HttpDeliveryState.ServerResponded;
            }

            string message = error ?? string.Empty;
            if (ContainsAnyNetworkKeyword(
                    message,
                    "could not resolve",
                    "cannot resolve",
                    "name resolution",
                    "dns",
                    "certificate",
                    "ssl",
                    "tls",
                    "failed to connect",
                    "could not connect",
                    "cannot connect",
                    "connection refused",
                    "no route to host"))
            {
                return HttpDeliveryState.NotReachedServer;
            }

            return HttpDeliveryState.Unknown;
        }

        /// <summary>
        /// 忽略大小写判断错误文本是否包含任一明确的网络阶段关键词。
        /// </summary>
        private static bool ContainsAnyNetworkKeyword(string value, params string[] keywords)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (value.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 向调用方报告一次下载进度，并在回调结束后归还中间响应。
        /// </summary>
        /// <param name="request">当前下载请求。</param>
        /// <param name="progressCallback">下载进度回调。</param>
        /// <param name="downloadedBytes">已下载字节数。</param>
        private static void ReportProgress(UnityWebRequest request, Action<HttpResponse> progressCallback, long downloadedBytes)
        {
            if (progressCallback == null)
            {
                return;
            }

            Dictionary<string, string> headers = CopyResponseHeaders(request.GetResponseHeaders());
            HttpResponse progress = HttpResponse.Create(
                0,
                null,
                null,
                null,
                null,
                false,
                downloadedBytes,
                ReadTotalBytes(headers, -1L));
            try
            {
                progressCallback(progress);
            }
            finally
            {
                ReferencePool.Put(progress);
            }
        }

        /// <summary>
        /// 复制响应头到大小写不敏感的字典。
        /// </summary>
        /// <param name="source">UnityWebRequest 响应头。</param>
        /// <returns>响应头副本，无响应头时返回 null。</returns>
        private static Dictionary<string, string> CopyResponseHeaders(Dictionary<string, string> source)
        {
            if (source == null)
            {
                return null;
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> header in source)
            {
                headers[header.Key] = header.Value;
            }

            return headers;
        }

        /// <summary>
        /// 从 Content-Length 读取总字节数，缺失时使用回退值。
        /// </summary>
        /// <param name="headers">响应头。</param>
        /// <param name="fallback">无法解析时的回退值。</param>
        /// <returns>总字节数。</returns>
        private static long ReadTotalBytes(Dictionary<string, string> headers, long fallback)
        {
            if (headers != null
                && headers.TryGetValue("Content-Length", out string contentLength)
                && long.TryParse(contentLength, out long totalBytes))
            {
                return totalBytes;
            }

            return fallback;
        }

        /// <summary>
        /// 解析本次请求应使用的总超时。
        /// </summary>
        /// <param name="requestTimeout">调用方超时，负数使用默认值。</param>
        /// <returns>有效总超时秒数。</returns>
        private float ResolveRequestTimeout(float requestTimeout)
        {
            return requestTimeout < 0f ? m_RequestTimeout : requestTimeout;
        }

        /// <summary>
        /// 安全地把 Unity 无符号下载字节数转换为有符号长整数。
        /// </summary>
        /// <param name="value">无符号字节数。</param>
        /// <returns>有符号字节数，溢出时钳制到 long 最大值。</returns>
        private static long ToLong(ulong value)
        {
            return value > long.MaxValue ? long.MaxValue : (long)value;
        }

        /// <summary>
        /// 安全地把 HTTP 状态码转换为框架使用的整数。
        /// </summary>
        /// <param name="statusCode">UnityWebRequest 状态码。</param>
        /// <returns>整数状态码，溢出时钳制到 int 最大值。</returns>
        private static int ToStatusCode(long statusCode)
        {
            if (statusCode <= 0)
            {
                return 0;
            }

            return statusCode > int.MaxValue ? int.MaxValue : (int)statusCode;
        }
    }
}
