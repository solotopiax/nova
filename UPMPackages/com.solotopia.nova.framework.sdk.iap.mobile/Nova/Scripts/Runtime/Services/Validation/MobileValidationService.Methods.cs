/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileValidationService.Methods.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   MobileValidationService 私有方法：持久化、补单扫描、验单流程
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobileValidationService
    {
        /// <summary>
        /// 触发 MobileStore 重新加载共享 PersistData 容器；本服务通过 m_OrderRecords 属性即时跟随。
        /// </summary>
        private void SyncOrderRecordsFromStore()
        {
            if (m_Hub.Store?.PersistData == null)
            {
                m_Hub.Store?.LoadPersistDataInternal();
            }
        }

        /// <summary>
        /// 触发统一存档落盘；订单字典已通过共享引用直接修改，无需赋值回 PersistData。
        /// </summary>
        private void SaveOrderRecords()
        {
            if (m_Hub.Store?.PersistData == null)
            {
                return;
            }

            m_Hub.Store.SavePersistDataInternal();
        }

        /// <summary>
        /// 当前是否已具备发起验单协议的账号条件。
        /// </summary>
        /// <returns>已登录并存在当前账号存档容器时返回 true。</returns>
        private bool IsUserReadyForValidation()
        {
            return !string.IsNullOrEmpty(m_Hub.Store?.GameUID) && m_Hub.Store?.PersistData != null;
        }

        /// <summary>
        /// 将登录前平台回调暂存的待验订单合并到当前账号存档。
        /// </summary>
        private void MergePreLoginOrderRecords()
        {
            if (m_PreLoginOrderRecords.Count == 0)
            {
                return;
            }

            if (!IsUserReadyForValidation())
            {
                Log.Debug(LogTag.IAPMobile, $"账号未登录，保留登录前待验订单，数量={m_PreLoginOrderRecords.Count}");
                return;
            }

            foreach (KeyValuePair<long, MobileOrderRecord> kv in m_PreLoginOrderRecords)
            {
                m_OrderRecords[kv.Key] = kv.Value;
            }

            Log.Debug(LogTag.IAPMobile, $"已合并登录前待验订单，数量={m_PreLoginOrderRecords.Count}");
            m_PreLoginOrderRecords.Clear();
            SaveOrderRecords();
        }

        /// <summary>
        /// 实际执行拉取：每次补单扫描都先发送 QueryPendingOrderCmd，
        /// 等服务端返回后将未完成订单列表写入本地记录，最后统一执行本地补单扫描。
        /// 远端成功但列表为空也视为本次拉取完成；网络不可用时降级本地扫描。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        private async UniTask DoQueryPendingFromServerAsync(CancellationToken ct)
        {
            MobileStorePersistData persistData = m_Hub.Store?.PersistData;
            bool shouldCheckLocalOrders = true;
            try
            {
                if (persistData == null)
                {
                    return;
                }

                if (!CanQueryPendingFromServer())
                {
                    Log.Warning(LogTag.IAPMobile, $"未完成订单查询跳过，网络或支付协议服务不可用，账号ID={m_Hub.Store?.GameUID}");
                    return;
                }

                bool querySuccess = await TryQueryPendingOrdersFromServerAsync();
                if (querySuccess)
                {
                    persistData.HasQueriedPendingFromServer = true;
                    SaveOrderRecords();
                }
                else
                {
                    Log.Warning(LogTag.IAPMobile, $"未完成订单查询网络失败，账号ID={m_Hub.Store?.GameUID}");
                }
            }
            catch (OperationCanceledException)
            {
                shouldCheckLocalOrders = false;
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.IAPMobile, $"未完成订单查询异常，账号ID={m_Hub.Store?.GameUID}，详情={ex.Message}");
            }
            finally
            {
                if (shouldCheckLocalOrders)
                {
                    await StartCheckLocalOrdersAsync(ct);
                }
            }
        }

        /// <summary>
        /// 当前运行时是否具备查询服务端未完成订单的必要依赖。
        /// </summary>
        /// <returns>PayService、NetworkManager 均可用且当前网络可用时返回 true。</returns>
        private bool CanQueryPendingFromServer()
        {
            return m_Hub.PayService != null &&
                   m_Hub.Context.NetworkManager != null &&
                   m_Hub.Context.NetworkManager.CheckNetworkActive();
        }

        /// <summary>
        /// 按当前平台查询服务端未完成订单，并把返回列表登记到本地补单记录。
        /// </summary>
        /// <returns>网络包装响应成功时返回 true；列表为空仍算成功。</returns>
        private async UniTask<bool> TryQueryPendingOrdersFromServerAsync()
        {
#if UNITY_ANDROID
            string cmdName = m_Hub.Config?.GoogleQueryPendingOrderCmdName;
            NetResponse<PbNetGoogleQueryPendingOrderResp> resp = await m_Hub.PayService.QueryGooglePendingOrderAsync(cmdName);
            if (resp != null && resp.IsSuccess)
            {
                RegisterGooglePendingOrders(resp.Data?.OrderList);
                return true;
            }

            return false;
#elif UNITY_IOS
            string cmdName = m_Hub.Config?.AppleQueryPendingOrderCmdName;
            NetResponse<PbNetAppleQueryPendingOrderResp> resp = await m_Hub.PayService.QueryApplePendingOrderAsync(cmdName);
            if (resp != null && resp.IsSuccess)
            {
                RegisterApplePendingOrders(resp.Data?.OrderList);
                return true;
            }

            return false;
#else
            Log.Warning(LogTag.IAPMobile, "当前平台不支持未完成订单查询。");
            await UniTask.CompletedTask;
            return false;
#endif
        }

