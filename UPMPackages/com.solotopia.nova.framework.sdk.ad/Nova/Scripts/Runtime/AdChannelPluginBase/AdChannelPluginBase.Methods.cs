/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.Methods.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   AdChannelPluginBase 私有工具方法
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
        /// 向指定 format 的状态机注册多个 AdUnit 槽位。
        /// 空 options 直接 Warning 跳过；同 placementId 重复时 Warning 跳过。
        /// 派生类须在 InitChannelSDKAsync 内部调用此方法完成槽位注入。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="options">槽位配置列表。</param>
        protected void RegisterAdUnits(AdFormat format, IReadOnlyList<AdUnitOptions> options)
        {
            if (options == null || options.Count == 0)
            {
                Log.Warning(LogTag.AD, $"[{Name}] RegisterAdUnits({format}) 传入空列表，跳过注册。");
                return;
            }
            if (!m_AdUnits.TryGetValue(format, out var list))
            {
                list = new List<AdUnit>(options.Count);
                m_AdUnits[format] = list;
            }
            for (int i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                if (string.IsNullOrEmpty(opt.PlacementId))
                {
                    Log.Warning(LogTag.AD, $"[{Name}] RegisterAdUnits({format}) 第 {i} 项 PlacementId 为空，跳过。");
                    continue;
                }
                if (FindAdUnit(opt.PlacementId) != null)
                {
                    Log.Warning(LogTag.AD, $"[{Name}] RegisterAdUnits({format}) PlacementId={opt.PlacementId} 重复，跳过。");
                    continue;
                }
                list.Add(new AdUnit
                {
                    PlacementId = opt.PlacementId,
                    Format = format,
                    State = AdUnitState.Idle,
                    Revenue = 0,
                    RetryCount = 0,
                    LastErrorMessage = null,
                    RetryCts = null,
                });
            }
        }

        /// <summary>
        /// 广告加载成功后推进状态机：找到对应 AdUnit，取消 pending 重试任务，置 Ready 并重置重试计数。
        /// 取消旧 RetryCts 防止 DelayedRetryAsync 倒计时结束后把已 Ready 的槽位回置 Loading。
        /// </summary>
        /// <param name="placementId">加载成功的广告位标识。</param>
        /// <param name="revenue">加载时 SDK 报告的预期收益（USD），无法获取时传 0。</param>
        private void MarkLoaded(string placementId, double revenue)
        {
            var unit = FindAdUnit(placementId);
            if (unit == null) return;
            if (unit.RetryCts != null)
            {
                unit.RetryCts.Cancel();
                unit.RetryCts.Dispose();
                unit.RetryCts = null;
            }
            unit.State = AdUnitState.Ready;
            unit.Revenue = revenue;
            unit.RetryCount = 0;
            unit.LastErrorMessage = null;
        }

        /// <summary>
        /// 广告加载失败后推进状态机（solar RetryRequestAd 语义）：
        /// 未达全局 RetryLoadAdMaxNum 时立即发起下一次 OnRequestAsync；达到上限后等待 RetryLoadAdInterv 秒，
        /// 计数清零后继续无限重试。槽位回置 Idle 表示当前轮次已结束，避免重复请求时被 Loading 卡住。
        /// </summary>
        /// <param name="placementId">加载失败的广告位标识。</param>
        /// <param name="err">失败错误描述。</param>
        private void MarkLoadFailed(string placementId, string err)
        {
            var unit = FindAdUnit(placementId);
            if (unit == null) return;
            unit.LastErrorMessage = err;
            unit.State = AdUnitState.Idle;
            unit.RetryCount++;
            if (unit.RetryCount < RetryLoadAdMaxNum)
            {
                Log.Debug(LogTag.AD, $"[{Name}] 广告位 {placementId} 立即重试（{unit.RetryCount}/{RetryLoadAdMaxNum}）。");
                ImmediateRetry(unit);
                return;
            }
            Log.Debug(LogTag.AD, $"[{Name}] 广告位 {placementId} 重试达到上限 {RetryLoadAdMaxNum}，等待 {RetryLoadAdInterv} 秒后清零继续。");
            ScheduleRetry(unit);
        }

        /// <summary>
        /// 广告收益回调后更新对应 AdUnit 的 Revenue 字段。
        /// </summary>
        /// <param name="placementId">广告位标识。</param>
        /// <param name="revenue">本次 SDK 报告的收益值（USD）。</param>
        private void MarkRevenue(string placementId, double revenue)
        {
            var unit = FindAdUnit(placementId);
            if (unit == null) return;
            unit.Revenue = revenue;
        }

        /// <summary>
        /// 广告展示结束后推进状态机：Showing 回置 Idle；非 Banner 自动触发续杯请求（fire-and-forget）。
        /// 续杯前清除 ShowCustomProps，以 AutoRefill 原因和空 customProps 调 RequestAsync；结果不被观察。
        /// </summary>
        /// <param name="placementId">展示结束的广告位标识。</param>
        private void MarkShown(string placementId)
        {
            var unit = FindAdUnit(placementId);
            if (unit == null) return;
            unit.State = AdUnitState.Idle;
            unit.Revenue = 0;
            if (unit.Format != AdFormat.Banner)
            {
                unit.ShowCustomProps = null;
                RequestAsync(unit.Format, AdRequestReason.AutoRefill, null, CancellationToken.None).Forget();
            }
        }

        /// <summary>
        /// Banner 隐藏/销毁后推进状态机：Showing 回置 Idle（Banner 不触发续杯）。
        /// 派生类在 HideBanner / DestroyBanner 回调中调用。
        /// </summary>
        /// <param name="placementId">Banner 广告位标识。</param>
        protected void MarkBannerHidden(string placementId)
        {
            var unit = FindAdUnit(placementId);
            if (unit == null) return;
            unit.State = AdUnitState.Idle;
            unit.Revenue = 0;
        }

        /// <summary>
        /// 安全包装 OnRequestAsync，捕获派生侧同步异常并将 AdUnit 回置 Idle 而不是卡在 Loading。
        /// OperationCanceledException 单独处理，不打印 Warning 日志，槽位同样回置 Idle 以防永久卡 Loading。
        /// </summary>
        /// <param name="unit">目标 AdUnit 槽位。</param>
        /// <param name="format">广告格式。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTask InvokeOnRequestSafeAsync(AdUnit unit, AdFormat format, CancellationToken ct)
        {
            try
            {
                await OnRequestAsync(format, unit.PlacementId, ct);
            }
            catch (OperationCanceledException)
            {
                unit.State = AdUnitState.Idle;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.AD, $"[{Name}] OnRequestAsync({format}, {unit.PlacementId}) 同步异常，槽位回置 Idle：{ex.Message}");
                unit.State = AdUnitState.Idle;
            }
        }

        /// <summary>
        /// 取消并清理所有 AdUnit 持有的 RetryCts，同时取消所有待定批次，防止资源泄露。
        /// 所有活跃 RequestBatch 以 OperationCanceledException 结束。
        /// </summary>
        private void CancelAllRetryCts()
        {
            foreach (var list in m_AdUnits.Values)
                for (int i = 0; i < list.Count; i++)
                {
                    var unit = list[i];
                    if (unit.RetryCts == null) continue;
                    unit.RetryCts.Cancel();
                    unit.RetryCts.Dispose();
                    unit.RetryCts = null;
                }
            for (int i = 0; i < m_PendingBatches.Count; i++)
            {
                var batch = m_PendingBatches[i];
                batch.Registration.Dispose();
                batch.Tcs.TrySetCanceled();
            }
            m_PendingBatches.Clear();
        }

        /// <summary>
        /// 从指定 format 的 Ready 槽位中按 Revenue 降序挑选最优 AdUnit；无就绪槽位时返回 null。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>eCPM 最高的 Ready AdUnit，或 null。</returns>
        private AdUnit PickBestReadyUnit(AdFormat format)
        {
            if (!m_AdUnits.TryGetValue(format, out var units)) return null;
            AdUnit best = null;
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (unit.State != AdUnitState.Ready) continue;
                if (best == null || unit.Revenue > best.Revenue) best = unit;
            }
            return best;
        }

        /// <summary>
        /// 立即重试：在当前调用栈外通过 UniTask.Yield 推迟一帧后重新发起 OnRequestAsync，
        /// 避免在 SDK 失败回调内同步深递归。每次调度前取消已有 RetryCts 防止重叠。
        /// </summary>
        /// <param name="u">需要立即重试的 AdUnit 槽位。</param>
        private void ImmediateRetry(AdUnit u)
        {
            if (u.RetryCts != null)
            {
                u.RetryCts.Cancel();
                u.RetryCts.Dispose();
            }
            u.RetryCts = new CancellationTokenSource();
            var ct = u.RetryCts.Token;
            ImmediateRetryAsync(u, ct).Forget();
        }

        /// <summary>
        /// 立即重试异步实现：让出一帧后将槽位置回 Loading 并调用 OnRequestAsync。
        /// 让出帧的目的是把递归打散为顺序调度，避免深栈和回调风暴。
        /// </summary>
        /// <param name="u">需要立即重试的 AdUnit 槽位。</param>
        /// <param name="ct">用于取消重试的令牌。</param>
        private async UniTaskVoid ImmediateRetryAsync(AdUnit u, CancellationToken ct)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            u.State = AdUnitState.Loading;
            await InvokeOnRequestSafeAsync(u, u.Format, ct);
        }

        /// <summary>
        /// 启动延时重试任务：等待全局 RetryLoadAdInterv 秒后将 RetryCount 清零、置回 Loading 并调用 OnRequestAsync。
        /// 每次调度前先取消已有的 RetryCts 防止重叠。
        /// </summary>
        /// <param name="u">需要重试的 AdUnit 槽位。</param>
        private void ScheduleRetry(AdUnit u)
        {
            if (u.RetryCts != null)
            {
                u.RetryCts.Cancel();
                u.RetryCts.Dispose();
            }
            u.RetryCts = new CancellationTokenSource();
            var ct = u.RetryCts.Token;
            DelayedRetryAsync(u, ct).Forget();
        }

        /// <summary>
        /// 延时重试异步实现：等待全局 RetryLoadAdInterv 秒后清零 RetryCount，置 Loading 并调用 OnRequestAsync。
        /// ct 被取消时 UniTask.Delay 抛出 OperationCanceledException，由 try/catch 捕获后安全退出，不再推进状态机。
        /// </summary>
        /// <param name="u">需要重试的 AdUnit 槽位。</param>
        /// <param name="ct">用于取消等待的令牌。</param>
        private async UniTaskVoid DelayedRetryAsync(AdUnit u, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(RetryLoadAdInterv), cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            u.RetryCount = 0;
            u.State = AdUnitState.Loading;
            await InvokeOnRequestSafeAsync(u, u.Format, ct);
        }

        /// <summary>
        /// 在所有已注册的 AdUnit 中按 placementId 查找目标槽位；未找到时返回 null。
        /// </summary>
        /// <param name="placementId">要查找的广告位唯一标识。</param>
        /// <returns>匹配的 AdUnit，或 null。</returns>
        private AdUnit FindAdUnit(string placementId)
        {
            foreach (var list in m_AdUnits.Values)
                for (int i = 0; i < list.Count; i++)
                    if (list[i].PlacementId == placementId) return list[i];
            return null;
        }

        /// <summary>
        /// 构造统一的加载失败结果。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="placementId">广告位唯一标识，可为 null。</param>
        /// <param name="errorCode">错误码。</param>
        /// <param name="errorMessage">错误描述。</param>
        /// <returns>Success=false 的加载结果。</returns>
        private static AdLoadResult BuildLoadFailure(AdFormat format, string placementId, int errorCode, string errorMessage)
        {
            return new AdLoadResult
            {
                Success = false,
                Format = format,
                PlacementId = placementId,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
            };
        }
    }
}
