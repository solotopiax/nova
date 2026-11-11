/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Banner.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin Banner 广告逻辑与基类虚方法重写
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {
#if NOVA_APPLOVIN_MAX
        /// <summary>
        /// Banner 控制固定操作配置首个 ID，就绪和收益查询必须使用同一广告位。
        /// </summary>
        /// <returns>配置首个 Banner ID；未配置时返回空字符串，避免选择其他槽位。</returns>
        protected override string GetBannerControlPlacementId() => BannerPlacementId ?? string.Empty;

        /// <summary>
        /// 创建并配置 Banner 广告位。
        /// </summary>
        /// <param name="placementId">广告位 ID。</param>
        private void RequestBanner(string placementId)
        {
            if (string.IsNullOrEmpty(placementId))
            {
                return;
            }

            if (m_CreatedBannerViews.TryAdd(placementId, new object()))
            {
                MaxSdk.CreateBanner(placementId, new MaxSdkBase.AdViewConfiguration(m_BannerPosition));
                MaxSdk.SetBannerBackgroundColor(placementId, Color.white);
            }
            else if (m_BannerDesiredVisible && placementId == BannerPlacementId)
            {
                Log.Debug(LogTag.Max, $"Banner 发起原厂展示：format={AdFormat.Banner}，placement={placementId}。");
                MaxSdk.ShowBanner(placementId);
            }
        }

        /// <summary>
        /// 显示 Banner 广告；不可用或原厂调用失败时只记录警告。
        /// </summary>
        public override void ShowBanner() => TryShowBanner();

        /// <summary>
        /// 尝试显示配置首个 Banner，失败时恢复可见期望，不向业务调用方抛异常。
        /// </summary>
        /// <returns>成功发出原厂显示调用时为 true，不表示已产生曝光。</returns>
        public override bool TryShowBanner()
        {
            string placementId = BannerPlacementId;
            if (!m_IsSdkInitialized || string.IsNullOrEmpty(placementId) || !m_CreatedBannerViews.ContainsKey(placementId))
            {
                Log.Warning(LogTag.Max, "MAX Banner 尚未创建或渠道不可用，请先请求 Banner。");
                return false;
            }
            bool previousVisible = m_BannerDesiredVisible;
            m_BannerDesiredVisible = true;
            try
            {
                StartBannerAutoRefresh();
                Log.Debug(LogTag.Max, $"Banner 发起原厂展示：format={AdFormat.Banner}，placement={placementId}。");
                MaxSdk.ShowBanner(placementId);
                return true;
            }
            catch (Exception ex)
            {
                m_BannerDesiredVisible = previousVisible && m_CreatedBannerViews.ContainsKey(placementId);
                Log.Warning(LogTag.Max, $"MAX Banner 显示失败：placement={placementId}，{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 隐藏 Banner 广告。
        /// </summary>
        public override void HideBanner()
        {
            m_BannerDesiredVisible = false;
            StopBannerAutoRefresh();
            MaxSdk.HideBanner(BannerPlacementId);
        }

        /// <summary>
        /// 销毁 Banner 广告并通知状态机。
        /// </summary>
        public override void DestroyBanner()
        {
            m_BannerDesiredVisible = false;
            string placementId = BannerPlacementId;
            if (string.IsNullOrEmpty(placementId)) return;
            bool created = m_CreatedBannerViews.TryRemove(placementId, out _);
            MarkBannerHidden(placementId);
            if (created) MaxSdk.DestroyBanner(placementId);
        }

        /// <summary>
        /// 更新 Banner 停靠位置枚举。
        /// </summary>
        /// <param name="position">目标停靠位置。</param>
        public override void UpdateBannerPosition(BannerPosition position)
        {
            m_BannerPosition = ToMaxPosition(position);
            MaxSdk.UpdateBannerPosition(BannerPlacementId, m_BannerPosition);
        }

        /// <summary>
        /// 更新 Banner 屏幕坐标位置。
        /// </summary>
        /// <param name="position">目标原生坐标；Android 使用 dp，iOS 使用 points，当前直接取整传给 MAX。</param>
        public override void UpdateBannerPosition(Vector2 position)
            => MaxSdk.UpdateBannerPosition(BannerPlacementId, (int)position.x, (int)position.y);

        /// <summary>
        /// 按配置写入刷新间隔后开启 Banner 自动刷新。
        /// </summary>
        public override void StartBannerAutoRefresh()
        {
            MaxSdk.SetBannerExtraParameter(
                BannerPlacementId,
                "ad_refresh_seconds",
                m_BannerAutoRefreshIntervalSeconds.ToString());
            MaxSdk.StartBannerAutoRefresh(BannerPlacementId);
        }

        /// <summary>
        /// 停止 Banner 自动刷新。
        /// </summary>
        public override void StopBannerAutoRefresh()
        {
            MaxSdk.StopBannerAutoRefresh(BannerPlacementId);
        }

        /// <summary>
        /// 设置 Banner 宽度。
        /// </summary>
        /// <param name="width">目标原生宽度；Android 使用 dp，iOS 使用 points，当前直接取整传给 MAX。</param>
        public override void SetBannerWidth(float width)
            => MaxSdk.SetBannerWidth(BannerPlacementId, (int)width);

        /// <summary>
        /// 获取 Banner 自适应高度；传 -1 表示使用当前屏幕宽度。
        /// </summary>
        /// <param name="width">参考宽度（像素），传 -1 使用屏幕宽度。</param>
        /// <returns>Banner 自适应高度（像素）。</returns>
        public override float GetAdaptiveBannerHeight(float width = -1f)
            => MaxSdkUtils.GetAdaptiveBannerHeight(width < 0 ? Screen.width : (int)width);

        /// <summary>
        /// 设置 Banner 背景色。
        /// </summary>
        /// <param name="color">目标背景颜色。</param>
        public override void SetBannerBackgroundColor(Color color)
            => MaxSdk.SetBannerBackgroundColor(BannerPlacementId, color);

        /// <summary>
        /// 将 Nova BannerPosition 枚举映射到 MaxSdk 停靠位置。
        /// </summary>
        /// <param name="pos">Nova 定义的停靠位置枚举值。</param>
        /// <returns>对应的 MaxSdk 广告视图停靠位置。</returns>
        private static MaxSdkBase.AdViewPosition ToMaxPosition(BannerPosition pos) => pos switch
        {
            BannerPosition.Top => MaxSdkBase.AdViewPosition.TopCenter,
            BannerPosition.Bottom => MaxSdkBase.AdViewPosition.BottomCenter,
            BannerPosition.TopLeft => MaxSdkBase.AdViewPosition.TopLeft,
            BannerPosition.TopRight => MaxSdkBase.AdViewPosition.TopRight,
            BannerPosition.BottomLeft => MaxSdkBase.AdViewPosition.BottomLeft,
            BannerPosition.BottomRight => MaxSdkBase.AdViewPosition.BottomRight,
            _ => MaxSdkBase.AdViewPosition.BottomCenter,
        };

        /// <summary>
        /// 复制 Banner 加载响应后于主线程推进状态并按既有业务期望恢复可见性。
        /// </summary>
        /// <param name="adUnitId">已注册的 Banner 真实 ID。</param>
        /// <param name="info">厂商加载响应，回调返回前复制。</param>
        private void OnBannerLoaded(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"收到原厂 Banner 加载成功回调：format={AdFormat.Banner}，placement={adUnitId}，network={info?.NetworkName}，revenueUSD={FormatRevenue((decimal)GetMaxRevenue(info))}，precision={info?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.Banner, adUnitId, nameof(OnBannerLoaded))) return;
            if (!m_CreatedBannerViews.TryGetValue(adUnitId, out object view)) return;
            var snapshot = new MaxAdInfoSnapshot(info);
            var result = new AdLoadResult
            {
                Success = true,
                Format = AdFormat.Banner,
                PlacementId = adUnitId,
                Network = snapshot.NetworkName,
                Revenue = snapshot.Revenue,
                Currency = "USD",
                CustomProps = BuildMaxLoadProps(snapshot),
            };
            PostAdCallbackToMainThread(() =>
            {
                // 入队后可能销毁并重建同 ID 视图，只有原身份仍有效时才接受结果。
                if (!m_CreatedBannerViews.TryGetValue(adUnitId, out object current) || !ReferenceEquals(current, view)) return;
                RaiseAdLoaded(result);
                if (m_BannerDesiredVisible && adUnitId == BannerPlacementId && m_CreatedBannerViews.TryGetValue(adUnitId, out current) && ReferenceEquals(current, view))
                {
                    Log.Debug(LogTag.Max, $"Banner 发起原厂展示：format={AdFormat.Banner}，placement={adUnitId}。");
                    MaxSdk.ShowBanner(adUnitId);
                }
            });
        }

        /// <summary>
        /// 复制 Banner 加载错误后于主线程推进既有刷新状态。
        /// </summary>
        /// <param name="adUnitId">失败 Banner 真实 ID。</param>
        /// <param name="err">原厂错误，保留错误码和消息。</param>
        private void OnBannerLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo err)
        {
            Log.Debug(LogTag.Max, $"Banner 加载失败回调：format={AdFormat.Banner}，placement={adUnitId}，code={(err == null ? -1 : (int)err.Code)}，message={err?.Message}。");
            if (!AcceptMaxCallback(AdFormat.Banner, adUnitId, nameof(OnBannerLoadFailed))) return;
            var result = new AdLoadResult
            {
                Success = false,
                Format = AdFormat.Banner,
                PlacementId = adUnitId,
                ErrorCode = err == null ? -1 : (int)err.Code,
                ErrorMessage = err?.Message ?? "MAX 未提供 Banner 加载错误。",
            };
            PostAdCallbackToMainThread(() => RaiseAdLoadFailed(result));
        }

        /// <summary>
        /// 已注册 Banner 点击回调排入主线程上报。
        /// </summary>
        /// <param name="adUnitId">点击的 Banner 真实 ID。</param>
        /// <param name="info">厂商响应，此处不保留。</param>
        private void OnBannerClicked(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Banner 点击回调：format={AdFormat.Banner}，placement={adUnitId}，network={info?.NetworkName}。");
            if (!AcceptMaxCallback(AdFormat.Banner, adUnitId, nameof(OnBannerClicked))) return;
            PostAdCallbackToMainThread(() => TrackAdClick(AdFormat.Banner, adUnitId));
        }

        /// <summary>
        /// 每次 Banner impression 立即上报；当前渠道内全部 Banner 累计 ILRD，保留主线程文件片段持久化。
        /// </summary>
        /// <param name="adUnitId">收益 Banner 真实 ID。</param>
        /// <param name="info">厂商收益响应，立即复制后使用。</param>
        private void OnBannerRevenuePaid(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Banner 收益回调：format={AdFormat.Banner}，placement={adUnitId}，network={info?.NetworkName}，revenueUSD={FormatRevenue((decimal)GetMaxRevenue(info))}，precision={info?.RevenuePrecision}。");
            if (!AcceptMaxCallback(AdFormat.Banner, adUnitId, nameof(OnBannerRevenuePaid))) return;
            var snapshot = new MaxAdInfoSnapshot(info);
            RaiseRevenueImmediately(new AdEvent
            {
                Format = AdFormat.Banner,
                PlacementId = adUnitId,
                Network = snapshot.NetworkName,
                Revenue = snapshot.Revenue,
                Currency = "USD",
                Precision = snapshot.RevenuePrecision,
            }, () =>
            {
                TrackMaxAdImpression(AdFormat.Banner, adUnitId, snapshot);
                TrackMaxBannerIlrdAggregated(adUnitId, snapshot);
            });
        }

        /// <summary>
        /// Banner 折叠只影响已注册视图，于主线程通知本地状态，不触发全屏续杯。
        /// </summary>
        /// <param name="adUnitId">折叠的 Banner 真实 ID。</param>
        /// <param name="info">厂商响应，此处不保留。</param>
        private void OnBannerCollapsed(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"Banner 折叠/隐藏回调：format={AdFormat.Banner}，placement={adUnitId}，network={info?.NetworkName}。");
            if (!AcceptMaxCallback(AdFormat.Banner, adUnitId, nameof(OnBannerCollapsed))) return;
            PostAdCallbackToMainThread(() => MarkBannerHidden(adUnitId));
        }
#endif
    }
}
