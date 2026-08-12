/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileProductFetchCoordinator.cs
 * author:    yingzheng
 * created:   2026/8/11
 * descrip:   移动端官方内购商品拉取状态机与自动重试协调器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 移动端官方内购商品拉取状态机。
    /// 负责 FetchProducts 幂等、自动重试、部分成功判定、迟到失败短路和不可用 SKU 校正。
    /// </summary>
    internal sealed class MobileProductFetchCoordinator
    {
        /// <summary>
        /// 默认商品拉取重试延迟表，单位毫秒。
        /// </summary>
        private static readonly int[] s_DefaultRetryDelaysMs = { 2000, 5000, 10000 };

        /// <summary>
        /// 判断移动端官方内购当前是否仍处于可发起商品拉取的就绪状态。
        /// </summary>
        private readonly Func<bool> m_IsReady;

        /// <summary>
        /// 获取本轮需要提交给 Unity IAP 的商品定义列表。
        /// </summary>
        private readonly Func<IReadOnlyList<ProductDefinition>> m_GetProductDefinitions;

        /// <summary>
        /// 向平台发起商品拉取请求的委托。
        /// </summary>
        private readonly Action<IReadOnlyList<ProductDefinition>> m_FetchProducts;

        /// <summary>
        /// 判断指定平台商品 ID 当前是否已经存在于 StoreController。
        /// </summary>
        private readonly Func<string, bool> m_HasProduct;

        /// <summary>
        /// 清空旧不可用 SKU 缓存的委托。
        /// </summary>
        private readonly Action m_ClearUnavailableSkus;

        /// <summary>
        /// 写入当前仍缺失 SKU 到不可用集合的委托。
        /// </summary>
        private readonly Action<string> m_AddUnavailableSku;

        /// <summary>
        /// 商品首次进入成功态后触发后续 Restore 和已有购买拉取的委托。
        /// </summary>
        private readonly Action m_OnPostFetchCompleted;

        /// <summary>
        /// 延迟执行商品重试的异步等待委托，测试中可替换。
        /// </summary>
        private readonly Func<int, CancellationToken, UniTask> m_DelayAsync;

        /// <summary>
        /// 当前协调器使用的商品拉取重试延迟表。
        /// </summary>
        private readonly IReadOnlyList<int> m_RetryDelaysMs;

        /// <summary>
        /// 商品拉取完成等待信号，桥接成功或失败回调到等待方。
        /// </summary>
        private UniTaskCompletionSource<MobileProductFetchState> m_ProductFetchTcs;

        /// <summary>
        /// 当前悬空商品拉取重试任务的取消源。
        /// </summary>
        private CancellationTokenSource m_ProductFetchRetryCts;

        /// <summary>
        /// 后置 Restore / FetchPurchases 流程是否已经触发过。
        /// </summary>
        private bool m_HasCompletedPostFetchFlow;

        /// <summary>
        /// 当前商品拉取状态。
        /// </summary>
        internal MobileProductFetchState State { get; private set; }

        /// <summary>
        /// 已调度的商品拉取重试次数。
        /// </summary>
        internal int RetryIndex { get; private set; }

        /// <summary>
        /// 构造商品拉取协调器。
        /// </summary>
        internal MobileProductFetchCoordinator(
            Func<bool> isReady,
            Func<IReadOnlyList<ProductDefinition>> getProductDefinitions,
            Action<IReadOnlyList<ProductDefinition>> fetchProducts,
            Func<string, bool> hasProduct,
            Action clearUnavailableSkus,
            Action<string> addUnavailableSku,
            Action onPostFetchCompleted,
            Func<int, CancellationToken, UniTask> delayAsync = null,
            IReadOnlyList<int> retryDelaysMs = null)
        {
            m_IsReady = isReady ?? (() => false);
            m_GetProductDefinitions = getProductDefinitions ?? (() => Array.Empty<ProductDefinition>());
            m_FetchProducts = fetchProducts ?? (_ => { });
            m_HasProduct = hasProduct ?? (_ => false);
            m_ClearUnavailableSkus = clearUnavailableSkus ?? (() => { });
            m_AddUnavailableSku = addUnavailableSku ?? (_ => { });
            m_OnPostFetchCompleted = onPostFetchCompleted ?? (() => { });
            m_DelayAsync = delayAsync ?? DefaultDelayAsync;
            m_RetryDelaysMs = NormalizeRetryDelaysMs(retryDelaysMs);
        }

        /// <summary>
        /// 在状态允许时发起商品拉取。
        /// Fetching / Succeeded 状态会直接跳过，Failed 状态允许后续重试或重连重新发起。
        /// </summary>
        internal void StartFetchIfAllowed()
        {
            if (State is MobileProductFetchState.Fetching or MobileProductFetchState.Succeeded)
            {
                Log.Debug(LogTag.IAPMobile, $"商品拉取请求已跳过，当前状态={State}。");
                return;
            }

            State = MobileProductFetchState.Fetching;
            m_ProductFetchTcs = new UniTaskCompletionSource<MobileProductFetchState>();
            IReadOnlyList<ProductDefinition> productDefs = m_GetProductDefinitions() ?? Array.Empty<ProductDefinition>();
            Log.Debug(LogTag.IAPMobile, $"Unity IAP 商品定义数量={productDefs.Count}。");
            m_ClearUnavailableSkus();
            m_FetchProducts(productDefs);
        }

        /// <summary>
        /// 商品完整成功回调。
        /// </summary>
        internal void OnProductsFetched(int fetchedCount)
        {
            if (State == MobileProductFetchState.Succeeded)
            {
                CancelRetry();
                return;
            }

            Log.Debug(LogTag.IAPMobile, $"商品拉取成功，数量={fetchedCount}。");
            CancelRetry();
            m_ClearUnavailableSkus();
            AddUnavailableSkusForMissingPendingProducts();
            State = MobileProductFetchState.Succeeded;
            CompleteProductFetchWaiters(State);
            CompletePostFetchFlowOnce();
        }

        /// <summary>
        /// 商品失败回调。失败数据必须由调用方在 Unity IAP 回调返回前物化。
        /// </summary>
        internal void OnProductsFetchFailed(IReadOnlyList<ProductDefinition> failedProducts, string failureReason)
        {
            int failedCount = failedProducts?.Count ?? 0;
            Log.Warning(LogTag.IAPMobile, $"商品拉取失败，数量={failedCount}，原因={failureReason}。");
            AddUnavailableSkusForMissingProducts(failedProducts);

            if (State == MobileProductFetchState.Succeeded)
            {
                CancelRetry();
                return;
            }

            if (HasFetchedAnyProduct(failedProducts))
            {
                Log.Warning(LogTag.IAPMobile, "商品拉取存在失败 SKU，但至少已有一个商品可用，本轮按商品拉取完成处理。");
                CancelRetry();
                State = MobileProductFetchState.Succeeded;
                CompleteProductFetchWaiters(State);
                CompletePostFetchFlowOnce();
                return;
            }

            State = MobileProductFetchState.Failed;
            CompleteProductFetchWaiters(State);
            ScheduleRetry();
        }

        /// <summary>
        /// 等待商品拉取完成；超时不抛出，返回当前状态。
        /// </summary>
        internal async UniTask<MobileProductFetchState> WaitForProductsFetchedAsync(int timeoutMs, CancellationToken ct)
        {
            if (State != MobileProductFetchState.Fetching)
            {
                return State;
            }

            UniTaskCompletionSource<MobileProductFetchState> tcs = m_ProductFetchTcs;
            if (tcs == null)
            {
                return State;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);
            try
            {
                return await tcs.Task.AttachExternalCancellation(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return State;
            }
        }

        /// <summary>
        /// 取消悬空重试并重置重试计数。
        /// </summary>
        internal void CancelRetry()
        {
            if (m_ProductFetchRetryCts != null)
            {
                m_ProductFetchRetryCts.Cancel();
                m_ProductFetchRetryCts.Dispose();
                m_ProductFetchRetryCts = null;
            }

            RetryIndex = 0;
        }

        /// <summary>
        /// 释放状态机资源。
        /// </summary>
        internal void Dispose()
        {
            CancelRetry();
            State = MobileProductFetchState.None;
            CompleteProductFetchWaiters(MobileProductFetchState.Failed);
        }

        /// <summary>
        /// 默认重试延迟实现，使用 UniTask 按帧循环等待。
        /// </summary>
        /// <param name="delayMs">延迟时间，单位毫秒。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>延迟等待任务。</returns>
        private static UniTask DefaultDelayAsync(int delayMs, CancellationToken ct)
        {
            return UniTask.Delay(delayMs, DelayType.DeltaTime, PlayerLoopTiming.Update, ct);
        }

        /// <summary>
        /// 规整外部传入的商品拉取重试延迟表；空列表或非正数都会回落默认延迟表。
        /// </summary>
        /// <param name="retryDelaysMs">外部配置的重试延迟表，单位毫秒。</param>
        /// <returns>可安全用于运行时调度的重试延迟表。</returns>
        private static IReadOnlyList<int> NormalizeRetryDelaysMs(IReadOnlyList<int> retryDelaysMs)
        {
            if (retryDelaysMs == null || retryDelaysMs.Count == 0)
            {
                Log.Warning(LogTag.IAPMobile, "商品拉取重试延迟配置为空或包含非法值，已回落到默认 2s/5s/10s。");
                return s_DefaultRetryDelaysMs;
            }

            for (int i = 0; i < retryDelaysMs.Count; i++)
            {
                if (retryDelaysMs[i] <= 0)
                {
                    Log.Warning(LogTag.IAPMobile, $"商品拉取重试延迟配置为空或包含非法值，索引={i}，值={retryDelaysMs[i]}，已回落到默认 2s/5s/10s。");
                    return s_DefaultRetryDelaysMs;
                }
            }

            return retryDelaysMs;
        }

        /// <summary>
        /// 在商品整体失败后调度下一轮重试。
        /// </summary>
        private void ScheduleRetry()
        {
            if (State == MobileProductFetchState.Succeeded || !m_IsReady())
            {
                return;
            }

            if (m_ProductFetchRetryCts != null)
            {
                return;
            }

            if (RetryIndex >= m_RetryDelaysMs.Count)
            {
                Log.Warning(LogTag.IAPMobile, $"商品拉取重试已达到最大次数={m_RetryDelaysMs.Count}，停止重试。");
                return;
            }

            int delayMs = m_RetryDelaysMs[RetryIndex++];
            m_ProductFetchRetryCts = new CancellationTokenSource();
            Log.Warning(LogTag.IAPMobile, $"商品拉取将在 {delayMs}ms 后重试，重试进度={RetryIndex}/{m_RetryDelaysMs.Count}。");
            RunRetryAsync(delayMs, m_ProductFetchRetryCts.Token).Forget();
        }

        /// <summary>
        /// 等待指定延迟后重新发起商品拉取；取消、未就绪或已成功时直接结束。
        /// </summary>
        /// <param name="delayMs">重试延迟，单位毫秒。</param>
        /// <param name="ct">重试取消令牌。</param>
        private async UniTaskVoid RunRetryAsync(int delayMs, CancellationToken ct)
        {
            try
            {
                await m_DelayAsync(delayMs, ct);
                if (ct.IsCancellationRequested || !m_IsReady() || State == MobileProductFetchState.Succeeded)
                {
                    return;
                }

                m_ProductFetchRetryCts?.Dispose();
                m_ProductFetchRetryCts = null;
                StartFetchIfAllowed();
            }
            catch (OperationCanceledException)
            {
                // 商品拉取成功、初始化失败或 Dispose 会取消悬空重试，属于正常路径。
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.IAPMobile, $"商品拉取重试执行异常，详情={e.Message}");
                m_ProductFetchRetryCts?.Dispose();
                m_ProductFetchRetryCts = null;
            }
        }

        /// <summary>
        /// 将失败列表中当前仍缺失的商品记录为不可用 SKU。
        /// </summary>
        /// <param name="failedProducts">Unity IAP 报告失败的商品定义列表。</param>
        private void AddUnavailableSkusForMissingProducts(IReadOnlyList<ProductDefinition> failedProducts)
        {
            if (failedProducts == null)
            {
                return;
            }

            foreach (ProductDefinition def in failedProducts)
            {
                AddUnavailableSkuIfMissing(def);
            }
        }

        /// <summary>
        /// 成功回调清理旧失败 SKU 后，按当前 Controller 状态恢复仍缺失的待拉取商品。
        /// </summary>
        private void AddUnavailableSkusForMissingPendingProducts()
        {
            IReadOnlyList<ProductDefinition> pendingProductDefs = m_GetProductDefinitions();
            if (pendingProductDefs == null)
            {
                return;
            }

            foreach (ProductDefinition def in pendingProductDefs)
            {
                AddUnavailableSkuIfMissing(def);
            }
        }

        /// <summary>
        /// 仅在 StoreController 当前仍查不到指定商品时写入不可用 SKU。
        /// </summary>
        /// <param name="def">待检查的商品定义。</param>
        private void AddUnavailableSkuIfMissing(ProductDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.id))
            {
                return;
            }

            if (m_HasProduct(def.id))
            {
                return;
            }

            m_AddUnavailableSku(def.id);
        }

        /// <summary>
        /// 根据失败数量判断当前回调是否已经存在至少一个成功商品。
        /// </summary>
        /// <param name="failedProducts">Unity IAP 报告失败的商品定义列表。</param>
        /// <returns>存在至少一个成功商品时返回 true。</returns>
        private bool HasFetchedAnyProduct(IReadOnlyList<ProductDefinition> failedProducts)
        {
            int requestedCount = m_GetProductDefinitions()?.Count ?? 0;
            int failedCount = failedProducts?.Count ?? requestedCount;
            return requestedCount > 0 && failedCount < requestedCount;
        }

        /// <summary>
        /// 完成并清空当前商品拉取等待信号。
        /// </summary>
        /// <param name="state">要返回给等待方的商品拉取状态。</param>
        private void CompleteProductFetchWaiters(MobileProductFetchState state)
        {
            m_ProductFetchTcs?.TrySetResult(state);
            m_ProductFetchTcs = null;
        }

        /// <summary>
        /// 只在商品首次进入成功态时触发后置流程。
        /// </summary>
        private void CompletePostFetchFlowOnce()
        {
            if (m_HasCompletedPostFetchFlow)
            {
                return;
            }

            m_HasCompletedPostFetchFlow = true;
            m_OnPostFetchCompleted();
        }
    }
}
