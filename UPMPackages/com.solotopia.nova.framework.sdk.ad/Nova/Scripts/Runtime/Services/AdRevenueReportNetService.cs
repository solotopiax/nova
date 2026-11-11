/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdRevenueReportNetService.cs
 * author:    Codex
 * created:   2026/11/2
 * descrip:   广告收益上报网络 Service
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告收益上报业务网络 Service。
    /// </summary>
    public sealed class AdRevenueReportNetService
    {

        /// <summary>
        /// 校验参数和路由后准备发送广告收益协议；请求头由 NetBuilder 生成。
        /// Debug 记录业务参数和网络结果，参数或路由无效时记录警告，不输出请求认证头。
        /// </summary>
        /// <param name="cmdName">广告收益上报使用的 NetCmd 名称。</param>
        /// <param name="revenue">已经冻结的广告收益事件。</param>
        /// <param name="reportId">本次上报的幂等 ID。</param>
        /// <param name="channel">广告渠道枚举。</param>
        /// <param name="cancellationToken">请求取消令牌。</param>
        /// <returns>收益上报网络响应。</returns>
        public async UniTask<NetResponse<PbNetIAAReportResp>> Async(string cmdName, AdEvent revenue, string reportId, AdChannelType channel, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidate(revenue, reportId, out string reason))
            {
                Log.Warning(LogTag.AD, $"广告收益上报参数无效：cmd={cmdName}，report_id={reportId}，{reason}");
                return NetResponse<PbNetIAAReportResp>.Fail(NetErrorCode.PARAM_ERROR, reason);
            }

            INetworkManager networkManager = FrameworkManagersGroup.GetManager<INetworkManager>();
            INetworkCmdRow cmdRow = string.IsNullOrWhiteSpace(cmdName) ? null : networkManager?.ResolveNetCmdRow(cmdName);
            if (cmdRow == null)
            {
                string message = networkManager == null ? "Network Manager 尚未就绪。" : $"未找到收益上报 NetCmd：{cmdName}。";
                Log.Warning(LogTag.AD, $"广告收益上报已跳过：cmd={cmdName}，report_id={reportId}，{message}");
                return NetResponse<PbNetIAAReportResp>.Fail(NetErrorCode.URL_NOT_FOUND, message);
            }

            var body = new PbNetIAAReportReq
            {
                Head = NetBuilder.BuildHeader(),
                AdChannel = channel.ToString(),
                Iaa = new PbIAAReportData
                {
                    ReportId = reportId,
                    AdType = revenue.Format.ToString(),
                    AdId = revenue.PlacementId,
                    PriceUsd = revenue.Revenue,
                },
            };
            // 这里只能确认准备调用网络层；HTTP 发送和服务端入库须以网络结果及服务端证据为准。
            Log.Debug(LogTag.AD, $"广告收益上报参数：cmd={cmdName}，ad_channel={body.AdChannel}，report_id={body.Iaa.ReportId}，ad_type={body.Iaa.AdType}，ad_id={body.Iaa.AdId}，price_usd={body.Iaa.PriceUsd.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}。");
            var response = await NetService.SendAsync(cmdRow, body, PbNetIAAReportResp.Parser, cancellationToken);
            if (response != null)
            {
                Log.Debug(LogTag.AD, $"广告收益上报网络结果：cmd={cmdName}，report_id={reportId}，success={response.IsSuccess}，data={response.Data?.ToString() ?? "null"}，error_code={response.ErrorCode}，error_message={response.ErrorMessage}，business_error_code={response.Data?.ErrorCode}。");
            }
            return response;
        }

        /// <summary>
        /// 校验收益上报所需的业务 ID、广告位、币种和收益金额，避免无效数据进入网络层。
        /// </summary>
        /// <param name="revenue">待上报的广告收益事件。</param>
        /// <param name="reportId">本次展示对应的稳定上报 ID。</param>
        /// <param name="reason">校验失败原因。</param>
        /// <returns>参数满足协议约束时返回 true。</returns>
        private static bool TryValidate(AdEvent revenue, string reportId, out string reason)
        {
            reason = string.Empty;
            if (revenue == null)
            {
                reason = "收益事件为空。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(reportId))
            {
                reason = "广告收益上报 report_id 为空。";
                return false;
            }
            return TryValidateRevenue(revenue, out reason);
        }

        /// <summary>发送前校验单次或已累计的收益载荷。</summary>
        /// <param name="revenue">原厂收益快照；允许合法的零美元收益。</param>
        /// <param name="reason">失败原因。</param>
        /// <returns>广告位、币种和金额有效时为 true。</returns>
        private static bool TryValidateRevenue(AdEvent revenue, out string reason)
        {
            reason = string.Empty;
            if (revenue == null)
            {
                reason = "收益事件为空。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(revenue.PlacementId))
            {
                reason = "广告收益上报广告位为空。";
                return false;
            }
            if (!string.Equals(revenue.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            {
                reason = $"收益币种不是 USD：{revenue.Currency ?? string.Empty}。";
                return false;
            }
            if (double.IsNaN(revenue.Revenue) || double.IsInfinity(revenue.Revenue) || revenue.Revenue < 0d)
            {
                reason = $"收益金额无效：{revenue.Revenue}。";
                return false;
            }
            return true;
        }
    }
}
