/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdMobAdPlugin.Banner.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   独立 AdMob Banner 原厂对象与已支持的展示/位置控制
 ***************************************************************/

#if NOVA_GOOGLE_MOBILE_ADS
using System;
using GoogleMobileAds.Api;
using AdFormat = NovaFramework.Runtime.AdFormat;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.AdMobAdPlugin.Runtime
{
    public sealed partial class AdMobAdPlugin
    {
        /// <summary>创建固定标准 Banner 并保持隐藏直到业务显式显示；保存所有准确事件委托。</summary>
        /// <param name="session">本次 Banner 广告位记录。</param>
        private void CreateBanner(AdSession session)
        {
            var banner = m_BannerCustomPosition.HasValue
                ? new BannerView(session.PlacementId, AdSize.Banner, Mathf.RoundToInt(m_BannerCustomPosition.Value.x), Mathf.RoundToInt(m_BannerCustomPosition.Value.y))
                : new BannerView(session.PlacementId, AdSize.Banner, MapPosition(m_BannerPosition));
            session.Banner = banner;
            session.Destroy = banner.Destroy;
            Action loaded = () => PostAd(session, () => Loaded(session));
            Action<LoadAdError> failed = error => PostAd(session, () =>
            {
                session.Loaded = false;
                RaiseAdLoadFailed(new AdLoadResult { Format = AdFormat.Banner, PlacementId = session.PlacementId, ErrorCode = error?.GetCode() ?? -1, ErrorMessage = error?.GetMessage() ?? "AdMob Banner 加载失败。" });
            });
            Action clicked = () => PostAd(session, () => TrackAdClick(AdFormat.Banner, session.PlacementId));
            Action<AdValue> paid = value => PostAd(session, () => Paid(session, value));
            banner.OnBannerAdLoaded += loaded;
            banner.OnBannerAdLoadFailed += failed;
            banner.OnAdClicked += clicked;
            banner.OnAdPaid += paid;
            session.Unsubscribe.Add(() => banner.OnBannerAdLoaded -= loaded);
            session.Unsubscribe.Add(() => banner.OnBannerAdLoadFailed -= failed);
            session.Unsubscribe.Add(() => banner.OnAdClicked -= clicked);
            session.Unsubscribe.Add(() => banner.OnAdPaid -= paid);
            banner.Hide();
            banner.LoadAd(new AdRequest());
        }

        /// <summary>取得配置首个 Banner 的当前有效对象，其余 ID 的加载不改变控制目标。</summary>
        /// <returns>当前首 ID 的原厂 Banner，未创建或失效为 null。</returns>
        private BannerView GetBanner()
            => m_BannerPlacementId != null && m_Ads.TryGetValue(m_BannerPlacementId, out var session) && IsCurrent(session) ? session.Banner : null;

        /// <summary>记录业务可见期望并显示首个 Banner；加载恢复后保持同一期望。</summary>
        public override void ShowBanner()
        {
            m_BannerDesiredVisible = true;
            GetBanner()?.Show();
        }

        /// <summary>隐藏首个 Banner，保留原厂对象与自动加载状态，不将可用广告位错误作废。</summary>
        public override void HideBanner()
        {
            m_BannerDesiredVisible = false;
            GetBanner()?.Hide();
        }

        /// <summary>准确注销并销毁首个 Banner；槽位回 Idle，之后需再次 RequestAsync 创建。</summary>
        public override void DestroyBanner()
        {
            m_BannerDesiredVisible = false;
            if (m_BannerPlacementId != null && m_Ads.TryGetValue(m_BannerPlacementId, out var session)) ReleaseAd(session);
            if (m_BannerPlacementId != null) MarkBannerHidden(m_BannerPlacementId);
        }

        /// <summary>映射既有停靠枚举到官方 AdPosition，未知枚举拒绝。</summary>
        /// <param name="position">既有 Banner 停靠位置。</param>
        /// <returns>官方原厂停靠常量。</returns>
        private static AdPosition MapPosition(BannerPosition position)
            => position switch
            {
                BannerPosition.Top => AdPosition.Top,
                BannerPosition.Bottom => AdPosition.Bottom,
                BannerPosition.TopLeft => AdPosition.TopLeft,
                BannerPosition.TopRight => AdPosition.TopRight,
                BannerPosition.BottomLeft => AdPosition.BottomLeft,
                BannerPosition.BottomRight => AdPosition.BottomRight,
                _ => throw new ArgumentOutOfRangeException(nameof(position)),
            };

        /// <summary>设置首个 Banner 停靠位置，未创建时保存位置供下次创建。</summary>
        /// <param name="position">停靠位置。</param>
        public override void UpdateBannerPosition(BannerPosition position)
        {
            AdPosition mapped = MapPosition(position);
            m_BannerPosition = position;
            m_BannerCustomPosition = null;
            GetBanner()?.SetPosition(mapped);
        }

        /// <summary>逻辑像素坐标直接传官方整数位置；保持既有接口单位，不换算为 AdSize 的 dp。</summary>
        /// <param name="position">Banner 位置逻辑像素。</param>
        public override void UpdateBannerPosition(Vector2 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.x) || float.IsInfinity(position.y))
                throw new ArgumentOutOfRangeException(nameof(position));
            m_BannerCustomPosition = position;
            GetBanner()?.SetPosition(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
        }
    }
}
#endif
