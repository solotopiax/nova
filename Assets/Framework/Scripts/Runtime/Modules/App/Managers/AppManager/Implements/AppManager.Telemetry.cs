/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppManager.Telemetry.cs
 * author:    taoye
 * created:   2026/9/2
 * descrip:   App 版本检查主备链路埋点适配
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// App 管理器。
    /// </summary>
    internal sealed partial class AppManager : AppManagerBase
    {
        /// <summary>
        /// App 版本检查在统一 UWR 埋点中的稳定操作名。
        /// </summary>
        private const string c_AppVersionCheckOperationName = "app_version_check";

        /// <summary>
        /// App 版本检查埋点的业务场景字段。
        /// </summary>
        private const string c_AppTelemetryScene = "app";

        /// <summary>
        /// HTTP 成功但 App 规则内容不可用时的链路终态。
        /// </summary>
        private const string c_InvalidVersionResponseResult = "invalid_response";

        /// <summary>
        /// HTTP 成功但正文为空时的稳定叶子错误码。
        /// </summary>
        private const string c_EmptyVersionResponseLeafErrorCode = "empty_response";

        /// <summary>
        /// HTTP 成功但 JSON 或版本规则无效时的稳定叶子错误码。
        /// </summary>
        private const string c_InvalidVersionResponseLeafErrorCode = "invalid_version_rule";

        /// <summary>
        /// 上报 App 版本检查主备链开始；内置物理 HTTP 入口不另建链，避免同次发送重复埋点。
        /// </summary>
        /// <param name="shouldTrack">是否启用本模块统一 UWR 埋点。</param>
        /// <param name="chainId">逻辑请求链关联 ID。</param>
        /// <param name="plan">本次共享候选执行计划。</param>
        private void TrackVersionCheckStart(
            bool shouldTrack,
            string chainId,
            HttpFallbackExecutionPlan plan)
        {
            if (plan == null || plan.CandidateCount == 0)
            {
                return;
            }

            HttpFallbackCandidate candidate = plan.Candidates[0];
            UwrNetworkTelemetry.TrackStart(
                shouldTrack,
                chainId,
                c_AppVersionCheckOperationName,
                "GET",
                candidate.Url,
                ToUwrRouteRole(candidate.RouteRole),
                0,
                m_Config.TimeoutSeconds,
                plan,
                null,
                c_AppTelemetryScene);
        }

        /// <summary>
        /// 上报 App 版本检查候选失败；正式 HTTP 错误是否产生 error 事件由统一 UWR 规则决定。
        /// </summary>
        /// <param name="shouldTrack">是否启用本模块统一 UWR 埋点。</param>
        /// <param name="chainId">逻辑请求链关联 ID。</param>
        /// <param name="plan">本次共享候选执行计划。</param>
        /// <param name="step">当前物理发送坐标。</param>
        /// <param name="requestElapsedMs">本次物理发送耗时。</param>
        /// <param name="response">当前请求响应，可为 null。</param>
        /// <param name="exception">当前请求异常，可为 null。</param>
        /// <param name="cancelled">是否由调用方取消。</param>
        /// <param name="leafErrorCodeOverride">业务校验失败的稳定叶子错误码，可为 null。</param>
        private void TrackVersionCheckAttemptFailure(
            bool shouldTrack,
            string chainId,
            HttpFallbackExecutionPlan plan,
            HttpFallbackStep step,
            long requestElapsedMs,
            HttpResponse response,
            Exception exception,
            bool cancelled,
            string leafErrorCodeOverride)
        {
            UwrNetworkTelemetry.TrackError(
                shouldTrack,
                chainId,
                c_AppVersionCheckOperationName,
                "GET",
                step.Candidate.Url,
                ToUwrRouteRole(step.Candidate.RouteRole),
                ToUwrSendIndex(step),
                m_Config.TimeoutSeconds,
                requestElapsedMs,
                response,
                exception,
                cancelled,
                plan,
                step,
                c_AppTelemetryScene,
                leafErrorCodeOverride);
        }

        /// <summary>
        /// 上报 App 版本检查主备链唯一终态。
        /// </summary>
        /// <param name="shouldTrack">是否启用本模块统一 UWR 埋点。</param>
        /// <param name="chainId">逻辑请求链关联 ID。</param>
        /// <param name="plan">本次共享候选执行计划。</param>
        /// <param name="step">终态物理发送坐标。</param>
        /// <param name="attemptsStarted">已经开始的物理发送次数。</param>
        /// <param name="requestElapsedMs">终态物理发送耗时。</param>
        /// <param name="totalElapsedMs">整条链累计耗时。</param>
        /// <param name="response">终态响应，可为 null。</param>
        /// <param name="exception">终态异常，可为 null。</param>
        /// <param name="cancelled">是否由调用方取消。</param>
        /// <param name="resultOverride">业务规则覆盖后的终态结果，可为 null。</param>
        /// <param name="leafErrorCodeOverride">业务校验失败的稳定叶子错误码，可为 null。</param>
        private void TrackVersionCheckEnd(
            bool shouldTrack,
            string chainId,
            HttpFallbackExecutionPlan plan,
            HttpFallbackStep step,
            int attemptsStarted,
            long requestElapsedMs,
            long totalElapsedMs,
            HttpResponse response,
            Exception exception,
            bool cancelled,
            string resultOverride,
            string leafErrorCodeOverride)
        {
            UwrNetworkTelemetry.TrackEnd(
                shouldTrack,
                chainId,
                c_AppVersionCheckOperationName,
                "GET",
                step.Candidate.Url,
                ToUwrRouteRole(step.Candidate.RouteRole),
                ToUwrSendIndex(step),
                m_Config.TimeoutSeconds,
                requestElapsedMs,
                totalElapsedMs,
                response,
                exception,
                cancelled,
                plan,
                step,
                attemptsStarted,
                c_AppTelemetryScene,
                resultOverride,
                leafErrorCodeOverride);
        }

        /// <summary>
        /// 转换共享候选角色为统一 UWR 埋点枚举文本。
        /// </summary>
        /// <param name="routeRole">共享候选角色。</param>
        /// <returns>埋点使用的稳定角色文本。</returns>
        private static string ToUwrRouteRole(HttpFallbackRouteRole routeRole)
        {
            return routeRole switch
            {
                HttpFallbackRouteRole.Primary => "primary",
                HttpFallbackRouteRole.Fallback => "fallback",
                _ => "other",
            };
        }

        /// <summary>
        /// 将共享游标的物理发送序号安全转换为当前 UWR 埋点字段使用的 int。
        /// </summary>
        /// <param name="step">当前物理发送坐标。</param>
        /// <returns>非负且不溢出的发送序号。</returns>
        private static int ToUwrSendIndex(HttpFallbackStep step)
        {
            return step.PhysicalSendIndex > int.MaxValue
                ? int.MaxValue
                : (int)Math.Max(0L, step.PhysicalSendIndex);
        }
    }
}
