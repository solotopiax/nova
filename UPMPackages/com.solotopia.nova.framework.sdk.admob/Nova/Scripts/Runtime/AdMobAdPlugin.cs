/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdMobAdPlugin.cs
 * author:    Codex
 * created:   2026/5/14
 * descrip:   独立 Google Mobile Ads 渠道；复用广告基类与聚合公开接口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;
using UnityEngine;
#if NOVA_GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
using AdFormat = NovaFramework.Runtime.AdFormat;
using GoogleMobileAds.Ump.Api;
#endif

namespace NovaFramework.SDK.AdMobAdPlugin.Runtime
{
    /// <summary>
    /// 独立 Google Mobile Ads 渠道，按内嵌配置由 AdPlugin 创建并托管。
    /// 四格式复用基类槽位、加载批次、重试与七类事件；不调用 MAX mediation 门面。
    /// </summary>
    [AdChannel(typeof(AdMobAdChannelConfig))]
    public sealed partial class AdMobAdPlugin : AdChannelPluginBase
    {
        /// <summary>渠道唯一名称，参与日志与 Banner 累计存储键。</summary>
        public override string Name => "AdMobAdPlugin";
        /// <summary>当前渠道类型为独立 AdMob。</summary>
        public override AdChannelType Channel => AdChannelType.AdMob;

        private readonly UniTaskCompletionSource m_PrivacyCompletion = new UniTaskCompletionSource();
#if NOVA_GOOGLE_MOBILE_ADS
        private readonly Dictionary<string, AdSession> m_Ads = new Dictionary<string, AdSession>();
        private object m_Lifetime;
        private CancellationToken m_LifetimeToken;
        private string m_BannerPlacementId;
        private bool m_BannerDesiredVisible;
        private Vector2? m_BannerCustomPosition;
        private BannerPosition m_BannerPosition = BannerPosition.Bottom;

        /// <summary>持有单次加载的原厂对象、准确退订操作与展示完成源；同 ID 新加载不接受旧回调。</summary>
        private sealed class AdSession
        {
            public readonly AdFormat Format;
            public readonly string PlacementId;
            public readonly object Lifetime;
            public readonly CancellationToken LifetimeToken;
            public readonly List<Action> Unsubscribe = new List<Action>();
            public Action Destroy;
            public Func<bool> Ready;
            public Action<Action<Reward>> Show;
            public BannerView Banner;
            public UniTaskCompletionSource<AdResult> Showing;
            public DateTime LoadedAtUtc;
            public bool Active = true;
            public bool Loaded;
            public bool Rewarded;

            /// <summary>保存当前广告对象所属格式、ID 与聚合生命周期，不创建原厂资源。</summary>
            /// <param name="format">广告格式。</param>
            /// <param name="placementId">配置的广告位 ID。</param>
            /// <param name="lifetime">渠道本轮身份。</param>
            /// <param name="lifetimeToken">渠道生命周期令牌。</param>
            public AdSession(AdFormat format, string placementId, object lifetime, CancellationToken lifetimeToken)
            {
                Format = format;
                PlacementId = placementId;
                Lifetime = lifetime;
                LifetimeToken = lifetimeToken;
            }
        }
#endif

