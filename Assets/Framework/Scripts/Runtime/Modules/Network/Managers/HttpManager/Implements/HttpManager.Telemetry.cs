/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpManager.Telemetry.cs
 * author:    taoye
 * created:   2026/9/1
 * descrip:   HTTP 管理器 UWR 埋点编排
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
    internal sealed partial class HttpManager
    {
        /// <summary>
        /// 执行并观测单 URL 请求，保持一条逻辑请求仅产生一个 start 和一个 end。
        /// </summary>
        private async UniTask<HttpResponse> ExecuteTrackedSingleRequestAsync(
            string operationName,
            string method,
            string url,
            float requestTimeout,
            Func<UniTask<HttpResponse>> requestFactory,
            CancellationToken cancellationToken = default)
        {
            if (!m_EnableUWRTracks)
            {
                return await requestFactory();
            }

            string chainId = UwrNetworkTelemetry.CreateChainId();
            float effectiveTimeout = requestTimeout < 0f ? m_RequestTimeout : requestTimeout;
            UwrNetworkTelemetry.TrackStart(
                true, chainId, operationName, method, url, "direct", 0, effectiveTimeout);

            Stopwatch stopwatch = Stopwatch.StartNew();
            HttpResponse response = null;
            Exception requestException = null;
            try
            {
                response = await requestFactory();
                return response;
            }
            catch (Exception exception)
            {
                requestException = exception;
                throw;
            }
            finally
            {
                bool cancelled = cancellationToken.IsCancellationRequested &&
                                 (response == null || !response.HasServerResponse);
                long elapsedMs = stopwatch.ElapsedMilliseconds;
                if (UwrNetworkTelemetry.ShouldTrackError(response, requestException, cancelled))
                {
                    UwrNetworkTelemetry.TrackError(
                        true,
                        chainId,
                        operationName,
                        method,
                        url,
                        "direct",
                        0,
                        effectiveTimeout,
                        elapsedMs,
                        response,
                        requestException,
                        cancelled);
                }

                UwrNetworkTelemetry.TrackEnd(
                    true,
                    chainId,
                    operationName,
                    method,
                    url,
                    "direct",
                    0,
                    effectiveTimeout,
                    elapsedMs,
                    elapsedMs,
                    response,
                    requestException,
                    cancelled);
            }
        }

        /// <summary>
        /// 按原始主备列表判断当前候选的稳定角色；最近成功排序不会改变角色。
        /// </summary>
        private static string ResolveBusinessRouteRole(string url, IReadOnlyList<string> originalRouteUrls)
        {
            if (originalRouteUrls == null || originalRouteUrls.Count == 0)
            {
                return "direct";
            }

            string endpointId = HttpFallbackPlanner.GetEndpointId(url);
            string primaryEndpointId = HttpFallbackPlanner.GetEndpointId(originalRouteUrls[0]);
            return string.Equals(endpointId, primaryEndpointId, StringComparison.OrdinalIgnoreCase)
                ? "primary"
                : "fallback";
        }
    }
}
