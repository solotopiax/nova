/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.Track.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   AdChannelPluginBase 事件触发与广告行为打点
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告渠道基类的打点与事件触发分部实现。
    /// </summary>
    public abstract partial class AdChannelPluginBase
    {
        /// <summary>
        /// 广告 Banner 收益累计专用文件片段分类；渠道汇总与兼容的广告位累计使用独立条目。
        /// </summary>
        private const string c_BannerIlrdPersistClassify = "NovaAdBannerIlrd";

        /// <summary>
        /// 从 SDKComponent 获取所有 ITrackPlugin 并缓存到 m_TrackPlugins。
        /// </summary>
        private void CacheTrackPlugins()
        {
            var sdkComponent = FrameworkComponentsGroup.GetComponent<SDKComponent>();
            if (sdkComponent == null)
            {
                Log.Warning(LogTag.AD, "广告打点插件缓存失败：SDKComponent 尚未就绪。");
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
        /// 将自定义属性合并到目标字典；已存在的键会跳过，不覆盖基础属性。
        /// 自定义属性参数为 null 时静默跳过。
        /// </summary>
        /// <param name="target">目标属性字典。</param>
        /// <param name="custom">自定义属性字典，可为 null。</param>
        private static void MergeCustom(Dictionary<string, object> target, Dictionary<string, object> custom)
        {
            MergeProps(target, custom);
        }

        /// <summary>
        /// 将附加属性合并到目标字典；已存在的键会跳过，不覆盖基础属性。
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
        /// 当前渠道全部 Banner 共用的 ILRD 累计入口，不按广告位拆分次数和收益。
        /// 后台回调先排入主线程，读写使用初始化时缓存的 IFileFragmentManager。
        /// </summary>
        /// <param name="revenue">本次 Banner 收益回调的收入值。</param>
        /// <param name="trackAction">达到累计间隔后的上传回调；返回 true 表示上传已派发。</param>
        protected void TrackBannerIlrdAggregated(double revenue, Func<decimal, bool> trackAction)
        {
            TrackBannerIlrdState($"Nova.Ad.BannerIlrd.{Name}.Count", $"Nova.Ad.BannerIlrd.{Name}.Revenue", revenue, trackAction);
        }

        /// <summary>
        /// 兼容既有渠道的按广告位累计入口；MAX 使用不带 placementId 的渠道汇总重载。
        /// </summary>
        /// <param name="placementId">Banner 广告位唯一标识。</param>
        /// <param name="revenue">本次 Banner 收益回调的收入值。</param>
        /// <param name="trackAction">达到累计间隔后执行的渠道侧 ad_ilrd 上传回调；返回 true 表示上传已派发。</param>
        protected void TrackBannerIlrdAggregated(string placementId, double revenue, Func<decimal, bool> trackAction)
        {
            TrackBannerIlrdState(BuildBannerIlrdCountKey(placementId), BuildBannerIlrdRevenueKey(placementId), revenue, trackAction);
        }

        /// <summary>
        /// 在主线程更新指定累计条目，达到间隔后派发并清理，未派发时保存待重试批次。
        /// </summary>
        /// <param name="countKey">累计次数条目名。</param>
        /// <param name="revenueKey">累计收益条目名。</param>
        /// <param name="revenue">本次收益。</param>
        /// <param name="trackAction">累计收益上传回调。</param>
        private void TrackBannerIlrdState(string countKey, string revenueKey, double revenue, Func<decimal, bool> trackAction)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                PostAdCallbackToMainThread(() => TrackBannerIlrdState(countKey, revenueKey, revenue, trackAction));
                return;
            }

            IFileFragmentManager persistManager = m_BannerIlrdPersistManager;
            if (persistManager == null)
                throw new InvalidOperationException("广告累计存储尚未缓存，请先初始化广告渠道，并在存档读写前完成 Persist.LoadAsync()。");

            if (BannerIlrdInterval <= 0)
            {
                ClearBannerIlrdState(persistManager, countKey, revenueKey);
                return;
            }

            int count = persistManager.GetInt(c_BannerIlrdPersistClassify, countKey, 0) + 1;
            decimal totalRevenue = ReadBannerIlrdRevenue(persistManager, revenueKey) + ToBannerIlrdRevenueDecimal(revenue);

            if (count < BannerIlrdInterval)
            {
                SaveBannerIlrdState(persistManager, countKey, revenueKey, count, totalRevenue);
                return;
            }

            if (trackAction != null && trackAction(totalRevenue))
            {
                ClearBannerIlrdState(persistManager, countKey, revenueKey);
            }
            else
            {
                SaveBannerIlrdState(persistManager, countKey, revenueKey, count, totalRevenue);
            }
        }

        /// <summary>
        /// 将渠道 SDK 回调中的 double 类型收益转换为 decimal 类型，避免累计存档时丢失小数精度。
        /// </summary>
        /// <param name="revenue">渠道 SDK 回调中的收益值。</param>
        /// <returns>用于累计和存档的 decimal 类型收益值。</returns>
        private static decimal ToBannerIlrdRevenueDecimal(double revenue)
            => (decimal)revenue;

        /// <summary>
        /// 将 Banner ILRD 累计收益格式化为稳定文本，写入文件片段存档。
        /// </summary>
        /// <param name="revenue">需要存档的累计收益。</param>
        /// <returns>使用固定区域性的收益文本。</returns>
        private static string FormatBannerIlrdRevenue(decimal revenue)
            => revenue.ToString("0.#############################", CultureInfo.InvariantCulture);

        /// <summary>
        /// 从文件片段读取 Banner ILRD 累计收益；缺失或文本格式无效时按 0 处理。
        /// </summary>
        /// <param name="persistManager">已就绪的文件片段管理器。</param>
        /// <param name="key">累计收益存档键。</param>
        /// <returns>已累计但尚未上传的 Banner ILRD 收益。</returns>
        private static decimal ReadBannerIlrdRevenue(IFileFragmentManager persistManager, string key)
        {
            string text = persistManager.GetString(c_BannerIlrdPersistClassify, key, "0");
            return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal revenue)
                ? revenue
                : 0m;
        }

        /// <summary>
        /// 保存 Banner ILRD 当前累计次数和累计收益，防止游戏重启后丢失未满间隔的批次。
        /// </summary>
        /// <param name="persistManager">已就绪的文件片段管理器。</param>
        /// <param name="countKey">累计次数存档键。</param>
        /// <param name="revenueKey">累计收益存档键。</param>
        /// <param name="count">当前已累计的 Banner 收益回调次数。</param>
        /// <param name="revenue">当前已累计的 Banner 收益。</param>
        private static void SaveBannerIlrdState(IFileFragmentManager persistManager, string countKey, string revenueKey, int count, decimal revenue)
        {
            persistManager.SetInt(c_BannerIlrdPersistClassify, countKey, count);
            persistManager.SetString(c_BannerIlrdPersistClassify, revenueKey, FormatBannerIlrdRevenue(revenue));
            if (!persistManager.Save(c_BannerIlrdPersistClassify))
                throw new InvalidOperationException("Banner ILRD 文件片段保存失败。");
        }

        /// <summary>
        /// 清除当前批次的累计条目，并立即处理文件片段待删除队列，避免旧批次被备份恢复。
        /// </summary>
        /// <param name="persistManager">已就绪的文件片段管理器。</param>
        /// <param name="countKey">累计次数条目名。</param>
        /// <param name="revenueKey">累计收益条目名。</param>
        private static void ClearBannerIlrdState(IFileFragmentManager persistManager, string countKey, string revenueKey)
        {
            // 先检查两个条目，避免删除最后一个条目后再次查询而从尚未删除的文件中重新加载旧数据。
            bool hasCount = persistManager.HasItem(c_BannerIlrdPersistClassify, countKey);
            bool hasRevenue = persistManager.HasItem(c_BannerIlrdPersistClassify, revenueKey);
            if (hasCount) persistManager.RemoveItem(c_BannerIlrdPersistClassify, countKey);
            if (hasRevenue) persistManager.RemoveItem(c_BannerIlrdPersistClassify, revenueKey);

            // Save(classify) 不处理空分类的待删文件；必须使用 Save() 清理正式文件及备份。
            if (!persistManager.Save())
                throw new InvalidOperationException("Banner ILRD 清理后的文件片段保存未全部完成，请检查 Persist 日志。");
        }

        /// <summary>
        /// 构建 Banner ILRD 累计次数存档键；按渠道名和广告位隔离不同渠道、不同广告位的批次。
        /// </summary>
        /// <param name="placementId">Banner 广告位唯一标识。</param>
        /// <returns>NovaAdBannerIlrd 分类中保存累计次数的条目名。</returns>
        private string BuildBannerIlrdCountKey(string placementId)
            => $"Nova.Ad.BannerIlrd.{Name}.{placementId ?? string.Empty}.Count";

        /// <summary>
        /// 构建 Banner ILRD 累计收益存档键；按渠道名和广告位隔离不同渠道、不同广告位的批次。
        /// </summary>
        /// <param name="placementId">Banner 广告位唯一标识。</param>
        /// <returns>NovaAdBannerIlrd 分类中保存累计收益的条目名。</returns>
        private string BuildBannerIlrdRevenueKey(string placementId)
            => $"Nova.Ad.BannerIlrd.{Name}.{placementId ?? string.Empty}.Revenue";

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
        /// 触发 OnAdRevenuePaid 事件，并推进收益状态。
        /// 执行顺序：先状态机推进（MarkRevenue）→ 再事件扇出 → 再打点。
        /// 派生类在广告展示产生收入的 SDK 回调中调用；只有 OnAdRevenuePaid 事件会切回主线程。
        /// </summary>
        /// <param name="e">广告变现埋点事件载荷。</param>
        protected void RaiseRevenue(AdEvent e)
        {
            MarkRevenue(e.PlacementId, e.Revenue);
            var revenueUnit = FindAdUnit(e.PlacementId);
            if (revenueUnit != null) revenueUnit.RewardGranted = true;
            PostAdCallbackToMainThread(() => OnAdRevenuePaid?.Invoke(e));
        }

        /// <summary>
        /// 触发 OnAdLoaded 事件，并驱动打点扇出和批次完成通知。
        /// 执行顺序：先状态机推进（MarkLoaded）→ 再批次通知 → 再打点（合并 RequestCustomProps）→ 再把事件扇出排入主线程。
        /// 派生类在广告加载成功的 SDK 回调中调用；只有 OnAdLoaded 事件会切回主线程。
        /// </summary>
        /// <param name="e">加载成功事件载荷。</param>
        protected void RaiseAdLoaded(AdLoadResult e)
        {
            e.Success = true;
            MarkLoaded(e.PlacementId, e.Revenue);
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
            PostAdCallbackToMainThread(() => OnAdLoaded?.Invoke(e));
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
        /// 触发 OnAdLoadFailed 事件，并驱动打点扇出和批次失败通知。
        /// 执行顺序：先状态机推进（MarkLoadFailed，含自动重试调度）→ 再批次通知 → 再打点（合并 RequestCustomProps）→ 再把事件扇出排入主线程。
        /// 派生类在广告加载失败的 SDK 回调中调用；只有 OnAdLoadFailed 事件会切回主线程。
        /// </summary>
        /// <param name="e">加载失败事件载荷。</param>
        protected void RaiseAdLoadFailed(AdLoadResult e)
        {
            e.Success = false;
            var unit = FindAdUnit(e.PlacementId);
            MarkLoadFailed(e.PlacementId, e.ErrorMessage);
            NotifyBatchFailed(e);
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
            PostAdCallbackToMainThread(() => OnAdLoadFailed?.Invoke(e));
        }

        /// <summary>
        /// 遍历 m_PendingBatches，从含有该广告位的批次中移除对应 ID。
        /// solar 重试语义下不存在失败终止态，每次 RaiseAdLoadFailed 都通知调用方"本批已失败"，
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
            TrackEvent("nova_ad_init", new Dictionary<string, object>
            {
                { "nova_success", success },
                { "nova_ad_channel", Name },
            });
            PostAdCallbackToMainThread(() => OnInitResult?.Invoke(success));
        }

        /// <summary>
        /// 上报 nova_ad_show_result 成功打点，再将 OnShowCompleted 事件排入主线程。
        /// 展示成功不是展示生命周期结束点，不推进 MarkShown，避免 displayed 与 hidden 各触发一次续杯。
        /// </summary>
        /// <param name="result">广告展示成功结果。</param>
        protected void RaiseShowCompleted(AdResult result)
            => RaiseShowCompleted(result, null);

        /// <summary>
        /// 上报带渠道附加属性的展示成功结果，再将 OnShowCompleted 事件排入主线程；不结束展示生命周期。
        /// </summary>
        /// <param name="result">广告展示成功结果。</param>
        /// <param name="extraProps">本次展示的渠道关联属性，可为 null；不覆盖基础字段或业务属性。</param>
        protected void RaiseShowCompleted(AdResult result, Dictionary<string, object> extraProps)
        {
            TrackAdShowResult(result, true, FindAdUnit(result.PlacementId)?.ShowCustomProps, extraProps);
            PostAdCallbackToMainThread(() => OnShowCompleted?.Invoke(result));
        }

        /// <summary>
        /// 触发 OnShowFailed 事件，并上报 nova_ad_show_result（失败）打点。
        /// 执行顺序：先保存展示属性，再推进状态机并打点，最后将业务事件排入主线程。
        /// </summary>
        /// <param name="result">广告播放失败结果。</param>
        protected void RaiseShowFailed(AdResult result)
            => RaiseShowFailed(result, null);

        /// <summary>
        /// 保存本次展示属性，推进失败收尾与续杯，再上报带渠道关联属性的失败结果并派发业务事件。
        /// </summary>
        /// <param name="result">广告展示失败结果。</param>
        /// <param name="extraProps">清理渠道上下文前保存的关联属性，可为 null；不覆盖基础字段或业务属性。</param>
        protected void RaiseShowFailed(AdResult result, Dictionary<string, object> extraProps)
        {
            var unit = FindAdUnit(result.PlacementId);
            var showCustomProps = unit?.ShowCustomProps;
            MarkShown(result.PlacementId);
            TrackAdShowResult(result, false, showCustomProps, extraProps);
            PostAdCallbackToMainThread(() => OnShowFailed?.Invoke(result));
        }

        /// <summary>
        /// 统一上报展示结果：成功为 1，失败为 0；合并本次展示属性和渠道关联属性。
        /// 失败调用方须在 MarkShown 清理属性前保存 showCustomProps，避免自动续杯导致属性丢失。
        /// </summary>
        /// <param name="result">本次展示结果，提供广告格式与广告位。</param>
        /// <param name="success">是否收到展示成功回调；与关闭、奖励结果无关。</param>
        /// <param name="showCustomProps">本次展示的业务属性，可为 null。</param>
        /// <param name="extraProps">渠道附加属性，可为 null。</param>
        private void TrackAdShowResult(AdResult result, bool success, Dictionary<string, object> showCustomProps, Dictionary<string, object> extraProps)
        {
            var props = BuildBaseProps(result.Format, result.PlacementId);
            props["nova_ad_result"] = success ? 1 : 0;
            MergeCustom(props, showCustomProps);
            MergeProps(props, extraProps);
            TrackEvent("nova_ad_show_result", props);
        }

        /// <summary>
        /// 触发 OnAdClosed 事件，并在基类内部上报 nova_ad_hidden 打点。
        /// 执行顺序：保存展示属性 → MarkShown（含非 Banner 续杯）→ 关闭打点 → 将业务通知排入主线程。
        /// </summary>
        /// <param name="result">广告关闭结果。</param>
        /// <param name="rewarded">写入 nova_can_get_reward 的渠道奖励判定；不改写 result.RewardGranted。</param>
        protected void RaiseAdClosed(AdResult result, bool rewarded = false)
            => RaiseAdClosed(result, rewarded, null);

        /// <summary>
        /// 结束展示并上报带渠道附加属性的 nova_ad_hidden，再将关闭业务事件排入主线程。
        /// 先保存业务属性再触发续杯；渠道属性不覆盖基础字段和业务属性。
        /// </summary>
        /// <param name="result">广告关闭结果。</param>
        /// <param name="rewarded">写入 nova_can_get_reward 的渠道奖励判定；MAX 插屏为 true，激励取官方奖励状态，开屏为 false。</param>
        /// <param name="extraProps">关闭前取得的渠道关联属性，可为 null。</param>
        protected void RaiseAdClosed(AdResult result, bool rewarded, Dictionary<string, object> extraProps)
        {
            var closedUnit = FindAdUnit(result.PlacementId);
            var showCustomProps = closedUnit?.ShowCustomProps;
            MarkShown(result.PlacementId);
            var props = BuildBaseProps(result.Format, result.PlacementId);
            props["nova_can_get_reward"] = rewarded;
            MergeCustom(props, showCustomProps);
            MergeCustom(props, extraProps);
            TrackEvent("nova_ad_hidden", props);
            PostAdCallbackToMainThread(() => OnAdClosed?.Invoke(result));
        }

        /// <summary>
        /// 派生类在实际调用渠道展示接口前调用，上报 nova_ad_show 展示尝试事件。
        /// 基类自动从对应 AdUnit 取 ShowCustomProps 合并；派生侧调用面无需传自定义属性。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识。</param>
        protected void TrackAdShow(AdFormat format, string placementId)
            => TrackAdShow(format, placementId, null);

        /// <summary>
        /// 派生类在实际调用渠道展示接口前调用，上报带渠道附加属性的 nova_ad_show 展示尝试事件。
        /// 基类先合并对应 AdUnit 的 ShowCustomProps，再合并渠道附加属性；已有键不会被覆盖。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识。</param>
        /// <param name="extraProps">渠道侧附加属性，可为 null。</param>
        protected void TrackAdShow(AdFormat format, string placementId, Dictionary<string, object> extraProps)
        {
            var unit = FindAdUnit(placementId);
            var props = BuildBaseProps(format, placementId);
            MergeCustom(props, unit?.ShowCustomProps);
            MergeProps(props, extraProps);
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