        /// <summary>完成 UMP 所需授权流程后初始化官方 SDK；取消只结束等待，旧回调因会话失效而不能注册广告位。</summary>
        /// <param name="config">由聚合传入的 AdMob 内嵌配置。</param>
        /// <param name="ct">聚合生命周期与初始化调用方的取消令牌。</param>
        /// <returns>真实渠道初始化任务。</returns>
        protected override async UniTask InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct)
        {
#if NOVA_GOOGLE_MOBILE_ADS
            object lifetime = null;
            try
            {
                ct.ThrowIfCancellationRequested();
                if (!(config is AdMobAdChannelConfig cfg)) throw new ArgumentException("AdMob 渠道配置类型不匹配。", nameof(config));
                if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer)
                {
                    Log.Warning(LogTag.AD, "独立 AdMob 广告仅在 Android / iOS Player 初始化；Editor 不自动请求测试广告。");
                    RaiseInitResult(false);
                    return;
                }
                string appId = Application.platform == RuntimePlatform.Android ? cfg.AppIdAndroid : cfg.AppIdIOS;
                if (string.IsNullOrWhiteSpace(appId)) throw new InvalidOperationException("目标平台 AdMob App ID 缺失，请先配置并核对构建注入。");
                lifetime = new object();
                m_Lifetime = lifetime;
                m_LifetimeToken = ct;
                m_BannerPlacementId = cfg.BannerPlacementIds.Count > 0 ? cfg.BannerPlacementIds[0] : null;
                var consent = new UniTaskCompletionSource();
                ConsentInformation.Update(new ConsentRequestParameters { TagForUnderAgeOfConsent = cfg.TagForUnderAgeOfConsent }, error =>
                    PostInitialization(lifetime, ct, () =>
                    {
                        if (error != null)
                        {
                            // 更新失败时仅可复用 UMP 当前明确允许的既有状态。
                            Log.Warning(LogTag.AD, $"AdMob UMP 更新失败：{error.Message}");
                            consent.TrySetResult();
                            return;
                        }
                        ConsentForm.LoadAndShowConsentFormIfRequired(formError => PostInitialization(lifetime, ct, () =>
                        {
                            if (formError != null) Log.Warning(LogTag.AD, $"AdMob UMP 表单失败：{formError.Message}");
                            consent.TrySetResult();
                        }, consent));
                    }, consent));
                await consent.Task.AttachExternalCancellation(ct);
                if (!ConsentInformation.CanRequestAds()) throw new InvalidOperationException("AdMob UMP 当前不允许请求广告。");
                m_PrivacyCompletion.TrySetResult();
                MobileAds.SetApplicationMuted(MuteAd);
                var initialized = new UniTaskCompletionSource();
                MobileAds.Initialize(status => PostInitialization(lifetime, ct, () =>
                {
                    if (status == null) initialized.TrySetException(new InvalidOperationException("AdMob SDK 初始化未返回状态。"));
                    else initialized.TrySetResult();
                }, initialized));
                await initialized.Task.AttachExternalCancellation(ct);
                ct.ThrowIfCancellationRequested();
                RegisterPlacements(AdFormat.Rewarded, cfg.RVPlacementIds);
                RegisterPlacements(AdFormat.Interstitial, cfg.InterPlacementIds);
                RegisterPlacements(AdFormat.Banner, cfg.BannerPlacementIds);
                RegisterPlacements(AdFormat.AppOpen, cfg.AppOpenPlacementIds);
                RaiseInitResult(true);
            }
            catch
            {
                if (ReferenceEquals(m_Lifetime, lifetime)) m_Lifetime = null;
                throw;
            }
            finally { m_PrivacyCompletion.TrySetResult(); }
#else
            Log.Warning(LogTag.AD, "Google Mobile Ads Unity SDK 未安装；独立 AdMob 渠道不可用。");
            m_PrivacyCompletion.TrySetResult();
            RaiseInitResult(false);
            await UniTask.CompletedTask;
#endif
        }

        /// <summary>等待本渠道真实 UMP 初始化流程结束；不以授权 bool 轮询代替流程。</summary>
        /// <param name="ct">本次业务等待令牌；取消不撤销原生表单。</param>
        /// <returns>授权流程结束任务。</returns>
        public override UniTask WaitForPrivacyFlowAsync(CancellationToken ct = default)
            => m_PrivacyCompletion.Task.AttachExternalCancellation(ct);

