/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileRestoreService.Methods.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   MobileRestoreService 私有方法
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobileRestoreService
    {
        /// <summary>
        /// 清空 CheckEntitlement 字典，遍历商品表中的订阅和非消耗品，逐个调用 Controller.CheckEntitlement 发起权益查询。
        /// 无需查询的商品时直接跳过进入 ProcessAllEntitlementsCompleted。
        /// </summary>
        private void StartCheckEntitlements()
        {
            m_Hub.ProductService.m_CheckEntitlements.Clear();
            if (m_Hub.Table?.Products == null || !m_Hub.ExtendedService.IsAttached)
            {
                ProcessAllEntitlementsCompleted();
                return;
            }

            bool skippedEligibleProductBecauseNotFetched = false;
            foreach (IAPProductEntry entry in m_Hub.Table.Products)
            {
                if (entry == null)
                {
                    continue;
                }

                if (entry.ProductType != IAPProductType.Subscription && entry.ProductType != IAPProductType.NonConsumable)
                {
                    continue;
                }

                if (m_Hub.Store.IsUnavailableSkuInternal(entry.ProductID))
                {
                    // 平台已确认不可购买（拉取失败）的商品不会再进入 StoreController，
                    // 跳过且不置 skippedEligibleProductBecauseNotFetched，避免权益刷新被永久延后挂起。
                    LogWarning($"跳过不可购买商品的权益检查，商品ID={entry.ProductID}");
                    continue;
                }

                Product product = m_Hub.ExtendedService.GetProductById(entry.ProductID);
                if (product == null)
                {
                    skippedEligibleProductBecauseNotFetched = true;
                    continue;
                }

                var info = new MobileCheckEntitlementInfo(entry.TableId, entry.ProductType, product);
                m_Hub.ProductService.m_CheckEntitlements[product.definition.id] = info;
            }

            if (m_Hub.ProductService.m_CheckEntitlements.Count > 0)
            {
                // 逐个发起 CheckEntitlement，结果通过 OnCheckEntitlement 异步回调收集
                foreach (MobileCheckEntitlementInfo info in m_Hub.ProductService.m_CheckEntitlements.Values)
                {
                    m_Hub.ExtendedService.CheckEntitlement(info.Product);
                }
            }
            else
            {
                // 无需查询的商品时直接进入汇总阶段，避免 Restore 流程永久挂起
                if (skippedEligibleProductBecauseNotFetched)
                {
                    m_PendingEntitlementRefreshAfterProductsFetched = true;
                    LogWarning("订阅或非消耗品尚未进入 StoreController，已延后权益刷新。");
                }
                else
                {
                    LogDebug("没有待查询的订阅或非消耗品。");
                }
                ProcessAllEntitlementsCompleted();
            }
        }

        /// <summary>
        /// 所有 CheckEntitlement 回调收集完成后的汇总处理：按类型筛选 FullyEntitled 列表，
        /// 设置 RestoreCoordinator 待处理计数，并向 ValidationService 分发验单任务。
        /// </summary>
        private void ProcessAllEntitlementsCompleted()
        {
            LogDebug("所有 CheckEntitlement 完成，分发 Restore 验单。");
            List<long> subTableIds = m_Hub.ProductService.GetFullyEntitledSubscriptionTableIds();
            List<long> ncTableIds = m_Hub.ProductService.GetFullyEntitledNonConsumableTableIds();
            List<long> preparedSubTableIds = m_Hub.ValidationService.PrepareRestoreSubscriptions(subTableIds);
            List<long> preparedNcTableIds = m_Hub.ValidationService.PrepareRestoreNonConsumables(ncTableIds);

            // 设置待完成计数
            m_RestoreCoordinator.SetPending(preparedSubTableIds.Count, preparedNcTableIds.Count);

            if (preparedSubTableIds.Count > 0)
            {
                // 批量入队订阅验单
                m_Hub.ValidationService.EnqueuePreparedRestoreRecords(preparedSubTableIds);
            }
            else
            {
                // 无订阅，直接标记完成
                m_RestoreCoordinator.MarkSubscriptionFinished();
            }

            if (preparedNcTableIds.Count > 0)
            {
                // 批量入队非消耗品验单
                m_Hub.ValidationService.EnqueuePreparedRestoreRecords(preparedNcTableIds);
            }
            else
            {
                // 无非消耗品，直接标记完成
                m_RestoreCoordinator.MarkNonConsumableFinished();
            }

            // 若两路均已完成（均为 0），立即结束
            TryFinishRestore();
        }

        /// <summary>
        /// 尝试结束 Restore 流程：仅在 RestoreCoordinator.CanFinishRestore() 为 true 时才调用 FinishRestore。
        /// </summary>
        private void TryFinishRestore()
        {
            if (!m_RestoreCoordinator.CanFinishRestore())
            {
                return;
            }

            FinishRestore();
        }

        /// <summary>
        /// 强制结束 Restore 流程：合并订阅和非消耗品结果列表，触发事件桥通知，完成 RestoreTcs。
        /// </summary>
        private void FinishRestore()
        {
            // 先取出 TCS 再清零，避免回调期间二次进入
            var tcs = m_RestoreTcs;
            // 防 null，结果已在收集阶段填充
            var subResults = m_SubscriptionResults ?? new List<IAPResult>();
            var ncResults = m_NonConsumeResults ?? new List<IAPResult>();
            // 清零防止重复 TrySetResult
            m_RestoreTcs = null;
            // 释放收集列表引用
            m_SubscriptionResults = null;
            m_NonConsumeResults = null;
            // 解除防重入锁
            m_IsInRestore = false;

            // 通知订阅恢复
            m_Hub.Context.EventBridge?.RaiseSubscriptionRestored(subResults);
            // 通知非消耗品恢复
            m_Hub.Context.EventBridge?.RaiseNonConsumeRestored(ncResults);
            var combined = new List<IAPResult>(subResults.Count + ncResults.Count);
            combined.AddRange(subResults);
            combined.AddRange(ncResults);
            // 完成 RestoreAsync 的 await 点
            tcs?.TrySetResult(combined);
        }

        /// <summary>
        /// 在非协调 Restore 路径下就地补发单条订阅恢复通知，保证订阅恢复成功必有全局 SubscriptionRestored 事件。
        /// </summary>
        /// <param name="result">已验单成功的订阅恢复结果。</param>
        private void RaiseStandaloneSubscriptionRestored(IAPResult result)
        {
            if (result == null)
            {
                return;
            }

            var single = new List<IAPResult>(1) { result };
            m_Hub.Context.EventBridge?.RaiseSubscriptionRestored(single);
        }

        /// <summary>
        /// 在非协调 Restore 路径下就地补发单条非消耗品恢复通知，保证非消耗品恢复必有全局 NonConsumeRestored 事件。
        /// </summary>
        /// <param name="result">已验单成功的非消耗品恢复结果。</param>
        private void RaiseStandaloneNonConsumeRestored(IAPResult result)
        {
            if (result == null)
            {
                return;
            }

            var single = new List<IAPResult>(1) { result };
            m_Hub.Context.EventBridge?.RaiseNonConsumeRestored(single);
        }
    }
}
