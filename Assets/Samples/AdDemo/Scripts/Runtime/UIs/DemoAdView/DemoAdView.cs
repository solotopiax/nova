/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAdView.cs
 * author:    nova-create-sample
 * created:   2026/06/03
 * descrip:   DemoAdView 演示 View — 生命周期与公开接口
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.Sdk.Ad.Samples.Runtime
{
    /// <summary>
    /// DemoAdView 演示 View，派生自 BaseDemoView，遵循三段式骨架（TitleBar / InteractionArea / FeedbackArea）。
    /// </summary>
    public sealed partial class DemoAdView : BaseDemoView
    {
        /// <summary>
        /// 视图打开钩子，每次 OpenUIViewAsync 调用时触发。
        /// 子类重写须调用 base.OnOpen(userData)。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            SubscribeAdEvents();
            AppendFeedback("Ad 演示已打开，可在 Debug UI 中请求、检查和播放广告。");
        }

        /// <summary>
        /// 视图关闭钩子，每次 CloseUIView 调用时触发。
        /// 子类重写须调用 base.OnClose(isShutdown, userData)。
        /// </summary>
        /// <param name="isShutdown">是否由 UI 管理器关闭流程触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            UnsubscribeAdEvents();

            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 广告渠道下拉框值变更回调，由 prefab 中 TMP_Dropdown.onValueChanged 持久化绑定。
        /// 当前 Demo 只提供 MAX 渠道选项，因此不依赖 value 做枚举映射。
        /// </summary>
        /// <param name="value">下拉框选项索引。</param>
        public void OnAdChannelChanged(int value)
        {
            m_SelectedAdChannel = AdChannelType.MAX;
        }

        /// <summary>
        /// 请求插屏广告按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnRequestInterstitialClicked()
        {
            RequestAdAsync(AdFormat.Interstitial, m_InterstitialCustomDataInput).Forget();
        }

        /// <summary>
        /// 检查插屏广告是否就绪按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnCheckInterstitialReadyClicked()
        {
            CheckAdReady(AdFormat.Interstitial);
        }

        /// <summary>
        /// 播放插屏广告按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnShowInterstitialClicked()
        {
            ShowAdAsync(AdFormat.Interstitial, m_InterstitialCustomDataInput).Forget();
        }

        /// <summary>
        /// 请求激励视频广告按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnRequestRewardedClicked()
        {
            RequestAdAsync(AdFormat.Rewarded, m_RewardedCustomDataInput).Forget();
        }

        /// <summary>
        /// 检查激励视频广告是否就绪按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnCheckRewardedReadyClicked()
        {
            CheckAdReady(AdFormat.Rewarded);
        }

        /// <summary>
        /// 播放激励视频广告按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnShowRewardedClicked()
        {
            ShowAdAsync(AdFormat.Rewarded, m_RewardedCustomDataInput).Forget();
        }

        /// <summary>
        /// 请求 Banner 广告按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnRequestBannerClicked()
        {
            RequestAdAsync(AdFormat.Banner, m_BannerCustomDataInput).Forget();
        }

        /// <summary>
        /// 显示 Banner 广告按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnShowBannerClicked()
        {
            ShowBanner();
        }

        /// <summary>
        /// 隐藏 Banner 广告按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnHideBannerClicked()
        {
            HideBanner();
        }

        /// <summary>
        /// 销毁 Banner 广告按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnDestroyBannerClicked()
        {
            DestroyBanner();
        }

        /// <summary>
        /// 请求开屏广告按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnRequestAppOpenClicked()
        {
            RequestAdAsync(AdFormat.AppOpen, m_AppOpenCustomDataInput).Forget();
        }

        /// <summary>
        /// 检查开屏广告是否就绪按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnCheckAppOpenReadyClicked()
        {
            CheckAdReady(AdFormat.AppOpen);
        }

        /// <summary>
        /// 播放开屏广告按钮回调，由 prefab 中 Button.onClick 持久化绑定。
        /// </summary>
        public void OnShowAppOpenClicked()
        {
            ShowAdAsync(AdFormat.AppOpen, m_AppOpenCustomDataInput).Forget();
        }
    }
}
