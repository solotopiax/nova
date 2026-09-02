/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppManager.Methods.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   App 管理器 —— 私有方法
 ***************************************************************/

using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 管理器。
    /// </summary>
    internal sealed partial class AppManager : AppManagerBase
    {
        /// <summary>
        /// 执行版本检查核心逻辑：按共享主备计划发送 HTTP GET，解析 CDN 版本规则并写入结果字段。
        /// 传输失败、可重试 HTTP 状态、空内容或无效规则会推进候选；合法规则结束整条链。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>App 版本检查结果枚举值。</returns>
        private async UniTask<AppVersionResult> InnerCheckVersionAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            HttpFallbackExecutionPlan plan = CreateVersionCheckFallbackPlan();
            if (plan.CandidateCount == 0)
            {
                return AppVersionResult.NoDownload;
            }

            IPhysicalHttpManager physicalHttpManager = m_HttpManager as IPhysicalHttpManager;
            bool shouldTrack = physicalHttpManager != null && m_Config.EnableUWRTracks;
            string chainId = UwrNetworkTelemetry.CreateChainId();
            Stopwatch chainStopwatch = Stopwatch.StartNew();
            TrackVersionCheckStart(shouldTrack, chainId, plan);

            HttpFallbackExecutionCursor cursor = plan.CreateCursor();
            int attemptsStarted = 0;
            while (cursor.TryBeginNext(out HttpFallbackStep step))
            {
                HttpResponse response = null;
                Exception requestException = null;
                Stopwatch requestStopwatch = Stopwatch.StartNew();
                try
                {
                    ct.ThrowIfCancellationRequested();
                    attemptsStarted++;
                    response = await GetVersionCheckResponseAsync(
                        physicalHttpManager,
                        step.Candidate.Url,
                        ct);
                    ct.ThrowIfCancellationRequested();

                    long requestElapsedMs = requestStopwatch.ElapsedMilliseconds;
                    if (response == null)
                    {
                        Log.Warning(LogTag.App, "{0}版本检查接口未返回响应，准备尝试下一个候选。",
                            DescribeVersionCheckCandidate(step));
                        RejectVersionCheckAttempt(
                            cursor, shouldTrack, chainId, plan, step, attemptsStarted, requestElapsedMs,
                            chainStopwatch.ElapsedMilliseconds, response, null, null, null);
                        continue;
                    }

                    if (!response.IsSuccess)
                    {
                        if (!ShouldRetryVersionCheckResponse(response))
                        {
                            Log.Warning(
                                LogTag.App,
                                "{0}版本检查接口返回不可重试 HTTP 状态码，停止主备链。StatusCode={1}",
                                DescribeVersionCheckCandidate(step),
                                response.StatusCode);
                            cursor.CompleteCurrent();
                            TrackVersionCheckEnd(
                                shouldTrack, chainId, plan, step, attemptsStarted, requestElapsedMs,
                                chainStopwatch.ElapsedMilliseconds, response, null, false, null, null);
                            return AppVersionResult.NoDownload;
                        }

                        Log.Warning(
                            LogTag.App,
                            "{0}版本检查接口请求失败，准备尝试下一个候选。StatusCode={1} Error={2}",
                            DescribeVersionCheckCandidate(step),
                            response.StatusCode,
                            response.Error);
                        RejectVersionCheckAttempt(
                            cursor, shouldTrack, chainId, plan, step, attemptsStarted, requestElapsedMs,
                            chainStopwatch.ElapsedMilliseconds, response, null, null, null);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(response.Body))
                    {
                        Log.Warning(LogTag.App, "{0}版本检查接口返回内容为空，准备尝试下一个候选。",
                            DescribeVersionCheckCandidate(step));
                        RejectVersionCheckAttempt(
                            cursor, shouldTrack, chainId, plan, step, attemptsStarted, requestElapsedMs,
                            chainStopwatch.ElapsedMilliseconds, response, null,
                            c_InvalidVersionResponseResult,
                            c_EmptyVersionResponseLeafErrorCode);
                        continue;
                    }

                    if (TryParseVersionResult(response.Body, out AppVersionResult result))
                    {
                        cursor.CompleteCurrent();
                        if (m_Config.PreferLastSuccessfulHost)
                        {
                            m_VersionCheckFallbackPreferences.MarkSuccess(
                                c_VersionCheckFallbackScopeKey,
                                step.Candidate.EndpointId);
                        }

                        TrackVersionCheckEnd(
                            shouldTrack, chainId, plan, step, attemptsStarted, requestElapsedMs,
                            chainStopwatch.ElapsedMilliseconds, response, null, false, null, null);
                        return result;
                    }

                    Log.Warning(LogTag.App, "{0}版本检查接口返回无效规则，准备尝试下一个候选。",
                        DescribeVersionCheckCandidate(step));
                    RejectVersionCheckAttempt(
                        cursor, shouldTrack, chainId, plan, step, attemptsStarted, requestElapsedMs,
                        chainStopwatch.ElapsedMilliseconds, response, null,
                        c_InvalidVersionResponseResult,
                        c_InvalidVersionResponseLeafErrorCode);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    cursor.Cancel();
                    long requestElapsedMs = requestStopwatch.ElapsedMilliseconds;
                    TrackVersionCheckAttemptFailure(
                        shouldTrack, chainId, plan, step, requestElapsedMs, response, null, true, null);
                    TrackVersionCheckEnd(
                        shouldTrack, chainId, plan, step, attemptsStarted, requestElapsedMs,
                        chainStopwatch.ElapsedMilliseconds, response, null, true, null, null);
                    throw;
                }
                catch (OperationCanceledException ex)
                {
                    requestException = ex;
                    Log.Warning(LogTag.App, "{0}版本检查接口请求超时，准备尝试下一个候选。",
                        DescribeVersionCheckCandidate(step));
                    RejectVersionCheckAttempt(
                        cursor, shouldTrack, chainId, plan, step, attemptsStarted,
                        requestStopwatch.ElapsedMilliseconds, chainStopwatch.ElapsedMilliseconds,
                        response, requestException, null, null);
                }
                catch (Exception ex)
                {
                    requestException = ex;
                    Log.Warning(LogTag.App, "{0}版本检查接口请求异常，准备尝试下一个候选：{1}",
                        DescribeVersionCheckCandidate(step),
                        ex.Message);
                    RejectVersionCheckAttempt(
                        cursor, shouldTrack, chainId, plan, step, attemptsStarted,
                        requestStopwatch.ElapsedMilliseconds, chainStopwatch.ElapsedMilliseconds,
                        response, requestException, null, null);
                }
                finally
                {
                    if (response != null)
                    {
                        ReferencePool.Put(response);
                    }
                }
            }

            return AppVersionResult.NoDownload;
        }

        /// <summary>
        /// 构建 App 版本检查的共享主备执行计划，并在配置候选不再包含旧偏好时清理该偏好。
        /// </summary>
        /// <returns>当前配置、轮数和重试次数对应的不可变执行计划。</returns>
        private HttpFallbackExecutionPlan CreateVersionCheckFallbackPlan()
        {
            var policy = new HttpFallbackPolicy(
                Math.Max(1, m_Config.VersionCheckFallbackRoundCount),
                Math.Max(0, m_Config.RetryRequestCount),
                m_Config.PreferLastSuccessfulHost);
            HttpFallbackPreferenceSnapshot preference = m_Config.PreferLastSuccessfulHost
                ? m_VersionCheckFallbackPreferences.Capture(c_VersionCheckFallbackScopeKey)
                : default;
            HttpFallbackExecutionPlan plan = HttpFallbackPlanner.Build(
                new[] { m_Config.AppDownloadCheckUrl, m_Config.AppDownloadCheckUrlFallback },
                policy,
                preference);

            if (preference.HasValue && !PlanContainsEndpoint(plan, preference.EndpointId))
            {
                m_VersionCheckFallbackPreferences.ClearIfUnchanged(preference);
            }

            return plan;
        }

        /// <summary>
        /// 判断当前候选计划是否仍包含指定的最近成功域名，用于识别配置已经切换的旧偏好。
        /// </summary>
        /// <param name="plan">当前版本检查候选计划。</param>
        /// <param name="endpointId">待匹配的规范化域名标识。</param>
        /// <returns>计划中包含该域名时返回 true。</returns>
        private static bool PlanContainsEndpoint(HttpFallbackExecutionPlan plan, string endpointId)
        {
            if (plan == null || string.IsNullOrEmpty(endpointId))
            {
                return false;
            }

            for (int i = 0; i < plan.Candidates.Count; i++)
            {
                if (string.Equals(
                        plan.Candidates[i].EndpointId,
                        endpointId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 使用支持物理取消的 HTTP 入口发送一次版本检查请求；旧自定义 IHttpManager 保持兼容回退。
        /// </summary>
        /// <param name="physicalHttpManager">内置 HTTP 管理器提供的可取消物理发送能力，可为 null。</param>
        /// <param name="url">本次候选完整 URL。</param>
        /// <param name="ct">调用方取消令牌。</param>
        /// <returns>本次物理请求的池化响应。</returns>
        private UniTask<HttpResponse> GetVersionCheckResponseAsync(
            IPhysicalHttpManager physicalHttpManager,
            string url,
            CancellationToken ct)
        {
            if (physicalHttpManager != null)
            {
                return physicalHttpManager.GetPhysicalAsync(url, m_Config.TimeoutSeconds, null, ct);
            }

            ct.ThrowIfCancellationRequested();
            return m_HttpManager.GetAsync(url, m_Config.TimeoutSeconds);
        }

        /// <summary>
        /// 判断 HTTP 失败是否允许继续主备候选。
        /// 传输失败、客户端数据处理失败、404、408、429 与 5xx 可继续，其他正式 HTTP 状态停止整链。
        /// </summary>
        /// <param name="response">本次失败响应。</param>
        /// <returns>允许继续下一个候选时返回 true。</returns>
        private static bool ShouldRetryVersionCheckResponse(HttpResponse response)
        {
            if (response == null || !response.HasServerResponse || response.StatusCode <= 0)
            {
                return true;
            }

            if (string.Equals(response.TransportState, "DataProcessingError", StringComparison.Ordinal))
            {
                return true;
            }

            return response.StatusCode == 404 ||
                   response.StatusCode == 408 ||
                   response.StatusCode == 429 ||
                   response.StatusCode >= 500;
        }

        /// <summary>
        /// 将当前候选标记为可继续失败，并在计划耗尽时输出该条链的唯一终态埋点。
        /// </summary>
        /// <param name="cursor">本次版本检查独占的共享候选游标。</param>
        /// <param name="shouldTrack">是否由 App 链路负责统一 UWR 埋点。</param>
        /// <param name="chainId">逻辑请求链关联 ID。</param>
        /// <param name="plan">本次共享候选执行计划。</param>
        /// <param name="step">当前物理发送坐标。</param>
        /// <param name="attemptsStarted">已经开始的物理发送次数。</param>
        /// <param name="requestElapsedMs">本次物理发送耗时。</param>
        /// <param name="totalElapsedMs">整条链当前累计耗时。</param>
        /// <param name="response">当前池化响应，可为 null。</param>
        /// <param name="exception">当前请求异常，可为 null。</param>
        /// <param name="resultOverride">终态埋点覆盖结果，可为 null。</param>
        /// <param name="leafErrorCodeOverride">业务校验失败的稳定叶子错误码，可为 null。</param>
        private void RejectVersionCheckAttempt(
            HttpFallbackExecutionCursor cursor,
            bool shouldTrack,
            string chainId,
            HttpFallbackExecutionPlan plan,
            HttpFallbackStep step,
            int attemptsStarted,
            long requestElapsedMs,
            long totalElapsedMs,
            HttpResponse response,
            Exception exception,
            string resultOverride,
            string leafErrorCodeOverride)
        {
            cursor.RejectCurrent();
            TrackVersionCheckAttemptFailure(
                shouldTrack, chainId, plan, step, requestElapsedMs, response, exception, false,
                leafErrorCodeOverride);
            if (cursor.State == HttpFallbackExecutionState.Exhausted)
            {
                TrackVersionCheckEnd(
                    shouldTrack, chainId, plan, step, attemptsStarted, requestElapsedMs, totalElapsedMs,
                    response, exception, false, resultOverride, leafErrorCodeOverride);
            }
        }

        /// <summary>
        /// 生成供日志与埋点使用的主备候选说明，不暴露完整 URL 以避免重复日志携带路径参数。
        /// </summary>
        /// <param name="step">当前物理发送坐标。</param>
        /// <returns>稳定的候选角色和物理发送序号说明。</returns>
        private static string DescribeVersionCheckCandidate(HttpFallbackStep step)
        {
            return Txt.Format(
                "{0}候选（轮次={1} 重试周期={2} 发送序号={3}）",
                step.Candidate.RouteRole,
                step.RoundIndex + 1,
                step.RetryCycleIndex,
                step.PhysicalSendIndex + 1);
        }

        /// <summary>
        /// 解析服务端版本响应 JSON，返回 AppVersionResult。
        /// 优先级：强制更新规则 > 推荐更新规则。
        /// 命中强制规则返回 ForcedDownload；命中推荐规则返回 RecommendedDownload；其余返回 NoDownload。
        /// JSON 解析异常或规则无效时返回 NoDownload 并 Log.Error。
        /// </summary>
        /// <param name="json">服务端返回的版本配置 JSON 文本。</param>
        /// <returns>App 版本检查结果枚举值。</returns>
        private AppVersionResult ParseVersionResult(string json)
        {
            return TryParseVersionResult(json, out AppVersionResult result)
                ? result
                : AppVersionResult.NoDownload;
        }

        /// <summary>
        /// 尝试解析并应用服务端版本规则。
        /// 仅当 JSON 可解析且两个版本阈值均符合 App 版本规则时返回 true，合法但未命中更新仍返回 true。
        /// </summary>
        /// <param name="json">服务端返回的版本配置 JSON 文本。</param>
        /// <param name="result">解析后的版本检查结果。</param>
        /// <returns>规则可用时返回 true；无效时返回 false，供调用方继续尝试备用地址。</returns>
        private bool TryParseVersionResult(string json, out AppVersionResult result)
        {
            result = AppVersionResult.NoDownload;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                AppVersionResponse resp = Util.Json.Deserialize<AppVersionResponse>(json);
                if (!IsValidVersionResponse(resp))
                {
                    Log.Error(LogTag.App, "大版本检查 JSON 不符合 App 版本规则。");
                    return false;
                }

                ResetMatchedRuleState();

                string localVersion = UnityEngine.Application.version;

                if (m_Config.UseForcedDownloadRule && IsVersionGreater(resp.ForcedDownloadVersion, localVersion))
                {
                    ApplyMatchedRule(AppDownloadRule.Forced);
                    result = AppVersionResult.ForcedDownload;
                    return true;
                }

                if (m_Config.UseRecommendedDownloadRule && IsVersionGreater(resp.RecommendedDownloadVersion, localVersion))
                {
                    if (resp.RecommendedDownloadPromptIntervalSeconds < 0)
                    {
                        Log.Warning(
                            LogTag.App,
                            "RecommendedDownloadPromptIntervalSeconds 不能为负数，本次按 0 秒处理：{0}",
                            resp.RecommendedDownloadPromptIntervalSeconds);
                    }

                    if (ShouldSuppressRecommendedDownloadPrompt(resp.RecommendedDownloadPromptIntervalSeconds))
                    {
                        Log.Debug(
                            LogTag.App,
                            "推荐更新规则已命中，但距离上次放弃更新尚未达到提示间隔，本次跳过提示。IntervalSeconds={0}",
                            resp.RecommendedDownloadPromptIntervalSeconds);
                        return true;
                    }

                    ApplyMatchedRule(AppDownloadRule.Recommended);
                    result = AppVersionResult.RecommendedDownload;
                    return true;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Log.Error(LogTag.App, "大版本检查 JSON 解析异常：{0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 判断当前推荐更新提示是否仍处于用户放弃后的抑制间隔内。
        /// 配置缺失、间隔非正数、记录无效、间隔已到或系统时钟回拨时均不抑制提示。
        /// </summary>
        /// <param name="intervalSeconds">远端配置的推荐更新提示间隔秒数。</param>
        /// <returns>仍在有效间隔内时返回 true。</returns>
        private static bool ShouldSuppressRecommendedDownloadPrompt(long intervalSeconds)
        {
            if (intervalSeconds <= 0)
            {
                return false;
            }

            string raw = PlatformPlayerPrefs.GetString(c_RecommendedDownloadDismissedAtKey, string.Empty);
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long dismissedAt))
            {
                return false;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return ShouldSuppressRecommendedDownloadPrompt(intervalSeconds, dismissedAt, now);
        }

        /// <summary>
        /// 按给定时间判断推荐更新提示是否仍处于抑制间隔内，供时间边界逻辑复用与验证。
        /// </summary>
        /// <param name="intervalSeconds">最小提示间隔秒数。</param>
        /// <param name="dismissedAt">上次放弃更新的 UTC Unix 秒。</param>
        /// <param name="now">当前 UTC Unix 秒。</param>
        /// <returns>当前时间有效且尚未达到间隔时返回 true。</returns>
        private static bool ShouldSuppressRecommendedDownloadPrompt(long intervalSeconds, long dismissedAt, long now)
        {
            if (intervalSeconds <= 0 || dismissedAt <= 0 || now < dismissedAt)
            {
                return false;
            }

            return now - dismissedAt < intervalSeconds;
        }

        /// <summary>
        /// 校验 CDN 响应是否包含符合 App 版本规则的推荐和强制版本阈值。
        /// </summary>
        /// <param name="response">待校验的版本响应对象。</param>
        /// <returns>两个版本阈值都可按 System.Version 解析时返回 true。</returns>
        private static bool IsValidVersionResponse(AppVersionResponse response)
        {
            return response != null
                   && Version.TryParse(response.RecommendedDownloadVersion, out _)
                   && Version.TryParse(response.ForcedDownloadVersion, out _);
        }

        /// <summary>
        /// 命中更新规则后，统一回写状态和目标地址。
        /// </summary>
        /// <param name="rule">命中的规则。</param>
        private void ApplyMatchedRule(AppDownloadRule rule)
        {
            m_MatchedRule = rule;
            ApplyRouteTargets();
        }

        /// <summary>
        /// 根据当前下载路由，仅解析当前流程真正需要的目标地址。
        /// Store 只解析当前平台商店地址；Apk 只解析主下载地址。
        /// </summary>
        private void ApplyRouteTargets()
        {
            m_TargetStoreUrl = null;
            m_TargetDownloadUrl = null;

            switch (m_Config.DownloadRoute)
            {
                case AppDownloadRoute.Store:
                    m_TargetStoreUrl = ResolveStoreUrl();
                    break;
                case AppDownloadRoute.Apk:
                    m_TargetDownloadUrl = ResolvePrimaryDownloadUrl();
                    break;
                default:
                    Log.Warning(LogTag.App, "未知的 DownloadRoute：{0}", m_Config.DownloadRoute);
                    break;
            }
        }

        /// <summary>
        /// 清空上一次检查结果，避免 NoDownload 或失败路径残留旧状态。
        /// </summary>
        private void ResetMatchedRuleState()
        {
            m_MatchedRule = AppDownloadRule.None;
            m_TargetStoreUrl = null;
            m_TargetDownloadUrl = null;
        }

        /// <summary>
        /// 比较两个版本号，返回正数表示 remote > local，0 表示相等，负数表示不命中或 remote < local。
        /// 仅接受 System.Version 可解析的纯数字点分版本号。
        /// </summary>
        /// <param name="remote">远端版本号。</param>
        /// <param name="local">本地版本号。</param>
        /// <returns>比较结果。</returns>
        private static int CompareVersions(string remote, string local)
        {
            bool hasRemote = TryParseVersion(remote, "远端", out Version remoteVersion);
            bool hasLocal = TryParseVersion(local, "本地", out Version localVersion);
            if (!hasRemote && !hasLocal)
            {
                return 0;
            }

            if (!hasRemote || !hasLocal)
            {
                return -1;
            }

            return remoteVersion.CompareTo(localVersion);
        }

        /// <summary>
        /// 判断远端版本是否高于本地版本。
        /// </summary>
        /// <param name="remote">远端版本号。</param>
        /// <param name="local">本地版本号。</param>
        /// <returns>远端更高时返回 true。</returns>
        private static bool IsVersionGreater(string remote, string local)
        {
            return CompareVersions(remote, local) > 0;
        }

        /// <summary>
        /// 解析 System.Version 格式的版本号。
        /// </summary>
        /// <param name="version">待解析的版本字符串。</param>
        /// <param name="source">版本来源，用于日志定位。</param>
        /// <param name="parsedVersion">解析结果。</param>
        /// <returns>解析成功返回 true。</returns>
        private static bool TryParseVersion(string version, string source, out Version parsedVersion)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                Log.Warning(LogTag.App, "{0}版本号为空，跳过更新比较。", source);
                parsedVersion = null;
                return false;
            }

            if (Version.TryParse(version, out parsedVersion))
            {
                return true;
            }

            Log.Warning(LogTag.App, "{0}版本号格式非法，需为纯数字点分格式：{1}", source, version);
            return false;
        }

        /// <summary>
        /// 按当前平台从 AppManagerConfig 解析商店跳转地址（iOS 取 AppStoreUrl，其余平台取 AndroidStoreUrl）。
        /// </summary>
        /// <returns>当前平台的商店跳转地址；未配置时返回 string.Empty。</returns>
        private string ResolveStoreUrl()
        {
#if UNITY_IOS
            string storeUrl = m_Config.AppStoreUrl;
#else
            string storeUrl = m_Config.AndroidStoreUrl;
#endif
            if (string.IsNullOrEmpty(storeUrl))
            {
                Log.Error(LogTag.App, "当前平台商店地址未配置。");
                return string.Empty;
            }

            return storeUrl;
        }

        /// <summary>
        /// 解析 APK 主下载地址。
        /// 启动期只校验主下载地址；备用下载地址留给后续下载实现自行决定是否回退。
        /// </summary>
        /// <returns>主下载地址；未配置时返回 null。</returns>
        private string ResolvePrimaryDownloadUrl()
        {
            if (!string.IsNullOrEmpty(m_Config.PrimaryDownloadUrl))
            {
                return m_Config.PrimaryDownloadUrl;
            }

            Log.Error(LogTag.App, "DownloadRoute=Apk，但 PrimaryDownloadUrl 未配置。");
            return null;
        }

        /// <summary>
        /// 检查候选 URL 是否有效（非 null、非空白）。
        /// </summary>
        /// <param name="url">待检查的地址字符串。</param>
        /// <returns>有效时返回 true。</returns>
        private bool IsValidUrl(string url)
        {
            return !string.IsNullOrWhiteSpace(url);
        }
    }
}
