/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Impression.cs
 * author:    Codex
 * created:   2026/9/22
 * descrip:   MAX 全屏广告曝光关联诊断
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {
#if NOVA_APPLOVIN_MAX
        private const string c_ImpressionIdProperty = "nova_ad_impression_id";
        private const string c_RevenueCallbackIndexProperty = "nova_ad_revenue_callback_index";
        private const string c_CorrelationStatusProperty = "nova_ad_correlation_status";
        private const string c_CorrelationBestEffortMatch = "best_effort_match";
        private const string c_CorrelationBeforeShow = "revenue_before_show";
        private const string c_CorrelationOrphan = "orphan";

        /// <summary>
        /// 在调用 MAX 全屏展示接口前建立本地曝光上下文。
        /// 本地 ID 只用于关联同一运行进程中的展示与收益回调，不冒充厂商原生曝光 ID。
        /// </summary>
        /// <param name="format">全屏广告格式。</param>
        /// <param name="placementId">本次选择的广告位 ID。</param>
        private void BeginFullscreenImpression(AdFormat format, string placementId)
        {
            lock (m_FullscreenImpressionLock)
            {
                m_FullscreenImpressions[format] = new FullscreenImpressionContext(
                    Guid.NewGuid().ToString("N"),
                    placementId);
            }
        }

        /// <summary>
        /// 标记全屏广告已收到展示回调，并返回需要并入 nova_ad_show 的关联属性。
        /// 未找到展示尝试时建立孤立上下文，确保后续收益回调仍可与该展示关联。
        /// </summary>
        /// <param name="format">全屏广告格式。</param>
        /// <param name="placementId">展示回调携带的广告位 ID。</param>
        /// <returns>包含本地曝光 ID 的展示属性。</returns>
        private Dictionary<string, object> MarkFullscreenDisplayed(AdFormat format, string placementId)
        {
            lock (m_FullscreenImpressionLock)
            {
                FullscreenImpressionContext context = GetOrCreateFullscreenImpression(format, placementId);
                context.Displayed = true;
                return new Dictionary<string, object>
                {
                    { c_ImpressionIdProperty, context.ImpressionId },
                };
            }
        }

        /// <summary>
        /// 记录一次全屏广告收益回调，并返回供 ad_ilrd 与 ad_impression 共用的关联诊断属性。
        /// 此方法只标记同一曝光内的回调序号，不删除收益事件，避免没有厂商唯一曝光 ID 时误删真实收入。
        /// </summary>
        /// <param name="format">全屏广告格式。</param>
        /// <param name="placementId">收益回调携带的广告位 ID。</param>
        /// <returns>曝光 ID、回调序号与当前关联状态。</returns>
        private Dictionary<string, object> RecordFullscreenRevenue(AdFormat format, string placementId)
        {
            lock (m_FullscreenImpressionLock)
            {
                bool existed = TryGetFullscreenImpression(format, placementId, out FullscreenImpressionContext context);
                if (!existed)
                {
                    context = GetOrCreateFullscreenImpression(format, placementId);
                }

                context.RevenueCallbackCount++;
                string status = !existed
                    ? c_CorrelationOrphan
                    : context.Displayed
                        ? c_CorrelationBestEffortMatch
                        : c_CorrelationBeforeShow;

                return new Dictionary<string, object>
                {
                    { c_ImpressionIdProperty, context.ImpressionId },
                    { c_RevenueCallbackIndexProperty, context.RevenueCallbackCount },
                    { c_CorrelationStatusProperty, status },
                };
            }
        }

        /// <summary>
        /// 展示失败时移除尚未形成曝光的本地上下文，防止后续展示错误复用旧 ID。
        /// </summary>
        /// <param name="format">失败的全屏广告格式。</param>
        /// <param name="placementId">失败的广告位 ID。</param>
        private void AbandonFullscreenImpression(AdFormat format, string placementId)
        {
            lock (m_FullscreenImpressionLock)
            {
                if (TryGetFullscreenImpression(format, placementId, out FullscreenImpressionContext context) &&
                    !context.Displayed)
                {
                    m_FullscreenImpressions.Remove(format);
                }
            }
        }

        /// <summary>
        /// 获取同格式、同广告位的当前曝光上下文。
        /// </summary>
        /// <param name="format">全屏广告格式。</param>
        /// <param name="placementId">广告位 ID。</param>
        /// <param name="context">匹配到的曝光上下文。</param>
        /// <returns>存在匹配上下文时返回 true。</returns>
        private bool TryGetFullscreenImpression(
            AdFormat format,
            string placementId,
            out FullscreenImpressionContext context)
        {
            return m_FullscreenImpressions.TryGetValue(format, out context) &&
                   string.Equals(context.PlacementId, placementId, StringComparison.Ordinal);
        }

        /// <summary>
        /// 获取当前曝光上下文；缺失时按回调广告位建立诊断上下文。
        /// 调用方必须持有 m_FullscreenImpressionLock。
        /// </summary>
        /// <param name="format">全屏广告格式。</param>
        /// <param name="placementId">广告位 ID。</param>
        /// <returns>匹配或新建的曝光上下文。</returns>
        private FullscreenImpressionContext GetOrCreateFullscreenImpression(
            AdFormat format,
            string placementId)
        {
            if (TryGetFullscreenImpression(format, placementId, out FullscreenImpressionContext context))
            {
                return context;
            }

            context = new FullscreenImpressionContext(Guid.NewGuid().ToString("N"), placementId);
            m_FullscreenImpressions[format] = context;
            return context;
        }

        /// <summary>
        /// 单次全屏展示尝试的本地关联状态。
        /// </summary>
        private sealed class FullscreenImpressionContext
        {
            /// <summary>
            /// 创建曝光关联状态。
            /// </summary>
            /// <param name="impressionId">本地生成的曝光关联 ID。</param>
            /// <param name="placementId">广告位 ID。</param>
            internal FullscreenImpressionContext(string impressionId, string placementId)
            {
                ImpressionId = impressionId;
                PlacementId = placementId;
            }

            internal string ImpressionId { get; }
            internal string PlacementId { get; }
            internal bool Displayed { get; set; }
            internal int RevenueCallbackCount { get; set; }
        }
#endif
    }
}
