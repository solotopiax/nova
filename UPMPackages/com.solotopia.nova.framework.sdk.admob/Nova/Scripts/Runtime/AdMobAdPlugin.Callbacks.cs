/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdMobAdPlugin.Callbacks.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   Google 官方四格式对象的准确回调绑定与释放
 ***************************************************************/

#if NOVA_GOOGLE_MOBILE_ADS
using System;
using GoogleMobileAds.Api;
using AdFormat = NovaFramework.Runtime.AdFormat;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.AdMobAdPlugin.Runtime
{
    public sealed partial class AdMobAdPlugin
    {
        /// <summary>原厂加载不可撤销；即使渠道已释放，也切主线程销毁晚到对象，不进入已停用的基类队列。</summary>
        /// <param name="session">加载时捕获的记录。</param>
        /// <param name="destroy">晚到原厂对象的销毁操作；空对象可为 null。</param>
        /// <param name="accept">对象有效时绑定准确事件与加载结果。</param>
        /// <returns>接受或销毁完成任务。</returns>
        private async UniTaskVoid AcceptNativeAd(AdSession session, Action destroy, Action accept)
        {
            await UniTask.SwitchToMainThread();
            if (!IsCurrent(session))
            {
                try { destroy?.Invoke(); }
                catch (Exception ex) { Log.Warning(LogTag.AD, $"AdMob 晚到对象销毁失败：{ex.Message}"); }
                return;
            }
            try { accept(); }
            catch (Exception ex)
            {
                if (session.Destroy == null) session.Destroy = destroy;
                ReleaseAd(session);
                RaiseAdLoadFailed(new AdLoadResult { Format = session.Format, PlacementId = session.PlacementId, ErrorCode = -11, ErrorMessage = ex.Message });
            }
        }

        /// <summary>接收激励加载对象；晚到失效对象也必须调用原厂 Destroy，避免取消后原生资源泄漏。</summary>
        /// <param name="session">本次加载身份。</param>
        /// <param name="ad">官方激励对象，失败可为 null。</param>
        /// <param name="error">官方加载错误，成功为 null。</param>
        private void AcceptRewarded(AdSession session, RewardedAd ad, LoadAdError error)
            => AcceptNativeAd(session, ad == null ? null : (Action)ad.Destroy, () =>
            {
                if (error != null || ad == null) { ad?.Destroy(); FailLoad(session, error); return; }
                session.Destroy = ad.Destroy;
                session.Ready = ad.CanShowAd;
                session.Show = ad.Show;
                Action opened = () => PostAd(session, () => Opened(session));
                Action closed = () => PostAd(session, () => Closed(session));
                Action<AdError> failed = e => PostAd(session, () => FailedShow(session, e));
                Action clicked = () => PostAd(session, () => TrackAdClick(session.Format, session.PlacementId));
                Action<AdValue> paid = value => PostAd(session, () => Paid(session, value));
                ad.OnAdFullScreenContentOpened += opened;
                ad.OnAdFullScreenContentClosed += closed;
                ad.OnAdFullScreenContentFailed += failed;
                ad.OnAdClicked += clicked;
                ad.OnAdPaid += paid;
                session.Unsubscribe.Add(() => ad.OnAdFullScreenContentOpened -= opened);
                session.Unsubscribe.Add(() => ad.OnAdFullScreenContentClosed -= closed);
                session.Unsubscribe.Add(() => ad.OnAdFullScreenContentFailed -= failed);
                session.Unsubscribe.Add(() => ad.OnAdClicked -= clicked);
                session.Unsubscribe.Add(() => ad.OnAdPaid -= paid);
                Loaded(session);
            }).Forget();

        /// <summary>接收插屏加载对象，并准确保存每个原厂委托供释放；失效对象立即销毁。</summary>
        /// <param name="session">本次加载身份。</param>
        /// <param name="ad">官方插屏对象。</param>
        /// <param name="error">官方加载错误。</param>
        private void AcceptInterstitial(AdSession session, InterstitialAd ad, LoadAdError error)
            => AcceptNativeAd(session, ad == null ? null : (Action)ad.Destroy, () =>
            {
                if (error != null || ad == null) { ad?.Destroy(); FailLoad(session, error); return; }
                session.Destroy = ad.Destroy;
                session.Ready = ad.CanShowAd;
                session.Show = _ => ad.Show();
                Action opened = () => PostAd(session, () => Opened(session));
                Action closed = () => PostAd(session, () => Closed(session));
                Action<AdError> failed = e => PostAd(session, () => FailedShow(session, e));
                Action clicked = () => PostAd(session, () => TrackAdClick(session.Format, session.PlacementId));
                Action<AdValue> paid = value => PostAd(session, () => Paid(session, value));
                ad.OnAdFullScreenContentOpened += opened;
                ad.OnAdFullScreenContentClosed += closed;
                ad.OnAdFullScreenContentFailed += failed;
                ad.OnAdClicked += clicked;
                ad.OnAdPaid += paid;
                session.Unsubscribe.Add(() => ad.OnAdFullScreenContentOpened -= opened);
                session.Unsubscribe.Add(() => ad.OnAdFullScreenContentClosed -= closed);
                session.Unsubscribe.Add(() => ad.OnAdFullScreenContentFailed -= failed);
                session.Unsubscribe.Add(() => ad.OnAdClicked -= clicked);
                session.Unsubscribe.Add(() => ad.OnAdPaid -= paid);
                Loaded(session);
            }).Forget();

        /// <summary>接收开屏加载对象，绑定当前对象事件；四小时过期检查沿用渠道统一就绪入口。</summary>
        /// <param name="session">本次加载身份。</param>
        /// <param name="ad">官方开屏对象。</param>
        /// <param name="error">官方加载错误。</param>
        private void AcceptAppOpen(AdSession session, AppOpenAd ad, LoadAdError error)
            => AcceptNativeAd(session, ad == null ? null : (Action)ad.Destroy, () =>
            {
                if (error != null || ad == null) { ad?.Destroy(); FailLoad(session, error); return; }
                session.Destroy = ad.Destroy;
                session.Ready = ad.CanShowAd;
                session.Show = _ => ad.Show();
                Action opened = () => PostAd(session, () => Opened(session));
                Action closed = () => PostAd(session, () => Closed(session));
                Action<AdError> failed = e => PostAd(session, () => FailedShow(session, e));
                Action clicked = () => PostAd(session, () => TrackAdClick(session.Format, session.PlacementId));
                Action<AdValue> paid = value => PostAd(session, () => Paid(session, value));
                ad.OnAdFullScreenContentOpened += opened;
                ad.OnAdFullScreenContentClosed += closed;
                ad.OnAdFullScreenContentFailed += failed;
                ad.OnAdClicked += clicked;
                ad.OnAdPaid += paid;
                session.Unsubscribe.Add(() => ad.OnAdFullScreenContentOpened -= opened);
                session.Unsubscribe.Add(() => ad.OnAdFullScreenContentClosed -= closed);
                session.Unsubscribe.Add(() => ad.OnAdFullScreenContentFailed -= failed);
                session.Unsubscribe.Add(() => ad.OnAdClicked -= clicked);
                session.Unsubscribe.Add(() => ad.OnAdPaid -= paid);
                Loaded(session);
            }).Forget();

        /// <summary>标记加载并通过基类完成全部匹配请求；加载阶段没有收益预测时保持零。</summary>
        /// <param name="session">当前已绑定原厂对象。</param>
        private void Loaded(AdSession session)
        {
            session.Loaded = true;
            session.LoadedAtUtc = DateTime.UtcNow;
            RaiseAdLoaded(new AdLoadResult { Format = session.Format, PlacementId = session.PlacementId, Network = "AdMob", Currency = "USD" });
            if (session.Banner != null)
            {
                if (m_BannerDesiredVisible && session.PlacementId == m_BannerPlacementId) session.Banner.Show();
                else session.Banner.Hide();
            }
        }

        /// <summary>准确保留官方错误码并进入基类加载失败/重试链。</summary>
        /// <param name="session">当前加载身份。</param>
        /// <param name="error">官方错误或空对象失败。</param>
        private void FailLoad(AdSession session, LoadAdError error)
        {
            ReleaseAd(session);
            RaiseAdLoadFailed(new AdLoadResult { Format = session.Format, PlacementId = session.PlacementId, ErrorCode = error?.GetCode() ?? -1, ErrorMessage = error?.GetMessage() ?? "AdMob 未返回广告对象。" });
        }

        /// <summary>官方全屏已打开才发布展示成功；不提前发奖励或重复发展示尝试。</summary>
        /// <param name="session">当前展示对象。</param>
        private void Opened(AdSession session)
        {
            if (session.Showing == null) return;
            RaiseShowCompleted(new AdResult { Success = true, Format = session.Format, PlacementId = session.PlacementId });
        }

        /// <summary>官方关闭时取得本次奖励状态，先完成自身等待，再走基类关闭/续杯；本对象准确注销并销毁。</summary>
        /// <param name="session">当前关闭对象。</param>
        private void Closed(AdSession session)
        {
            var completion = session.Showing;
            session.Showing = null;
            var result = new AdResult { Success = true, Format = session.Format, PlacementId = session.PlacementId, UserCompleted = session.Rewarded, RewardGranted = session.Rewarded };
            ReleaseAd(session);
            RaiseAdClosed(result, session.Rewarded);
            completion?.TrySetResult(result);
        }

        /// <summary>展示失败立即结算，不等不存在的关闭回调；基类记录失败并负责一次续杯。</summary>
        /// <param name="session">当前失败对象。</param>
        /// <param name="error">官方展示错误。</param>
        private void FailedShow(AdSession session, AdError error)
        {
            var completion = session.Showing;
            session.Showing = null;
            var result = new AdResult { Success = false, Format = session.Format, PlacementId = session.PlacementId, ErrorMessage = error?.GetMessage() ?? "AdMob 广告展示失败。" };
            ReleaseAd(session);
            RaiseShowFailed(result);
            completion?.TrySetResult(result);
        }

        /// <summary>官方微单位收益只接受 USD，避免未换汇金额违反共享 AdEvent 契约；非 USD 保留明确诊断，不伪造美元。</summary>
        /// <param name="session">当前广告对象。</param>
        /// <param name="value">官方收益，1,000,000 微单位等于一货币单位。</param>
        private void Paid(AdSession session, AdValue value)
        {
            if (value == null || value.Value < 0 || !string.Equals(value.CurrencyCode, "USD", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning(LogTag.AD, "AdMob 收益回调不是有效 USD 金额，当前共享收益接口不执行自动换汇。");
                return;
            }
            RaiseRevenue(new AdEvent { EventType = AdEventType.Revenue, Format = session.Format, PlacementId = session.PlacementId, Network = "AdMob", Revenue = value.Value / 1000000d, Currency = "USD", Precision = value.Precision.ToString(), Timestamp = DateTime.UtcNow });
        }
    }
}
#endif
