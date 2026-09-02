/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpManager.Methods.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   HTTP管理器 —— 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器。
    /// </summary>
    internal sealed partial class HttpManager : HttpManagerBase
    {
        /// <summary>
        /// 使用冻结的请求数据按主域名、备用域名顺序执行业务 POST。
        /// 每个候选均使用 UnityWebRequest 和系统 DNS；获得任何正式 HTTP 响应后立即结束。
        /// </summary>
        /// <param name="routeUrls">NetworkManager 解析出的主备完整 URL。</param>
        /// <param name="routeKey">最近成功偏好的隔离键，通常为 HostKey。</param>
        /// <param name="operationName">稳定业务操作名，通常为 NetCmd 名称。</param>
        /// <param name="contentBytes">整条请求链复用的冻结请求体。</param>
        /// <param name="requestTimeout">单次发送超时；负数使用默认值。</param>
        /// <param name="headerInfos">整条请求链复用的请求头 JSON。</param>
        /// <param name="cancellationToken">取消令牌；取消后立即停止且不切换候选。</param>
        /// <returns>正式 HTTP 响应或整条候选链的最终失败响应。</returns>
        public async UniTask<HttpResponse> PostBusinessRawDataAsync(
            IReadOnlyList<string> routeUrls,
            string routeKey,
            string operationName,
            byte[] contentBytes,
            float requestTimeout,
            string headerInfos,
            CancellationToken cancellationToken)
        {
            if (routeUrls == null || routeUrls.Count == 0)
            {
                Log.Error(LogTag.Http, "【通信失败】所有请求均未到达服务器，本次请求已结束。");
                return HttpResponse.Create(
                    0, null, null, null, "没有可用的主域名或备用域名。", false, 0, -1L,
                    HttpDeliveryState.NotReachedServer);
            }

            string effectiveRouteKey = string.IsNullOrWhiteSpace(routeKey)
                ? BuildBusinessRouteKey(routeUrls)
                : routeKey;
            HttpFallbackPreferenceSnapshot preference = m_PreferLastSuccessfulHost
                ? m_BusinessRoutePreferenceStore.Capture(effectiveRouteKey)
                : default;
            var policy = new HttpFallbackPolicy(
                m_BusinessFallbackRoundCount,
                m_RetryRequestCount,
                m_PreferLastSuccessfulHost);
            HttpFallbackExecutionPlan routePlan = HttpFallbackPlanner.Build(routeUrls, policy, preference);
            if (preference.HasValue && !PlanContainsEndpoint(routePlan, preference.EndpointId))
            {
                // 配置候选已变化时旧域名偏好失效；普通整链失败仍保留最近成功偏好。
                m_BusinessRoutePreferenceStore.ClearIfUnchanged(preference);
            }

            float effectiveRequestTimeout = requestTimeout < 0f ? m_RequestTimeout : requestTimeout;
            Stopwatch chainStopwatch = Stopwatch.StartNew();
            string chainId = UwrNetworkTelemetry.CreateChainId();
            string firstUrl = routePlan.CandidateCount > 0 ? routePlan.Candidates[0].Url : routeUrls[0];
            UwrNetworkTelemetry.TrackStart(
                m_EnableUWRTracks,
                chainId,
                operationName,
                "POST",
                firstUrl,
                ResolveBusinessRouteRole(firstUrl, routeUrls),
                0,
                effectiveRequestTimeout,
                routePlan,
                null,
                "network");
            HttpResponse lastFailedResponse = null;
            bool mayHaveReachedServer = false;
            string lastAttemptUrl = firstUrl;
            int lastSendIndex = 0;
            long lastSendElapsedMs = 0;
            Exception lastException = null;
            int attemptsStarted = 0;
            HttpFallbackStep lastStep = default;
            HttpFallbackExecutionCursor cursor = routePlan.CreateCursor();

            while (cursor.TryBeginNext(out HttpFallbackStep step))
            {
                string url = step.Candidate.Url;
                lastStep = step;
                lastAttemptUrl = url;
                lastSendIndex = ToTelemetrySendIndex(step.PhysicalSendIndex);
                if (cancellationToken.IsCancellationRequested)
                {
                    cursor.Cancel();
                    if (lastFailedResponse != null)
                    {
                        ReferencePool.Put(lastFailedResponse);
                    }

                    HttpResponse cancelledResponse = CreateCancelledResponse();
                    UwrNetworkTelemetry.TrackEnd(
                        m_EnableUWRTracks,
                        chainId,
                        operationName,
                        "POST",
                        url,
                        ResolveBusinessRouteRole(url, routeUrls),
                        lastSendIndex,
                        effectiveRequestTimeout,
                        0,
                        chainStopwatch.ElapsedMilliseconds,
                        cancelledResponse,
                        null,
                        true,
                        routePlan,
                        step,
                        attemptsStarted,
                        "network");
                    return cancelledResponse;
                }

                HttpResponse response;
                Exception attemptException = null;
                Stopwatch sendStopwatch = Stopwatch.StartNew();
                attemptsStarted++;
                try
                {
                    response = await m_Transport.PostRawDataAsync(
                        url,
                        contentBytes,
                        effectiveRequestTimeout,
                        headerInfos,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    attemptException = exception;
                    response = HttpResponse.Create(
                        0, null, null, null, exception.GetBaseException().Message, false, 0, -1L,
                        DetermineExceptionDeliveryState(exception));
                }
                lastSendElapsedMs = sendStopwatch.ElapsedMilliseconds;
                lastException = attemptException;

                if (cancellationToken.IsCancellationRequested &&
                    (response == null || !response.HasServerResponse))
                {
                    cursor.Cancel();
                    if (lastFailedResponse != null)
                    {
                        ReferencePool.Put(lastFailedResponse);
                    }

                    HttpResponse cancelledResponse = response ?? CreateCancelledResponse();
                    UwrNetworkTelemetry.TrackError(
                        m_EnableUWRTracks,
                        chainId,
                        operationName,
                        "POST",
                        url,
                        ResolveBusinessRouteRole(url, routeUrls),
                        lastSendIndex,
                        effectiveRequestTimeout,
                        lastSendElapsedMs,
                        cancelledResponse,
                        attemptException,
                        true,
                        routePlan,
                        step,
                        "network");
                    UwrNetworkTelemetry.TrackEnd(
                        m_EnableUWRTracks,
                        chainId,
                        operationName,
                        "POST",
                        url,
                        ResolveBusinessRouteRole(url, routeUrls),
                        lastSendIndex,
                        effectiveRequestTimeout,
                        lastSendElapsedMs,
                        chainStopwatch.ElapsedMilliseconds,
                        cancelledResponse,
                        attemptException,
                        true,
                        routePlan,
                        step,
                        attemptsStarted,
                        "network");
                    return cancelledResponse;
                }

                if (response != null && response.HasServerResponse)
                {
                    cursor.CompleteCurrent();
                    if (response.IsSuccess)
                    {
                        m_BusinessRoutePreferenceStore.MarkSuccess(effectiveRouteKey, step.Candidate.EndpointId);
                    }

                    if (lastFailedResponse != null)
                    {
                        ReferencePool.Put(lastFailedResponse);
                    }

                    if (UwrNetworkTelemetry.ShouldTrackError(response, attemptException, false))
                    {
                        UwrNetworkTelemetry.TrackError(
                            m_EnableUWRTracks,
                            chainId,
                            operationName,
                            "POST",
                            url,
                            ResolveBusinessRouteRole(url, routeUrls),
                            lastSendIndex,
                            effectiveRequestTimeout,
                            lastSendElapsedMs,
                            response,
                            attemptException,
                            false,
                            routePlan,
                            step,
                            "network");
                    }
                    UwrNetworkTelemetry.TrackEnd(
                        m_EnableUWRTracks,
                        chainId,
                        operationName,
                        "POST",
                        url,
                        ResolveBusinessRouteRole(url, routeUrls),
                        lastSendIndex,
                        effectiveRequestTimeout,
                        lastSendElapsedMs,
                        chainStopwatch.ElapsedMilliseconds,
                        response,
                        attemptException,
                        false,
                        routePlan,
                        step,
                        attemptsStarted,
                        "network");
                    return response;
                }

                UwrNetworkTelemetry.TrackError(
                    m_EnableUWRTracks,
                    chainId,
                    operationName,
                    "POST",
                    url,
                    ResolveBusinessRouteRole(url, routeUrls),
                    lastSendIndex,
                    effectiveRequestTimeout,
                    lastSendElapsedMs,
                    response,
                    attemptException,
                    false,
                    routePlan,
                    step,
                    "network");
                mayHaveReachedServer |= response == null || response.DeliveryState != HttpDeliveryState.NotReachedServer;
                cursor.RejectCurrent();
                if (cursor.State != HttpFallbackExecutionState.Exhausted)
                {
                    Log.Warning(
                        LogTag.Http,
                        "【继续重试】{0}，正在尝试下一个域名。当前地址：{1}。",
                        DescribeFailure(response),
                        url);
                }

                if (lastFailedResponse != null)
                {
                    ReferencePool.Put(lastFailedResponse);
                }

                lastFailedResponse = response;
            }

            if (mayHaveReachedServer)
            {
                Log.Error(LogTag.Http, "【结果未确认】请求可能已到达服务器，但未获得可确认的响应，本次请求已结束。");
            }
            else
            {
                Log.Error(LogTag.Http, "【通信失败】所有请求均未到达服务器，本次请求已结束。");
            }

            HttpDeliveryState finalDeliveryState = mayHaveReachedServer
                ? HttpDeliveryState.Unknown
                : HttpDeliveryState.NotReachedServer;
            if (lastFailedResponse == null)
            {
                HttpResponse emptyFailureResponse = HttpResponse.Create(
                    0, null, null, null,
                    mayHaveReachedServer ? "请求结果未确认。" : "网络通信失败。",
                    false, 0, -1L, finalDeliveryState);
                UwrNetworkTelemetry.TrackEnd(
                    m_EnableUWRTracks,
                    chainId,
                    operationName,
                    "POST",
                    lastAttemptUrl,
                    ResolveBusinessRouteRole(lastAttemptUrl, routeUrls),
                    lastSendIndex,
                    effectiveRequestTimeout,
                    lastSendElapsedMs,
                    chainStopwatch.ElapsedMilliseconds,
                    emptyFailureResponse,
                    lastException,
                    false,
                    routePlan,
                    lastStep,
                    attemptsStarted,
                    "network");
                return emptyFailureResponse;
            }

            HttpResponse finalResponse = HttpResponse.Create(
                lastFailedResponse.StatusCode,
                lastFailedResponse.Body,
                lastFailedResponse.RawData,
                lastFailedResponse.Headers,
                lastFailedResponse.Error,
                lastFailedResponse.IsSuccess,
                lastFailedResponse.DownloadedBytes,
                lastFailedResponse.TotalBytes,
                finalDeliveryState,
                lastFailedResponse.TransportState,
                lastFailedResponse.UploadedBytes,
                lastFailedResponse.TotalBytesIsKnown);
            UwrNetworkTelemetry.TrackEnd(
                m_EnableUWRTracks,
                chainId,
                operationName,
                "POST",
                lastAttemptUrl,
                ResolveBusinessRouteRole(lastAttemptUrl, routeUrls),
                lastSendIndex,
                effectiveRequestTimeout,
                lastSendElapsedMs,
                chainStopwatch.ElapsedMilliseconds,
                finalResponse,
                lastException,
                false,
                routePlan,
                lastStep,
                attemptsStarted,
                "network");
            ReferencePool.Put(lastFailedResponse);
            return finalResponse;
        }

        /// <summary>
        /// 清理全部业务最近成功偏好；网络环境变化、初始化关闭与 Shutdown 共用。
        /// </summary>
        private void ClearAllBusinessRoutePreferences()
        {
            m_BusinessRoutePreferenceStore.ClearAll();
        }

        /// <summary>
        /// 构造调用方主动取消时的响应，供业务链终止后统一释放。
        /// </summary>
        /// <returns>取消状态的池化响应。</returns>
        private static HttpResponse CreateCancelledResponse()
        {
            return HttpResponse.Create(
                0,
                null,
                null,
                null,
                "Request cancelled.",
                false,
                0,
                -1L,
                HttpDeliveryState.Unknown);
        }

        /// <summary>
        /// 使用所有候选基础地址构造没有显式 HostKey 时的隔离键。
        /// </summary>
        /// <param name="routeUrls">主备完整 URL。</param>
        /// <returns>稳定的候选基础地址组合。</returns>
        private static string BuildBusinessRouteKey(IReadOnlyList<string> routeUrls)
        {
            var parts = new List<string>(routeUrls.Count);
            for (int i = 0; i < routeUrls.Count; i++)
            {
                string endpointId = HttpFallbackPlanner.GetEndpointId(routeUrls[i]);
                if (!string.IsNullOrEmpty(endpointId))
                {
                    parts.Add(endpointId);
                }
            }

            return string.Join("|", parts);
        }

        /// <summary>
        /// 判断计划是否仍包含偏好快照指向的域名。
        /// </summary>
        private static bool PlanContainsEndpoint(HttpFallbackExecutionPlan plan, string endpointId)
        {
            for (int i = 0; i < plan.CandidateCount; i++)
            {
                if (string.Equals(plan.Candidates[i].EndpointId, endpointId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 将 long 物理发送索引安全压缩到既有埋点 int 字段。
        /// </summary>
        private static int ToTelemetrySendIndex(long physicalSendIndex)
        {
            return physicalSendIndex > int.MaxValue ? int.MaxValue : (int)physicalSendIndex;
        }

        private static string DescribeFailure(HttpResponse response)
        {
            if (response == null)
            {
                return "未获得任何网络响应";
            }

            string error = response.Error ?? string.Empty;
            if (ContainsAny(error, "timeout", "timed out", "超时")) return "请求超时";
            if (ContainsAny(error, "certificate", "证书")) return "证书校验失败";
            if (ContainsAny(error, "tls", "ssl")) return "TLS 握手失败";
            if (ContainsAny(error, "dns", "resolve", "name resolution")) return "DNS 解析失败";
            if (ContainsAny(error, "connect", "connection")) return "网络连接失败";
            return string.IsNullOrWhiteSpace(error) ? "网络通信失败" : "网络通信失败：" + error;
        }

        private static bool ContainsAny(string value, params string[] keywords)
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

        private static HttpDeliveryState DetermineExceptionDeliveryState(Exception exception)
        {
            string message = exception?.GetBaseException().Message ?? string.Empty;
            return ContainsAny(
                message,
                "dns",
                "resolve",
                "name resolution",
                "certificate",
                "证书",
                "tls",
                "ssl",
                "failed to connect",
                "could not connect",
                "cannot connect",
                "connection refused",
                "no route to host")
                ? HttpDeliveryState.NotReachedServer
                : HttpDeliveryState.Unknown;
        }
    }
}