#if UNITY_ANDROID
        /// <summary>
        /// 将 Google QueryPendingOrder 返回的订单列表登记为本地补单记录。
        /// </summary>
        /// <param name="orders">服务端返回的 Google 未完成订单列表，可为空。</param>
        private void RegisterGooglePendingOrders(IList<PbNetGoogleQueryPendingOrderInfo> orders)
        {
            if (orders == null)
            {
                return;
            }

            foreach (PbNetGoogleQueryPendingOrderInfo info in orders)
            {
                if (!TryResolveServerTableId(info.TableId, info.Parameter, out long tableId))
                {
                    continue;
                }

                RegisterPendingOrder(tableId, string.Empty, info.Token);
            }
        }
#endif

#if UNITY_IOS
        /// <summary>
        /// 将 Apple QueryPendingOrder 返回的订单列表登记为本地补单记录。
        /// </summary>
        /// <param name="orders">服务端返回的 Apple 未完成订单列表，可为空。</param>
        private void RegisterApplePendingOrders(IList<PbNetAppleQueryPendingOrderInfo> orders)
        {
            if (orders == null)
            {
                return;
            }

            foreach (PbNetAppleQueryPendingOrderInfo info in orders)
            {
                if (!TryResolveServerTableId(info.TableId, info.Parameter, out long tableId))
                {
                    continue;
                }

                RegisterPendingOrder(tableId, info.OrderId, string.Empty);
            }
        }