#if NOVA_GOOGLE_MOBILE_ADS
        /// <summary>将初始化回调切回主线程并再次核对身份和取消，防止旧初始化恢复渠道。</summary>
        /// <param name="lifetime">回调捕获的渠道身份。</param>
        /// <param name="ct">初始化取消令牌。</param>
        /// <param name="action">当前有效初始化阶段操作。</param>
        /// <param name="completion">该阶段完成源，处理异常明确结算而非留下永久等待。</param>
        private void PostInitialization(object lifetime, CancellationToken ct, Action action, UniTaskCompletionSource completion)
            => PostAdCallbackToMainThread(() =>
            {
                if (!ReferenceEquals(m_Lifetime, lifetime) || ct.IsCancellationRequested) return;
                try { action(); }
                catch (Exception ex) { completion.TrySetException(ex); }
            });

        /// <summary>将配置 ID 转成基类槽位；无效与重复项仍由统一注册方法处理。</summary>
        /// <param name="format">广告格式。</param>
        /// <param name="ids">配置的广告位列表。</param>
        private void RegisterPlacements(AdFormat format, IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count == 0) return;
            var options = new AdUnitOptions[ids.Count];
            for (int i = 0; i < ids.Count; i++) options[i] = new AdUnitOptions(ids[i]);
            RegisterAdUnits(format, options);
        }

        /// <summary>核对原厂广告对象仍属于当前 ID 与生命周期；取消后旧回调不能发事件或续杯。</summary>
        /// <param name="session">回调捕获的广告对象记录。</param>
        /// <returns>本轮且该 ID 仍持有同一记录时为 true。</returns>
        private bool IsCurrent(AdSession session)
            => session.Active && !session.LifetimeToken.IsCancellationRequested && ReferenceEquals(m_Lifetime, session.Lifetime)
               && m_Ads.TryGetValue(session.PlacementId, out var current) && ReferenceEquals(current, session);

        /// <summary>原厂回调统一进入基类主线程 FIFO，执行时再核对广告对象身份。</summary>
        /// <param name="session">回调所属对象。</param>
        /// <param name="action">有效对象才执行的操作。</param>
        private void PostAd(AdSession session, Action action)
            => PostAdCallbackToMainThread(() => { if (IsCurrent(session)) action(); });

        /// <summary>按真实格式 API 加载；替换对象前准确注销和销毁旧对象。</summary>
        /// <param name="format">已注册格式。</param>
        /// <param name="placementId">本次广告位。</param>
        /// <param name="ct">当前加载等待令牌，不取消原厂已经发起的请求。</param>
        /// <returns>加载命令已发起；结果由原厂回调结算基类批次。</returns>
        protected override UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (m_Lifetime == null || m_LifetimeToken.IsCancellationRequested) throw new OperationCanceledException(m_LifetimeToken);
            if (format == AdFormat.Banner && m_Ads.TryGetValue(placementId, out var existing) && IsCurrent(existing) && existing.Banner != null)
            {
                existing.Banner.LoadAd(new AdRequest());
                return UniTask.CompletedTask;
            }
            if (m_Ads.TryGetValue(placementId, out var old))
            {
                if (old.Showing != null) return UniTask.CompletedTask; // 业务等待取消不撤销仍在展示的原厂对象。
                ReleaseAd(old);
            }
            var session = new AdSession(format, placementId, m_Lifetime, m_LifetimeToken);
            m_Ads[placementId] = session;
            switch (format)
            {
                case AdFormat.Rewarded: RewardedAd.Load(placementId, new AdRequest(), (ad, error) => AcceptRewarded(session, ad, error)); break;
                case AdFormat.Interstitial: InterstitialAd.Load(placementId, new AdRequest(), (ad, error) => AcceptInterstitial(session, ad, error)); break;
                case AdFormat.AppOpen: AppOpenAd.Load(placementId, new AdRequest(), (ad, error) => AcceptAppOpen(session, ad, error)); break;
                case AdFormat.Banner: CreateBanner(session); break;
                default: throw new AdFormatNotSupportedException(Name, format);
            }
            return UniTask.CompletedTask;
        }

        /// <summary>就绪同时查询原厂 CanShowAd；全屏缓存一小时、AppOpen 四小时，过期对象按原厂限期作废。</summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">目标广告位。</param>
        /// <returns>对象有效、已加载且未过期并由原厂确认可播时为 true。</returns>
        protected override bool OnIsReady(AdFormat format, string placementId)
        {
            if (!m_Ads.TryGetValue(placementId, out var session) || !IsCurrent(session) || !session.Loaded) return false;
            if (format == AdFormat.Banner) return session.Banner != null && !session.Banner.IsDestroyed;
            TimeSpan lifetime = TimeSpan.FromHours(format == AdFormat.AppOpen ? 4 : 1);
            if (DateTime.UtcNow - session.LoadedAtUtc >= lifetime)
            {
                ReleaseAd(session);
                RaiseAdLoadFailed(new AdLoadResult { Format = format, PlacementId = placementId, ErrorCode = -10, ErrorMessage = "AdMob 已加载广告超出原厂缓存有效期。" });
                return false;
            }
            return session.Ready != null && session.Ready();
        }

        /// <summary>展示指定原厂对象；官方奖励仅标记本次激励状态，关闭时才发布奖励结果。</summary>
        /// <param name="format">Rewarded、Interstitial 或 AppOpen。</param>
        /// <param name="placementId">已就绪广告位。</param>
        /// <param name="ct">业务等待取消令牌，不撤销正在展示的原生广告。</param>
        /// <returns>关闭或展示失败结果。</returns>
        protected override async UniTask<AdResult> OnShowAsync(AdFormat format, string placementId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (m_Ads.TryGetValue(placementId, out var playing) && playing.Showing != null)
                throw new InvalidOperationException("该 AdMob 原厂广告仍在展示，取消业务等待不撤销当前展示。");
            if (!OnIsReady(format, placementId) || !m_Ads.TryGetValue(placementId, out var session) || session.Show == null)
            {
                var failed = new AdResult { Success = false, Format = format, PlacementId = placementId, ErrorMessage = "AdMob 广告当前不可展示。" };
                RaiseShowFailed(failed);
                return failed;
            }
            if (session.Showing != null) throw new InvalidOperationException("该 AdMob 广告仍在展示。");
            var completion = new UniTaskCompletionSource<AdResult>();
            session.Showing = completion;
            session.Rewarded = false;
            session.Loaded = false;
            TrackAdShow(format, placementId);
            try { session.Show(reward => PostAd(session, () => session.Rewarded = true)); }
            catch (Exception ex)
            {
                session.Showing = null;
                ReleaseAd(session);
                var failed = new AdResult { Success = false, Format = format, PlacementId = placementId, ErrorMessage = ex.Message };
                RaiseShowFailed(failed);
                return failed;
            }
            return await completion.Task.AttachExternalCancellation(ct);
        }

        /// <summary>释放失效对象的准确订阅与全部原厂资源，取消展示等待；单条原厂失败不跳过其他清理。</summary>
        /// <param name="session">需要收回的广告记录。</param>
        private void ReleaseAd(AdSession session)
        {
            session.Active = false;
            if (m_Ads.TryGetValue(session.PlacementId, out var current) && ReferenceEquals(current, session)) m_Ads.Remove(session.PlacementId);
            session.Showing?.TrySetCanceled();
            session.Showing = null;
            foreach (var unsubscribe in session.Unsubscribe)
            {
                try { unsubscribe(); }
                catch (Exception ex) { Log.Warning(LogTag.AD, $"AdMob 回调退订失败：{ex.Message}"); }
            }
            session.Unsubscribe.Clear();
            try { session.Destroy?.Invoke(); }
            catch (Exception ex) { Log.Warning(LogTag.AD, $"AdMob 广告资源释放失败：{ex.Message}"); }
            session.Destroy = null;
            session.Banner = null;
            session.Loaded = false;
        }

        /// <summary>失效本轮并收回全部四格式原厂对象；不清原厂全局其他使用者或重置用户隐私状态。</summary>
        /// <param name="ct">基类释放令牌，完整清理不受外部取消中断。</param>
        /// <returns>全部本渠道对象清理完成任务。</returns>
        protected override UniTask DisposeChannelSDKAsync(CancellationToken ct)
        {
            m_Lifetime = null;
            m_PrivacyCompletion.TrySetResult();
            var sessions = new List<AdSession>(m_Ads.Values);
            foreach (var session in sessions) ReleaseAd(session);
            m_Ads.Clear();
            m_BannerPlacementId = null;
            m_BannerDesiredVisible = false;
            return UniTask.CompletedTask;
        }
#endif
    }
}
