/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   AppLovin MAX 广告渠道插件主类
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    [AdChannel(typeof(MaxAdChannelConfig))]
    public sealed partial class MaxAdPlugin : AdChannelPluginBase
    {
  
        /// <summary>
        /// 初始化 AppLovin MAX 渠道 SDK，注册广告组、缓存真实 ID、初始化 Facebook GDPR 并等待 MAX SDK 回调。
        /// 完成、取消或异常后解绑本次初始化委托并结束隐私等待；不自动预载。
        /// </summary>
        /// <param name="config">渠道配置，须为 MaxAdChannelConfig 实例。</param>
        /// <param name="ct">初始化等待的取消令牌；不能取消已经发出的原厂初始化。</param>
        /// <returns>异步任务；原厂完成后通知初始化结果，异常交由聚合层处理。</returns>
        protected override async UniTask InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct)
        {
#if NOVA_APPLOVIN_MAX
            var initTcs = new UniTaskCompletionSource<MaxSdkBase.SdkConfiguration>();
            Action<MaxSdkBase.SdkConfiguration> initializedHandler = sdkConfig => initTcs.TrySetResult(sdkConfig);
            try
            {
                var cfg = (MaxAdChannelConfig)config;

                // 校验并注册广告组，缓存展开后的真实 ID，不能把组表达式直接传给 MAX。
                RegisterChannelPlacements(cfg);

                // 初始化只重置收益会话；插件引用在实际派发时按需获取。
                ClearMaxRevenueTrackers();
                m_RevenueTrackingSession = new object();

                // Facebook GDPR 初始化。
                FacebookAdSetting.Initialize();

                // 应用静音设置（MuteAd 由 AdPlugin 聚合层通过 ApplyGlobalConfig 注入）。
                MaxSdk.SetMuted(MuteAd);

                // 控制 SDK 详细日志输出。
                MaxSdk.SetVerboseLogging(cfg.LogEnable);

                // 原厂回调只完成本次等待，初始化状态统一在下方主流程更新。
                MaxSdkCallbacks.OnSdkInitializedEvent += initializedHandler;

                // MAX 展示 Consent Flow 时，会在用户完成流程后才触发初始化完成回调。
                MaxSdk.InitializeSdk();
                MaxSdkBase.SdkConfiguration sdkConfig = await initTcs.Task.AttachExternalCancellation(ct);
                await UniTask.SwitchToMainThread(ct);
                OnSdkInitializedCallback(sdkConfig, cfg);
            }
            finally
            {
                // 取消续体可能在后台线程执行；状态与事件解绑统一回到 Unity 主线程。
                await UniTask.SwitchToMainThread();
                MaxSdkCallbacks.OnSdkInitializedEvent -= initializedHandler;

                // 正常回调已完成隐私等待；取消或异常时由此兜底，避免启动流程一直等待。
                m_PrivacyFlowCompletionSource.TrySetResult();
            }
#else
            // AppLovin MAX 未安装：渠道降级为不可用，标记初始化失败。
            Log.Warning(LogTag.Max, "AppLovin MAX（com.applovin.mediation.ads）未安装，MAX 广告渠道降级不可用。");
            RaiseInitResult(false);
            await UniTask.CompletedTask;
#endif
        }

#if NOVA_APPLOVIN_MAX
        /// <summary>
        /// 基类在预留请求轮之前使用 MAX 真实初始化门，未就绪直接返回 -11。
        /// </summary>
        protected override bool IsChannelInitialized => m_IsSdkInitialized;

        /// <summary>
        /// 根据广告格式向对应 AdUnit 发起加载请求。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位 ID。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>完成的异步任务。</returns>
        protected override UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct)
        {
            switch (format)
            {
                case AdFormat.Rewarded: RequestRV(placementId); break;
                case AdFormat.Interstitial: RequestInter(placementId); break;
                case AdFormat.Banner: RequestBanner(placementId); break;
                case AdFormat.AppOpen: RequestAppOpen(placementId); break;
                default: throw new AdFormatNotSupportedException(Name, format);
            }
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 查询已注册真实 ID 在 MAX 中的可播放状态；仅由基类的主线程候选链调用。
        /// Banner 继续沿用本地视图状态，不加入全屏缓存判断。
        /// </summary>
        /// <param name="format">待查询的广告格式。</param>
        /// <param name="placementId">分组计划中的真实广告位 ID。</param>
        /// <returns>该广告位当前在原厂 SDK 中可播放时为 true。</returns>
        protected override bool OnIsReady(AdFormat format, string placementId)
        {
            if (!IsMaxPlacementRegistered(format, placementId))
            {
                return false;
            }
            return format switch
            {
                AdFormat.Rewarded => MaxSdk.IsRewardedAdReady(placementId),
                AdFormat.Interstitial => MaxSdk.IsInterstitialReady(placementId),
                AdFormat.AppOpen => MaxSdk.IsAppOpenAdReady(placementId),
                AdFormat.Banner => true,
                _ => false,
            };
        }

        /// <summary>
        /// 根据广告格式展示对应广告并等待结果。
        /// Banner 格式不经此方法，通过 ShowBanner() / HideBanner() 虚方法控制。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位 ID。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>广告播放结果。</returns>
        protected override UniTask<AdResult> OnShowAsync(AdFormat format, string placementId, CancellationToken ct)
        {
            return format switch
            {
                AdFormat.Rewarded => ShowRVAsync(placementId, ct),
                AdFormat.Interstitial => ShowInterAsync(placementId, ct),
                AdFormat.AppOpen => ShowAppOpenAsync(placementId, ct),
                _ => throw new AdFormatNotSupportedException(Name, format),
            };
        }
#endif

    }
}
