/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelPluginBase.Groups.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   广告组串行加载、轮次观察与循环间隔重试
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
        /// 解析并累加注册广告组；先完成配置和 ID 归属校验，再创建组与槽位。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="groupExpressions">每项一组，项内英文逗号或竖线分隔串行广告 ID。</param>
        protected void RegisterAdGroups(AdFormat format, IReadOnlyList<string> groupExpressions)
        {
            RequireAdMainThread();
            if (m_IsDisposed)
            {
                return;
            }

            var plan = AdGroupConfigParser.Parse(format, groupExpressions);
            foreach (string warning in plan.Warnings)
            {
                Log.Warning(LogTag.AD, $"[{Name}] {warning}");
            }

            if (!plan.IsValid)
            {
                RegisterAdConfigFailure(format, string.Join("；", plan.Errors));
                return;
            }

            // 本次计划中的重复 ID 已由解析器检查，这里只检查与已有注册的冲突。
            foreach (string placementId in plan.EffectivePlacementIds)
            {
                var existingUnit = FindAdUnit(placementId);
                if (existingUnit == null)
                {
                    continue;
                }

                string error = $"广告ID {placementId} 已在本渠道注册，不能归属多个广告组或格式。";
                RegisterAdConfigFailure(existingUnit.Format, error);
                RegisterAdConfigFailure(format, error);
                return;
            }

            // 整份计划校验完成后再登记，避免失败时只注册了一部分广告组。
            foreach (var placementIds in plan.Groups)
            {
                RegisterGroupUnits(format, placementIds);
            }
        }

        /// <summary>
        /// 记录格式配置错误，使业务请求明确失败且不向原厂发加载。
        /// </summary>
        /// <param name="format">无效广告格式。</param>
        /// <param name="message">配置错误说明。</param>
        protected void RegisterAdConfigFailure(AdFormat format, string message)
        {
            RequireAdMainThread();
            if (m_IsDisposed)
            {
                return;
            }

            m_AdConfigFailures[format] = message;
            Log.Warning(LogTag.AD, $"[{Name}] {format} 配置无效：{message}");
        }

        /// <summary>
        /// 返回已注册真实 ID 的只读快照，保留组序和组内顺序。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <returns>真实 ID 快照，未配置或配置无效时为空。</returns>
        protected IReadOnlyList<string> GetRegisteredPlacementIds(AdFormat format)
        {
            RequireAdMainThread();
            var ids = new List<string>();
            if (m_IsDisposed || m_AdConfigFailures.ContainsKey(format))
            {
                return ids.AsReadOnly();
            }

            if (!m_AdUnits.TryGetValue(format, out var units))
            {
                return ids.AsReadOnly();
            }

            foreach (var unit in units)
            {
                ids.Add(unit.PlacementId);
            }

            return ids.AsReadOnly();
        }

        /// <summary>
        /// 注册一个已校验广告组；旧入口用单元素列表调用，保持累加行为。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="ids">非空、唯一的真实 ID 列表。</param>
        private void RegisterGroupUnits(AdFormat format, IReadOnlyList<string> ids)
        {
            if (!m_AdGroups.TryGetValue(format, out var groups))
            {
                groups = new List<AdGroup>();
                m_AdGroups[format] = groups;
            }

            if (!m_AdUnits.TryGetValue(format, out var units))
            {
                units = new List<AdUnit>();
                m_AdUnits[format] = units;
            }

            var group = new AdGroup
            {
                Format = format,
                State = AdGroupState.Idle
            };

            foreach (string id in ids)
            {
                var unit = new AdUnit
                {
                    PlacementId = id,
                    Format = format,
                    State = AdUnitState.Idle,
                    Group = group
                };

                group.Units.Add(unit);
                units.Add(unit);
            }

            groups.Add(group);
        }

        /// <summary>
        /// 主线程执行请求的两阶段调度；先注册完整轮次观察，再发任何原厂加载。
        /// </summary>
        /// <param name="format">广告格式。</param>
        /// <param name="reason">仅新轮使用的原因。</param>
        /// <param name="customProps">仅新轮复制的属性。</param>
        /// <param name="ct">只取消本次观察的令牌。</param>
        /// <returns>首个可播结果或所有观察轮的最近失败。</returns>
        private UniTask<AdLoadResult> RequestGroups(AdFormat format, AdRequestReason reason, Dictionary<string, object> customProps, CancellationToken ct)
        {
            RequireAdMainThread();

            // 先检查请求、渠道和配置，任何失败都不进入组调度。
            if (ct.IsCancellationRequested)
            {
                return UniTask.FromCanceled<AdLoadResult>(ct);
            }

            if (m_IsDisposed)
            {
                return UniTask.FromCanceled<AdLoadResult>(ChannelLifetimeToken);
            }

            if (m_AdConfigFailures.TryGetValue(format, out string error))
            {
                return UniTask.FromResult(BuildLoadFailure(format, null, -10, error));
            }

            if (!IsChannelInitialized)
            {
                return UniTask.FromResult(BuildLoadFailure(format, null, -11, "渠道SDK尚未初始化成功。"));
            }

            if (!m_AdGroups.TryGetValue(format, out var groups) || groups.Count == 0)
            {
                return UniTask.FromResult(BuildLoadFailure(format, null, -1, $"[{Name}] 未注册 {format} 广告位。"));
            }

            var batch = new RequestBatch
            {
                Tcs = new UniTaskCompletionSource<AdLoadResult>()
            };
            var groupsToStart = new List<AdGroup>();
            AdLoadResult cachedResult = null;

            // 第一阶段只检查缓存、预留新轮并收集观察目标，不调用原厂加载。
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group.State == AdGroupState.Ready)
                {
                    var unit = group.Units[group.ActiveUnitIndex];
                    if (IsUnitReallyReady(unit))
                    {
                        if (cachedResult == null)
                        {
                            cachedResult = new AdLoadResult
                            {
                                Success = true,
                                Format = format,
                                PlacementId = unit.PlacementId,
                                Revenue = unit.Revenue
                            };
                        }

                        continue;
                    }

                    // 不可播缓存回到空闲，本次即可从组首预留新轮。
                    ResetConsumedGroup(group);
                }

                switch (group.State)
                {
                    case AdGroupState.Idle:
                        ReserveGroupRound(group, reason, customProps);
                        groupsToStart.Add(group);
                        batch.PendingRounds[group] = group.RoundId;
                        break;

                    case AdGroupState.Loading:
                        // 重叠请求只观察已有轮次，不覆盖其原因和属性。
                        batch.PendingRounds[group] = group.RoundId;
                        break;

                    case AdGroupState.WaitingRetry:
                        if (group.LastFailure != null)
                        {
                            batch.LastFailure = group.LastFailure;
                        }
                        break;

                    case AdGroupState.Showing:
                        // 展示中的组由展示收尾补充，当前请求不抢占。
                        break;
                }
            }

            // 有缓存时直接复用结果；无缓存时先登记全部轮次和取消回调。
            // 此判断必须在加载前冻结，同步回调可能立即清空 PendingRounds。
            bool observeRounds = batch.PendingRounds.Count > 0 && cachedResult == null;
            if (observeRounds)
            {
                m_PendingBatches.Add(batch);
                if (ct.CanBeCanceled)
                {
                    batch.Registration = ct.Register(() =>
                    {
                        PostAdCallbackToMainThread(() => CancelRequestBatch(batch, ct));
                    });
                }
            }

            // 第二阶段才发原厂加载；已有缓存也要补充其他空闲组。
            foreach (var group in groupsToStart)
            {
                InvokeGroupUnit(group, group.RoundId);
            }

            // 原厂调用和打点可能重入释放，返回结果前再次确认渠道存活。
            if (m_IsDisposed)
            {
                return UniTask.FromCanceled<AdLoadResult>(ChannelLifetimeToken);
            }

            if (cachedResult != null)
            {
                return UniTask.FromResult(cachedResult);
            }

            if (observeRounds)
            {
                return batch.Tcs.Task;
            }

            // 只有等待重试或展示中的组时，立即返回，不建立无终点观察。
            var failure = batch.LastFailure ?? BuildLoadFailure(format, null, -2, $"[{Name}] 当前没有可请求的 {format} 广告组。");
            return UniTask.FromResult(failure);
        }

        /// <summary>
        /// 预留新轮首项与属性，不执行原厂调用；加入现有轮不会进入此方法。
        /// </summary>
        /// <param name="group">目标空闲组。</param>
        /// <param name="reason">新轮请求原因。</param>
        /// <param name="customProps">新轮属性，将浅复制字典结构。</param>
        private void ReserveGroupRound(AdGroup group, AdRequestReason reason, Dictionary<string, object> customProps)
        {
            group.RoundId++;
            group.State = AdGroupState.Loading;
            group.ActiveUnitIndex = 0;
            group.Reason = reason;
            group.RequestCustomProps = SnapshotAdProps(customProps);
            ReserveActiveUnit(group);
            // 手动请求已有业务入口日志；展示后补量和自动重试只记录各组的新轮。
            if (reason == AdRequestReason.AutoRefill || reason == AdRequestReason.Retry)
            {
                Log.Debug(LogTag.AD, $"[{Name}] 广告组准备新一轮加载：format={group.Format}，group={group.Units[0].PlacementId}，round={group.RoundId}，reason={reason}，placement={group.Units[0].PlacementId}，items={group.Units.Count}；从首项开始。");
            }
        }

        /// <summary>
        /// 在发起加载之前标记当前首项或后项，防止重复请求形成第二条组内加载。
        /// </summary>
        /// <param name="group">当前加载中的组。</param>
        private void ReserveActiveUnit(AdGroup group)
        {
            var unit = group.Units[group.ActiveUnitIndex];
            unit.State = AdUnitState.Loading;
            unit.LoadTerminalReceived = false;
            unit.LastReason = group.Reason;
            unit.RequestCustomProps = group.RequestCustomProps;
        }

        /// <summary>
        /// 只对仍有效的当前轮当前项发一次原厂加载。
        /// </summary>
        /// <param name="group">已预留的加载组。</param>
        /// <param name="roundId">预留时的托管轮次。</param>
        private void InvokeGroupUnit(AdGroup group, long roundId)
        {
            if (m_IsDisposed || !IsChannelInitialized || m_AdConfigFailures.ContainsKey(group.Format))
            {
                return;
            }

            if (group.State != AdGroupState.Loading || group.RoundId != roundId)
            {
                return;
            }

            var unit = group.Units[group.ActiveUnitIndex];
            TrackAdRequest(group.Format, unit.PlacementId, group.Reason, group.RequestCustomProps);
            // 打点插件可同步重入释放或请求，原厂调用前必须再次确认本轮仍有效。
            if (m_IsDisposed || !IsChannelInitialized)
            {
                return;
            }

            if (group.State != AdGroupState.Loading || group.RoundId != roundId)
            {
                return;
            }

            if (group.Units[group.ActiveUnitIndex] != unit)
            {
                return;
            }

            Log.Debug(LogTag.AD, $"[{Name}] 调用渠道请求广告：format={group.Format}，group={group.Units[0].PlacementId}，round={roundId}，reason={group.Reason}，placement={unit.PlacementId}，item={group.ActiveUnitIndex + 1}/{group.Units.Count}。");
            InvokeOnRequestSafeAsync(unit, group.Format, ChannelLifetimeToken).Forget();
        }

        /// <summary>
        /// 检查加载终态是否属于本实例当前活动项；无法识别原厂同 ID跨轮的迟到回调。
        /// </summary>
        /// <param name="e">原厂加载终态快照。</param>
        /// <returns>活动槽位或null；重复、未知、错格式回调不推进组。</returns>
        private AdUnit AcceptLoadTerminal(AdLoadResult e)
        {
            var unit = FindAdUnit(e.PlacementId);
            if (m_IsDisposed || !IsChannelInitialized)
            {
                return null;
            }

            if (unit == null || unit.Format != e.Format)
            {
                return null;
            }

            if (unit.State != AdUnitState.Loading || unit.LoadTerminalReceived)
            {
                return null;
            }

            var group = unit.Group;
            if (group.State != AdGroupState.Loading || group.Units[group.ActiveUnitIndex] != unit)
            {
                return null;
            }

            unit.LoadTerminalReceived = true;
            return unit;
        }

        /// <summary>
        /// 加载成功回调需确认原厂真实 Ready；首次不可播时只复查一个 Update，不重复当前 ID。
        /// </summary>
        /// <param name="e">加载成功结果快照。</param>
        private void ProcessAdLoaded(AdLoadResult e)
        {
            var unit = AcceptLoadTerminal(e);
            if (unit == null)
            {
                RefreshPassiveBanner(e);
                return;
            }
            if (IsSdkReady(unit))
            {
                CompleteGroupLoaded(unit, e);
            }
            else
            {
                RecheckLoadedReadyAsync(unit, unit.Group.RoundId, e).Forget();
            }
        }

        /// <summary>
        /// Banner 原厂自主刷新可更新既有视图缓存，不作为全屏主动轮终态。
        /// </summary>
        /// <param name="e">Banner 加载成功快照。</param>
        private void RefreshPassiveBanner(AdLoadResult e)
        {
            if (!IsChannelInitialized || e.Format != AdFormat.Banner)
            {
                return;
            }

            var unit = FindAdUnit(e.PlacementId);
            if (unit == null || unit.Format != AdFormat.Banner)
            {
                return;
            }

            if (unit.State == AdUnitState.Loading || !IsSdkReady(unit))
            {
                return;
            }

            var group = unit.Group;
            CancelGroupRetry(group);
            group.LastFailure = null;
            group.RetryIntervalIndex = 0;
            unit.Revenue = e.Revenue;
            if (unit.State != AdUnitState.Showing)
            {
                group.State = AdGroupState.Ready;
                unit.State = AdUnitState.Ready;
            }
            TrackLoadedResult(unit, e);
            if (!m_IsDisposed)
            {
                OnAdLoaded?.Invoke(e);
            }
        }

        /// <summary>
        /// 一次 Update 复查加载成功后的状态，仍不可播按当前尝试失败推进。
        /// </summary>
        /// <param name="unit">仍需复查的活动项。</param>
        /// <param name="roundId">回调发生时的托管轮次。</param>
        /// <param name="e">加载成功结果快照。</param>
        private async UniTask RecheckLoadedReadyAsync(AdUnit unit, long roundId, AdLoadResult e)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ChannelLifetimeToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            PostAdCallbackToMainThread(() =>
            {
                var group = unit.Group;
                if (group.State != AdGroupState.Loading || group.RoundId != roundId)
                {
                    return;
                }

                if (group.Units[group.ActiveUnitIndex] != unit)
                {
                    return;
                }

                if (IsSdkReady(unit))
                {
                    CompleteGroupLoaded(unit, e);
                }
                else
                {
                    var failure = BuildLoadFailure(unit.Format, unit.PlacementId, -13, "原厂loaded后复查仍不可播放。");
                    CompleteGroupFailed(unit, failure);
                }
            });
        }

        /// <summary>
        /// 当前轮成功停止后项并重置间隔下标；先更新缓存和打点，再完成观察者。
        /// </summary>
        /// <param name="unit">当前已确认可播项。</param>
        /// <param name="e">加载成功结果。</param>
        private void CompleteGroupLoaded(AdUnit unit, AdLoadResult e)
        {
            var group = unit.Group;
            CancelGroupRetry(group);
            group.State = AdGroupState.Ready;
            group.LastFailure = null;
            group.RetryIntervalIndex = 0;
            unit.State = AdUnitState.Ready;
            unit.Revenue = e.Revenue;
            unit.LastErrorMessage = null;
            TrackLoadedResult(unit, e);
            NotifyGroupRound(group, e);
            if (!m_IsDisposed)
            {
                OnAdLoaded?.Invoke(e);
            }
        }

        /// <summary>
        /// 当前项失败仅推进后项；末项失败才结算整轮并启动本组循环间隔等待。
        /// </summary>
        /// <param name="e">原厂加载失败结果。</param>
        private void ProcessAdLoadFailed(AdLoadResult e)
        {
            var unit = AcceptLoadTerminal(e);
            if (unit != null)
            {
                CompleteGroupFailed(unit, e);
            }
        }

        /// <summary>
        /// 处理已接受的失败；轮内后项走 FIFO 队列，避免同步回调深层递归。
        /// </summary>
        /// <param name="unit">失败活动项。</param>
        /// <param name="e">失败结果快照。</param>
        private void CompleteGroupFailed(AdUnit unit, AdLoadResult e)
        {
            var group = unit.Group;
            unit.State = AdUnitState.Idle;
            unit.LastErrorMessage = e.ErrorMessage;
            unit.Revenue = 0;
            group.LastFailure = e;
            bool exhausted = group.ActiveUnitIndex + 1 == group.Units.Count;
            if (exhausted)
            {
                group.State = AdGroupState.WaitingRetry;
                ScheduleGroupRetry(group);
            }
            else
            {
                group.ActiveUnitIndex++;
                ReserveActiveUnit(group);
                long round = group.RoundId;
                PostAdCallbackToMainThread(() => InvokeGroupUnit(group, round));
            }
            TrackFailedResult(unit, e);
            if (exhausted)
            {
                NotifyGroupRound(group, e);
            }

            if (!m_IsDisposed)
            {
                OnAdLoadFailed?.Invoke(e);
            }
        }

        /// <summary>
        /// 从全部匹配观察者先摘除，再完成任务，允许续接重入请求或释放。
        /// </summary>
        /// <param name="group">完成的组。</param>
        /// <param name="result">本轮成功或末项失败。</param>
        private void NotifyGroupRound(AdGroup group, AdLoadResult result)
        {
            var completed = new List<RequestBatch>();
            for (int i = m_PendingBatches.Count - 1; i >= 0; i--)
            {
                var batch = m_PendingBatches[i];
                if (!batch.PendingRounds.TryGetValue(group, out long round) || round != group.RoundId)
                {
                    continue;
                }

                batch.PendingRounds.Remove(group);
                if (!result.Success)
                {
                    batch.LastFailure = result;
                    if (batch.PendingRounds.Count != 0)
                    {
                        continue;
                    }
                }

                m_PendingBatches.RemoveAt(i);
                batch.Registration.Dispose();
                completed.Add(batch);
            }
            // 全部已完成批次先移出集合，再触发可能重入的业务续接。
            foreach (var batch in completed)
            {
                var batchResult = result.Success ? result : batch.LastFailure;
                batch.Tcs.TrySetResult(batchResult);
            }
        }

        /// <summary>
        /// 取消只摘除单个业务观察者，保持所有原厂加载和其他观察者。
        /// </summary>
        /// <param name="batch">待取消批次。</param>
        /// <param name="ct">调用方令牌。</param>
        private void CancelRequestBatch(RequestBatch batch, CancellationToken ct)
        {
            if (!m_PendingBatches.Remove(batch))
            {
                return;
            }

            batch.Registration.Dispose();
            batch.Tcs.TrySetCanceled(ct);
        }

        /// <summary>
        /// 整组失败后启动独立等待，并循环推进本组的全局间隔下标。
        /// </summary>
        /// <param name="group">已经进入 WaitingRetry的组。</param>
        private void ScheduleGroupRetry(AdGroup group)
        {
            CancelGroupRetry(group);

            // 只有整组耗尽才取间隔并推进一次；重新注入较短配置时仍保持有效下标。
            var intervals = GroupRetryIntervalsSeconds;
            int intervalIndex = group.RetryIntervalIndex % intervals.Count;
            float seconds = intervals[intervalIndex];
            group.RetryIntervalIndex = (intervalIndex + 1) % intervals.Count;

            group.RetryCancellation = CancellationTokenSource.CreateLinkedTokenSource(ChannelLifetimeToken);
            RetryGroupAsync(group, group.RoundId, seconds, group.RetryCancellation).Forget();
        }

        /// <summary>
        /// 等待本轮选定的真实秒数；到期仅重启仍在等待的同一轮组。
        /// </summary>
        /// <param name="group">目标组。</param>
        /// <param name="roundId">失败轮次。</param>
        /// <param name="seconds">本次整组失败选定的等待秒数，不受后续下标变化影响。</param>
        /// <param name="retryCancellation">本次等待的身份与取消源。</param>
        private async UniTask RetryGroupAsync(AdGroup group, long roundId, float seconds, CancellationTokenSource retryCancellation)
        {
            try
            {
                await WaitForGroupRetryAsync(seconds, retryCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Error(LogTag.AD, $"[{Name}] 广告组等待失败：format={group.Format}，group={group.Units[0].PlacementId}，round={roundId}，error={ex}");
                return;
            }

            PostAdCallbackToMainThread(() =>
            {
                if (group.State != AdGroupState.WaitingRetry || group.RoundId != roundId)
                {
                    return;
                }

                if (group.RetryCancellation != retryCancellation)
                {
                    return;
                }

                CancelGroupRetry(group);
                ReserveGroupRound(group, AdRequestReason.Retry, null);
                InvokeGroupUnit(group, group.RoundId);
            });
        }

        /// <summary>
        /// 按真实秒数等待，不受 timeScale 影响；派生渠道可重写等待实现。
        /// </summary>
        /// <param name="seconds">本轮从全局间隔列表选定的有限正数。</param>
        /// <param name="ct">本组与渠道生命周期取消令牌。</param>
        /// <returns>等待完成任务。</returns>
        protected virtual UniTask WaitForGroupRetryAsync(float seconds, CancellationToken ct)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: ct);
        }

        /// <summary>
        /// 取消并释放本组等待；先清身份再取消，避免同步续接误判仍有效。
        /// </summary>
        /// <param name="group">需要停止等待的组。</param>
        private void CancelGroupRetry(AdGroup group)
        {
            var retry = group.RetryCancellation;
            group.RetryCancellation = null;
            if (retry == null)
            {
                return;
            }

            retry.Cancel();
            retry.Dispose();
        }

        /// <summary>
        /// 采纳原厂自主 expiry 缓存，不完成主动加载轮或复活已结束批次。
        /// </summary>
        /// <param name="format">原厂事件格式。</param>
        /// <param name="placementId">真实缓存 ID。</param>
        /// <param name="revenue">替换缓存的预期收益。</param>
        protected void RaiseAdCacheReplaced(AdFormat format, string placementId, double revenue)
        {
            DispatchAdStateCallback(() =>
            {
                var unit = FindAdUnit(placementId);
                if (!IsChannelInitialized || unit == null || unit.Format != format)
                {
                    return;
                }

                var group = unit.Group;
                if (group.State == AdGroupState.Loading || group.State == AdGroupState.Showing)
                {
                    return;
                }
                if (group.State == AdGroupState.Ready && group.Units[group.ActiveUnitIndex] != unit)
                {
                    return;
                }

                if (!IsSdkReady(unit))
                {
                    return;
                }

                CancelGroupRetry(group);
                group.State = AdGroupState.Ready;
                group.ActiveUnitIndex = group.Units.IndexOf(unit);
                group.LastFailure = null;
                group.RetryIntervalIndex = 0;
                unit.State = AdUnitState.Ready;
                unit.Revenue = revenue;
                unit.LastErrorMessage = null;
            });
        }

        /// <summary>
        /// 消费或失效缓存回空闲，下一次普通检查从组首项补充。
        /// </summary>
        /// <param name="group">被消费或已不可播的组。</param>
        private void ResetConsumedGroup(AdGroup group)
        {
            foreach (var unit in group.Units)
            {
                unit.State = AdUnitState.Idle;
                unit.Revenue = 0;
                unit.ShowCustomProps = null;
            }
            group.State = AdGroupState.Idle;
            group.ActiveUnitIndex = 0;
        }
    }
}
