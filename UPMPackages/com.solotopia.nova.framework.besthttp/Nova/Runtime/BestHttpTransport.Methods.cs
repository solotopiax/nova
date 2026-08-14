/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BestHttpTransport.Methods.cs
 * author:    taoye
 * created:   2026/6/15
 * descrip:   BestHTTP transport adapter methods
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
#if NOVA_BEST_HTTP
using Best.HTTP;
#endif
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.BestHTTP.Runtime
{
    internal sealed partial class BestHttpTransport
    {
#if NOVA_BEST_HTTP
        /// <summary>
        /// 每个进程只检测一次内部 BestHTTP 的 SetIPAddress，并缓存开放实例委托供后续请求直接调用。
        /// </summary>
        private static void EnsureIPAddressCapability()
        {
            if (s_IPAddressCapabilityChecked)
            {
                return;
            }

            lock (s_IPAddressCapabilityLock)
            {
                if (s_IPAddressCapabilityChecked)
                {
                    return;
                }

                try
                {
                    MethodInfo method = typeof(HTTPRequest).GetMethod(
                        "SetIPAddress",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[] { typeof(IPAddress[]) },
                        null);
                    if (method != null)
                    {
                        s_SetIPAddress = (Action<HTTPRequest, IPAddress[]>)Delegate.CreateDelegate(
                            typeof(Action<HTTPRequest, IPAddress[]>),
                            null,
                            method);
                    }

                    s_IPAddressRoutingUnavailableReason = s_SetIPAddress == null
                        ? MissingSetIPAddressWarning
                        : null;
                }
                catch (Exception exception)
                {
                    DisableIPAddressRouting(exception);
                }
                finally
                {
                    s_IPAddressCapabilityChecked = true;
                }
            }
        }

        /// <summary>
        /// SetIPAddress 检测或调用失败后关闭本进程的 IP 路由能力，并保存固定中文原因。
        /// </summary>
        /// <param name="exception">导致能力不可用的真实异常。</param>
        private static void DisableIPAddressRouting(Exception exception)
        {
            s_SetIPAddress = null;
            s_IPAddressRoutingUnavailableReason = Txt.Format(
                "DoH 已配置为启用，但调用 BestHTTP 的 SetIPAddress 时发生异常，无法继续指定连接 IP，运行时已自动禁用 DoH 并改用系统 DNS。异常原因：{0}。",
                exception?.GetBaseException().Message ?? "未知异常");
        }

        private void ApplyTimeoutSettings(HTTPRequest request, float requestTimeout, float connectTimeout)
        {
            float effectiveRequestTimeout = requestTimeout < 0f ? m_RequestTimeout : requestTimeout;
            float effectiveConnectTimeout = connectTimeout < 0f ? m_ConnectTimeout : connectTimeout;
            request.TimeoutSettings.Timeout = TimeSpan.FromSeconds(effectiveRequestTimeout);
            request.TimeoutSettings.ConnectTimeout = TimeSpan.FromSeconds(effectiveConnectTimeout);
        }

        private static void ApplyHeaderInfos(HTTPRequest request, string headerInfos, string hostHeader)
        {
            bool hasHostHeader = false;
            if (!string.IsNullOrEmpty(headerInfos))
            {
                JObject headerJson = JObject.Parse(headerInfos);
                foreach (var kvp in headerJson)
                {
                    if (string.Equals(kvp.Key, "Host", StringComparison.OrdinalIgnoreCase))
                    {
                        hasHostHeader = true;
                    }

                    request.AddHeader(kvp.Key, kvp.Value.ToString());
                }
            }

            if (!hasHostHeader && !string.IsNullOrEmpty(hostHeader))
            {
                request.AddHeader("Host", hostHeader);
            }
        }

        private static void LogRequestResult(HTTPRequest request, Best.HTTP.HTTPResponse response)
        {
            if (response == null || !response.IsSuccess)
            {
                string error = response != null ? Txt.Format("{0} {1}", response.StatusCode, response.Message) : "No response";
                Log.Warning(LogTag.Http, "请求异常：{0}，URL：{1}。", error, request.CurrentUri);
            }
        }

        private async UniTask<HttpResponse> ExecuteRequestAsync(HTTPRequest request, string url, string cancelledMessage, string exceptionLogPrefix)
        {
            try
            {
                Best.HTTP.HTTPResponse response = await request.GetHTTPResponseAsync();
                LogRequestResult(request, response);
                return BuildHttpResponse(response);
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                return HttpResponse.Create(0, null, null, null, cancelledMessage, false, 0, -1L, HttpDeliveryState.Unknown);
            }
            catch (AsyncHTTPException e)
            {
                Log.Warning(LogTag.Http, "{0}：{1}，URL：{2}。", exceptionLogPrefix, e.Message, url);
                return HttpResponse.Create(
                    e.StatusCode,
                    e.Content,
                    null,
                    null,
                    e.Message,
                    false,
                    0,
                    -1L,
                    DetermineDeliveryState(request, e));
            }
        }

        /// <summary>
        /// 根据 BestHTTP 已知状态区分确定未发送与结果无法确认；正式 HTTP 状态码由 HttpResponse 自动判为已响应。
        /// </summary>
        private static HttpDeliveryState DetermineDeliveryState(HTTPRequest request, Exception exception)
        {
            if (request != null && request.State == HTTPRequestStates.ConnectionTimedOut)
            {
                return HttpDeliveryState.NotReachedServer;
            }

            string message = exception?.GetBaseException().Message ?? string.Empty;
            if (ContainsNetworkKeyword(
                    message,
                    "dns",
                    "resolve",
                    "name resolution",
                    "certificate",
                    "tls",
                    "ssl",
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
        /// 忽略大小写判断错误文本是否包含任一网络阶段关键词。
        /// </summary>
        private static bool ContainsNetworkKeyword(string value, params string[] keywords)
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
        /// 执行下载请求，并在 BestHTTP 回调返回前复制响应内容，避免响应释放后读取到空数据。
        /// </summary>
        private async UniTask<HttpResponse> DownloadCoreAsync(
            string url,
            int idleTimeout,
            Action<HttpResponse> progressCallback,
            CancellationToken cancellationToken,
            string cancelledMessage,
            string exceptionLogPrefix,
            string hostHeader)
        {
            int effectiveIdleTimeout = idleTimeout < 0 ? Mathf.CeilToInt(m_RequestTimeout) : idleTimeout;

            HTTPRequest request = HTTPRequest.CreateGet(url);
            request.TimeoutSettings.ConnectTimeout = TimeSpan.FromSeconds(m_ConnectTimeout);
            request.DownloadSettings.DisableCache = true;
            if (!string.IsNullOrEmpty(hostHeader))
            {
                request.AddHeader("Host", hostHeader);
            }

            long lastDownloadedBytes = 0;
            float lastProgressTime = Time.realtimeSinceStartup;

            request.DownloadSettings.OnDownloadProgress += (HTTPRequest req, long downloaded, long total) =>
            {
                lastDownloadedBytes = downloaded;
                lastProgressTime = Time.realtimeSinceStartup;
                if (progressCallback != null)
                {
                    HttpResponse progressResp = HttpResponse.Create(0, null, null, null, null, false, downloaded, total);
                    try
                    {
                        progressCallback.Invoke(progressResp);
                    }
                    finally
                    {
                        ReferencePool.Put(progressResp);
                    }
                }
            };

            CancellationTokenSource idleCts = new CancellationTokenSource();
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idleCts.Token);
            var responseTcs = new UniTaskCompletionSource<HttpResponse>();
            int completionState = 0;

            request.Callback = (req, response) =>
            {
                if (Volatile.Read(ref completionState) != 0)
                {
                    return;
                }

                HttpResponse capturedResponse;
                try
                {
                    capturedResponse = BuildDownloadResponse(
                        req,
                        response,
                        cancelledMessage,
                        exceptionLogPrefix,
                        url,
                        lastDownloadedBytes);
                }
                catch (Exception exception)
                {
                    if (Interlocked.CompareExchange(ref completionState, 1, 0) == 0)
                    {
                        responseTcs.TrySetException(exception);
                    }
                    return;
                }

                if (Interlocked.CompareExchange(ref completionState, 1, 0) == 0)
                {
                    if (!responseTcs.TrySetResult(capturedResponse))
                    {
                        ReferencePool.Put(capturedResponse);
                    }
                }
                else
                {
                    ReferencePool.Put(capturedResponse);
                }
            };
            _ = request.Send();

            try
            {
                while (true)
                {
                    bool completed = responseTcs.Task.Status != UniTaskStatus.Pending;
                    if (completed)
                    {
                        break;
                    }

                    if (Time.realtimeSinceStartup - lastProgressTime > effectiveIdleTimeout)
                    {
                        if (Interlocked.CompareExchange(ref completionState, 2, 0) == 0)
                        {
                            idleCts.Cancel();
                            request.Abort();
                            return HttpResponse.Create(0, null, null, null, Txt.Format("Idle timeout ({0}s) exceeded for URL: {1}。", effectiveIdleTimeout, url), false, lastDownloadedBytes, -1L);
                        }
                        break;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);
                }

                return await responseTcs.Task;
            }
            catch (OperationCanceledException)
            {
                if (Interlocked.CompareExchange(ref completionState, 2, 0) == 0)
                {
                    request.Abort();
                    return HttpResponse.Create(0, null, null, null, cancelledMessage, false, lastDownloadedBytes, -1L);
                }
                return await responseTcs.Task;
            }
            catch (AsyncHTTPException e)
            {
                Log.Warning(LogTag.Http, "{0}：{1}，URL：{2}。", exceptionLogPrefix, e.Message, url);
                return HttpResponse.Create(e.StatusCode, e.Content, null, null, e.Message, false, lastDownloadedBytes, -1L);
            }
            finally
            {
                linkedCts.Dispose();
                idleCts.Dispose();
            }
        }

        /// <summary>
        /// 在 BestHTTP 完成回调内将请求状态与响应内容转换为框架响应。
        /// </summary>
        private static HttpResponse BuildDownloadResponse(
            HTTPRequest request,
            Best.HTTP.HTTPResponse response,
            string cancelledMessage,
            string exceptionLogPrefix,
            string url,
            long downloadedBytes)
        {
            if (request.State == HTTPRequestStates.Finished)
            {
                return BuildHttpResponse(response);
            }

            string error;
            switch (request.State)
            {
                case HTTPRequestStates.Aborted:
                    error = cancelledMessage;
                    break;
                case HTTPRequestStates.ConnectionTimedOut:
                    error = "Connection Timed Out!";
                    break;
                case HTTPRequestStates.TimedOut:
                    error = "Processing the request Timed Out!";
                    break;
                case HTTPRequestStates.Error:
                    error = request.Exception?.Message ?? "No Exception";
                    break;
                default:
                    error = Txt.Format("Unexpected request state: {0}", request.State);
                    break;
            }

            if (request.State != HTTPRequestStates.Aborted)
            {
                Log.Warning(LogTag.Http, "{0}：{1}，URL：{2}。", exceptionLogPrefix, error, url);
            }
            return HttpResponse.Create(0, null, null, null, error, false, downloadedBytes, -1L);
        }

        private static HttpResponse BuildHttpResponse(Best.HTTP.HTTPResponse response)
        {
            if (response == null)
            {
                return HttpResponse.Create(0, null, null, null, "No response received.", false, 0, -1L);
            }

            Dictionary<string, string> headers = null;
            if (response.Headers != null)
            {
                headers = new Dictionary<string, string>();
                foreach (var kvp in response.Headers)
                {
                    headers[kvp.Key] = kvp.Value != null && kvp.Value.Count > 0 ? kvp.Value[0] : string.Empty;
                }
            }

            long downloadedBytes = response.Data != null ? response.Data.Length : 0;
            long totalBytes = response.Data != null ? response.Data.Length : -1L;
            return HttpResponse.Create(response.StatusCode, response.DataAsText, response.Data, headers, response.IsSuccess ? null : response.Message, response.IsSuccess, downloadedBytes, totalBytes);
        }
#endif
    }
}
