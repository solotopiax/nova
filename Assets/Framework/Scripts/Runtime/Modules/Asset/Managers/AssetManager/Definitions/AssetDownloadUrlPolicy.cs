/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetDownloadUrlPolicy.cs
 * author:    taoye
 * created:   2026/8/5
 * descrip:   Asset 远端候选 URL 与重试策略
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset 远端候选 URL 与重试策略。
    /// 每个文件冻结自己的完整主备计划，避免同一 Package 内的并发文件互相推进游标。
    /// </summary>
    internal sealed class AssetDownloadUrlPolicy : IDownloadUrlPolicy, IDownloadRetryPolicy
    {
        private const float c_MaxRetryDelaySeconds = 10f;

        private readonly bool m_EnableWhitelistMetadataDebugLog;
        private readonly HttpFallbackPolicy m_FallbackPolicy;
        private readonly int m_DefaultLogicalRetryCount;
        private readonly bool m_PreferLastSuccessfulHost;
        private readonly bool m_EnableUwrTracks;
        private readonly string m_PackageName;
        private readonly float m_CheckTimeout;
        private readonly float m_ManifestRequestTimeout;
        private readonly float m_BundleRequestTimeout;
        private readonly HttpFallbackPreferenceStore m_PreferenceStore = new();
        private readonly Dictionary<string, RequestState> m_RequestStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Queue<CandidateSelection>> m_PendingSelections = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CandidateSelection> m_LastFailures = new(StringComparer.Ordinal);
        private readonly Dictionary<string, RetryOverride> m_RetryOverrides = new(StringComparer.Ordinal);
        private readonly List<CandidateSelection> m_ActiveMetadataSelections = new();
        private readonly Dictionary<string, MetadataTransportFailure> m_TransportFailedMetadataSelections = new(StringComparer.Ordinal);

        private enum CandidateFamily
        {
            Regular,
            Metadata,
        }

        private sealed class RequestState
        {
            public string RequestKey;
            public string FileName;
            public CandidateFamily Family;
            public string CandidateSignature;
            public HttpFallbackExecutionPlan Plan;
            public HttpFallbackExecutionCursor Cursor;
            public string ChainId;
            public string DownloadOperationId;
            public Stopwatch ChainStopwatch;
            public Stopwatch SendStopwatch;
            public int StartedSendCount;
            public bool TelemetryEnded;
        }

        private sealed class RetryOverride
        {
            public int ReferenceCount;
            public int LogicalRetryCount;
            public string DownloadOperationId;
        }

        private readonly struct CandidateSelection
        {
            public CandidateSelection(string requestKey, string fileName, CandidateFamily family,
                string selectedUrl, HttpFallbackStep step, int maxAttempts)
            {
                RequestKey = requestKey;
                FileName = fileName;
                Family = family;
                SelectedUrl = selectedUrl;
                Step = step;
                MaxAttempts = maxAttempts;
            }

            public string RequestKey { get; }
            public string FileName { get; }
            public CandidateFamily Family { get; }
            public string SelectedUrl { get; }
            public HttpFallbackStep Step { get; }
            public int AttemptIndex => (int)Math.Min(int.MaxValue, Step.PhysicalSendIndex);
            public int CandidateCount => Step.CandidateCount;
            public int MaxAttempts { get; }
            public string Token => $"{RequestKey}#{AttemptIndex}";
        }

        private readonly struct MetadataTransportFailure
        {
            public MetadataTransportFailure(long httpCode, string httpError)
            {
                HttpCode = httpCode;
                HttpError = httpError;
            }

            public long HttpCode { get; }
            public string HttpError { get; }
        }

        /// <summary>
        /// 使用默认的一轮主备与三次逻辑重试创建策略，供兼容调用和测试使用。
        /// </summary>
        public AssetDownloadUrlPolicy() : this(false, 1, 3, true)
        {
        }

        /// <summary>
        /// 使用默认的一轮主备与三次逻辑重试创建策略。
        /// </summary>
        internal AssetDownloadUrlPolicy(bool enableWhitelistMetadataDebugLog)
            : this(enableWhitelistMetadataDebugLog, 1, 3, true)
        {
        }

        /// <summary>
        /// 创建指定轮数、逻辑重试次数和最近成功优先策略的 Asset 下载策略。
        /// </summary>
        internal AssetDownloadUrlPolicy(bool enableWhitelistMetadataDebugLog, int fallbackRoundCount,
            int logicalRetryCount, bool preferLastSuccessfulHost)
            : this(enableWhitelistMetadataDebugLog, fallbackRoundCount, logicalRetryCount,
                preferLastSuccessfulHost, false, null, 60f, 0f, 60f)
        {
        }

        /// <summary>
        /// 创建带 Asset UWR 埋点上下文的 URL 与重试策略。
        /// </summary>
        internal AssetDownloadUrlPolicy(bool enableWhitelistMetadataDebugLog, int fallbackRoundCount,
            int logicalRetryCount, bool preferLastSuccessfulHost, bool enableUwrTracks,
            string packageName, float checkTimeout, float bundleRequestTimeout, float manifestRequestTimeout = 60f)
        {
            m_EnableWhitelistMetadataDebugLog = enableWhitelistMetadataDebugLog;
            m_DefaultLogicalRetryCount = Math.Max(0, logicalRetryCount);
            m_PreferLastSuccessfulHost = preferLastSuccessfulHost;
            m_EnableUwrTracks = enableUwrTracks;
            m_PackageName = packageName ?? string.Empty;
            m_CheckTimeout = checkTimeout;
            m_BundleRequestTimeout = bundleRequestTimeout;
            m_ManifestRequestTimeout = manifestRequestTimeout;
            m_FallbackPolicy = new HttpFallbackPolicy(
                Math.Max(1, fallbackRoundCount),
                m_DefaultLogicalRetryCount,
                preferLastSuccessfulHost);
        }

        /// <summary>
        /// 把逻辑轮次与逻辑重试换算成 YooAsset 所需的额外物理重试次数。
        /// </summary>
        internal static int CalculatePhysicalRetryCount(int candidateCount, int fallbackRoundCount,
            int logicalRetryCount)
        {
            int count = Math.Max(1, candidateCount);
            var urls = new string[count];
            for (int i = 0; i < count; i++)
            {
                urls[i] = $"https://candidate-{i}.invalid/file";
            }
            HttpFallbackExecutionPlan plan = HttpFallbackPlanner.Build(
                urls,
                new HttpFallbackPolicy(
                    Math.Max(1, fallbackRoundCount),
                    Math.Max(0, logicalRetryCount),
                    false));
            return plan.PlannedPhysicalSendCount > int.MaxValue
                ? int.MaxValue
                : (int)Math.Max(0L, plan.PlannedPhysicalSendCount - 1L);
        }

        /// <summary>
        /// 按文件自己的不可变计划选择本次 URL。
        /// </summary>
        public string SelectUrl(IReadOnlyList<string> candidateUrls)
        {
            if (candidateUrls == null || candidateUrls.Count == 0)
            {
                throw new YooInternalException("Candidate URL list is null or empty.");
            }

            string requestKey = BuildRequestKey(candidateUrls[0]);
            CandidateFamily family = IsMetadataUrl(candidateUrls[0])
                ? CandidateFamily.Metadata
                : CandidateFamily.Regular;
            string candidateSignature = BuildCandidateSignature(candidateUrls);
            if (m_RequestStates.TryGetValue(requestKey, out RequestState existingState)
                && !string.Equals(existingState.CandidateSignature, candidateSignature, StringComparison.Ordinal))
            {
                RemoveRequestState(requestKey);
            }
            if (!m_RequestStates.TryGetValue(requestKey, out RequestState state))
            {
                state = CreateRequestState(requestKey, candidateUrls, family, candidateSignature);
                m_RequestStates[requestKey] = state;
            }

            if (state.TelemetryEnded)
            {
                state.ChainId = UwrNetworkTelemetry.CreateChainId();
                state.ChainStopwatch = Stopwatch.StartNew();
                state.StartedSendCount = 0;
                state.TelemetryEnded = false;
            }

            if (!state.Cursor.TryBeginNext(out HttpFallbackStep step))
            {
                throw new YooInternalException($"Asset download retry plan exhausted: {state.FileName}");
            }
            string selectedUrl = step.Candidate.Url;
            state.StartedSendCount++;
            state.SendStopwatch = Stopwatch.StartNew();
            var selection = new CandidateSelection(state.RequestKey, state.FileName, state.Family,
                selectedUrl, step,
                (int)Math.Min(int.MaxValue, state.Plan.PlannedPhysicalSendCount));
            RecordSelection(selection);
            if (state.StartedSendCount == 1)
            {
                UwrNetworkTelemetry.TrackAssetStart(
                    m_EnableUwrTracks,
                    state.ChainId,
                    selectedUrl,
                    GetRequestTimeout(state.Family, state.FileName),
                    state.Plan,
                    step,
                    state.DownloadOperationId,
                    m_PackageName,
                    GetFileType(state.FileName));
            }
            if (state.Family == CandidateFamily.Metadata)
            {
                m_ActiveMetadataSelections.Add(selection);
            }
            return selectedUrl;
        }

        /// <summary>
        /// 开始一次由 Nova 编排的版本元数据操作。
        /// </summary>
        public void BeginMetadataRequest()
        {
            m_ActiveMetadataSelections.Clear();
            m_TransportFailedMetadataSelections.Clear();
        }

        /// <summary>
        /// 根据 YooAsset 元数据操作结果收口本次选择记录。
        /// </summary>
        public bool CompleteMetadataRequest(bool succeeded, string operationError)
        {
            int contentFailureIndex = !succeeded && m_TransportFailedMetadataSelections.Count == 0
                ? m_ActiveMetadataSelections.Count - 1
                : -1;
            bool shouldRetry = false;

            for (int i = 0; i < m_ActiveMetadataSelections.Count; i++)
            {
                CandidateSelection selection = m_ActiveMetadataSelections[i];
                if (m_TransportFailedMetadataSelections.TryGetValue(
                        selection.Token, out MetadataTransportFailure transportFailure))
                {
                    m_LastFailures.Remove(NormalizeUrl(selection.SelectedUrl));
                    bool retryable = IsRetryableAssetError(selection.SelectedUrl, transportFailure.HttpCode)
                                     && RejectSelectionIfInFlight(selection);
                    if (!retryable)
                    {
                        CompleteSelectionIfInFlight(selection);
                        TrackSelectionEnd(selection, false, transportFailure.HttpCode,
                            transportFailure.HttpError, null);
                        m_RequestStates.Remove(selection.RequestKey);
                    }
                    shouldRetry |= retryable;
                    continue;
                }

                RemovePendingSelection(selection);
                if (i == contentFailureIndex)
                {
                    TrackSelectionError(selection, 0L, operationError, "content_verification_failed");
                    bool retryable = RejectSelectionIfInFlight(selection);
                    if (!retryable)
                    {
                        TrackSelectionEnd(selection, false, 0L, operationError, "content_verification_failed");
                        m_RequestStates.Remove(selection.RequestKey);
                    }
                    shouldRetry |= retryable;
                    if (m_EnableWhitelistMetadataDebugLog)
                    {
                        LogMetadataFailure(selection.SelectedUrl, 0L, operationError ?? "Operation failed");
                    }
                }
                else
                {
                    CompleteSelectionSucceeded(selection);
                }
            }

            BeginMetadataRequest();
            return shouldRetry;
        }

        /// <summary>
        /// 请求成功后记录最近成功域名，并结束该文件计划。
        /// </summary>
        public void OnRequestSucceeded(string url)
        {
            if (TryTakeSelection(url, out CandidateSelection selection))
            {
                // .version/.hash/.bytes 的 HTTP 成功并不代表内容已经通过 YooAsset 校验，
                // 由 CompleteMetadataRequest 根据最终 operation 状态再确认成功或推进候选。
                if (selection.Family == CandidateFamily.Metadata)
                {
                    return;
                }
                CompleteSelectionSucceeded(selection);
            }
        }

        /// <summary>
        /// 记录传输失败；是否继续由紧随其后的 <see cref="IsRetryableError"/> 裁决。
        /// </summary>
        public void OnRequestFailed(string url, long httpCode, string httpError)
        {
            string normalizedUrl = NormalizeUrl(url);
            if (m_LastFailures.ContainsKey(normalizedUrl))
            {
                return;
            }
            if (!TryTakeSelection(url, out CandidateSelection selection))
            {
                return;
            }

            m_LastFailures[normalizedUrl] = selection;
            TrackSelectionError(selection, httpCode, httpError, null);
            if (selection.Family == CandidateFamily.Metadata)
            {
                m_TransportFailedMetadataSelections[selection.Token] =
                    new MetadataTransportFailure(httpCode, httpError);
                if (m_EnableWhitelistMetadataDebugLog)
                {
                    LogMetadataFailure(selection.FileName, url, httpCode, httpError);
                }
                return;
            }
            if (selection.AttemptIndex + 1 >= selection.MaxAttempts)
            {
                RejectSelectionIfInFlight(selection);
                TrackSelectionEnd(selection, false, httpCode, httpError, null);
                m_LastFailures.Remove(normalizedUrl);
                m_RequestStates.Remove(selection.RequestKey);
            }
        }

        /// <summary>
        /// 404、408、416、429、5xx、无响应和内容校验失败继续；其余 4xx 停止。
        /// </summary>
        public bool IsRetryableError(string url, long httpCode, string httpError)
        {
            string normalizedUrl = NormalizeUrl(url);
            if (!m_LastFailures.TryGetValue(normalizedUrl, out CandidateSelection selection))
            {
                return false;
            }

            m_LastFailures.Remove(normalizedUrl);
            bool retryable = IsRetryableAssetError(url, httpCode);
            if (m_RequestStates.TryGetValue(selection.RequestKey, out RequestState state)
                && state.Cursor.State == HttpFallbackExecutionState.CandidateInFlight)
            {
                if (retryable)
                {
                    state.Cursor.RejectCurrent();
                    retryable = state.Cursor.State != HttpFallbackExecutionState.Exhausted;
                }
                else
                {
                    state.Cursor.CompleteCurrent();
                }
            }
            else
            {
                retryable = false;
            }
            if (!retryable)
            {
                TrackSelectionEnd(selection, false, httpCode, httpError, null);
                m_RequestStates.Remove(selection.RequestKey);
            }
            return retryable;
        }

        /// <summary>
        /// 计算每次物理重试前的线性等待时间，上限十秒。
        /// </summary>
        public float CalculateRetryDelay(int retryCount, float previousDelay)
        {
            return Math.Min(c_MaxRetryDelaySeconds, previousDelay + 1f);
        }

        /// <summary>
        /// 为显式 ResourceDownloader 注册单文件逻辑重试次数。
        /// </summary>
        internal void RegisterDownloaderFile(string fileName, int logicalRetryCount, string downloadOperationId)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }
            if (!m_RetryOverrides.TryGetValue(fileName, out RetryOverride retryOverride))
            {
                retryOverride = new RetryOverride();
                m_RetryOverrides.Add(fileName, retryOverride);
            }
            retryOverride.ReferenceCount++;
            retryOverride.LogicalRetryCount = Math.Max(retryOverride.LogicalRetryCount,
                Math.Max(0, logicalRetryCount));
            if (!string.IsNullOrWhiteSpace(downloadOperationId))
            {
                retryOverride.DownloadOperationId = downloadOperationId;
            }
        }

        /// <summary>
        /// 释放显式 ResourceDownloader 的单文件注册，并清理该文件残余状态。
        /// </summary>
        internal void UnregisterDownloaderFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)
                || !m_RetryOverrides.TryGetValue(fileName, out RetryOverride retryOverride))
            {
                return;
            }
            retryOverride.ReferenceCount--;
            if (retryOverride.ReferenceCount > 0)
            {
                return;
            }
            m_RetryOverrides.Remove(fileName);
            RemoveFileState(fileName);
        }

        private RequestState CreateRequestState(string requestKey, IReadOnlyList<string> candidateUrls,
            CandidateFamily family, string candidateSignature)
        {
            string fileName = GetFileName(candidateUrls[0]);
            int logicalRetryCount = m_RetryOverrides.TryGetValue(fileName, out RetryOverride retryOverride)
                ? retryOverride.LogicalRetryCount
                : m_DefaultLogicalRetryCount;
            string preferenceScope = GetPreferenceScope(family);
            HttpFallbackPreferenceSnapshot preference = m_PreferenceStore.Capture(preferenceScope);
            var policy = new HttpFallbackPolicy(
                m_FallbackPolicy.RoundCount,
                logicalRetryCount,
                m_PreferLastSuccessfulHost);
            HttpFallbackExecutionPlan plan = HttpFallbackPlanner.Build(candidateUrls, policy, preference);
            if (plan.CandidateCount == 0)
            {
                throw new YooInternalException("Candidate URL list contains no valid URL.");
            }
            if (preference.HasValue && !PlanContainsEndpoint(plan, preference.EndpointId))
            {
                m_PreferenceStore.ClearIfUnchanged(preference);
                plan = HttpFallbackPlanner.Build(candidateUrls, policy);
            }
            return new RequestState
            {
                RequestKey = requestKey,
                FileName = fileName,
                Family = family,
                CandidateSignature = candidateSignature,
                Plan = plan,
                Cursor = plan.CreateCursor(),
                ChainId = UwrNetworkTelemetry.CreateChainId(),
                DownloadOperationId = string.IsNullOrWhiteSpace(retryOverride?.DownloadOperationId)
                    ? UwrNetworkTelemetry.CreateChainId()
                    : retryOverride.DownloadOperationId,
                ChainStopwatch = Stopwatch.StartNew(),
            };
        }

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

        private static string GetPreferenceScope(CandidateFamily family)
        {
            return family == CandidateFamily.Metadata ? "asset:metadata" : "asset:regular";
        }

        private static string BuildCandidateSignature(IReadOnlyList<string> candidateUrls)
        {
            HttpFallbackExecutionPlan plan = HttpFallbackPlanner.Build(
                candidateUrls,
                new HttpFallbackPolicy(1, 0, false));
            var candidates = new string[plan.CandidateCount];
            for (int i = 0; i < plan.CandidateCount; i++)
            {
                candidates[i] = NormalizeUrl(plan.Candidates[i].Url);
            }
            return string.Join("\n", candidates);
        }

        private void CompleteSelectionSucceeded(CandidateSelection selection)
        {
            // WebNetworkFileSystem 的内存 Bundle 可能在 YooAsset 内容校验前收到成功回调；常规资源此处不能结束游标，
            // 否则后续校验失败再次 SelectUrl 时会错误地从首候选重新开始。HostPlayMode 通常在缓存校验后回调。
            // 元数据的最终内容结果由 AssetManager.CompleteMetadataRequest 收口，可立即结束。
            if (m_RequestStates.TryGetValue(selection.RequestKey, out RequestState state)
                && state.Cursor.State == HttpFallbackExecutionState.CandidateInFlight)
            {
                if (selection.Family == CandidateFamily.Metadata)
                {
                    state.Cursor.CompleteCurrent();
                    TrackSelectionEnd(selection, true, 200L, null, null);
                    m_RequestStates.Remove(selection.RequestKey);
                }
                else
                {
                    state.Cursor.RejectCurrent();
                    TrackSelectionEnd(selection, true, 200L, null, null);
                }
            }
            m_LastFailures.Remove(NormalizeUrl(selection.SelectedUrl));
            if (m_PreferLastSuccessfulHost)
            {
                m_PreferenceStore.MarkSuccess(
                    GetPreferenceScope(selection.Family),
                    HttpFallbackPlanner.GetEndpointId(selection.SelectedUrl));
            }
            if (m_EnableWhitelistMetadataDebugLog && selection.Family == CandidateFamily.Metadata)
            {
                Log.Debug(LogTag.Asset, "启动白名单版本元数据拉取成功：File={0}, URL={1}",
                    selection.FileName, selection.SelectedUrl);
            }
        }

        private bool RejectSelectionIfInFlight(CandidateSelection selection)
        {
            if (m_RequestStates.TryGetValue(selection.RequestKey, out RequestState state)
                && state.Cursor.State == HttpFallbackExecutionState.CandidateInFlight)
            {
                state.Cursor.RejectCurrent();
                return state.Cursor.State != HttpFallbackExecutionState.Exhausted;
            }
            return false;
        }

        private void CompleteSelectionIfInFlight(CandidateSelection selection)
        {
            if (m_RequestStates.TryGetValue(selection.RequestKey, out RequestState state)
                && state.Cursor.State == HttpFallbackExecutionState.CandidateInFlight)
            {
                state.Cursor.CompleteCurrent();
            }
        }

        private void TrackSelectionError(
            CandidateSelection selection,
            long statusCode,
            string error,
            string leafErrorCode)
        {
            if (!m_RequestStates.TryGetValue(selection.RequestKey, out RequestState state))
            {
                return;
            }
            UwrNetworkTelemetry.TrackAssetError(
                m_EnableUwrTracks,
                state.ChainId,
                selection.SelectedUrl,
                GetRequestTimeout(selection.Family, selection.FileName),
                state.Plan,
                selection.Step,
                state.SendStopwatch?.ElapsedMilliseconds ?? 0L,
                statusCode,
                error,
                leafErrorCode,
                state.DownloadOperationId,
                m_PackageName,
                GetFileType(selection.FileName));
        }

        private void TrackSelectionEnd(
            CandidateSelection selection,
            bool succeeded,
            long statusCode,
            string error,
            string leafErrorCode)
        {
            if (!m_RequestStates.TryGetValue(selection.RequestKey, out RequestState state) || state.TelemetryEnded)
            {
                return;
            }
            state.TelemetryEnded = true;
            UwrNetworkTelemetry.TrackAssetEnd(
                m_EnableUwrTracks,
                state.ChainId,
                selection.SelectedUrl,
                GetRequestTimeout(selection.Family, selection.FileName),
                state.Plan,
                selection.Step,
                state.StartedSendCount,
                state.SendStopwatch?.ElapsedMilliseconds ?? 0L,
                state.ChainStopwatch?.ElapsedMilliseconds ?? 0L,
                succeeded,
                statusCode,
                error,
                leafErrorCode,
                state.DownloadOperationId,
                m_PackageName,
                GetFileType(selection.FileName));
        }

        private float GetRequestTimeout(CandidateFamily family, string fileName)
        {
            if (family == CandidateFamily.Regular)
            {
                return m_BundleRequestTimeout;
            }
            string extension = System.IO.Path.GetExtension(fileName);
            return string.Equals(extension, ".version", StringComparison.OrdinalIgnoreCase)
                ? m_CheckTimeout
                : m_ManifestRequestTimeout;
        }

        private static string GetFileType(string fileName)
        {
            string extension = System.IO.Path.GetExtension(fileName)?.ToLowerInvariant();
            return extension switch
            {
                ".version" => "version",
                ".hash" => "manifest_hash",
                ".bytes" => "manifest_bytes",
                _ => "bundle",
            };
        }

        private void RecordSelection(CandidateSelection selection)
        {
            string normalizedUrl = NormalizeUrl(selection.SelectedUrl);
            if (!m_PendingSelections.TryGetValue(normalizedUrl, out Queue<CandidateSelection> selections))
            {
                selections = new Queue<CandidateSelection>();
                m_PendingSelections.Add(normalizedUrl, selections);
            }
            selections.Enqueue(selection);
        }

        private bool TryTakeSelection(string url, out CandidateSelection selection)
        {
            selection = default;
            string normalizedUrl = NormalizeUrl(url);
            if (!m_PendingSelections.TryGetValue(normalizedUrl, out Queue<CandidateSelection> selections)
                || selections.Count == 0)
            {
                return false;
            }
            selection = selections.Dequeue();
            if (selections.Count == 0)
            {
                m_PendingSelections.Remove(normalizedUrl);
            }
            return true;
        }

        private void RemovePendingSelection(CandidateSelection target)
        {
            string normalizedUrl = NormalizeUrl(target.SelectedUrl);
            if (!m_PendingSelections.TryGetValue(normalizedUrl, out Queue<CandidateSelection> selections))
            {
                return;
            }
            var remaining = new Queue<CandidateSelection>(selections.Count);
            while (selections.Count > 0)
            {
                CandidateSelection selection = selections.Dequeue();
                if (selection.Token != target.Token)
                {
                    remaining.Enqueue(selection);
                }
            }
            if (remaining.Count == 0)
            {
                m_PendingSelections.Remove(normalizedUrl);
            }
            else
            {
                m_PendingSelections[normalizedUrl] = remaining;
            }
        }

        private void RemoveFileState(string fileName)
        {
            var requestKeys = new List<string>();
            foreach (KeyValuePair<string, RequestState> pair in m_RequestStates)
            {
                if (string.Equals(pair.Value.FileName, fileName, StringComparison.Ordinal))
                {
                    requestKeys.Add(pair.Key);
                }
            }
            for (int i = 0; i < requestKeys.Count; i++)
            {
                m_RequestStates.Remove(requestKeys[i]);
            }

            var pendingUrls = new List<string>();
            foreach (KeyValuePair<string, Queue<CandidateSelection>> pair in m_PendingSelections)
            {
                if (QueueContainsFile(pair.Value, fileName))
                {
                    pendingUrls.Add(pair.Key);
                }
            }
            for (int i = 0; i < pendingUrls.Count; i++)
            {
                m_PendingSelections.Remove(pendingUrls[i]);
                m_LastFailures.Remove(pendingUrls[i]);
            }
        }

        private void RemoveRequestState(string requestKey)
        {
            m_RequestStates.Remove(requestKey);
            var pendingUrls = new List<string>();
            foreach (KeyValuePair<string, Queue<CandidateSelection>> pair in m_PendingSelections)
            {
                bool containsRequest = false;
                foreach (CandidateSelection selection in pair.Value)
                {
                    if (string.Equals(selection.RequestKey, requestKey, StringComparison.Ordinal))
                    {
                        containsRequest = true;
                        break;
                    }
                }
                if (containsRequest)
                {
                    pendingUrls.Add(pair.Key);
                }
            }
            for (int i = 0; i < pendingUrls.Count; i++)
            {
                m_PendingSelections.Remove(pendingUrls[i]);
                m_LastFailures.Remove(pendingUrls[i]);
            }
        }

        private static bool QueueContainsFile(IEnumerable<CandidateSelection> selections, string fileName)
        {
            foreach (CandidateSelection selection in selections)
            {
                if (string.Equals(selection.FileName, fileName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool IsRetryableAssetError(string url, long httpCode)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.Scheme == Uri.UriSchemeFile)
            {
                return false;
            }
            if (httpCode == 0 || httpCode == 404 || httpCode == 408 || httpCode == 416 || httpCode == 429)
            {
                return true;
            }
            if (httpCode >= 400 && httpCode < 500)
            {
                return false;
            }
            return true;
        }

        private static string BuildRequestKey(string url)
        {
            string normalizedUrl = NormalizeUrl(url);
            return Uri.TryCreate(normalizedUrl, UriKind.Absolute, out Uri uri)
                ? uri.AbsolutePath
                : normalizedUrl;
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return uri.GetLeftPart(UriPartial.Path);
            }
            int suffixIndex = url.IndexOfAny(new[] { '?', '#' });
            return suffixIndex >= 0 ? url.Substring(0, suffixIndex) : url;
        }

        private static string GetFileName(string url)
        {
            string normalizedUrl = NormalizeUrl(url);
            return Uri.TryCreate(normalizedUrl, UriKind.Absolute, out Uri uri)
                ? System.IO.Path.GetFileName(uri.AbsolutePath)
                : System.IO.Path.GetFileName(normalizedUrl);
        }

        private static bool IsMetadataUrl(string url)
        {
            string fileName = GetFileName(url);
            return fileName.EndsWith(".version", StringComparison.OrdinalIgnoreCase)
                   || fileName.EndsWith(".hash", StringComparison.OrdinalIgnoreCase)
                   || fileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase);
        }

        private static void LogMetadataFailure(string url, long httpCode, string error)
        {
            LogMetadataFailure(GetFileName(url), url, httpCode, error);
        }

        private static void LogMetadataFailure(string fileName, string url, long httpCode, string error)
        {
            Log.Debug(LogTag.Asset,
                "启动白名单版本元数据拉取失败：File={0}, URL={1}, HttpCode={2}, Error={3}",
                fileName, url, httpCode, error);
        }
    }
}
