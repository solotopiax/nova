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
using System.IO;
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
        /// 每个进程只反射检测一次内部 BestHTTP 的业务整链遥测入口。
        /// 官方原版没有这些入口时保持静默，不影响普通请求或 DoH 能力判断。
        /// </summary>
        private static void EnsureBusinessTelemetryCapability()
        {
            if (s_BusinessTelemetryCapabilityChecked)
            {
                return;
            }

            lock (s_BusinessTelemetryCapabilityLock)
            {
                if (s_BusinessTelemetryCapabilityChecked)
                {
                    return;
                }

                try
                {
                    Type telemetryChainType = typeof(HTTPRequest).Assembly.GetType(
                        "Best.HTTP.Telemetry.BestHttpTelemetryChain",
                        false);
                    MethodInfo createMethod = telemetryChainType?.GetMethod(
                        "Create",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(string) },
                        null);
                    MethodInfo completeMethod = telemetryChainType?.GetMethod(
                        "Complete",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(object) },
                        null);
                    MethodInfo bindMethod = typeof(HTTPRequest).GetMethod(
                        "SetTelemetryChain",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new[] { typeof(object) },
                        null);

                    if (createMethod != null && completeMethod != null && bindMethod != null)
                    {
                        s_CreateBusinessTelemetryChain = (Func<string, string, object>)Delegate.CreateDelegate(
                            typeof(Func<string, string, object>),
                            createMethod);
                        s_SetBusinessTelemetryChain = (Action<HTTPRequest, object>)Delegate.CreateDelegate(
                            typeof(Action<HTTPRequest, object>),
                            null,
                            bindMethod);
                        s_CompleteBusinessTelemetryChain = (Action<object>)Delegate.CreateDelegate(
                            typeof(Action<object>),
                            completeMethod);
                    }
                }
                catch
                {
                    // 内部 fork 与官方原版的签名不匹配时静默降级，不能影响请求发送。
                    s_CreateBusinessTelemetryChain = null;
                    s_SetBusinessTelemetryChain = null;
                    s_CompleteBusinessTelemetryChain = null;
                }
                finally
                {
                    s_BusinessTelemetryCapabilityChecked = true;
                }
            }
        }

        /// <summary>
        /// 发送绑定整链遥测的业务候选请求；连接 IP 为空时保留系统 DNS 路径。
        /// </summary>
        /// <param name="telemetryScope">本传输创建的整链遥测作用域。</param>
        /// <param name="url">保留原域名的业务 URL。</param>
        /// <param name="connectIPAddress">指定 TCP 连接 IP；null 表示系统 DNS。</param>
        /// <param name="contentBytes">冻结后的业务请求字节。</param>
        /// <param name="requestTimeout">本候选独享的请求超时。</param>
        /// <param name="connectTimeout">本候选独享的连接超时。</param>
        /// <param name="headerInfos">冻结后的请求头 JSON。</param>
        /// <returns>本候选的网络响应。</returns>
        private UniTask<HttpResponse> PostBusinessRawDataWithTelemetryAsync(
            BestHttpBusinessTelemetryScope telemetryScope,
            string url,
            IPAddress connectIPAddress,
            byte[] contentBytes,
            float requestTimeout,
            float connectTimeout,
            string headerInfos)
        {
            if (telemetryScope == null)
            {
                return connectIPAddress == null
                    ? PostRawDataAsync(url, contentBytes, requestTimeout, connectTimeout, headerInfos, null)
                    : PostRawDataAsync(url, connectIPAddress, contentBytes, requestTimeout, connectTimeout, headerInfos);
            }

            HTTPRequest request = HTTPRequest.CreatePost(url);
            if (connectIPAddress != null)
            {
                EnsureIPAddressCapability();
                if (s_SetIPAddress == null)
                {
                    return UniTask.FromResult(HttpResponse.Create(
                        0,
                        null,
                        null,
                        null,
                        IPAddressRoutingUnavailableReason,
                        false,
                        0,
                        -1L,
                        HttpDeliveryState.NotReachedServer));
                }

                try
                {
                    s_SetIPAddress(request, new[] { connectIPAddress });
                }
                catch (Exception exception)
                {
                    DisableIPAddressRouting(exception);
                    return UniTask.FromResult(HttpResponse.Create(
                        0,
                        null,
                        null,
                        null,
                        s_IPAddressRoutingUnavailableReason,
                        false,
                        0,
                        -1L,
                        HttpDeliveryState.NotReachedServer));
                }
            }

            telemetryScope.Attach(request, s_SetBusinessTelemetryChain);
            ApplyTimeoutSettings(request, requestTimeout, connectTimeout);
            request.UploadSettings.UploadStream = new MemoryStream(contentBytes);
            request.AddHeader("Content-Type", "application/octet-stream");
            ApplyHeaderInfos(request, headerInfos, null);
            return ExecuteRequestAsync(request, url, "POST RawData 请求已取消。", "POST RawData 请求异常");
        }

        /// <summary>
        /// 持有内部 fork 业务遥测链对象，并将完成信号收口为最多一次反射调用。
        /// </summary>
        private sealed class BestHttpBusinessTelemetryScope : IBusinessHttpTelemetryScope
        {
            private readonly object m_Chain;
            private readonly Action<object> m_Complete;
            private int m_Completed;

            /// <summary>
            /// 创建业务整链遥测作用域。
            /// </summary>
            /// <param name="chain">内部 fork 创建的链对象。</param>
            /// <param name="complete">内部 fork 的 BCL 完成委托。</param>
            internal BestHttpBusinessTelemetryScope(object chain, Action<object> complete)
            {
                m_Chain = chain;
                m_Complete = complete;
            }

            /// <summary>
            /// 将共享链对象绑定到一个即将发送的物理 HTTPRequest；绑定异常不会中断业务请求。
            /// </summary>
            /// <param name="request">即将发送的物理请求。</param>
            /// <param name="bind">内部 fork 的 BCL 绑定委托。</param>
            internal void Attach(HTTPRequest request, Action<HTTPRequest, object> bind)
            {
                try
                {
                    bind?.Invoke(request, m_Chain);
                }
                catch
                {
                    // 遥测绑定失败时继续发送，不能让观测能力影响网络可用性。
                }
            }

            /// <summary>
            /// 请求内部 fork 完成整条业务链；重复调用只保留第一次。
            /// </summary>
            public void Complete()
            {
                if (Interlocked.Exchange(ref m_Completed, 1) != 0)
                {
                    return;
                }

                try
                {
                    m_Complete?.Invoke(m_Chain);
                }
                catch
                {
                    // 遥测收口失败时不影响已完成的业务请求。
                }
            }

            /// <summary>
            /// using 作用域退出时自动完成，保证所有 return 和异常出口都不会遗漏最终收口。
            /// </summary>
            public void Dispose()
            {
                Complete();
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
