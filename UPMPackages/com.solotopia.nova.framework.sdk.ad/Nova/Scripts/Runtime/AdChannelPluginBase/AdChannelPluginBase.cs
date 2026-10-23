/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   广告渠道插件公共基类，含打点、事件通知与通用格式模板方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告渠道插件公共基类，实现 IAdInternalPlugin 与可选生命周期接口 IAdChannelLifecycle。
    /// 封装所有渠道共用逻辑：广告组状态机（组内串行、组间并行、按序列重试及真实Ready收益选择）、
    /// 打点扇出（IMonetizeTrackPlugin）、七类事件通知，
    /// 以及通用三段式模板方法（OnRequestAsync / OnShowAsync）。
    /// MaxAdPlugin 等渠道继承此类，只需重写 InitChannelSDKAsync，
    /// 以及 OnRequestAsync / OnShowAsync（内部用 switch(format) 处理支持的格式）。
    /// 是否支持某 AdFormat 由 RegisterAdUnits 注册的槽位推导：注册即支持，未注册即不支持。
    /// 线程契约：公开 API 由 Unity 主线程调用；渠道 SDK 回调中可直接调用 Raise*，基类将所有状态和业务事件串行排入Unity主线程，纯收益上报保持即时。
    /// </summary>
    public abstract partial class AdChannelPluginBase : IAdInternalPlugin, IAdChannelLifecycle
    {
        /// <summary>
        /// 创建渠道托管生命周期，缓存令牌用于幂等释放后的取消结果。
        /// </summary>
        protected AdChannelPluginBase() => m_ChannelLifetimeToken = m_ChannelLifetimeCts.Token;

        /// <summary>
        /// 渠道插件唯一名称；派生类必须实现，用于日志打点和 BuildBaseProps 标识来源渠道。
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 当前渠道的枚举类型标识；派生类必须实现，返回对应的 AdChannelType 值。
        /// </summary>
        public abstract AdChannelType Channel { get; }

        /// <summary>
        /// 广告收入业务事件，每次广告展示产生收入后由基类排入 Unity 主线程触发；收益打点可在 SDK 原始回调线程即时执行。
        /// </summary>
        public event System.Action<AdEvent> OnAdRevenuePaid;

        /// <summary>
        /// 广告加载成功业务事件，由基类排入 Unity 主线程触发。
        /// </summary>
        public event System.Action<AdLoadResult> OnAdLoaded;

        /// <summary>
        /// 广告加载失败业务事件，由基类排入 Unity 主线程触发。
        /// </summary>
        public event System.Action<AdLoadResult> OnAdLoadFailed;

        /// <summary>
        /// SDK 初始化完成事件，由渠道 SDK 初始化回调后触发。
        /// </summary>
        public event System.Action<bool> OnInitResult;

        /// <summary>
        /// 广告展示成功事件，由渠道 SDK displayed 回调触发；不表示广告已关闭或激励已完成。
        /// </summary>
        public event System.Action<AdResult> OnShowCompleted;

        /// <summary>
        /// 广告播放失败事件，由渠道 SDK 展示回调触发。
        /// </summary>
        public event System.Action<AdResult> OnShowFailed;

        /// <summary>
        /// 广告关闭事件，由渠道 SDK 关闭回调触发。
        /// </summary>
        public event System.Action<AdResult> OnAdClosed;

        /// <summary>
        /// 创建渠道后应用全局开关，并复制有效重试间隔序列，使后续配置编辑不影响运行中的渠道。
        /// </summary>
        /// <param name="globals">广告全局配置；为空或渠道已释放时忽略。</param>
        public void ApplyGlobalConfig(AdChannelConfigList globals)
        {
            RequireAdMainThread();
            if (m_IsDisposed || globals == null)
            {
                return;
            }
            EnableBidding = globals.EnableBidding;
            BannerIlrdInterval = globals.BannerIlrdInterval;
            MuteAd = globals.MuteAd;
            IReadOnlyList<float> configuredIntervals = globals.GroupRetryIntervalsSeconds;
            float[] retryIntervals = new float[configuredIntervals.Count];
            for (int i = 0; i < configuredIntervals.Count; i++)
            {
                retryIntervals[i] = configuredIntervals[i];
            }
            GroupRetryIntervalsSeconds = retryIntervals;
        }

        /// <summary>
        /// 派生SDK渠道的初始化加载门，旧渠道默认可用以保持注册入口兼容。
        /// </summary>
        protected virtual bool IsChannelInitialized => true;

        /// <summary>
        /// 查询指定格式当前已加载广告的最高预期收益（USD）。
        /// 基类以真实Ready候选的收益为默认值，外部渠道已有override保持兼容。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>最高预期收益（USD），无就绪广告时为 0。</returns>
        public virtual float GetMaxRevenue(AdFormat format) => (float)(PickBestReadyUnit(format)?.Revenue ?? 0d);

        /// <summary>
        /// 获取当前渠道 SDK 返回的国家或地区代码；默认渠道无可用国家代码。
        /// </summary>
        /// <returns>国家或地区代码；默认返回空字符串。</returns>
        public virtual string GetCountryCode() => string.Empty;

        /// <summary>
        /// 查询当前渠道是否已取得用户明确的广告隐私授权决定；默认渠道不支持查询。
        /// </summary>
        /// <returns>已明确授权或拒绝时返回 true；默认返回 false。</returns>
        public virtual bool IsUserConsentSet() => false;

        /// <summary>
        /// 查询当前渠道记录的用户广告隐私授权结果；默认渠道不支持查询。
        /// 必须结合 IsUserConsentSet() 区分“拒绝”和“尚未设置”。
        /// </summary>
        /// <returns>用户已明确同意时返回 true；默认返回 false。</returns>
        public virtual bool HasUserConsent() => false;

        /// <summary>
        /// 等待当前渠道初始化期间的隐私授权流程结束；默认渠道没有独立隐私流程，因此立即完成。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>隐私授权流程完成任务。</returns>
        public virtual UniTask WaitForPrivacyFlowAsync(CancellationToken ct = default) => UniTask.CompletedTask;

        /// <summary>
        /// 同步登录用户 userId 到本渠道 SDK，默认空实现。
        /// 渠道有原生 SetUserId（如 MaxSdk.SetUserId）时 override。
        /// </summary>
        /// <param name="userId">已登录用户的唯一标识。</param>
        public virtual void SetUserId(string userId) { }

        /// <summary>
        /// 初始化模板方法：先缓存存储管理器和打点插件，再调派生类 InitChannelSDKAsync。
        /// 由 AdPlugin 在创建渠道实例后调用；派生类不得重写此方法，只能实现 InitChannelSDKAsync。
        /// </summary>
        /// <param name="config">渠道配置，派生类按需转型使用。</param>
        /// <param name="ct">取消令牌。</param>
        public async UniTask InitializeAsync(IAdChannelConfig config, CancellationToken ct)
        {
            RequireAdMainThread();
            if (m_IsDisposed) throw new ObjectDisposedException(Name);
            ct.ThrowIfCancellationRequested();
            m_BannerIlrdPersistManager = FrameworkManagersGroup.GetManager<IFileFragmentManager>();
            CacheTrackPlugins();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, ChannelLifetimeToken);
            await InitChannelSDKAsync(config, linked.Token);
        }

        /// <summary>
        /// 释放渠道并共享本次清理结果；重复调用等待同一次清理，不因外部令牌取消而跳过。
        /// </summary>
        /// <param name="ct">兼容既有调用签名；释放过程不使用此令牌中止清理。</param>
        /// <returns>渠道清理完成任务；清理失败时，各次调用观察到同一异常。</returns>
        public async UniTask DisposeAsync(CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            if (m_ChannelDisposeCompletion == null)
            {
                // 先保存完成源，保证取消续体同步重入时仍复用当前清理。
                m_ChannelDisposeCompletion = new UniTaskCompletionSource();
                DisposeChannelAsync(m_ChannelDisposeCompletion).Forget();
            }
            await m_ChannelDisposeCompletion.Task;
        }

        /// <summary>
        /// 关闭托管回调与广告组等待，执行派生 SDK 清理，并在清空缓存后完成共享释放结果。
        /// 任一阶段失败仍继续后续清理；共享结果保留首先发生的异常。
        /// </summary>
        /// <param name="completion">首次释放创建的共享完成源。</param>
        private async UniTask DisposeChannelAsync(UniTaskCompletionSource completion)
        {
            Exception failure = null;
            try
            {
                lock (m_AdCallbackQueueLock)
                {
                    m_IsDisposed = true;
                    m_AdCallbackGeneration++;
                    m_AdCallbackQueue.Clear();
                }

                try
                {
                    m_ChannelLifetimeCts.Cancel();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                try
                {
                    CancelGroupRetriesAndBatches();
                }
                catch (Exception ex)
                {
                    failure ??= ex;
                }

                m_ActiveAdShow = null;
                await DisposeChannelSDKAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                failure ??= ex;
            }
            finally
            {
                // 派生异步清理可能在后台完成，缓存和业务事件仍在主线程清空。
                await UniTask.SwitchToMainThread();
                m_AdUnits.Clear();
                m_AdGroups.Clear();
                m_AdConfigFailures.Clear();
                m_TrackPlugins = null;
                m_BannerIlrdPersistManager = null;
                m_ChannelLifetimeCts.Dispose();
                OnAdRevenuePaid = null;
                OnAdLoaded = null;
                OnAdLoadFailed = null;
                OnInitResult = null;
                OnShowCompleted = null;
                OnShowFailed = null;
                OnAdClosed = null;

                if (failure == null)
                {
                    completion.TrySetResult();
                }
                else
                {
                    completion.TrySetException(failure);
                }
            }
        }

        /// <summary>
        /// 派生类实现点：执行渠道 SDK 的实际初始化逻辑。
        /// </summary>
        /// <param name="config">渠道配置，派生类按需转型使用。</param>
        /// <param name="ct">取消令牌。</param>
        protected abstract UniTask InitChannelSDKAsync(IAdChannelConfig config, CancellationToken ct);

        /// <summary>
        /// 派生类可选重写点：执行渠道 SDK 的实际释放逻辑。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        protected virtual UniTask DisposeChannelSDKAsync(CancellationToken ct) => UniTask.CompletedTask;

        /// <summary>
        /// 异步请求指定格式广告；每个空组首项并行发起，组内失败串行推进下一真实ID，
        /// SDK 回调通过 RaiseAdLoaded/RaiseAdLoadFailed 推进状态机。
        /// 任一广告位加载成功后立即返回 Success=true 的 AdLoadResult；全部失败或该渠道未注册该 format 时返回 Success=false 的 AdLoadResult。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="reason">新轮原因，默认Auto；加入正在加载的轮不会覆盖其原因。</param>
        /// <param name="customProps">新轮属性字典，将复制字典结构；加入正在加载轮只观察。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>首个完成的加载结果；全部失败时返回最后一次失败结果。</returns>
        public UniTask<AdLoadResult> RequestAsync(AdFormat format, AdRequestReason reason = AdRequestReason.Auto, Dictionary<string, object> customProps = null, CancellationToken ct = default)
            => RequestGroups(format, reason, customProps, ct);

        /// <summary>
        /// 查询指定格式广告是否已加载完毕；要求满足两个条件：
        /// ① 列表中存在 AdUnit 处于 Ready 状态（基类状态机视角）；
        /// ② 该 placementId 在渠道 SDK 侧也确认 Ready（OnIsReady 回调，派生类对接 SDK 真实查询）。
        /// 仅当两条件同时成立才返回 true，避免基类状态机已置 Ready 但 SDK 因刷新/失效已不可播的误判。
        /// 该渠道未注册该 format 时返回 false。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>已加载完毕返回 true，未注册或无就绪槽位时返回 false。</returns>
        public bool IsReady(AdFormat format) => PickBestReadyUnit(format) != null;

        /// <summary>
        /// 查询本渠道指定全屏格式是否仍占用原厂展示；业务await取消不解除占用。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>当前原生展示占用属于该格式时为true。</returns>
        public bool IsAdPlaying(AdFormat format)
        {
            RequireAdMainThread();
            return !m_IsDisposed && m_ActiveAdShow != null && m_ActiveAdShow.Format == format;
        }

        /// <summary>
        /// 派生类可选重写点：向渠道 SDK 查询指定 placementId 当前是否仍处于可播放状态。
        /// 默认返回 true（仅以基类状态机为准）；MAX/AdMob 等渠道重写此方法对接
        /// MaxSdk.IsRewardedAdReady(placementId) / AdMob 各广告位 IsLoaded 等真实查询接口。
        /// 通常按 switch(format) 分发到具体广告位的 SDK 查询调用。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识。</param>
        /// <returns>SDK 侧仍可播放返回 true，否则 false。</returns>
        protected virtual bool OnIsReady(AdFormat format, string placementId) => true;

        /// <summary>
        /// 选择最高收益的真实Ready广告并展示；不可用时返回失败，全屏占用由真实终态或释放解除，终态后自动补量。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">自定义属性字典，写入选中 AdUnit.ShowCustomProps；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>广告展示结果。</returns>
        public UniTask<AdResult> ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default)
        {
            RequireAdMainThread();
            if (ct.IsCancellationRequested)
            {
                return UniTask.FromCanceled<AdResult>(ct);
            }
            if (m_IsDisposed)
            {
                return UniTask.FromCanceled<AdResult>(ChannelLifetimeToken);
            }
            if (!IsChannelInitialized)
            {
                return UniTask.FromResult(new AdResult { Format = format, ErrorMessage = "渠道SDK尚未初始化成功。" });
            }
            if (IsFullscreenAd(format) && m_ActiveAdShow != null)
            {
                return UniTask.FromResult(new AdResult { Format = format, ErrorMessage = "广告正在展示，不能重叠发出全屏Show。" });
            }
            var unit = PickBestReadyUnit(format);
            if (unit == null)
            {
                return UniTask.FromResult(new AdResult { Format = format, ErrorMessage = "当前没有真实可播广告。" });
            }
            unit.ShowCustomProps = SnapshotAdProps(customProps);
            unit.State = AdUnitState.Showing;
            unit.Group.State = AdGroupState.Showing;
            if (IsFullscreenAd(format))
            {
                m_ActiveAdShow = new ActiveAdShow { Format = format, PlacementId = unit.PlacementId };
            }
            return InvokeOnShowSafeAsync(unit).AttachExternalCancellation(ChannelLifetimeToken).AttachExternalCancellation(ct);
        }

        /// <summary>
        /// 派生类实现点：执行指定格式广告的预加载逻辑；基类默认抛 AdFormatNotSupportedException。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识。</param>
        /// <param name="ct">取消令牌。</param>
        protected virtual UniTask OnRequestAsync(AdFormat format, string placementId, CancellationToken ct)
            => throw new AdFormatNotSupportedException(Name, format);

        /// <summary>
        /// 派生类实现点：展示指定格式广告；基类默认抛 AdFormatNotSupportedException。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>广告展示结果。</returns>
        protected virtual UniTask<AdResult> OnShowAsync(AdFormat format, string placementId, CancellationToken ct)
            => throw new AdFormatNotSupportedException(Name, format);

        /// <summary>
        /// 显示已加载的 Banner 广告；Banner 渠道重写此方法。
        /// </summary>
        public virtual void ShowBanner() { }

        /// <summary>
        /// 隐藏 Banner 广告（不销毁）；Banner 渠道重写此方法。
        /// </summary>
        public virtual void HideBanner() { }

        /// <summary>
        /// 销毁 Banner 广告实例；Banner 渠道重写此方法。
        /// </summary>
        public virtual void DestroyBanner() { }

        /// <summary>
        /// 通过枚举更新 Banner 停靠位置；Banner 渠道重写此方法。
        /// </summary>
        /// <param name="position">Banner 停靠位置。</param>
        public virtual void UpdateBannerPosition(BannerPosition position) { }

        /// <summary>
        /// 通过像素坐标更新 Banner 位置；Banner 渠道重写此方法。
        /// </summary>
        /// <param name="position">Banner 屏幕坐标（逻辑像素）。</param>
        public virtual void UpdateBannerPosition(Vector2 position) { }

        /// <summary>
        /// 开启 Banner 自动刷新；Banner 渠道重写此方法。
        /// </summary>
        public virtual void StartBannerAutoRefresh() { }

        /// <summary>
        /// 停止 Banner 自动刷新；Banner 渠道重写此方法。
        /// </summary>
        public virtual void StopBannerAutoRefresh() { }

        /// <summary>
        /// 设置 Banner 宽度；Banner 渠道重写此方法。
        /// </summary>
        /// <param name="width">Banner 宽度（逻辑像素）。</param>
        public virtual void SetBannerWidth(float width) { }

        /// <summary>
        /// 获取 Banner 自适应高度；基类返回 -1，支持自适应高度的渠道重写。
        /// </summary>
        /// <param name="width">指定宽度，-1 表示屏幕宽度。</param>
        /// <returns>自适应高度（逻辑像素）；不支持时返回 -1。</returns>
        public virtual float GetAdaptiveBannerHeight(float width = -1f) => -1f;

        /// <summary>
        /// 设置 Banner 背景色；Banner 渠道重写此方法。
        /// </summary>
        /// <param name="color">背景颜色。</param>
        public virtual void SetBannerBackgroundColor(Color color) { }
    }
}
