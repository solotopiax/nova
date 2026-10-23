/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.Methods.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   广告渠道公共工具、原厂调用保护与展示生命周期
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    public abstract partial class AdChannelPluginBase
    {
        /// <summary>
        /// 保留旧注册入口，每个真实ID作为独立单ID组累加；重复ID保持跳过兼容。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="options">仅包含真实ID的旧槽位列表。</param>
        protected void RegisterAdUnits(AdFormat format, IReadOnlyList<AdUnitOptions> options)
        {
            RequireAdMainThread();
            if (m_IsDisposed || options == null) return;
            foreach (var option in options)
            {
                string id = option.PlacementId;
                if (string.IsNullOrEmpty(id) || FindAdUnit(id) != null)
                {
                    Log.Warning(LogTag.AD, $"[{Name}] RegisterAdUnits({format}) 的空或重复广告ID已跳过：{id}");
                    continue;
                }
                // 旧入口不解析表达式，防止外部旧渠道的真实ID含特殊字符被静默改写。
                RegisterGroupUnits(format, new[] { id });
            }
        }

        /// <summary>
        /// 在主线程更新收益槽位，收益金额不会决定业务奖励资格。
        /// </summary>
        /// <param name="placementId">已注册真实ID。</param>
        /// <param name="revenue">本次收益金额。</param>
        private void MarkRevenue(string placementId, double revenue)
        {
            var unit = FindAdUnit(placementId);
            if (unit != null) unit.Revenue = revenue;
        }

        /// <summary>
        /// Banner隐藏或销毁只重置自身槽位与单ID组，不触发全屏续杯。
        /// </summary>
        /// <param name="placementId">Banner真实ID。</param>
        protected void MarkBannerHidden(string placementId)
        {
            DispatchAdStateCallback(() =>
            {
                var unit = FindAdUnit(placementId);
                if (unit != null && unit.Format == AdFormat.Banner) ResetConsumedGroup(unit.Group);
            });
        }

        /// <summary>
        /// SDK调用完成只代表发出请求；异常统一成为当前活动项失败，不取消其他观察者。
        /// </summary>
        /// <param name="unit">已预留的活动项。</param>
        /// <param name="format">广告格式。</param>
        /// <param name="ct">渠道生命周期令牌。</param>
        private async UniTask InvokeOnRequestSafeAsync(AdUnit unit, AdFormat format, CancellationToken ct)
        {
            long roundId = unit.Group.RoundId;
            try
            {
                await OnRequestAsync(format, unit.PlacementId, ct);
            }
            catch (OperationCanceledException)
            {
                if (!m_IsDisposed) PostAdCallbackToMainThread(() =>
                {
                    if (unit.Group.RoundId == roundId) RaiseAdLoadFailed(BuildLoadFailure(format, unit.PlacementId, -3, "渠道加载调用已取消。"));
                });
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"[{Name}] OnRequestAsync({format}, {unit.PlacementId}) 异常：{ex.Message}");
                PostAdCallbackToMainThread(() =>
                {
                    if (unit.Group.RoundId == roundId) RaiseAdLoadFailed(BuildLoadFailure(format, unit.PlacementId, -3, ex.Message));
                });
            }
        }

        /// <summary>
        /// 释放全部广告组等待和业务观察；先摘除批次，再取消完成源以支持重入。
        /// </summary>
        private void CancelGroupRetriesAndBatches()
        {
            foreach (var groups in m_AdGroups.Values)
            {
                foreach (var group in groups)
                {
                    CancelGroupRetry(group);
                }
            }

            var batches = m_PendingBatches.ToArray();
            m_PendingBatches.Clear();
            foreach (var batch in batches)
            {
                batch.Registration.Dispose();
            }

            foreach (var batch in batches)
            {
                batch.Tcs.TrySetCanceled(ChannelLifetimeToken);
            }
        }

        /// <summary>
        /// 所有候选共同检查真实SDK Ready；过期槽位在主线程清空等待普通请求补充。
        /// </summary>
        /// <param name="unit">本地缓存槽位。</param>
        /// <returns>本地和原厂均可播才为true。</returns>
        private bool IsUnitReallyReady(AdUnit unit)
        {
            if (unit.State != AdUnitState.Ready) return false;
            if (IsSdkReady(unit)) return true;
            ResetConsumedGroup(unit.Group);
            return false;
        }

        /// <summary>
        /// 安全查询原厂Ready，查询异常按不可播处理。
        /// </summary>
        /// <param name="unit">真实ID槽位。</param>
        /// <returns>原厂仍可播时为true。</returns>
        private bool IsSdkReady(AdUnit unit)
        {
            try { return OnIsReady(unit.Format, unit.PlacementId); }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"[{Name}] 查询 {unit.Format}/{unit.PlacementId} Ready失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 以真实 Ready 筛选最高收益；相同收益保留注册顺序。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>可播槽位或null。</returns>
        private AdUnit PickBestReadyUnit(AdFormat format)
        {
            RequireAdMainThread();
            if (m_IsDisposed || !IsChannelInitialized || m_AdConfigFailures.ContainsKey(format) || !m_AdUnits.TryGetValue(format, out var units)) return null;
            AdUnit best = null;
            foreach (var unit in units)
            {
                bool ready = IsUnitReallyReady(unit);
                if (ready && (best == null || unit.Revenue > best.Revenue)) best = unit;
            }
            return best;
        }

        /// <summary>
        /// 检查展示回调是否属于当前占用，不接受未知、错误格式、重复displayed。
        /// </summary>
        /// <param name="result">原厂展示结果。</param>
        /// <returns>当前显示槽位，未匹配时为null。</returns>
        private AdUnit GetActiveShowUnit(AdResult result)
        {
            var unit = FindAdUnit(result.PlacementId);
            if (unit == null || unit.Format != result.Format || unit.State != AdUnitState.Showing) return null;
            if (!IsFullscreenAd(result.Format)) return unit;
            return m_ActiveAdShow != null && m_ActiveAdShow.Format == result.Format && m_ActiveAdShow.PlacementId == result.PlacementId ? unit : null;
        }

        /// <summary>
        /// 匹配真实终态后一次清除原生占用与消费组；调用方随后完成事件/原厂TCS。
        /// </summary>
        /// <param name="result">hidden或displayFailed结果。</param>
        /// <returns>本次消费槽位，重复终态为null。</returns>
        private AdUnit FinishActiveShow(AdResult result)
        {
            var unit = GetActiveShowUnit(result);
            if (unit == null) return null;
            if (IsFullscreenAd(result.Format))
            {
                m_ActiveAdShow = null;
            }
            return unit;
        }

        /// <summary>
        /// 真实终态后清空消费组，并普通检查全部组；仅消费组从首ID重启。
        /// </summary>
        /// <param name="unit">本次结束展示的槽位。</param>
        private void RefillAfterShow(AdUnit unit)
        {
            ResetConsumedGroup(unit.Group);
            if (unit.Format != AdFormat.Banner && !m_IsDisposed)
                RequestAsync(unit.Format, AdRequestReason.AutoRefill, null, CancellationToken.None).Forget();
        }

        /// <summary>
        /// 异步观察原厂show结果，业务取消只结束外层等待，异常结果统一排队收尾。
        /// </summary>
        /// <param name="unit">已占用的广告位。</param>
        /// <returns>原厂等待结果。</returns>
        private async UniTask<AdResult> InvokeOnShowSafeAsync(AdUnit unit)
        {
            var showContext = m_ActiveAdShow;
            try { return await OnShowAsync(unit.Format, unit.PlacementId, ChannelLifetimeToken); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                var failure = new AdResult { Format = unit.Format, PlacementId = unit.PlacementId, ErrorMessage = ex.Message };
                var completed = new UniTaskCompletionSource<AdResult>();
                PostAdCallbackToMainThread(() =>
                {
                    // 可识别的旧托管任务异常不能按同ID误终结后来的新Show；原厂无token限制仍保留。
                    try
                    {
                        if (!IsFullscreenAd(unit.Format) || m_ActiveAdShow == showContext) RaiseShowFailed(failure);
                    }
                    finally { completed.TrySetResult(failure); }
                });
                return await completed.Task.AttachExternalCancellation(ChannelLifetimeToken);
            }
        }

        /// <summary>
        /// 仅三种全屏格式参与渠道互斥，Banner保留原厂视图生命周期。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>激励、插屏或开屏时为true。</returns>
        private static bool IsFullscreenAd(AdFormat format)
            => format == AdFormat.Rewarded || format == AdFormat.Interstitial || format == AdFormat.AppOpen;

        /// <summary>
        /// 查找渠道内唯一真实ID；format在各回调门再次验证。
        /// </summary>
        /// <param name="placementId">真实ID。</param>
        /// <returns>对应槽位或null。</returns>
        private AdUnit FindAdUnit(string placementId)
        {
            foreach (var units in m_AdUnits.Values)
                foreach (var unit in units)
                    if (string.Equals(unit.PlacementId, placementId, StringComparison.Ordinal)) return unit;
            return null;
        }

        /// <summary>
        /// 构造失败载荷，保留原厂码或基类明确错误码。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">真实ID或null。</param>
        /// <param name="errorCode">失败码。</param>
        /// <param name="errorMessage">失败说明。</param>
        /// <returns>失败载荷。</returns>
        private static AdLoadResult BuildLoadFailure(AdFormat format, string placementId, int errorCode, string errorMessage)
            => new AdLoadResult { Format = format, PlacementId = placementId, ErrorCode = errorCode, ErrorMessage = errorMessage };
    }
}