#endif

        /// <summary>
        /// 从服务端未完成订单响应解析商品表 ID；优先使用 table_id，缺失时兼容旧 parameter 透传。
        /// </summary>
        /// <param name="serverTableId">服务端返回的 table_id。</param>
        /// <param name="parameter">旧协议透传参数。</param>
        /// <param name="tableId">解析出的商品配置表行 ID。</param>
        /// <returns>成功解析到有效 tableId 时返回 true。</returns>
        private static bool TryResolveServerTableId(long serverTableId, string parameter, out long tableId)
        {
            if (serverTableId > 0L)
            {
                tableId = serverTableId;
                return true;
            }

            tableId = MobileStoreParameterCodec.DecodeTableId(parameter);
            return tableId > 0L;
        }

        /// <summary>
        /// 将一条服务端未完成订单合并到本地记录：按 tableId 找到商品配置后读取本地凭据，
        /// 本地凭据缺失时回退到服务端透传的订单号 / token，保留已有透传数据并统一标记为补单待验。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="serverOrderId">服务端返回的订单号（Apple 透传；Google 无此字段时传空）。</param>
        /// <param name="serverToken">服务端返回的购买 token（Google 透传；Apple 无此字段时传空）。</param>
        private void RegisterPendingOrder(long tableId, string serverOrderId, string serverToken)
        {
            IAPProductEntry entry = m_Hub.Table?.FindByTableId(tableId);
            if (entry == null)
            {
                return;
            }

            m_Hub.ProductService.GetReceiptInfo(entry.ProductID, out string localOrderId, out string localGoogleToken);
            m_OrderRecords.TryGetValue(tableId, out MobileOrderRecord existing);
            var record = existing ?? new MobileOrderRecord
            {
                TableId = tableId,
            };

            if (!string.IsNullOrEmpty(localOrderId))
            {
                record.TransactionId = localOrderId;
            }
            else if (string.IsNullOrEmpty(record.TransactionId) && !string.IsNullOrEmpty(serverOrderId))
            {
                record.TransactionId = serverOrderId;
            }

            if (!string.IsNullOrEmpty(localGoogleToken))
            {
                record.GoogleToken = localGoogleToken;
            }
            else if (!string.IsNullOrEmpty(serverToken))
            {
                record.GoogleToken = serverToken;
            }

            record.Status = MobileOrderStatus.PendingValidate;
            record.IsReplenish = true;
            m_OrderRecords[tableId] = record;
        }

        /// <summary>
        /// 启动时扫描本地存档订单：将具备验单凭据的 Purchasing 重置为 PendingValidate，
        /// 移除本地支付失败终态，把需要补单的条目入队，最后触发队列处理。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        private async UniTask StartCheckLocalOrdersAsync(CancellationToken ct)
        {
            var toRemove = new List<long>();
            foreach (var kv in m_OrderRecords)
            {
                long tableId = kv.Key;
                MobileOrderRecord record = kv.Value;
                switch (record.Status)
                {
                    case MobileOrderStatus.Purchasing:
                        if (!CanEnqueueLocalRecord(record))
                        {
                            if (ShouldRemoveLocalRecordWithoutCredential(record))
                            {
                                toRemove.Add(tableId);
                            }
                            continue;
                        }
                        record.Status = MobileOrderStatus.PendingValidate;
                        break;
                    case MobileOrderStatus.LocalPayFailed:
                        toRemove.Add(tableId);
                        continue;
                }
                if (record.Status == MobileOrderStatus.PendingValidate || record.Status == MobileOrderStatus.ValidateFailed)
                {
                    if (!CanEnqueueLocalRecord(record))
                    {
                        if (ShouldRemoveLocalRecordWithoutCredential(record))
                        {
                            toRemove.Add(tableId);
                        }
                        continue;
                    }
                    record.IsReplenish = true;
                    if (!m_ValidateQueue.Contains(tableId))
                    {
                        m_ValidateQueue.Enqueue(tableId);
                    }
                }
            }
            foreach (long id in toRemove)
            {
                m_OrderRecords.Remove(id);
            }

            SaveOrderRecords();
            await ProcessQueueAsync(ct);
        }

        /// <summary>
        /// 串行处理验单队列：每轮取出当前全部待验订单，按普通/订阅分组批量请求；
        /// m_IsProcessingQueue 防止并发重入。
        /// </summary>
        /// <param name="ct">取消令牌，ThrowIfCancellationRequested 会中断队列处理。</param>
        private async UniTask ProcessQueueAsync(CancellationToken ct)
        {
            if (m_IsProcessingQueue)
            {
                return;
            }

            m_IsProcessingQueue = true;
            try
            {
                while (m_ValidateQueue.Count > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    var contexts = new List<VerifyOrderContext>();
                    while (m_ValidateQueue.Count > 0)
                    {
                        long tableId = m_ValidateQueue.Dequeue();
                        if (!m_OrderRecords.TryGetValue(tableId, out MobileOrderRecord record))
                        {
                            continue;
                        }

                        contexts.Add(BuildVerifyContext(record));
                    }

                    await ValidateBatchWithServerAsync(contexts, ct);
                }
            }
            finally
            {
                m_IsProcessingQueue = false;
            }
        }

        /// <summary>
        /// 向服务端批量发起验单请求；普通内购与订阅协议不同，分组后各自一次 OrderList 请求。
        /// </summary>
        /// <param name="contexts">待验单订单上下文。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTask ValidateBatchWithServerAsync(List<VerifyOrderContext> contexts, CancellationToken ct)
        {
            if (contexts == null || contexts.Count == 0)
            {
                return;
            }

            if (m_Hub.Context.NetworkManager == null || !m_Hub.Context.NetworkManager.CheckNetworkActive())
            {
                foreach (VerifyOrderContext context in contexts)
                {
                    MarkValidateFailed(context, IAPMobileErrorCode.ValidateNetworkUnavailable, "验单网络不可用。", "验单网络不可用，订单已保留等待补单。", 1, true, 0, "验单网络不可用。");
                }

                return;
            }

            var normalContexts = new List<VerifyOrderContext>();
            var subscriptionContexts = new List<VerifyOrderContext>();
            foreach (VerifyOrderContext context in contexts)
            {
                if (!CanSendVerifyContext(context))
                {
                    continue;
                }

                if (context.IsSubscription)
                {
                    subscriptionContexts.Add(context);
                }
                else
                {
                    normalContexts.Add(context);
                }
            }

            await ValidateGroupWithServerAsync(normalContexts, false, ct);
            await ValidateGroupWithServerAsync(subscriptionContexts, true, ct);
        }

        /// <summary>
        /// 判断订单状态是否代表平台已支付成功但服务端尚未完成验单。
        /// </summary>
        /// <param name="status">待判断的订单状态。</param>
        /// <returns>需要进入服务端验单时返回 true。</returns>
        private static bool IsPaidUnvalidatedStatus(MobileOrderStatus status)
        {
            return status == MobileOrderStatus.PendingValidate || status == MobileOrderStatus.ValidateFailed;
        }

        /// <summary>
        /// 批量验单同一协议组。首次支付组允许重试，纯补单组不重试，避免阻塞补单队列。
        /// </summary>
        /// <param name="contexts">同一商品类型组的待验单订单。</param>
        /// <param name="isSubscription">当前组是否为订阅协议。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTask ValidateGroupWithServerAsync(List<VerifyOrderContext> contexts, bool isSubscription, CancellationToken ct)
        {
            if (contexts == null || contexts.Count == 0)
            {
                return;
            }

            int maxRetry = 0;
            foreach (VerifyOrderContext context in contexts)
            {
                if (!context.Record.IsReplenish)
                {
                    maxRetry = m_Hub.Context.RetryValidateMaxNum;
                    break;
                }
            }

            var pending = new List<VerifyOrderContext>(contexts);
            for (int attempt = 0; attempt <= maxRetry && pending.Count > 0; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                NetResponse<PbNetMobileVerifyResp> resp;
                try
                {
                    resp = await SendVerifyBatchAsync(pending, isSubscription, attempt, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warning(LogTag.IAPMobile, $"批量验单请求异常，重试次数={attempt}，数量={pending.Count}，详情={ex.Message}");
                    TrackValidateFailBatch(pending, attempt + 1, true, 0, IAPMobileErrorCode.ValidateNetworkRequestFailed, ex.Message);
                    continue;
                }

                if (!resp.IsSuccess || resp.Data == null)
                {
                    Log.Warning(LogTag.IAPMobile, $"批量验单网络失败，重试次数={attempt}，数量={pending.Count}");
                    TrackValidateFailBatch(pending, attempt + 1, true, resp.ErrorCode, IAPMobileErrorCode.ValidateNetworkRequestFailed, resp.ErrorMessage);
                    continue;
                }

                var nextPending = new List<VerifyOrderContext>();
                bool allowSingleFallback = pending.Count == 1;
                foreach (VerifyOrderContext context in pending)
                {
                    PbNetMobileVerifyOrderResult orderResult = FindOrderResult(resp.Data, context, allowSingleFallback);
                    if (orderResult == null)
                    {
                        Log.Warning(LogTag.IAPMobile, $"验单响应未找到对应订单，重试次数={attempt}，商品表ID={context.Record.TableId}，订单号={context.TransactionId}");
                        TrackValidateFail(context, attempt + 1, false, resp.ErrorCode, IAPMobileErrorCode.ValidateResponseMissing, "验单响应未找到对应订单。");
                        nextPending.Add(context);
                        continue;
                    }

                    if (!TryFinishVerifyResult(context, orderResult, isSubscription, attempt + 1))
                    {
                        TrackValidateFail(context, attempt + 1, false, resp.ErrorCode, IAPMobileErrorCode.ValidatePending, orderResult.Status.ToString());
                        nextPending.Add(context);
                    }
                }

                pending = nextPending;
            }

            foreach (VerifyOrderContext context in pending)
            {
                MarkValidateFailed(context, IAPMobileErrorCode.ValidateNetworkRequestFailed, "验单请求失败。", "验单请求失败，订单已保留等待补单。", maxRetry + 1, true, 0, "验单请求失败。");
            }
        }

        /// <summary>
        /// 首次支付验单因网络/HTTP 异常无法完成时，保留订单用于后续补单，同时结束当前 PayAsync 等待。
        /// 补单路径不触发支付失败回调，避免启动扫描时重复打扰业务层。
        /// </summary>
        /// <param name="record">当前验单订单。</param>
        /// <param name="code">返回给支付调用方的错误码。</param>
        /// <param name="reason">返回给支付调用方的失败说明。</param>
        private void FailCurrentPayValidationIfNeeded(MobileOrderRecord record, IAPMobileErrorCode code, string reason)
        {
            if (record.IsReplenish || CurrentPayTcs == null)
            {
                return;
            }

            var failResult = new IAPResult(record.TableId, (int)code, reason, BuildCustomData(record));
            m_Hub.Context.EventBridge?.RaisePayFailed(failResult);
            CurrentPayTcs.TrySetResult(failResult);
            CurrentPayTcs = null;
        }

        /// <summary>
        /// 构建单笔订单验单上下文，缓存商品配置、平台商品对象与订单号，避免批量处理中反复查表。
        /// </summary>
        /// <param name="record">待验单订单记录。</param>
        /// <returns>验单上下文。</returns>
        private VerifyOrderContext BuildVerifyContext(MobileOrderRecord record)
        {
            IAPProductEntry entry = m_Hub.Table?.FindByTableId(record.TableId);
            Product product = entry == null ? null : m_Hub.ProductService.GetProduct(entry.ProductID);
            return new VerifyOrderContext
            {
                Record = record,
                Entry = entry,
                Product = product,
                IsSubscription = entry?.ProductType == IAPProductType.Subscription,
                TransactionId = record.TransactionId ?? string.Empty,
            };
        }

        /// <summary>
        /// 将订单标记为验单失败并通知首次支付等待点；补单路径只保留存档，不重复触发业务失败事件。
        /// </summary>
        /// <param name="record">目标订单记录。</param>
        /// <param name="reason">失败原因。</param>
        private void MarkValidateFailed(VerifyOrderContext context, IAPMobileErrorCode trackReason, string reasonDetail, string reason, int validateCount, bool netError, int protocolCode, string protocolMessage)
        {
            MobileOrderRecord record = context.Record;
            m_Hub.Store.TrackValidateFailFinishInternal(record, context.Product, validateCount, netError, protocolCode, protocolMessage, trackReason, reasonDetail);
            record.Status = MobileOrderStatus.ValidateFailed;
            SaveOrderRecords();
            FailCurrentPayValidationIfNeeded(record, IAPMobileErrorCode.NetworkError, reason);
        }

        /// <summary>
        /// 判断本地订单是否具备入验单队列的必要凭据。
        /// Android Google 订单必须有 purchase token；iOS Apple 订单必须有 order_id。
        /// </summary>
        /// <param name="record">待检查订单记录。</param>
        /// <returns>可入队时返回 true。</returns>
        private static bool CanEnqueueLocalRecord(MobileOrderRecord record)
        {
#if UNITY_ANDROID
            if (record == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(record.GoogleToken))
            {
                return true;
            }

            Log.Warning(LogTag.IAPMobile, $"Google 补单缺少 purchase token，跳过本轮验单，商品表ID={record.TableId}，状态={record.Status}");
            return false;
#elif UNITY_IOS
            if (record == null)
            {
                return false;
            }

            if (!MobileValidationPolicy.ShouldDropAppleOrderMissingCredential(record.TransactionId))
            {
                return true;
            }

            Log.Warning(LogTag.IAPMobile, $"Apple 补单缺少 order_id，删除本地待验记录，商品表ID={record.TableId}，状态={record.Status}");
            return false;
#else
            return record != null;
#endif
        }

        /// <summary>
        /// 判断验单上下文是否允许发起协议请求。
        /// Android Google 订单缺少 token 时保留本地记录等待下次补齐；iOS Apple 订单缺少 order_id 时删除本地记录。
        /// </summary>
        /// <param name="context">待检查验单上下文。</param>
        /// <returns>可发送验单协议时返回 true。</returns>
        private bool CanSendVerifyContext(VerifyOrderContext context)
        {
#if UNITY_ANDROID
            MobileOrderRecord record = context?.Record;
            if (record == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(record.GoogleToken))
            {
                return true;
            }

            Log.Warning(LogTag.IAPMobile, $"Google 验单缺少 purchase token，保留订单等待补齐，商品表ID={record.TableId}，状态={record.Status}");
            TrackValidateFail(context, 1, false, 0, IAPMobileErrorCode.ValidateCredentialMissing, "Google 验单缺少 purchase token。");
            FailCurrentPayValidationIfNeeded(record, IAPMobileErrorCode.StoreNotAvailable, "Google 验单缺少 purchase token，订单已保留等待补单。");
            return false;
#elif UNITY_IOS
            MobileOrderRecord record = context?.Record;
            if (record == null)
            {
                return false;
            }

            if (!MobileValidationPolicy.ShouldDropAppleOrderMissingCredential(record.TransactionId))
            {
                return true;
            }

            Log.Warning(LogTag.IAPMobile, $"Apple 验单缺少 order_id，删除本地待验记录，商品表ID={record.TableId}，状态={record.Status}");
            TrackValidateFail(context, 1, false, 0, IAPMobileErrorCode.ValidateCredentialMissing, "Apple 验单缺少 order_id。");
            FailCurrentPayValidationIfNeeded(record, IAPMobileErrorCode.StoreNotAvailable, "Apple 验单缺少 order_id，订单已删除。");
            RemoveLocalOrderRecord(record.TableId, "Apple 验单缺少 order_id，删除本地待验记录。");
            return false;
#else
            return context?.Record != null;
#endif
        }

        /// <summary>
        /// 判断本地订单是否因缺少不可恢复凭据而应删除。
        /// </summary>
        /// <param name="record">待检查订单记录。</param>
        /// <returns>应删除本地记录时返回 true。</returns>
        private static bool ShouldRemoveLocalRecordWithoutCredential(MobileOrderRecord record)
        {
#if UNITY_IOS
            return record != null && MobileValidationPolicy.ShouldDropAppleOrderMissingCredential(record.TransactionId);
#else
            return false;
#endif
        }

        /// <summary>
        /// 删除本地待验订单并立即落盘。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="reason">删除原因，用于日志。</param>
        private void RemoveLocalOrderRecord(long tableId, string reason)
        {
            if (tableId == 0L)
            {
                return;
            }

            if (m_OrderRecords.Remove(tableId))
            {
                Log.Warning(LogTag.IAPMobile, $"{reason} 商品表ID={tableId}");
                SaveOrderRecords();
            }
        }

        /// <summary>
        /// 处理单笔验单结果。返回 false 表示该订单本轮未终结，需要进入下一次重试或最终 ValidateFailed。
        /// </summary>
        /// <param name="context">订单验单上下文。</param>
        /// <param name="orderResult">服务端返回的单笔验单结果。</param>
        /// <param name="isSubscription">当前协议组是否为订阅。</param>
        /// <returns>订单已终结时返回 true；需要重试时返回 false。</returns>
        private bool TryFinishVerifyResult(VerifyOrderContext context, PbNetMobileVerifyOrderResult orderResult, bool isSubscription, int validateCount)
        {
            PbNetMobileVerifyOrderStatus status = orderResult.Status;
            if (string.IsNullOrEmpty(context.Record.TransactionId) && !string.IsNullOrEmpty(orderResult.OrderId))
            {
                context.Record.TransactionId = orderResult.OrderId;
            }

            switch (status)
            {
                case PbNetMobileVerifyOrderStatus.Verified:
                case PbNetMobileVerifyOrderStatus.Delivered:
                {
                    long expireMs = orderResult.HasExpireTime ? orderResult.ExpireTime : 0L;
                    m_Hub.Store.TrackValidateSuccessInternal(context.Record, context.Product, validateCount, orderResult.OrderId);
                    ConfirmAndFinish(context.Record, context.Product, true, expireMs, isSubscription, false, orderResult.OrderId, MobileValidationPolicy.ShouldCollectSubscriptionRestored((int)status, isSubscription));
                    return true;
                }
                case PbNetMobileVerifyOrderStatus.Reissued:
                {
                    long expireMs = orderResult.HasExpireTime ? orderResult.ExpireTime : 0L;
                    m_Hub.Store.TrackValidateSuccessInternal(context.Record, context.Product, validateCount, orderResult.OrderId);
                    ConfirmAndFinish(context.Record, context.Product, false, expireMs, isSubscription, false, orderResult.OrderId, MobileValidationPolicy.ShouldCollectSubscriptionRestored((int)status, isSubscription));
                    return true;
                }
                case PbNetMobileVerifyOrderStatus.Invalid:
                    m_Hub.Store.TrackValidateFailFinishInternal(context.Record, context.Product, validateCount, false, 0, "服务端拒绝无效订单。", IAPMobileErrorCode.ValidateInvalid, status.ToString());
                    ConfirmAndFinish(context.Record, context.Product, false, 0L, false, true, orderResult.OrderId, false);
                    return true;
                case PbNetMobileVerifyOrderStatus.Unspecified:
                case PbNetMobileVerifyOrderStatus.PendingVerify:
                default:
                    Log.Warning(LogTag.IAPMobile, $"验单服务端未完成状态，状态={status}，商品表ID={context.Record.TableId}");
                    return false;
            }
        }

        /// <summary>
        /// 批量上报本轮验单失败。
        /// </summary>
        /// <param name="contexts">本轮仍在等待验单的订单上下文。</param>
        /// <param name="validateCount">验单尝试次数。</param>
        /// <param name="netError">是否网络错误。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="reason">失败原因。</param>
        private void TrackValidateFailBatch(List<VerifyOrderContext> contexts, int validateCount, bool netError, int protocolCode, IAPMobileErrorCode reason, string reasonDetail)
        {
            foreach (VerifyOrderContext context in contexts)
            {
                TrackValidateFail(context, validateCount, netError, protocolCode, reason, reasonDetail);
            }
        }

        /// <summary>
        /// 上报单笔验单失败，首次主动支付的第一次失败额外上报首单验单失败事件。
        /// </summary>
        /// <param name="context">订单验单上下文。</param>
        /// <param name="validateCount">验单尝试次数。</param>
        /// <param name="netError">是否网络错误。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="reason">失败原因。</param>
        private void TrackValidateFail(VerifyOrderContext context, int validateCount, bool netError, int protocolCode, IAPMobileErrorCode reason, string reasonDetail)
        {
            if (context?.Record == null)
            {
                return;
            }

            m_Hub.Store.TrackValidateFailInternal(context.Record, context.Product, validateCount, netError, protocolCode, reason, reasonDetail);
            if (validateCount == 1 && !context.Record.IsReplenish)
            {
                m_Hub.Store.TrackFirstPayOrderValidateInternal(context.Record, context.Product, validateCount, netError);
            }
        }

        /// <summary>
        /// 验单完成后的最终处理：假单时触发失败事件并清理记录；真单时更新订阅/非消耗品存档，
        /// 发送支付成功事件，区分 replenish 与正常购买路由结果通知。
        /// </summary>
        /// <param name="record">已完成验单的订单记录。</param>
        /// <param name="product">对应的 Unity IAP Product 对象（可为 null）。</param>
        /// <param name="canDeliver">服务端确认本次需要客户端发货时为 true；已补发或已发货时为 false。</param>
        /// <param name="expireMs">订阅到期 Unix 毫秒时间戳；非订阅商品传 0。</param>
        /// <param name="isSubscription">当前商品是否为订阅类型。</param>
        /// <param name="isFake">服务端判定为无效订单时为 true。</param>
        /// <param name="orderId">订单ID。</param>
        /// <param name="collectSubscriptionRestored">是否收集订阅恢复结果。</param>
        private void ConfirmAndFinish(MobileOrderRecord record, Product product, bool canDeliver, long expireMs, bool isSubscription, bool isFake, string orderId, bool collectSubscriptionRestored)
        {
            if (isFake)
            {
                m_Hub.Store.MarkRuntimeHandledTransactionInternal(record.TransactionId);
                m_PendingPlatformOrders.Remove(record.TableId);
                m_OrderRecords.Remove(record.TableId);
                SaveOrderRecords();
                var failResult = new IAPResult(record.TableId, (int)IAPMobileErrorCode.ServerValidationFailed, "服务端拒绝无效订单。", BuildCustomData(record));
                m_Hub.Context.EventBridge?.RaisePayFailed(failResult);
                CurrentPayTcs?.TrySetResult(failResult);
                CurrentPayTcs = null;
                return;
            }

            if (isSubscription && expireMs > 0L)
            {
                m_Hub.SubscriptionService.SaveExpireTime(record.TableId, expireMs);
                if (SubscriptionUpgradeTableId > 0L && SubscriptionUpgradeTableId != record.TableId)
                {
                    // 升降级成功后将旧订阅到期时间清零，避免旧订阅仍显示为有效
                    m_Hub.SubscriptionService.SaveExpireTime(SubscriptionUpgradeTableId, 0L);
                    SubscriptionUpgradeTableId = 0L;
                }
            }
            else if (product?.definition.type == ProductType.NonConsumable && canDeliver)
            {
                SaveNonConsumeOwnership(record.TableId, true);
                m_Hub.ProductService.m_NonConsumablePurchased.Add(record.TableId);
            }

            m_OrderRecords.Remove(record.TableId);
            SaveOrderRecords();
            ConfirmPendingPlatformOrder(record.TableId);

            bool isReplenish = record.IsReplenish;
            IAPResult result = IAPResult.SuccessWithExpire(record.TableId, record.TransactionId ?? string.Empty, isReplenish, canDeliver, BuildCustomData(record), expireMs);
            bool shouldRaisePaySuccess = ShouldRaisePaySuccess(record, canDeliver, isSubscription);
            m_Hub.Store.MarkRuntimeHandledTransactionInternal(record.TransactionId);
            if (shouldRaisePaySuccess)
            {
                MarkPaySuccessDispatched(record);
                m_Hub.Context.EventBridge?.RaisePaySuccess(result);
            }

            ProductType pt = product?.definition.type ?? ProductType.Consumable;
              if (isReplenish && m_Hub.RestoreService != null)
            {
                bool collectRestoreResult = !isSubscription || collectSubscriptionRestored;
                m_Hub.RestoreService.NotifyValidationComplete(result, pt, collectRestoreResult);
            }
            else
            {
                CurrentPayTcs?.TrySetResult(result);
                CurrentPayTcs = null;
            }
        }

        /// <summary>
        /// 判断本次验单完成是否需要派发全局 PaySuccess。
        /// </summary>
        /// <param name="record">已完成验单的订单记录。</param>
        /// <param name="canDeliver">服务端确认本地需要发奖时为 true。</param>
        /// <param name="isSubscription">当前商品是否为订阅类型。</param>
        /// <returns>需要派发 PaySuccess 时返回 true。</returns>
        private bool ShouldRaisePaySuccess(MobileOrderRecord record, bool canDeliver, bool isSubscription)
        {
            if (!canDeliver)
            {
                return false;
            }

            if (HasDispatchedPaySuccess(record))
            {
                Log.Debug(LogTag.IAPMobile, $"订单已派发过支付成功事件，本次只清理订单不重复通知，商品表ID={record.TableId}，订单号={record.TransactionId}");
                return false;
            }

            if (!isSubscription)
            {
                return true;
            }

            return !record.IsReplenish && CurrentPayTcs != null;
        }

        /// <summary>
        /// 判断订单在当前运行期是否已经派发过 PaySuccess。
        /// </summary>
        /// <param name="record">待检查订单。</param>
        /// <returns>已派发过时返回 true。</returns>
        private bool HasDispatchedPaySuccess(MobileOrderRecord record)
        {
            return record != null &&
                   record.TableId != 0L &&
                   m_DispatchedPaySuccessTableIds.Contains(record.TableId);
        }

        /// <summary>
        /// 记录订单在当前运行期已派发 PaySuccess。
        /// </summary>
        /// <param name="record">已派发成功事件的订单。</param>
        private void MarkPaySuccessDispatched(MobileOrderRecord record)
        {
            if (record?.TableId != 0L)
            {
                m_DispatchedPaySuccessTableIds.Add(record.TableId);
            }
        }

        /// <summary>
        /// 将非消耗品持有标记写入统一存档并触发 Save。
        /// </summary>
        /// <param name="tableId">非消耗品商品配置表行 ID。</param>
        /// <param name="owned">true = 已持有，false = 未持有。</param>
        private void SaveNonConsumeOwnership(long tableId, bool owned)
        {
            var persist = m_Hub.Store?.PersistData;
            if (persist == null)
            {
                return;
            }

            persist.NonConsumeOwnership[tableId] = owned;
            m_Hub.Store.SavePersistDataInternal();
        }

        /// <summary>
        /// 在验单响应 OrderList 中按 tableId 匹配本次请求的订单结果；
        /// 响应中只有一条时才采用唯一结果，避免多订单响应错配。
        /// </summary>
        /// <param name="verifyResp">服务端验单响应数据。</param>
        /// <param name="context">本次验单请求上下文。</param>
        /// <param name="allowSingleFallback">是否允许单请求单响应时按唯一结果兜底。</param>
        /// <returns>匹配到的 OrderResult；OrderList 为空时返回 null。</returns>
        private static PbNetMobileVerifyOrderResult FindOrderResult(PbNetMobileVerifyResp verifyResp, VerifyOrderContext context, bool allowSingleFallback)
        {
            if (verifyResp?.OrderList == null || verifyResp.OrderList.Count == 0)
            {
                return null;
            }

            long tableId = context.Record.TableId;
            foreach (PbNetMobileVerifyOrderResult item in verifyResp.OrderList)
            {
                if (item != null && item.TableId == tableId)
                {
                    return item;
                }
            }

            return allowSingleFallback && verifyResp.OrderList.Count == 1 ? verifyResp.OrderList[0] : null;
        }

        /// <summary>
        /// 服务端验单通过后确认平台 PendingOrder，通知平台业务侧已完成发货处理。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        private void ConfirmPendingPlatformOrder(long tableId)
        {
            if (!m_PendingPlatformOrders.TryGetValue(tableId, out PendingOrder pendingOrder))
            {
                return;
            }

            m_PendingPlatformOrders.Remove(tableId);
            m_Hub.ExtendedService.ConfirmPurchase(pendingOrder);
        }

        /// <summary>
        /// 从订单记录中读取透传字符串数据；字段为空时返回 null。
        /// </summary>
        /// <param name="record">目标订单记录，读取其 CustomDataParam 字段。</param>
        /// <returns>透传字符串数据；CustomDataParam 为空时返回 null。</returns>
        private static string BuildCustomData(MobileOrderRecord record)
        {
            return string.IsNullOrEmpty(record.CustomDataParam) ? null : record.CustomDataParam;
        }

        /// <summary>
        /// 按当前平台和商品类型路由到对应验单协议：普通订单与订阅订单使用各自独立的请求类型与 cmdName（不同 URL），返回通用验单响应。
        /// </summary>
        /// <param name="contexts">同一协议组的待验单订单上下文。</param>
        /// <param name="isSubscription">当前商品是否为订阅类型，true 时路由到订阅协议与订阅 cmdName。</param>
        /// <param name="attempt">当前请求重试次数，从 0 开始。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>包含验单响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetMobileVerifyResp>> SendVerifyBatchAsync(IReadOnlyList<VerifyOrderContext> contexts, bool isSubscription, int attempt, CancellationToken ct)
        {
#if UNITY_ANDROID
            string cmdName = isSubscription ? m_Hub.Config?.GoogleVerifySubscriptionCmdName : m_Hub.Config?.GoogleVerifyCmdName;
            var items = new List<PbNetGoogleVerifyIapOrderItem>();
            foreach (VerifyOrderContext context in contexts)
            {
                items.Add(new PbNetGoogleVerifyIapOrderItem
                {
                    ProductId = context.Entry?.ProductID ?? string.Empty,
                    Token = context.Record.GoogleToken ?? string.Empty,
                    Price = context.Product != null ? (float)context.Product.metadata.localizedPrice : 0f,
                });
            }
            NetResponse<PbNetMobileVerifyResp> resp = await m_Hub.PayService.VerifyGoogleAsync(cmdName, items, isSubscription);
            return resp;
#elif UNITY_IOS
            string cmdName = isSubscription ? m_Hub.Config?.AppleVerifySubscriptionCmdName : m_Hub.Config?.AppleVerifyCmdName;
            var items = new List<PbNetAppleVerifyIapOrderItem>();
            foreach (VerifyOrderContext context in contexts)
            {
                items.Add(new PbNetAppleVerifyIapOrderItem
                {
                    OrderId = context.TransactionId,
                    Price = context.Product != null ? (float)context.Product.metadata.localizedPrice : 0f,
                });
            }
            NetResponse<PbNetMobileVerifyResp> resp = await m_Hub.PayService.VerifyAppleAsync(cmdName, items, isSubscription);
            return resp;
#else
            Log.Warning(LogTag.IAPMobile, $"不支持的平台，批量验单数量={contexts?.Count ?? 0}");
            return NetResponse<PbNetMobileVerifyResp>.Fail(0, "不支持的平台");
#endif
        }

        /// <summary>
        /// 批量验单时使用的单笔订单上下文。
        /// </summary>
        private sealed class VerifyOrderContext
        {
            /// <summary>
            /// 当前待验单的本地订单记录。
            /// </summary>
            internal MobileOrderRecord Record;

            /// <summary>
            /// 当前订单对应的 IAP 商品配置。
            /// </summary>
            internal IAPProductEntry Entry;

            /// <summary>
            /// 当前订单对应的 Unity IAP 商品对象。
            /// </summary>
            internal Product Product;

            /// <summary>
            /// 当前订单是否属于订阅商品。
            /// </summary>
            internal bool IsSubscription;

            /// <summary>
            /// 当前订单用于服务端匹配的交易订单号。
            /// </summary>
            internal string TransactionId;
        }
    }
}
