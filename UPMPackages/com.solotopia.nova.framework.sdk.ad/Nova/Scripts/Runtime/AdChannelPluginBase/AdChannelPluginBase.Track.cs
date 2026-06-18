/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.Track.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   AdChannelPluginBase 事件触发与广告行为打点（9 事件全口径）
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    public abstract partial class AdChannelPluginBase
    {
        /// <summary>
        /// 从 SDKComponent 获取所有 ITrackPlugin 并缓存到 m_TrackPlugins。
        /// </summary>
        private void CacheTrackPlugins()
        {
            var sdkComponent = FrameworkComponentsGroup.GetComponent<SDKComponent>();
            if (sdkComponent == null)
            {
                Log.Warning(LogTag.AD, "CacheTrackPlugins：sdkComponent 尚未就绪。");
                return;
            }
            m_TrackPlugins = sdkComponent.GetAll<ITrackPlugin>();
        }

        /// <summary>
        /// 向 m_TrackPlugins 中所有变现打点插件上报一条自定义事件。
        /// m_TrackPlugins 为空时静默跳过。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="props">事件属性字典。</param>
        private void TrackEvent(string eventName, Dictionary<string, object> props)
        {
            if (m_TrackPlugins == null || m_TrackPlugins.Count == 0) return;
            for (int i = 0; i < m_TrackPlugins.Count; i++)
                m_TrackPlugins[i].TrackEvent(eventName, props);
        }

        /// <summary>
        /// 构建包含格式、广告位 ID 和渠道名称的基础属性字典。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识，为 null 时使用空字符串。</param>
        /// <returns>包含三个基础属性的字典。</returns>
        private Dictionary<string, object> BuildBaseProps(AdFormat format, string placementId)
        {
            return new Dictionary<string, object>
            {
                { "nova_ad_channel", Name },
                { "nova_ad_format", (int)format },
                { "nova_ad_id", placementId ?? string.Empty },
            };
        }

        /// <summary>
        /// 将自定义属性合并到目标字典；已存在的 key 跳过，不覆盖基础属性。
        /// custom 为 null 时静默跳过。
        /// </summary>
        /// <param name="target">目标属性字典。</param>
        /// <param name="custom">自定义属性字典，可为 null。</param>
        private static void MergeCustom(Dictionary<string, object> target, Dictionary<string, object> custom)
        {
            MergeProps(target, custom);
        }

        /// <summary>
        /// 将附加属性合并到目标字典；已存在的 key 跳过，不覆盖基础属性。
        /// </summary>
        /// <param name="target">目标属性字典。</param>
        /// <param name="extra">附加属性字典，可为 null。</param>
        private static void MergeProps(Dictionary<string, object> target, Dictionary<string, object> extra)
        {
            if (extra == null) return;
            foreach (var kvp in extra)
                if (!target.ContainsKey(kvp.Key))
                    target[kvp.Key] = kvp.Value;
        }

        /// <summary>
        /// 上报 nova_ad_request 打点事件。
        /// 由 RequestAsync 在发起每个 AdUnit 请求前调用，封装字典构造和 TrackEvent 调用。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识。</param>
        /// <param name="reason">本次请求的原因枚举值。</param>
        /// <param name="customProps">自定义属性字典，将合并到打点属性中；可为 null。</param>
        protected void TrackAdRequest(AdFormat format, string placementId, AdRequestReason reason, Dictionary<string, object> customProps)
        {
            var props = BuildBaseProps(format, placementId);
            props["nova_ad_reason"] = (int)reason;
            MergeCustom(props, customProps);
            TrackEvent("nova_ad_request", props);
        }

        /// <summary>
        /// 在主线程触发 OnAdRevenuePaid 事件，并驱动打点扇出。
        /// 执行顺序：先状态机推进（MarkRevenue）→ 再事件 fan-out → 再打点。
        /// 派生类在广告展示产生收入的 SDK 回调中调用；须确保已切回主线程。
        /// </summary>
        /// <param name="e">广告变现埋点事件载荷。</param>
        protected void RaiseRevenue(AdEvent e)
        {
            MarkRevenue(e.PlacementId, e.Revenue);
            var revenueUnit = FindAdUnit(e.PlacementId);
            if (revenueUnit != null) revenueUnit.RewardGranted = true;
            OnAdRevenuePaid?.Invoke(e);
        }

        /// <summary>
        /// 在主线程触发 OnAdLoaded 事件，并驱动打点扇出和批次完成通知。
        /// 执行顺序：先状态机推进（MarkLoaded）→ 再事件 fan-out → 再批次通知 → 再打点（合并 RequestCustomProps）。
        /// 派生类在广告加载成功的 SDK 回调中调用；须确保已切回主线程。
        /// </summary>
        /// <param name="e">加载成功事件载荷。</param>
        protected void RaiseAdLoaded(AdLoadResult e)
        {
            e.Success = true;
            MarkLoaded(e.PlacementId, e.Revenue);
            OnAdLoaded?.Invoke(e);
            NotifyBatchLoaded(e);
            var unit = FindAdUnit(e.PlacementId);
            var props = new Dictionary<string, object>
            {
                { "nova_ad_channel", Name },
                { "nova_ad_format", (int)e.Format },
                { "nova_ad_id", e.PlacementId ?? string.Empty },
                { "nova_ad_result", 1 },
                { "nova_ad_network", e.Network ?? string.Empty },
            };
            MergeCustom(props, unit?.RequestCustomProps);
            MergeCustom(props, e.CustomProps);
            TrackEvent("nova_ad_fill", props);
        }

        /// <summary>
        /// 遍历 m_PendingBatches，通知所有包含该广告位的批次加载成功；TrySetResult 成功的批次立即移除。
        /// 倒序遍历确保删除安全。
        /// </summary>
        /// <param name="e">加载成功事件载荷。</param>
        private void NotifyBatchLoaded(AdLoadResult e)
        {
            for (int i = m_PendingBatches.Count - 1; i >= 0; i--)
            {
                var batch = m_PendingBatches[i];
                if (!batch.PendingPlacementIds.Contains(e.PlacementId)) continue;
                if (batch.Tcs.TrySetResult(e))
                {
                    batch.Registration.Dispose();
                    m_PendingBatches.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 在主线程触发 OnAdLoadFailed 事件，并驱动打点扇出和批次失败通知。
        /// 执行顺序：先状态机推进（MarkLoadFailed，含自动重试调度）→ 再事件 fan-out → 再批次通知 → 再打点（合并 RequestCustomProps）。
        /// 派生类在广告加载失败的 SDK 回调中调用；须确保已切回主线程。
        /// </summary>
        /// <param name="e">加载失败事件载荷。</param>
        protected void RaiseAdLoadFailed(AdLoadResult e)
        {
            e.Success = false;
            MarkLoadFailed(e.PlacementId, e.ErrorMessage);
            OnAdLoadFailed?.Invoke(e);
            NotifyBatchFailed(e);
            var unit = FindAdUnit(e.PlacementId);
            var props = new Dictionary<string, object>
            {
                { "nova_ad_channel", Name },
                { "nova_ad_format", (int)e.Format },
                { "nova_ad_id", e.PlacementId ?? string.Empty },
                { "nova_ad_result", 0 },
                { "nova_ad_errorcode", e.ErrorCode },
                { "nova_ad_error_message", e.ErrorMessage ?? string.Empty },
            };
            MergeCustom(props, unit?.RequestCustomProps);
            TrackEvent("nova_ad_fill_fail", props);
        }

        /// <summary>
        /// 遍历 m_PendingBatches，从含有该广告位的批次中移除对应 ID。
        /// solar 重试语义下不存在 Failed 终止态，每次 RaiseAdLoadFailed 都通知调用方"本批已失败"，
        /// 底层 ImmediateRetry / DelayedRetry 仍会持续重试，下一轮成功时通过 RaiseAdLoaded 触发新的 RequestAsync 链路。
        /// 某批次 PendingPlacementIds 清空后，以最近一次失败结果结束该批次。
        /// </summary>
        /// <param name="e">加载失败结果。</param>
        private void NotifyBatchFailed(AdLoadResult e)
        {
            for (int i = m_PendingBatches.Count - 1; i >= 0; i--)
            {
                var batch = m_PendingBatches[i];
                if (!batch.PendingPlacementIds.Remove(e.PlacementId)) continue;
                batch.LastFailure = e;
                if (batch.PendingPlacementIds.Count > 0) continue;
                batch.Registration.Dispose();
                batch.Tcs.TrySetResult(batch.LastFailure);
                m_PendingBatches.RemoveAt(i);
            }
        }

        /// <summary>
        /// 触发 OnInitResult 事件，并上报 nova_ad_init 打点。
        /// </summary>
        /// <param name="success">初始化是否成功。</param>
        protected void RaiseInitResult(bool success)
        {
            OnInitResult?.Invoke(success);
            TrackEvent("nova_ad_init", new Dictionary<string, object>
            {
                { "nova_success", success },
                { "nova_ad_channel", Name },
            });
        }

        /// <summary>
        /// 触发 OnShowCompleted 事件，并上报 nova_ad_show_result（Success）打点。
        /// 执行顺序：先状态机推进（MarkShown，含非 Banner 续杯）→ 再事件 fan-out → 再打点。
        /// </summary>
        /// <param name="result">广告展示结果。</param>
        protected void RaiseShowCompleted(AdResult result)
        {
            var unit = FindAdUnit(result.PlacementId);
            var showCustomProps = unit?.ShowCustomProps;
            MarkShown(result.PlacementId);
            OnShowCompleted?.Invoke(result);
            var props = BuildBaseProps(result.Format, result.PlacementId);
            props["nova_ad_result"] = 1;
            MergeCustom(props, showCustomProps);
            TrackEvent("nova_ad_show_result", props);
        }

        /// <summary>
        /// 触发 OnShowFailed 事件，并上报 nova_ad_show_result（Fail）打点。
        /// 执行顺序：先状态机推进（MarkShown，含非 Banner 续杯）→ 再事件 fan-out → 再打点。
        /// </summary>
        /// <param name="result">广告播放失败结果。</param>
        protected void RaiseShowFailed(AdResult result)
        {
            var unit = FindAdUnit(result.PlacementId);
            var showCustomProps = unit?.ShowCustomProps;
            MarkShown(result.PlacementId);
            OnShowFailed?.Invoke(result);
            var props = BuildBaseProps(result.Format, result.PlacementId);
            props["nova_ad_result"] = 0;
            MergeCustom(props, showCustomProps);
            TrackEvent("nova_ad_show_result", props);
        }

        /// <summary>
        /// 触发 OnAdClosed 事件，并在基类内部上报 nova_ad_hidden 打点。
        /// 执行顺序：先状态机推进（MarkShown，含非 Banner 续杯）→ 再事件 fan-out → 再打点。
        /// </summary>
        /// <param name="result">广告关闭结果。</param>
        /// <param name="rewarded">是否满足奖励条件；激励视频使用，其他格式传 false。</param>
        protected void RaiseAdClosed(AdResult result, bool rewarded = false)
        {
            var closedUnit = FindAdUnit(result.PlacementId);
            result.RewardGranted = rewarded || (closedUnit?.RewardGranted ?? false);
            if (closedUnit != null) closedUnit.RewardGranted = false;
            var showCustomProps = closedUnit?.ShowCustomProps;
            MarkShown(result.PlacementId);
            OnAdClosed?.Invoke(result);
            var props = BuildBaseProps(result.Format, result.PlacementId);
            props["nova_can_get_reward"] = rewarded;
            MergeCustom(props, showCustomProps);
            TrackEvent("nova_ad_hidden", props);
        }

        /// <summary>
        /// 派生类在广告展示回调中调用，上报 nova_ad_show 事件。
        /// 基类自动从对应 AdUnit 取 ShowCustomProps 合并；派生侧调用面无需传 customProps。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识。</param>
        protected void TrackAdShow(AdFormat format, string placementId)
        {
            var unit = FindAdUnit(placementId);
            var props = BuildBaseProps(format, placementId);
            MergeCustom(props, unit?.ShowCustomProps);
            TrackEvent("nova_ad_show", props);
        }

        /// <summary>
        /// 派生类在广告点击回调中调用，上报 nova_ad_click 事件。
        /// 基类自动从对应 AdUnit 取 ShowCustomProps 合并。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识。</param>
        protected void TrackAdClick(AdFormat format, string placementId)
        {
            var unit = FindAdUnit(placementId);
            var props = BuildBaseProps(format, placementId);
            MergeCustom(props, unit?.ShowCustomProps);
            TrackEvent("nova_ad_click", props);
        }
    }
}
