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
        /// 缓存为空时静默跳过；单插件异常隔离，不能截断广告组或业务批次。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="props">事件属性字典。</param>
        private void TrackEvent(string eventName, Dictionary<string, object> props)
        {
            var plugins = m_TrackPlugins;
            if (m_IsDisposed || plugins == null || plugins.Count == 0) return;
            for (int i = 0; i < plugins.Count; i++)
            {
                try { plugins[i].TrackEvent(eventName, props); }
                catch (Exception ex) { Log.Error(LogTag.AD, $"[{Name}] 广告事件 {eventName} 上报失败：{ex}"); }
            }
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
            if (m_IsDisposed) return;
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
        /// 收益状态与业务事件在主线程更新，纯收益打点仍由即时入口在原厂线程执行。
        /// </summary>
        /// <param name="e">收益载荷，将复制后排队。</param>
        protected void RaiseRevenue(AdEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var snapshot = SnapshotRevenueEvent(e);
            DispatchAdStateCallback(() =>
            {
                var unit = FindAdUnit(snapshot.PlacementId);
                if (unit != null && unit.Format == snapshot.Format)
                {
                    MarkRevenue(snapshot.PlacementId, snapshot.Revenue);
                    unit.RewardGranted = true;
                }
                OnAdRevenuePaid?.Invoke(snapshot);
            });
        }

        /// <summary>
        /// 冻结loaded载荷并进入主线程组状态门；重复或未知回调不结算业务轮。
        /// </summary>
        /// <param name="e">原厂加载成功载荷。</param>
        protected void RaiseAdLoaded(AdLoadResult e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var snapshot = SnapshotLoadResult(e);
            snapshot.Success = true;
            DispatchAdStateCallback(() => ProcessAdLoaded(snapshot));
        }

        /// <summary>
        /// 上报已接受的真实可播fill，保留现有schema和轮内自定义属性。
        /// </summary>
        /// <param name="unit">成功槽位。</param>
        /// <param name="e">原厂结果快照。</param>
        private void TrackLoadedResult(AdUnit unit, AdLoadResult e)
        {
            var props = BuildBaseProps(e.Format, e.PlacementId);
            props["nova_ad_result"] = 1;
            props["nova_ad_network"] = e.Network ?? string.Empty;
            MergeCustom(props, unit.RequestCustomProps);
            MergeCustom(props, e.CustomProps);
            TrackEvent("nova_ad_fill", props);
        }

        /// <summary>
        /// 冻结failed载荷并进入主线程组状态门；当前项失败事件不等于整轮业务失败。
        /// </summary>
        /// <param name="e">原厂加载失败载荷。</param>
        protected void RaiseAdLoadFailed(AdLoadResult e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var snapshot = SnapshotLoadResult(e);
            snapshot.Success = false;
            DispatchAdStateCallback(() => ProcessAdLoadFailed(snapshot));
        }

        /// <summary>
        /// 记录已接受的单次加载失败，保留原厂码及当前轮属性。
        /// </summary>
        /// <param name="unit">失败槽位。</param>
        /// <param name="e">失败快照。</param>
        private void TrackFailedResult(AdUnit unit, AdLoadResult e)
        {
            var props = BuildBaseProps(e.Format, e.PlacementId);
            props["nova_ad_result"] = 0;
            props["nova_ad_errorcode"] = e.ErrorCode;
            props["nova_ad_error_message"] = e.ErrorMessage ?? string.Empty;
            MergeCustom(props, unit.RequestCustomProps);
            TrackEvent("nova_ad_fill_fail", props);
        }

        /// <summary>
        /// 初始化结果打点及事件均串行进入主线程；成功门由具体SDK渠道管理。
        /// </summary>
        /// <param name="success">原厂真实初始化结果。</param>
        protected void RaiseInitResult(bool success)
        {
            DispatchAdStateCallback(() =>
            {
                TrackEvent("nova_ad_init", new Dictionary<string, object> { { "nova_success", success }, { "nova_ad_channel", Name } });
                OnInitResult?.Invoke(success);
            });
        }

        /// <summary>
        /// 保留单参数displayed入口；成功展示不消费槽位或触发续杯。
        /// </summary>
        /// <param name="result">displayed结果。</param>
        protected void RaiseShowCompleted(AdResult result) => RaiseShowCompleted(result, null);

        /// <summary>
        /// 匹配当前展示后一次派发displayed，保留原奖励字段与渠道曝光属性。
        /// </summary>
        /// <param name="result">displayed结果。</param>
        /// <param name="extraProps">可空曝光属性，复制字典结构。</param>
        protected void RaiseShowCompleted(AdResult result, Dictionary<string, object> extraProps)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var snapshot = SnapshotShowResult(result);
            var extra = SnapshotAdProps(extraProps);
            DispatchAdStateCallback(() =>
            {
                var unit = GetActiveShowUnit(snapshot);
                if (unit == null || snapshot.Format == AdFormat.Banner)
                {
                    return;
                }
                if (m_ActiveAdShow != null)
                {
                    if (m_ActiveAdShow.Displayed)
                    {
                        return;
                    }
                    m_ActiveAdShow.Displayed = true;
                }
                TrackAdShowResult(snapshot, true, unit.ShowCustomProps, extra);
                OnShowCompleted?.Invoke(snapshot);
            });
        }

        /// <summary>
        /// 保留单参数displayFailed入口，匹配终态后一次收口。
        /// </summary>
        /// <param name="result">原厂展示失败结果。</param>
        protected void RaiseShowFailed(AdResult result) => RaiseShowFailed(result, null);

        /// <summary>
        /// 先结束匹配展示占用再记录失败与普通续杯，重复终态不能再补载。
        /// </summary>
        /// <param name="result">展示失败结果。</param>
        /// <param name="extraProps">原厂上下文清理前取得的曝光属性。</param>
        protected void RaiseShowFailed(AdResult result, Dictionary<string, object> extraProps)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var snapshot = SnapshotShowResult(result);
            var extra = SnapshotAdProps(extraProps);
            DispatchAdStateCallback(() =>
            {
                var unit = FinishActiveShow(snapshot);
                if (unit == null)
                {
                    return;
                }
                var custom = unit.ShowCustomProps;
                TrackAdShowResult(snapshot, false, custom, extra);
                RefillAfterShow(unit);
                if (!m_IsDisposed) OnShowFailed?.Invoke(snapshot);
            });
        }

        /// <summary>
        /// 统一上报展示结果：成功为 1，失败为 0；合并本次展示属性和渠道关联属性。
        /// 失败调用方在消费组清理前保存showCustomProps，避免普通续杯导致属性丢失。
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
        /// 保留旧hidden入口，rewarded只写埋点不推导业务奖励字段。
        /// </summary>
        /// <param name="result">关闭结果。</param>
        /// <param name="rewarded">写入nova_can_get_reward的渠道奖励判定。</param>
        protected void RaiseAdClosed(AdResult result, bool rewarded = false) => RaiseAdClosed(result, rewarded, null);

        /// <summary>
        /// 匹配hidden一次收尾并补充所有组，展示属性在消费前保存。
        /// </summary>
        /// <param name="result">原厂关闭结果。</param>
        /// <param name="rewarded">关闭埋点奖励判定，不改result.RewardGranted。</param>
        /// <param name="extraProps">曝光关联属性，字典结构将复制。</param>
        protected void RaiseAdClosed(AdResult result, bool rewarded, Dictionary<string, object> extraProps)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var snapshot = SnapshotShowResult(result);
            var extra = SnapshotAdProps(extraProps);
            DispatchAdStateCallback(() =>
            {
                var unit = FinishActiveShow(snapshot);
                if (unit == null)
                {
                    return;
                }
                var props = BuildBaseProps(snapshot.Format, snapshot.PlacementId);
                props["nova_can_get_reward"] = rewarded;
                MergeCustom(props, unit.ShowCustomProps);
                MergeCustom(props, extra);
                TrackEvent("nova_ad_hidden", props);
                RefillAfterShow(unit);
                if (!m_IsDisposed) OnAdClosed?.Invoke(snapshot);
            });
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
            DispatchAdStateCallback(() =>
            {
                var unit = FindAdUnit(placementId);
                if (unit == null || unit.Format != format) return;
                var props = BuildBaseProps(format, placementId);
                MergeCustom(props, unit.ShowCustomProps);
                TrackEvent("nova_ad_click", props);
            });
        }
    }
}
