/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileValidationService.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   订单状态机核心：验单队列、状态转移、启动补单扫描
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 订单状态机服务。
    /// 管理内存订单字典（与持久化同步）、验单队列、服务端重试、启动时补单扫描。
    /// </summary>
    internal sealed partial class MobileValidationService
    {
        /// <summary>
        /// 服务容器，持有共享外部依赖与其他服务引用。
        /// </summary>
        private readonly MobileServiceHub m_Hub;

        /// <summary>
        /// 构造 MobileValidationService。
        /// </summary>
        /// <param name="hub">服务容器，持有共享外部依赖与其他服务引用。</param>
        internal MobileValidationService(MobileServiceHub hub)
        {
            m_Hub = hub;
        }

        /// <summary>
        /// 扫描本地未完成订单并触发补单验单流程。
        /// 须在用户登录成功、SetAccountID 调用后手动触发。
        /// 每次扫描都会先向服务端拉取未发货订单再执行本地扫描。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        internal async UniTask CheckLocalOrdersAsync(CancellationToken ct)
        {
            SyncOrderRecordsFromStore();
            if (!IsUserReadyForValidation())
            {
                Log.Debug(LogTag.IAPMobile, "账号未登录，跳过补单扫描和订阅查询。");
                return;
            }

            MergePreLoginOrderRecords();
            await DoQueryPendingFromServerAsync(ct);
            await m_Hub.RestoreService.RefreshEntitlementsAsync(ct);
        }

        /// <summary>
        /// 支付发起前优先处理当前 tableId 已支付但尚未完成服务端验单的订单。
        /// 命中当前请求 tableId 时，将验单结果直接作为本次 PayAsync 结果返回，避免再次向平台发起购买。
        /// </summary>
        /// <param name="requestTableId">当前支付请求的商品配置表行 ID。</param>
        /// <param name="customData">当前支付请求透传数据，仅在旧订单未记录透传数据时兜底使用。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>当前请求命中本地待验订单时返回验单结果；未命中时返回 null。</returns>
        internal async UniTask<IAPResult> TryValidatePaidLocalOrdersBeforePayAsync(long requestTableId, string customData, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (CurrentPayTcs != null)
            {
                return new IAPResult(requestTableId, (int)IAPMobileErrorCode.AlreadyPurchasing, "当前已有支付验单进行中。", customData);
            }

            while (m_IsProcessingQueue)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            SyncOrderRecordsFromStore();

            if (!m_OrderRecords.TryGetValue(requestTableId, out MobileOrderRecord currentRequestRecord) ||
                !IsPaidUnvalidatedStatus(currentRequestRecord.Status))
            {
                return null;
            }

            currentRequestRecord.IsReplenish = false;
            if (string.IsNullOrEmpty(currentRequestRecord.CustomDataParam))
            {
                currentRequestRecord.CustomDataParam = customData;
            }

            m_ValidateQueue.Clear();
            m_ValidateQueue.Enqueue(requestTableId);

            var payTcs = new UniTaskCompletionSource<IAPResult>();
            CurrentPayTcs = payTcs;

            m_Hub.Store.AddWaitingRef();
            try
            {
                SaveOrderRecords();
                await ProcessQueueAsync(ct);
                return payTcs == null ? null : await payTcs.Task;
            }
            finally
            {
                if (payTcs != null && CurrentPayTcs == payTcs)
                {
                    CurrentPayTcs = null;
                }

                m_Hub.Store.SubWaitingRef();
            }
        }

        /// <summary>
        /// 将指定 tableId 的订单加入验单队列（去重），并触发队列处理。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        internal void EnqueueValidation(long tableId)
        {
            if (tableId == 0L)
            {
                return;
            }

            if (!IsUserReadyForValidation())
            {
                Log.Debug(LogTag.IAPMobile, $"账号未登录，暂不启动验单队列，商品表ID={tableId}");
                return;
            }

            if (!m_ValidateQueue.Contains(tableId))
            {
                m_ValidateQueue.Enqueue(tableId);
            }

            ProcessQueueAsync(CancellationToken.None).Forget();
        }

        /// <summary>
        /// 将新购订单记录写入内存字典和持久化，并加入验单队列。
        /// </summary>
        /// <param name="record">新建的订单记录（Status 应为 PendingValidate）。</param>
        internal void AddAndEnqueue(MobileOrderRecord record)
        {
            if (record == null || record.TableId == 0L)
            {
                return;
            }

            if (!IsUserReadyForValidation())
            {
                m_PreLoginOrderRecords[record.TableId] = record;
                Log.Debug(LogTag.IAPMobile, $"账号未登录，仅暂存待验订单，商品表ID={record.TableId}，订单号={record.TransactionId}");
                return;
            }

            m_OrderRecords[record.TableId] = record;
            SaveOrderRecords();
            EnqueueValidation(record.TableId);
        }

        /// <summary>
        /// 登记待服务端验单通过后再确认的平台 PendingOrder。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="order">平台待确认订单。</param>
        internal void RegisterPendingPlatformOrder(long tableId, PendingOrder order)
        {
            if (tableId == 0L || order == null)
            {
                return;
            }

            m_PendingPlatformOrders[tableId] = order;
        }

        /// <summary>
        /// 判断本地是否仍保留指定 tableId 的待验订单记录。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>仍存在订单记录时返回 true。</returns>
        internal bool HasOrderRecord(long tableId)
        {
            return tableId != 0L && m_OrderRecords.ContainsKey(tableId);
        }

        /// <summary>
        /// 支付发起前写入 Purchasing 占位存档，防止支付中途退出后订单丢失。
        /// 平台回调到来后 HandleConfirmedOrder 会覆盖为完整记录并重新入队验单。
        /// </summary>
        /// <param name="tableId">待支付商品配置表行 ID。</param>
        /// <param name="customDataParam">业务透传数据，原样写入存档。</param>
        internal void WritePurchasingRecord(long tableId, string customDataParam)
        {
            m_DispatchedPaySuccessTableIds.Remove(tableId);
            var record = new MobileOrderRecord
            {
                TableId = tableId,
                Status = MobileOrderStatus.Purchasing,
                CustomDataParam = customDataParam,
            };
            m_OrderRecords[tableId] = record;
            SaveOrderRecords();
        }

        /// <summary>
        /// 清除所有 Purchasing 状态的存档，防止上次异常遗留的占位记录干扰本次支付。
        /// 每次发起新支付前调用。
        /// </summary>
        internal void RemoveAllPurchasingRecords()
        {
            var toRemove = new List<long>();
            foreach (var kv in m_OrderRecords)
            {
                if (kv.Value.Status == MobileOrderStatus.Purchasing)
                {
                    // 收集 Purchasing 记录，不在遍历中直接删
                    toRemove.Add(kv.Key);
                }
            }

            // 无 Purchasing 记录，无需落盘
            if (toRemove.Count == 0)
            {
                return;
            }

            // 批量删除
            foreach (long id in toRemove)
            {
                m_OrderRecords.Remove(id);
            }

            // 落盘
            SaveOrderRecords();
        }

        /// <summary>
        /// 平台本地支付失败时，先标记 LocalPayFailed 并落盘，再删除记录。
        /// 如果标记后删除前崩溃，下次启动扫描会按失败终态直接清理，不会误入补单。
        /// </summary>
        /// <param name="tableId">失败订单的商品配置表行 ID。</param>
        internal void MarkLocalPayFailedAndRemove(long tableId)
        {
            if (tableId == 0L || !m_OrderRecords.TryGetValue(tableId, out MobileOrderRecord record))
            {
                return;
            }

            record.Status = MobileOrderStatus.LocalPayFailed;
            SaveOrderRecords();
            m_OrderRecords.Remove(tableId);
            SaveOrderRecords();
        }

        /// <summary>
        /// Restore 流程：准备订阅 tableId 验单记录，返回实际可发送验单的 tableId。
        /// </summary>
        /// <param name="tableIds">需要验单的订阅商品 tableId 列表。</param>
        /// <returns>实际具备验单凭据、可入队的 tableId 列表。</returns>
        internal List<long> PrepareRestoreSubscriptions(List<long> tableIds)
        {
            var prepared = new List<long>();
            foreach (long tableId in tableIds)
            {
                MobileOrderRecord record = EnsureRestoreOrderRecord(tableId);
                if (!CanEnqueueRestoreRecord(record))
                {
                    continue;
                }

                prepared.Add(tableId);
            }

            SaveOrderRecords();
            return prepared;
        }

        /// <summary>
        /// Restore 流程：准备非消耗品 tableId 验单记录，返回实际可发送验单的 tableId。
        /// </summary>
        /// <param name="tableIds">需要验单的非消耗品商品 tableId 列表。</param>
        /// <returns>实际具备验单凭据、可入队的 tableId 列表。</returns>
        internal List<long> PrepareRestoreNonConsumables(List<long> tableIds)
        {
            var prepared = new List<long>();
            foreach (long tableId in tableIds)
            {
                MobileOrderRecord record = EnsureRestoreOrderRecord(tableId);
                if (!CanEnqueueRestoreRecord(record))
                {
                    continue;
                }

                prepared.Add(tableId);
            }

            SaveOrderRecords();
            return prepared;
        }

        /// <summary>
        /// Restore 准备完成后按实际待验 tableId 入队。
        /// </summary>
        /// <param name="tableIds">实际可验单的 tableId 列表。</param>
        internal void EnqueuePreparedRestoreRecords(List<long> tableIds)
        {
            if (tableIds == null)
            {
                return;
            }

            foreach (long tableId in tableIds)
            {
                EnqueueValidation(tableId);
            }
        }

        /// <summary>
        /// 确保 Restore tableId 有本地待验订单记录。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>待验订单记录；商品配置缺失时返回 null。</returns>
        private MobileOrderRecord EnsureRestoreOrderRecord(long tableId)
        {
            if (m_OrderRecords.TryGetValue(tableId, out MobileOrderRecord existing))
            {
                existing.IsReplenish = true;
                return existing;
            }

            IAPProductEntry entry = m_Hub.Table?.FindByTableId(tableId);
            if (entry == null)
            {
                return null;
            }

            m_Hub.ProductService.GetReceiptInfo(entry.ProductID, out string orderId, out string googleToken);
            var record = new MobileOrderRecord
            {
                TransactionId = orderId ?? string.Empty,
                TableId = tableId,
                GoogleToken = googleToken,
                Status = MobileOrderStatus.PendingValidate,
                IsReplenish = true,
            };
            m_OrderRecords[tableId] = record;
            return record;
        }

        /// <summary>
        /// Restore 入队前按平台校验必要凭据。
        /// </summary>
        /// <param name="record">待验订单记录。</param>
        /// <returns>凭据完整、可入队时返回 true。</returns>
        private bool CanEnqueueRestoreRecord(MobileOrderRecord record)
        {
            if (record == null)
            {
                return false;
            }

            if (CanEnqueueLocalRecord(record))
            {
                return true;
            }

            if (ShouldRemoveLocalRecordWithoutCredential(record))
            {
                RemoveLocalOrderRecord(record.TableId, "Restore 订单缺少 iOS order_id，删除本地待验记录。");
            }

            return false;
        }

        /// <summary>
        /// 释放服务资源。
        /// </summary>
        internal void Dispose()
        {
            // 不清空 m_OrderRecords：它指向 Store.PersistData.OrderRecords 共享引用，清空会抹除持久化中未完成订单
            // 丢弃队列中未处理的 tableId
            m_ValidateQueue.Clear();
            // PendingOrder 引用只在当前运行期有效
            m_PendingPlatformOrders.Clear();
            // 重置队列锁，防止 Dispose 后无法再入队
            m_IsProcessingQueue = false;
            // 释放可能悬空的 TCS
            CurrentPayTcs = null;
        }
    }
}
