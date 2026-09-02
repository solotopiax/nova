/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UwrNetworkTelemetry.cs
 * author:    taoye
 * created:   2026/9/1
 * descrip:   UnityWebRequest 网络链路埋点
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 按“1 start → 0～N error → 1 end”契约构造并派发 UWR 网络链路事件。
    /// </summary>
    internal static class UwrNetworkTelemetry
    {
        internal const string StartEventName = "uwr_request_start";
        internal const string ErrorEventName = "uwr_request_error";
        internal const string EndEventName = "uwr_request_end";
        internal const int SchemaVersion = 1;
        private const int c_MaxPendingEvents = 128;

        private static readonly object s_Gate = new object();
        private static readonly Queue<TelemetryEventData> s_PendingEvents = new Queue<TelemetryEventData>();
        private static CancellationTokenSource s_ReadinessCancellation;

        /// <summary>
        /// 子系统重载时清理启动缓存与等待任务。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_ReadinessCancellation?.Cancel();
            s_ReadinessCancellation?.Dispose();
            s_ReadinessCancellation = null;
            lock (s_Gate)
            {
                s_PendingEvents.Clear();
            }
        }

        /// <summary>
        /// 创建单条逻辑请求链使用的关联 ID。
        /// </summary>
        /// <returns>无连字符的小写 GUID。</returns>
        internal static string CreateChainId()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 上报逻辑请求链开始事件；每条链只调用一次。
        /// </summary>
        internal static void TrackStart(
            bool enabled,
            string chainId,
            string operationName,
            string method,
            string url,
            string routeRole,
            int sendIndex,
            float requestTimeout,
            HttpFallbackExecutionPlan plan = null,
            HttpFallbackStep? step = null,
            string module = null,
            string downloadOperationId = null,
            string package = null,
            string fileType = null)
        {
            if (!enabled)
            {
                return;
            }

            Dictionary<string, object> properties = BuildBaseProperties(
                chainId, operationName, method, url, sendIndex, requestTimeout, plan, step, module,
                downloadOperationId, package, fileType);
            Track(StartEventName, properties);
        }

        /// <summary>
        /// 上报某个物理候选发送失败；HTTP 4xx/5xx 不调用本方法。
        /// </summary>
        internal static void TrackError(
            bool enabled,
            string chainId,
            string operationName,
            string method,
            string url,
            string routeRole,
            int sendIndex,
            float requestTimeout,
            long sendElapsedMs,
            HttpResponse response,
            Exception exception,
            bool cancelled,
            HttpFallbackExecutionPlan plan = null,
            HttpFallbackStep? step = null,
            string module = null,
            string leafErrorCodeOverride = null)
        {
            if (!enabled)
            {
                return;
            }

            Dictionary<string, object> properties = BuildBaseProperties(
                chainId, operationName, method, url, sendIndex, requestTimeout, plan, step, module,
                null, null, null);
            AddFailureProperties(properties, response, exception, cancelled);
            if (!string.IsNullOrWhiteSpace(leafErrorCodeOverride))
            {
                properties["uwr_leaf_error_code"] = SanitizeText(leafErrorCodeOverride);
                if (!properties.ContainsKey("uwr_error_type"))
                {
                    properties["uwr_error_type"] = "data_processing";
                }
            }
            properties["uwr_send_elapsed_ms"] = Math.Max(0L, sendElapsedMs);
            AddTransferProperties(properties, response);
            Track(ErrorEventName, properties);
        }

        /// <summary>
        /// 上报逻辑请求链唯一终态。
        /// </summary>
        internal static void TrackEnd(
            bool enabled,
            string chainId,
            string operationName,
            string method,
            string url,
            string routeRole,
            int sendIndex,
            float requestTimeout,
            long sendElapsedMs,
            long totalElapsedMs,
            HttpResponse response,
            Exception exception,
            bool cancelled,
            HttpFallbackExecutionPlan plan = null,
            HttpFallbackStep? step = null,
            int attemptsStarted = -1,
            string module = null,
            string resultOverride = null,
            string leafErrorCodeOverride = null,
            string downloadOperationId = null,
            string package = null,
            string fileType = null)
        {
            if (!enabled)
            {
                return;
            }

            Dictionary<string, object> properties = BuildBaseProperties(
                chainId, operationName, method, url, sendIndex, requestTimeout, plan, step, module,
                downloadOperationId, package, fileType);
            AddFailureProperties(properties, response, exception, cancelled);
            if (!string.IsNullOrWhiteSpace(leafErrorCodeOverride))
            {
                properties["uwr_leaf_error_code"] = SanitizeText(leafErrorCodeOverride);
                if (!properties.ContainsKey("uwr_error_type"))
                {
                    properties["uwr_error_type"] = "data_processing";
                }
            }
            properties["uwr_result"] = string.IsNullOrWhiteSpace(resultOverride)
                ? ResolveResult(response, cancelled)
                : SanitizeText(resultOverride);
            properties["uwr_send_elapsed_ms"] = Math.Max(0L, sendElapsedMs);
            properties["uwr_total_elapsed_ms"] = Math.Max(0L, totalElapsedMs);
            int startedSendCount = attemptsStarted < 0 ? Math.Max(1, sendIndex + 1) : Math.Max(0, attemptsStarted);
            properties["uwr_started_send_count"] = startedSendCount;
            properties["uwr_recovered_by_retry"] =
                string.Equals((string)properties["uwr_result"], "success", StringComparison.Ordinal) &&
                startedSendCount > 1;
            AddTransferProperties(properties, response);
            Track(EndEventName, properties);
        }

        /// <summary>
        /// 上报 YooAsset 管理的一条文件请求链开始。YooAsset 不公开 UWR 对象，仅记录其稳定暴露的信息。
        /// </summary>
        internal static void TrackAssetStart(
            bool enabled,
            string chainId,
            string url,
            float requestTimeout,
            HttpFallbackExecutionPlan plan,
            HttpFallbackStep step,
            string downloadOperationId,
            string package,
            string fileType)
        {
            TrackStart(enabled, chainId, "asset_download", "GET", url, string.Empty,
                ToSendIndex(step.PhysicalSendIndex), requestTimeout, plan, step, "asset",
                downloadOperationId, package, fileType);
        }

        /// <summary>
        /// 上报 YooAsset 暴露的候选失败。原始错误文本只用于本地分类，不上传。
        /// </summary>
        internal static void TrackAssetError(
            bool enabled,
            string chainId,
            string url,
            float requestTimeout,
            HttpFallbackExecutionPlan plan,
            HttpFallbackStep step,
            long sendElapsedMs,
            long statusCode,
            string error,
            string leafErrorCode,
            string downloadOperationId,
            string package,
            string fileType)
        {
            if (!enabled)
            {
                return;
            }

            Dictionary<string, object> properties = BuildBaseProperties(
                chainId, "asset_download", "GET", url, ToSendIndex(step.PhysicalSendIndex),
                requestTimeout, plan, step, "asset", downloadOperationId, package, fileType);
            AddRawFailureProperties(properties, statusCode, error, leafErrorCode);
            properties["uwr_send_elapsed_ms"] = Math.Max(0L, sendElapsedMs);
            Track(ErrorEventName, properties);
        }

        /// <summary>
        /// 上报 YooAsset 文件请求链终态。
        /// </summary>
        internal static void TrackAssetEnd(
            bool enabled,
            string chainId,
            string url,
            float requestTimeout,
            HttpFallbackExecutionPlan plan,
            HttpFallbackStep step,
            int startedSendCount,
            long sendElapsedMs,
            long totalElapsedMs,
            bool succeeded,
            long statusCode,
            string error,
            string leafErrorCode,
            string downloadOperationId,
            string package,
            string fileType)
        {
            if (!enabled)
            {
                return;
            }

            Dictionary<string, object> properties = BuildBaseProperties(
                chainId, "asset_download", "GET", url, ToSendIndex(step.PhysicalSendIndex),
                requestTimeout, plan, step, "asset", downloadOperationId, package, fileType);
            if (!succeeded)
            {
                AddRawFailureProperties(properties, statusCode, error, leafErrorCode);
            }
            properties["uwr_result"] = succeeded
                ? "success"
                : statusCode > 0 ? "http_error" : ResolveRawResult(error, leafErrorCode);
            properties["uwr_send_elapsed_ms"] = Math.Max(0L, sendElapsedMs);
            properties["uwr_total_elapsed_ms"] = Math.Max(0L, totalElapsedMs);
            properties["uwr_started_send_count"] = Math.Max(0, startedSendCount);
            properties["uwr_recovered_by_retry"] = succeeded && startedSendCount > 1;
            Track(EndEventName, properties);
        }

        /// <summary>
        /// 判断当前失败是否需要产生 error 事件；正式 HTTP 错误仅产生 end。
        /// </summary>
        internal static bool ShouldTrackError(HttpResponse response, Exception exception, bool cancelled)
        {
            if (cancelled || exception != null || response == null)
            {
                return true;
            }

            return !response.IsSuccess &&
                   (!response.HasServerResponse ||
                    string.Equals(response.TransportState, "DataProcessingError", StringComparison.Ordinal));
        }

        /// <summary>
        /// 构造三个事件共享的低基数请求与路由属性。
        /// </summary>
        private static Dictionary<string, object> BuildBaseProperties(
            string chainId,
            string operationName,
            string method,
            string url,
            int sendIndex,
            float requestTimeout,
            HttpFallbackExecutionPlan plan,
            HttpFallbackStep? step,
            string module,
            string downloadOperationId,
            string package,
            string fileType)
        {
            var properties = new Dictionary<string, object>
            {
                ["uwr_schema_version"] = SchemaVersion,
                ["uwr_chain_id"] = chainId ?? string.Empty,
                ["uwr_module"] = string.IsNullOrWhiteSpace(module) ? "network" : SanitizeText(module),
                ["uwr_method"] = (method ?? string.Empty).ToUpperInvariant(),
                ["uwr_send_index"] = sendIndex,
                ["uwr_request_timeout_sec"] = requestTimeout,
                ["uwr_network_reachability"] = Application.internetReachability.ToString()
            };

            if (!string.IsNullOrWhiteSpace(operationName))
            {
                properties["uwr_operation_name"] = SanitizeText(operationName);
            }

            if (plan != null)
            {
                properties["uwr_candidate_count"] = plan.CandidateCount;
            }

            if (step.HasValue)
            {
                HttpFallbackStep value = step.Value;
                properties["uwr_retry_index"] = value.RetryCycleIndex;
                properties["uwr_round_index"] = value.RoundIndex;
                properties["uwr_candidate_index"] = value.CandidateIndex;
                properties["uwr_candidate_count"] = value.CandidateCount;
                properties["uwr_send_index"] = value.PhysicalSendIndex;
            }
            else
            {
                properties["uwr_retry_index"] = 0;
                properties["uwr_round_index"] = 0;
                properties["uwr_candidate_index"] = 0;
                properties["uwr_candidate_count"] = plan?.CandidateCount ?? 1;
            }

            if (!string.IsNullOrWhiteSpace(downloadOperationId))
            {
                properties["uwr_download_operation_id"] = SanitizeText(downloadOperationId);
            }
            if (!string.IsNullOrWhiteSpace(package))
            {
                properties["uwr_package"] = SanitizeText(package);
            }
            if (!string.IsNullOrWhiteSpace(fileType))
            {
                properties["uwr_file_type"] = SanitizeText(fileType);
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                properties["uwr_scheme"] = uri.Scheme;
                properties["uwr_host"] = uri.Host;
                properties["uwr_port"] = uri.Port;
                properties["uwr_path"] = uri.AbsolutePath;
            }

            return properties;
        }

        /// <summary>
        /// 添加 UWR 结果、HTTP 状态和稳定错误分类，不上传原始错误文本。
        /// </summary>
        private static void AddFailureProperties(
            Dictionary<string, object> properties,
            HttpResponse response,
            Exception exception,
            bool cancelled)
        {
            if (response?.IsSuccess == false || exception != null || cancelled)
            {
                properties["uwr_error_type"] = ResolveErrorType(response, exception, cancelled);
            }

            if (response != null && response.StatusCode > 0)
            {
                properties["uwr_status_code"] = response.StatusCode;
            }

            string leafErrorCode = ResolveLeafErrorCode(response, exception, cancelled);
            if (!string.IsNullOrEmpty(leafErrorCode))
            {
                properties["uwr_leaf_error_code"] = leafErrorCode;
            }
        }

        /// <summary>
        /// 添加 UWR 公共 API 能可靠提供的传输字节数。
        /// </summary>
        private static void AddTransferProperties(Dictionary<string, object> properties, HttpResponse response)
        {
            if (response == null)
            {
                return;
            }

            properties["uwr_downloaded_bytes"] = Math.Max(0L, response.DownloadedBytes);
            if (response.TotalBytesIsKnown && response.TotalBytes >= 0)
            {
                properties["uwr_total_bytes"] = response.TotalBytes;
            }
        }

        /// <summary>
        /// 将终态映射为稳定的聚合结果。
        /// </summary>
        private static string ResolveResult(HttpResponse response, bool cancelled)
        {
            if (cancelled)
            {
                return "aborted";
            }

            if (response?.IsSuccess == true)
            {
                return "success";
            }

            if (string.Equals(response?.TransportState, "DataProcessingError", StringComparison.Ordinal))
            {
                return "data_processing_error";
            }

            if (response != null && response.HasServerResponse)
            {
                return "http_error";
            }

            string error = response?.Error ?? string.Empty;
            return ContainsAny(error, "timeout", "timed out", "超时") ? "timeout" : "network_error";
        }

        /// <summary>
        /// 先使用框架明确的取消、超时与 UWR 结果，再以错误文本作末级最佳努力分类。
        /// </summary>
        private static string ResolveLeafErrorCode(HttpResponse response, Exception exception, bool cancelled)
        {
            if (cancelled)
            {
                return "request_aborted_by_client";
            }

            if (response?.IsSuccess == true ||
                (response?.HasServerResponse == true &&
                 !string.Equals(response.TransportState, "DataProcessingError", StringComparison.Ordinal)))
            {
                return null;
            }

            string error = response?.Error ?? exception?.GetBaseException().Message ?? string.Empty;
            if (ContainsAny(error, "idle timeout")) return "request_idle_timeout";
            if (ContainsAny(error, "timeout", "timed out", "超时")) return "request_timeout";
            if (string.Equals(response?.TransportState, "DataProcessingError", StringComparison.Ordinal)) return "data_processing_error";
            if (ContainsAny(error, "could not resolve", "cannot resolve", "name resolution", "dns")) return "dns_resolve_failed";
            if (ContainsAny(error, "certificate", "证书")) return "certificate_error";
            if (ContainsAny(error, "tls", "ssl")) return "tls_handshake_failed";
            if (ContainsAny(error, "failed to connect", "could not connect", "cannot connect", "connection refused")) return "tcp_connect_failed";
            if (ContainsAny(error, "no route to host", "network unreachable")) return "network_unreachable";
            if (exception != null) return "managed_exception";
            if (!string.IsNullOrEmpty(response?.TransportState)) return "uwr_" + response.TransportState.ToLowerInvariant();
            return response?.IsSuccess == false ? "network_error" : null;
        }

        /// <summary>
        /// 将失败归并为稳定低基数类型；更具体原因由 leaf error code 表达。
        /// </summary>
        private static string ResolveErrorType(HttpResponse response, Exception exception, bool cancelled)
        {
            if (cancelled)
            {
                return "cancelled";
            }
            if (exception != null)
            {
                return exception is OperationCanceledException ? "timeout" : "exception";
            }
            if (string.Equals(response?.TransportState, "DataProcessingError", StringComparison.Ordinal))
            {
                return "data_processing";
            }
            if (response?.HasServerResponse == true)
            {
                return "http";
            }
            return "transport";
        }

        private static void AddRawFailureProperties(
            Dictionary<string, object> properties,
            long statusCode,
            string error,
            string leafErrorCode)
        {
            properties["uwr_error_type"] = statusCode > 0
                ? "http"
                : string.Equals(leafErrorCode, "request_aborted_by_client", StringComparison.Ordinal)
                    ? "cancelled"
                : string.Equals(leafErrorCode, "content_verification_failed", StringComparison.Ordinal)
                    ? "data_processing"
                    : ContainsAny(error ?? string.Empty, "timeout", "timed out", "超时")
                        ? "timeout"
                        : "transport";
            if (statusCode > 0)
            {
                properties["uwr_status_code"] = statusCode;
            }
            string resolvedLeaf = string.IsNullOrWhiteSpace(leafErrorCode)
                ? ResolveRawLeafErrorCode(error)
                : leafErrorCode;
            if (!string.IsNullOrWhiteSpace(resolvedLeaf))
            {
                properties["uwr_leaf_error_code"] = SanitizeText(resolvedLeaf);
            }
        }

        private static string ResolveRawResult(string error, string leafErrorCode)
        {
            if (string.Equals(leafErrorCode, "request_aborted_by_client", StringComparison.Ordinal))
            {
                return "aborted";
            }
            if (string.Equals(leafErrorCode, "content_verification_failed", StringComparison.Ordinal))
            {
                return "data_processing_error";
            }
            return ContainsAny(error ?? string.Empty, "timeout", "timed out", "超时")
                ? "timeout"
                : "network_error";
        }

        private static string ResolveRawLeafErrorCode(string error)
        {
            string value = error ?? string.Empty;
            if (ContainsAny(value, "idle timeout")) return "request_idle_timeout";
            if (ContainsAny(value, "timeout", "timed out", "超时")) return "request_timeout";
            if (ContainsAny(value, "could not resolve", "cannot resolve", "name resolution", "dns"))
                return "dns_resolve_failed";
            if (ContainsAny(value, "certificate", "证书")) return "certificate_error";
            if (ContainsAny(value, "tls", "ssl")) return "tls_handshake_failed";
            if (ContainsAny(value, "failed to connect", "could not connect", "cannot connect", "connection refused"))
                return "tcp_connect_failed";
            if (ContainsAny(value, "no route to host", "network unreachable")) return "network_unreachable";
            return "network_error";
        }

        private static int ToSendIndex(long sendIndex)
        {
            return sendIndex > int.MaxValue ? int.MaxValue : (int)Math.Max(0L, sendIndex);
        }

        /// <summary>
        /// 忽略大小写判断文本是否包含任一稳定分类关键词。
        /// </summary>
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

        /// <summary>
        /// 对调用方业务名执行 query 脱敏和长度限制。
        /// </summary>
        private static string SanitizeText(string value)
        {
            int queryIndex = value.IndexOf('?');
            string sanitized = queryIndex >= 0 ? value.Substring(0, queryIndex) + "?<redacted>" : value;
            return sanitized.Length <= 256 ? sanitized : sanitized.Substring(0, 256);
        }

        /// <summary>
        /// SDK 未就绪时有界缓存，SDK 就绪后按原始顺序派发到全部通用埋点插件。
        /// </summary>
        private static void Track(string eventName, Dictionary<string, object> properties)
        {
            try
            {
                var telemetryEvent = new TelemetryEventData(eventName, properties);
                if (IsSdkReady())
                {
                    FlushPending();
                    Dispatch(telemetryEvent);
                    return;
                }

                lock (s_Gate)
                {
                    if (s_PendingEvents.Count >= c_MaxPendingEvents)
                    {
                        s_PendingEvents.Dequeue();
                    }
                    s_PendingEvents.Enqueue(telemetryEvent);
                }
                EnsureReadinessWatch();
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.SDK, "UWR 网络埋点处理异常，已隔离：{0}", exception.Message);
            }
        }

        /// <summary>
        /// 确保仅启动一个 SDK 就绪等待任务。
        /// </summary>
        private static void EnsureReadinessWatch()
        {
            lock (s_Gate)
            {
                if (s_ReadinessCancellation != null)
                {
                    return;
                }

                s_ReadinessCancellation = new CancellationTokenSource();
                WaitForSdkReadyAsync(s_ReadinessCancellation.Token).Forget();
            }
        }

        /// <summary>
        /// 按帧等待 SDK 初始化，避免启动阶段网络事件丢失。
        /// </summary>
        private static async UniTaskVoid WaitForSdkReadyAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (IsSdkReady())
                    {
                        FlushPending();
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 子系统重载负责取消等待任务。
            }
            finally
            {
                lock (s_Gate)
                {
                    s_ReadinessCancellation?.Dispose();
                    s_ReadinessCancellation = null;
                }
            }
        }

        /// <summary>
        /// 判断 Nova SDK 及通用埋点插件查询入口是否已就绪。
        /// </summary>
        private static bool IsSdkReady()
        {
            return Nova.SDK != null && Nova.SDK.IsInitialized;
        }

        /// <summary>
        /// 派发全部启动缓存事件。
        /// </summary>
        private static void FlushPending()
        {
            while (true)
            {
                TelemetryEventData telemetryEvent;
                lock (s_Gate)
                {
                    if (s_PendingEvents.Count == 0)
                    {
                        return;
                    }

                    telemetryEvent = s_PendingEvents.Dequeue();
                }
                Dispatch(telemetryEvent);
            }
        }

        /// <summary>
        /// 将单个事件扇出到全部通用埋点插件，并隔离单插件异常。
        /// </summary>
        private static void Dispatch(TelemetryEventData telemetryEvent)
        {
            IReadOnlyList<ITrackPlugin> plugins = Nova.SDK?.GetAll<ITrackPlugin>();
            if (plugins == null)
            {
                return;
            }

            for (int i = 0; i < plugins.Count; i++)
            {
                ITrackPlugin plugin = plugins[i];
                if (plugin == null)
                {
                    continue;
                }

                try
                {
                    plugin.TrackEvent(telemetryEvent.Name, new Dictionary<string, object>(telemetryEvent.Properties));
                }
                catch (Exception exception)
                {
                    Log.Warning(LogTag.SDK, "UWR 单个埋点插件上报异常，已隔离：{0}", exception.Message);
                }
            }
        }

        /// <summary>
        /// 不可变的事件快照，防止异步等待期间属性被调用方修改。
        /// </summary>
        private sealed class TelemetryEventData
        {
            internal TelemetryEventData(string name, Dictionary<string, object> properties)
            {
                Name = name;
                Properties = new Dictionary<string, object>(properties);
            }

            internal string Name { get; }
            internal Dictionary<string, object> Properties { get; }
        }
    }
}
