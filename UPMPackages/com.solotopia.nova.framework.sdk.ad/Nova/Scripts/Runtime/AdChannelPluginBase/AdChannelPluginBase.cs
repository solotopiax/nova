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
    /// 广告渠道插件公共基类，直接实现 IAdInternalPlugin。
    /// 封装所有渠道共用逻辑：多 ID 状态机（AdUnit 池 + 重试调度 + eCPM 优先 Show）、
    /// 打点扇出（IMonetizeTrackPlugin）、七类事件通知，
    /// 以及通用三段式模板方法（OnRequestAsync / OnShowAsync）。
    /// MaxAdPlugin 等渠道继承此类，只需重写 InitChannelSDKAsync，
    /// 以及 OnRequestAsync / OnShowAsync（内部用 switch(format) 处理支持的格式）。
    /// 是否支持某 AdFormat 由 RegisterAdUnits 注册的槽位推导：注册即支持，未注册即不支持。
    /// 线程契约：所有 virtual/abstract 方法主线程调用；On* 实现内部可切后台线程，完成前须切回主线程。
    /// </summary>
    public abstract partial class AdChannelPluginBase : IAdInternalPlugin
    {
        /// <summary>
        /// 渠道插件唯一名称；派生类必须实现，用于日志打点和 BuildBaseProps 标识来源渠道。
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// 当前渠道的枚举类型标识；派生类必须实现，返回对应的 AdChannelType 值。
        /// </summary>
        public abstract AdChannelType Channel { get; }

        /// <summary>
        /// 广告收入回调事件，每次广告展示产生收入时在主线程触发。
        /// </summary>
        public event System.Action<AdEvent> OnAdRevenuePaid;

        /// <summary>
        /// 广告加载成功事件，主线程触发。
        /// </summary>
        public event System.Action<AdLoadResult> OnAdLoaded;

        /// <summary>
        /// 广告加载失败事件，主线程触发。
        /// </summary>
        public event System.Action<AdLoadResult> OnAdLoadFailed;

        /// <summary>
        /// SDK 初始化完成事件，由渠道 SDK 初始化回调后触发。
        /// </summary>
        public event System.Action<bool> OnInitResult;

        /// <summary>
        /// 广告播放完成事件，由渠道 SDK 展示回调触发。
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
        /// 由 AdPlugin 在创建渠道实例后立即调用，将 AdChannelConfigList 上的全局广告开关写入渠道。
        /// </summary>
        /// <param name="globals">AdPluginConfig.ChannelConfigs，承载竞价 / Banner ILRD / 静音 / 加载重试等全局开关。</param>
        public void ApplyGlobalConfig(AdChannelConfigList globals)
        {
            if (globals == null) return;
            EnableBidding = globals.EnableBidding;
            BannerIlrdInterval = globals.BannerIlrdInterval;
            MuteAd = globals.MuteAd;
            RetryLoadAdMaxNum = globals.RetryLoadAdMaxNum;
            RetryLoadAdInterv = globals.RetryLoadAdInterv;
        }

        /// <summary>
        /// 查询指定格式当前已加载广告的最高预期收益（USD）。
        /// 基类返回 0；支持收益感知的渠道重写此方法返回真实值。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>最高预期收益（USD），无就绪广告时为 0。</returns>
        public virtual float GetMaxRevenue(AdFormat format) => 0f;

        /// <summary>
        /// 同步登录用户 userId 到本渠道 SDK，默认空实现。
        /// 渠道有原生 SetUserId（如 MaxSdk.SetUserId）时 override。
        /// </summary>
        /// <param name="userId">已登录用户的唯一标识。</param>
        public virtual void SetUserId(string userId) { }

        /// <summary>
        /// 初始化模板方法：先缓存打点插件，再调派生类 InitChannelSDKAsync。
        /// 由 AdPlugin 在创建渠道实例后调用；派生类不得重写此方法，只能实现 InitChannelSDKAsync。
        /// </summary>
        /// <param name="config">渠道配置，派生类按需转型使用。</param>
        /// <param name="ct">取消令牌。</param>
        public async UniTask InitializeAsync(IAdChannelConfig config, CancellationToken ct)
        {
            CacheTrackPlugins();
            await InitChannelSDKAsync(config, ct);
        }

        /// <summary>
        /// 释放模板方法：先取消所有 AdUnit 的延时重试任务，再委托派生类 DisposeChannelSDKAsync。
        /// 由 AdPlugin 在 Dispose 时调用；派生类不得重写此方法，只能重写 DisposeChannelSDKAsync。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        public UniTask DisposeAsync(CancellationToken ct)
        {
            CancelAllRetryCts();
            return DisposeChannelSDKAsync(ct);
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
        /// 异步请求指定格式广告；遍历该 format 的 AdUnit 列表，对所有 Idle 槽位并行发起 OnRequestAsync，
        /// SDK 回调通过 RaiseAdLoaded/RaiseAdLoadFailed 推进状态机。
        /// 任一广告位加载成功后立即返回 Success=true 的 AdLoadResult；全部失败或该渠道未注册该 format 时返回 Success=false 的 AdLoadResult。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="reason">请求原因，默认 Auto；写入每个 AdUnit.LastReason 并随 nova_ad_request 事件上报。</param>
        /// <param name="customProps">自定义属性字典，写入每个 AdUnit.RequestCustomProps；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>首个完成的加载结果；全部失败时返回最后一次失败结果。</returns>
        public UniTask<AdLoadResult> RequestAsync(AdFormat format, AdRequestReason reason = AdRequestReason.Auto, Dictionary<string, object> customProps = null, CancellationToken ct = default)
        {
            if (!m_AdUnits.TryGetValue(format, out var units) || units.Count == 0)
                return UniTask.FromResult(BuildLoadFailure(format, null, -1, $"[{Name}] 未注册 {format} 广告位。"));
            var tcs = new UniTaskCompletionSource<AdLoadResult>();
            var batch = new RequestBatch
            {
                Tcs = tcs,
                PendingPlacementIds = new HashSet<string>(),
            };
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.State == AdUnitState.Ready)
                {
                    // 已有 Ready 槽位时，若 ct 已请求取消则直接返回取消；否则立即 SetResult。
                    // 此时 tcs 已是 Completed，外部 await 不会被取消，无需注册 ct 回调。
                    if (ct.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(ct);
                        return tcs.Task;
                    }
                    var readyEvent = new AdLoadResult { Success = true, Format = format, PlacementId = unit.PlacementId, Revenue = unit.Revenue };
                    tcs.TrySetResult(readyEvent);
                    return tcs.Task;
                }
                if (unit.State == AdUnitState.Loading || unit.State == AdUnitState.Showing)
                {
                    batch.PendingPlacementIds.Add(unit.PlacementId);
                    continue;
                }
                if (unit.RetryCts != null)
                {
                    batch.PendingPlacementIds.Add(unit.PlacementId);
                    continue;
                }
                unit.LastReason = reason;
                unit.RequestCustomProps = customProps;
                unit.State = AdUnitState.Loading;
                batch.PendingPlacementIds.Add(unit.PlacementId);
                TrackAdRequest(format, unit.PlacementId, reason, customProps);
                InvokeOnRequestSafeAsync(unit, format, ct).Forget();
            }
            if (batch.PendingPlacementIds.Count == 0)
                return UniTask.FromResult(BuildLoadFailure(format, null, -2, $"[{Name}] 当前没有可请求的 {format} 广告位。"));
            m_PendingBatches.Add(batch);
            // 主线程契约：m_PendingBatches 及 batch 字段仅在主线程读写，ct.Register 回调亦在主线程执行（Unity 主线程取消场景）。
            // 若未来出现 SDK 非主线程取消场景，需改用 UniTaskSynchronizationContext.Post 切回主线程后再操作。
            if (ct.CanBeCanceled)
            {
                batch.Registration = ct.Register(() =>
                {
                    if (batch.Tcs.TrySetCanceled(ct))
                        m_PendingBatches.Remove(batch);
                });
            }
            return tcs.Task;
        }

        /// <summary>
        /// 查询指定格式广告是否已加载完毕；要求满足两个条件：
        /// ① 列表中存在 AdUnit 处于 Ready 状态（基类状态机视角）；
        /// ② 该 placementId 在渠道 SDK 侧也确认 Ready（OnIsReady 回调，派生类对接 SDK 真实查询）。
        /// 仅当两条件同时成立才返回 true，避免基类状态机已置 Ready 但 SDK 因刷新/失效已不可播的误判。
        /// 该渠道未注册该 format 时返回 false。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>已加载完毕返回 true，未注册或无就绪槽位时返回 false。</returns>
        public bool IsReady(AdFormat format)
        {
            if (!m_AdUnits.TryGetValue(format, out var units)) return false;
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.State != AdUnitState.Ready) continue;
                if (!OnIsReady(format, unit.PlacementId)) continue;
                return true;
            }
            return false;
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
        /// 异步展示指定格式广告；从 Ready 槽位中按 Revenue 降序取最高 eCPM 的槽位展示。
        /// 非 Banner 展示结束后自动触发 RequestAsync 续杯（Banner 不续杯）。
        /// 该渠道未注册该 format 或无 Ready 槽位时返回失败结果。
        /// OnShowAsync 抛出异常时槽位回置 Idle，防止永久卡在 Showing 状态。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="customProps">自定义属性字典，写入选中 AdUnit.ShowCustomProps；可为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>广告展示结果。</returns>
        public async UniTask<AdResult> ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default)
        {
            var unit = PickBestReadyUnit(format);
            if (unit == null)
                return new AdResult { Success = false, Format = format, ErrorMessage = "no ready ad unit" };
            unit.ShowCustomProps = customProps;
            unit.State = AdUnitState.Showing;
            try
            {
                return await OnShowAsync(format, unit.PlacementId, ct);
            }
            catch (OperationCanceledException)
            {
                unit.State = AdUnitState.Idle;
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"[{Name}] OnShowAsync({format}, {unit.PlacementId}) 异常，槽位回置 Idle：{ex.Message}");
                unit.State = AdUnitState.Idle;
                return new AdResult { Success = false, Format = format, PlacementId = unit.PlacementId, ErrorMessage = ex.Message };
            }
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
