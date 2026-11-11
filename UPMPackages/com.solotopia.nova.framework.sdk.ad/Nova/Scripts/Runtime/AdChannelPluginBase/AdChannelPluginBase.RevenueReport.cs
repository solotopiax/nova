/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.RevenueReport.cs
 * author:    Codex
 * created:   2026/11/2
 * descrip:   AdChannelPluginBase 广告收益统一上报
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告渠道基类的收益统一上报调度。
    /// </summary>
    public abstract partial class AdChannelPluginBase
    {
        /// <summary>
        /// 由渠道在收益派发入口调用，发送本次单笔或已累计的收益；本方法不累计、不存档。
        /// 后台线程先冻结载荷并进入主线程，网络异常只记录警告。
        /// </summary>
        /// <param name="revenue">与渠道本次收益派发相同的载荷，Banner 必须传入累计金额。</param>
        protected void StartRevenueReport(AdEvent revenue)
        {
            if (m_IsDisposed)
            {
                Log.Debug(LogTag.AD, $"[{Name}] 广告收益上报已跳过：渠道已释放。");
                return;
            }
            if (revenue == null)
            {
                Log.Warning(LogTag.AD, $"[{Name}] 广告收益上报已跳过：收益事件为空。");
                return;
            }
            if (!PlayerLoopHelper.IsMainThread)
            {
                var snapshot = SnapshotRevenueEvent(revenue);
                PostAdCallbackToMainThread(() => StartRevenueReport(snapshot));
                return;
            }
            Log.Debug(LogTag.AD, $"[{Name}] 收到广告收益：channel={Channel}，format={revenue.Format}，placement={revenue.PlacementId}，price_usd={revenue.Revenue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}。");
            if (string.IsNullOrWhiteSpace(m_RevenueReportCmdName))
            {
                Log.Debug(LogTag.AD, $"[{Name}] 广告收益上报已跳过：未配置收益上报 NetCmd。");
                return;
            }

            ReportRevenueAsync(revenue, Guid.NewGuid().ToString("N"), Channel, ChannelLifetimeToken).Forget();
        }

        /// <summary>
        /// 调用广告收益上报 Service，并隔离后台网络异常。
        /// </summary>
        /// <param name="revenue">收益事件。</param>
        /// <param name="reportId">本次请求复用的幂等 ID。</param>
        /// <param name="channel">广告渠道枚举。</param>
        /// <param name="cancellationToken">渠道生命周期令牌。</param>
        private async UniTask ReportRevenueAsync(AdEvent revenue, string reportId, AdChannelType channel, CancellationToken cancellationToken)
        {
            try
            {
                NetResponse<PbNetIAAReportResp> response = await m_RevenueReportNetService.Async(m_RevenueReportCmdName, revenue, reportId, channel, cancellationToken);
                if (response == null)
                {
                    Log.Warning(LogTag.AD, $"[{Name}] 广告收益上报未返回网络响应，report_id={reportId}。");
                    return;
                }
                if (!response.IsSuccess)
                {
                    Log.Warning(LogTag.AD, $"[{Name}] 广告收益上报失败，report_id={reportId}，error={response.ErrorCode}，message={response.ErrorMessage}。");
                    return;
                }

                PbNetIAAReportResp data = response.Data;
                if (data != null && data.ErrorCode != 0)
                {
                    Log.Warning(LogTag.AD, $"[{Name}] 广告收益上报业务响应 error_code={data.ErrorCode}，report_id={data.IaaReportId}，duplicate={data.Duplicate}。");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || m_IsDisposed)
            {
                // 渠道释放只取消未完成请求，不向原厂收益回调抛出异常。
                Log.Debug(LogTag.AD, $"[{Name}] 广告收益上报已取消：report_id={reportId}，渠道生命周期结束。");
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"[{Name}] 广告收益上报异常，report_id={reportId}，{ex.Message}。");
            }
        }
    }
}
