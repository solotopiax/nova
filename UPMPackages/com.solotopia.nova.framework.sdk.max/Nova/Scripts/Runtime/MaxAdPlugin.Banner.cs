/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.Banner.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   MaxAdPlugin Banner 广告逻辑与基类虚方法重写
 ***************************************************************/

using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    public sealed partial class MaxAdPlugin
    {
#if NOVA_APPLOVIN_MAX
        /// <summary>
        /// 创建并配置 Banner 广告位。
        /// </summary>
        /// <param name="placementId">广告位 ID。</param>
        private void RequestBanner(string placementId)
        {
            if (string.IsNullOrEmpty(placementId))
            {
                Log.Debug(LogTag.Max, "MAX Banner 请求被跳过：广告位为空。");
                return;
            }

            if (m_CreatedBannerPlacementIds.Add(placementId))
            {
                Log.Debug(LogTag.Max, $"MAX Banner 创建请求：placement={placementId}，position={m_BannerPosition}。");
                MaxSdk.CreateBanner(placementId, new MaxSdkBase.AdViewConfiguration(m_BannerPosition));
                MaxSdk.SetBannerBackgroundColor(placementId, Color.white);
            }
            else if (m_BannerDesiredVisible)
            {
                Log.Debug(LogTag.Max, $"MAX Banner 已创建，按业务期望恢复显示：placement={placementId}。");
                MaxSdk.ShowBanner(placementId);
            }
            else
            {
                Log.Debug(LogTag.Max, $"MAX Banner 已创建，跳过重复创建：placement={placementId}，当前不要求显示。");
            }
        }

        /// <summary>
        /// 显示 Banner 广告。
        /// </summary>
        public override void ShowBanner()
        {
            Log.Debug(LogTag.Max, $"MAX Banner 显示请求：placement={BannerPlacementId}。");
            m_BannerDesiredVisible = true;
            StartBannerAutoRefresh();
            MaxSdk.ShowBanner(BannerPlacementId);
        }

        /// <summary>
        /// 隐藏 Banner 广告。
        /// </summary>
        public override void HideBanner()
        {
            Log.Debug(LogTag.Max, $"MAX Banner 隐藏请求：placement={BannerPlacementId}。");
            m_BannerDesiredVisible = false;
            StopBannerAutoRefresh();
            MaxSdk.HideBanner(BannerPlacementId);
        }

        /// <summary>
        /// 销毁 Banner 广告并通知状态机。
        /// </summary>
        public override void DestroyBanner()
        {
            Log.Debug(LogTag.Max, $"MAX Banner 销毁请求：placement={BannerPlacementId}。");
            m_BannerDesiredVisible = false;
            MaxSdk.DestroyBanner(BannerPlacementId);
            m_CreatedBannerPlacementIds.Remove(BannerPlacementId);
            MarkBannerHidden(BannerPlacementId);
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
        /// <param name="position">目标屏幕坐标（像素）。</param>
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
            => MaxSdk.StopBannerAutoRefresh(BannerPlacementId);

        /// <summary>
        /// 设置 Banner 宽度。
        /// </summary>
        /// <param name="width">目标宽度（像素）。</param>
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
        /// Banner 加载成功回调，触发 RaiseAdLoaded。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnBannerLoaded(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"MAX Banner 加载成功：placement={adUnitId}，network={info.NetworkName}，期望显示={m_BannerDesiredVisible}。");
            RaiseAdLoaded(new AdLoadResult
            {
                Success = true,
                Format = AdFormat.Banner,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
                CustomProps = BuildMaxLoadProps(info),
            });

            if (m_BannerDesiredVisible)
            {
                Log.Debug(LogTag.Max, $"MAX Banner 加载成功后恢复显示：placement={adUnitId}。");
                PostAdCallbackToMainThread(() => MaxSdk.ShowBanner(adUnitId));
            }
        }

        /// <summary>
        /// Banner 加载失败回调，触发 RaiseAdLoadFailed。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="err">错误信息。</param>
        private void OnBannerLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo err)
        {
            Log.Debug(LogTag.Max, $"MAX Banner 加载失败：placement={adUnitId}，code={(int)err.Code}，message={err.Message}。");
            RaiseAdLoadFailed(new AdLoadResult
            {
                Success = false,
                Format = AdFormat.Banner,
                PlacementId = adUnitId,
                ErrorCode = (int)err.Code,
                ErrorMessage = err.Message,
            });
        }

        /// <summary>
        /// Banner 点击回调，上报点击打点。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnBannerClicked(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"MAX Banner 点击回调：placement={adUnitId}，network={info.NetworkName}。");
            TrackAdClick(AdFormat.Banner, adUnitId);
        }

        /// <summary>
        /// Banner 收益回调，触发 RaiseRevenue。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnBannerRevenuePaid(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"MAX Banner 收益回调：placement={adUnitId}，network={info.NetworkName}，revenue={FormatRevenue((decimal)info.Revenue)}，precision={info.RevenuePrecision}。");
            RaiseRevenueImmediately(new AdEvent
            {
                Format = AdFormat.Banner,
                PlacementId = adUnitId,
                Network = info.NetworkName,
                Revenue = info.Revenue,
                Currency = "USD",
                Precision = info.RevenuePrecision,
            }, () =>
            {
                TrackMaxAdImpression(AdFormat.Banner, adUnitId, info);
                TrackMaxBannerIlrdAggregated(adUnitId, info);
            });
        }

        /// <summary>
        /// Banner 折叠/隐藏回调，通知状态机。
        /// </summary>
        /// <param name="adUnitId">广告位 ID。</param>
        /// <param name="info">广告信息。</param>
        private void OnBannerCollapsed(string adUnitId, MaxSdkBase.AdInfo info)
        {
            Log.Debug(LogTag.Max, $"MAX Banner 折叠/隐藏回调：placement={adUnitId}，network={info.NetworkName}。");
            MarkBannerHidden(adUnitId);
        }
#endif
    }
}
