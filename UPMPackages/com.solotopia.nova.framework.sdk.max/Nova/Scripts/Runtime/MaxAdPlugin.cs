/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdPlugin.cs
 * author:    yingzheng
 * created:   2026/5/15
 * descrip:   AppLovin MAX 广告渠道插件主类
 ***************************************************************/

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
        /// 初始化 AppLovin MAX 渠道 SDK，缓存广告位 ID、注册 AdUnit、初始化 Facebook GDPR 并等待 MAX SDK 完成初始化。
        /// 支持的 AdFormat 由 RegisterCallbacks 中调用 RegisterAdUnits 的 format 集合推导（Rewarded/Interstitial/Banner/AppOpen）。
        /// </summary>
        /// <param name="config">渠道配置，须为 MaxAdChannelConfig 实例。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>异步任务，完成后 RaiseInitResult 已调用。</returns>
        protected override async UniTask InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct)
        {
            var cfg = (MaxAdChannelConfig)config;

            // 缓存广告位 ID 列表
            m_RVPlacementIds = cfg.RVPlacementIds;
            m_InterPlacementIds = cfg.InterPlacementIds;
            m_BannerPlacementIds = cfg.BannerPlacementIds;
            m_AppOpenPlacementIds = cfg.AppOpenPlacementIds;

            // Facebook GDPR 初始化
            FacebookAdSetting.Initialize();

            // 设置主线程回调在 Unity 主线程执行
            MaxSdkBase.InvokeEventsOnUnityMainThread = true;

            // 应用静音设置（MuteAd 由 AdPlugin 聚合层通过 ApplyGlobalConfig 注入）
            MaxSdk.SetMuted(MuteAd);

            // 控制 SDK 详细日志输出
            MaxSdk.SetVerboseLogging(cfg.LogEnable);

            // 注册初始化完成事件，等待 MAX SDK 回调
            var initTcs = new UniTaskCompletionSource<bool>();

            // 注册初始化完成事件
            MaxSdkCallbacks.OnSdkInitializedEvent += sdkConfig => OnSdkInitializedCallback(sdkConfig, cfg, initTcs);

            // 初始化 SDK
            MaxSdk.InitializeSdk();
            
            // 等待初始化完成
            await initTcs.Task;
        }

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

    }
}
