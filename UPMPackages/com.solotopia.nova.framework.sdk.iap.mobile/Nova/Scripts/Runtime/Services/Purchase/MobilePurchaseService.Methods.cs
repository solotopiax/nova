/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobilePurchaseService.Methods.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   MobilePurchaseService 私有方法：路由、编码、回调处理、等待桥接
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobilePurchaseService
    {
        /// <summary>
        /// 处理订阅购买路由：检查同订阅组内是否已有有效订阅，有则走升降级或返回已订阅结果，无则发起订阅购买。
        /// </summary>
        /// <param name="request">原始支付请求。</param>
        /// <param name="entry">待购买商品的配置表行。</param>
        /// <param name="product">Unity IAP Product 对象。</param>
        /// <returns>订阅购买结果；异常未进入任何订阅分支时返回 null。</returns>
        private async UniTask<IAPResult> HandleSubscriptionPayAsync(IAPMobileRequest request, IAPProductEntry entry, Product product)
        {
            if (m_Hub.Store.InSubscriptionPeriod(request.TableId))
            {
                return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.SubscriptionIsReady, $"当前订阅商品仍在有效期内（tableId={request.TableId}）。", request.CustomData));
            }

            long activeGroupTableId = FindActiveSubscriptionInGroup(request.TableId);
            if (activeGroupTableId == 0)
            {
                return await DoPlatformPayAsync(request, product);
            }

#if UNITY_ANDROID
            // Android 支持同组订阅升降级，使用旧商品 ID 触发替换购买
            IAPProductEntry oldEntry = m_Hub.Table.FindByTableId(activeGroupTableId);
            if (oldEntry == null)
            {
                return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.ProductNotFound, $"订阅组内有效订阅 tableId={activeGroupTableId} 未在配置中找到。", request.CustomData));
            }

            return await DoUpgradePayAsync(request, entry, oldEntry);
#elif UNITY_IOS
            // iOS 不支持组内升降级，直接返回已订阅错误，让业务层提示用户
            return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.SubscriptionIsReady, $"订阅组内已有有效订阅（tableId={activeGroupTableId}），iOS 不支持组内升降级。", request.CustomData));
#else
            // 其他平台同样不支持升降级
            return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.SubscriptionIsReady, $"订阅组内已有有效订阅（tableId={activeGroupTableId}）。", request.CustomData));
#endif
        }

#if UNITY_ANDROID
        /// <summary>
        /// Android 订阅升降级购买流程：设置防重入标志，写入 Purchasing 存档，透传 UUID 参数后调用 PurchaseProduct，await 平台回调结果。
        /// </summary>
        /// <param name="request">原始支付请求。</param>
        /// <param name="newEntry">目标新订阅商品配置行。</param>
        /// <param name="oldEntry">当前已有订阅商品配置行。</param>
        /// <returns>包含支付结果的 IAPResult。</returns>
        private async UniTask<IAPResult> DoUpgradePayAsync(IAPMobileRequest request, IAPProductEntry newEntry, IAPProductEntry oldEntry)
        {
            InPayTableId = request.TableId;
            m_CurrentCustomData = request.CustomData;
            var payTcs = new UniTaskCompletionSource<IAPResult>();
            m_PayTcs = payTcs;
            var replaceMode = (GooglePlayReplacementMode)m_SubscriptionReplaceMode;
            m_Hub.ValidationService.SubscriptionUpgradeTableId = oldEntry.TableId;
            m_Hub.ValidationService.RemoveAllPurchasingRecords();
            m_Hub.ValidationService.WritePurchasingRecord(request.TableId, request.CustomData);
            m_Hub.Store.AddWaitingRef();
            try
            {
                // IAP 5.x：直接通过 Controller 购买（升降级通过 PurchaseProduct 带旧商品 ID 参数不再直接暴露 UpgradeDowngrade API）
                ApplyPurchaseContext(request.TableId);
                Product product = m_Hub.ExtendedService.GetProductById(newEntry.ProductID);
                if (!m_Hub.ExtendedService.IsAttached || product == null)
                {
                    return CompleteActivePayFailure(request.TableId, IAPMobileErrorCode.ProductNotFound, $"平台商品 {newEntry.ProductID} 不可购买。", request.CustomData, true)
                           ?? new IAPResult(request.TableId, (int)IAPMobileErrorCode.ProductNotFound, $"平台商品 {newEntry.ProductID} 不可购买。", request.CustomData);
                }

                m_Hub.Store.TrackBuyInternal(request.TableId, product, request.CustomData);
                m_Hub.ExtendedService.PurchaseProduct(product);
                return await payTcs.Task;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.IAPMobile, $"平台发起订阅升降级购买异常，商品表ID={request.TableId}，详情={ex.Message}");
                return CompleteActivePayFailure(request.TableId, IAPMobileErrorCode.StoreNotAvailable, $"平台发起购买异常：{ex.Message}", request.CustomData, true)
                       ?? new IAPResult(request.TableId, (int)IAPMobileErrorCode.StoreNotAvailable, $"平台发起购买异常：{ex.Message}", request.CustomData);
            }
            finally
            {
                m_Hub.Store.SubWaitingRef();
            }
        }
#endif

        /// <summary>
        /// 平台购买流程：设置防重入标志，写入 Purchasing 存档，透传 UUID 参数后调用 PurchaseProduct，await 平台回调结果。
        /// </summary>
        /// <param name="request">原始支付请求。</param>
        /// <param name="product">目标商品 Unity IAP Product 对象。</param>
        /// <returns>包含支付结果的 IAPResult。</returns>
        private async UniTask<IAPResult> DoPlatformPayAsync(IAPMobileRequest request, Product product)
        {
            InPayTableId = request.TableId;
            m_CurrentCustomData = request.CustomData;
            var payTcs = new UniTaskCompletionSource<IAPResult>();
            m_PayTcs = payTcs;
            m_Hub.ValidationService.RemoveAllPurchasingRecords();
            m_Hub.ValidationService.WritePurchasingRecord(request.TableId, request.CustomData);
            m_Hub.Store.AddWaitingRef();
            try
            {
                ApplyPurchaseContext(request.TableId);
                if (!m_Hub.ExtendedService.IsAttached || product == null)
                {
                    return CompleteActivePayFailure(request.TableId, IAPMobileErrorCode.ProductNotFound, "平台商品不可购买。", request.CustomData, true)
                           ?? new IAPResult(request.TableId, (int)IAPMobileErrorCode.ProductNotFound, "平台商品不可购买。", request.CustomData);
                }

                m_Hub.Store.TrackBuyInternal(request.TableId, product, request.CustomData);
                m_Hub.ExtendedService.PurchaseProduct(product);
                return await payTcs.Task;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.IAPMobile, $"平台发起购买异常，商品表ID={request.TableId}，详情={ex.Message}");
                return CompleteActivePayFailure(request.TableId, IAPMobileErrorCode.StoreNotAvailable, $"平台发起购买异常：{ex.Message}", request.CustomData, true)
                       ?? new IAPResult(request.TableId, (int)IAPMobileErrorCode.StoreNotAvailable, $"平台发起购买异常：{ex.Message}", request.CustomData);
            }
            finally
            {
                m_Hub.Store.SubWaitingRef();
            }
        }

        /// <summary>
        /// 将 UID + tableId 编码为 UUID，写入平台透传参数（Android: ObfuscatedAccountId/ProfileId；iOS: AppAccountToken）。
        /// </summary>
        /// <param name="tableId">当前购买商品的配置表行 ID。</param>
        private void ApplyPurchaseContext(long tableId)
        {
            string uid = m_Hub.Store?.GameUID ?? string.Empty;
            string uuid = MobileStoreParameterCodec.Encode(uid, tableId);
            if (string.IsNullOrEmpty(uuid))
            {
                return;
            }

            m_Hub.ExtendedService.SetObfuscatedAccountId(uuid);
            m_Hub.ExtendedService.SetObfuscatedProfileId(uuid);
#if UNITY_IOS
            if (Guid.TryParse(uuid, out Guid parsedGuid))
            {
                m_Hub.ExtendedService.SetAppAccountToken(parsedGuid);
            }
#endif
        }

        /// <summary>
        /// 处理 ConfirmedOrder：缓存 receipt，解析 tableId，构建 MobileOrderRecord，加入验单队列；
        /// 非 recovered order 时桥接 WaitForValidationAsync 到 m_PayTcs。
        /// </summary>
        /// <param name="order">平台返回的已确认订单。</param>
        private void HandleConfirmedOrder(ConfirmedOrder order)
        {
            Product product = m_Hub.ProductService.GetFirstProductInOrder(order);
            if (product == null)
            {
                Log.Warning(LogTag.IAPMobile, "平台订单确认回调中未找到商品。");
                CompleteActivePayFailure(InPayTableId, IAPMobileErrorCode.ProductNotFound, "平台确认回调中未找到商品。", m_CurrentCustomData, true);
                return;
            }

            m_Hub.ProductService.CacheReceipt(product.definition.id, order.Info.Receipt);

            string encodedUuid = order.Info.Google?.ObfuscatedAccountId ?? order.Info.Apple?.AppAccountToken?.ToString();
            bool hasPurchaseContext = TryParseTableId(encodedUuid, out long tableId);
            if (!hasPurchaseContext)
            {
                tableId = ResolveTableIdFromTable(product.definition.id);
                if (tableId == 0L)
                {
                    Log.Warning(LogTag.IAPMobile, $"平台订单确认回调无法解析商品表ID，商品ID={product.definition.id}");
                    CompleteActivePayFailure(InPayTableId, IAPMobileErrorCode.ProductNotFound, $"平台确认回调无法解析 tableId，productId={product.definition.id}", m_CurrentCustomData, true);
                    return;
                }
            }

            if (m_PayTcs == null && !m_Hub.ValidationService.HasOrderRecord(tableId))
            {
                Log.Debug(LogTag.IAPMobile, $"平台订单确认完成，商品表ID={tableId}，商品ID={product.definition.id}");
                return;
            }

            bool isCurrentPayOrder = m_PayTcs != null && tableId == InPayTableId;
            bool isRecovered = !isCurrentPayOrder;
            var payTcs = isCurrentPayOrder ? m_PayTcs : null;
            var customData = isCurrentPayOrder ? m_CurrentCustomData : null;
            if (isCurrentPayOrder)
            {
                // 只允许当前点击的商品消费活跃支付状态；历史 PendingOrder 按补单处理。
                InPayTableId = 0;
                m_CurrentCustomData = null;
                m_PayTcs = null;
            }

            m_Hub.ProductService.GetReceiptInfo(product.definition.id, out string orderId, out string googleToken);
            Log.Debug(LogTag.IAPMobile, $"平台订单确认回调：商品表ID={tableId}，商品ID={product.definition.id}，订单号={orderId}，是否补单={isRecovered}");

            var record = new MobileOrderRecord
            {
                TransactionId = orderId ?? string.Empty,
                TableId = tableId,
                GoogleToken = googleToken,
                Status = MobileOrderStatus.PendingValidate,
                IsReplenish = isRecovered,
                CustomDataParam = customData,
            };

            UniTaskCompletionSource<IAPResult> validateTcs = null;
            if (!isRecovered)
            {
                validateTcs = new UniTaskCompletionSource<IAPResult>();
                m_Hub.ValidationService.CurrentPayTcs = validateTcs;
            }

            m_Hub.ValidationService.AddAndEnqueue(record);
            m_Hub.Store.TrackLocalPaySuccessInternal(record, product);

            if (!isRecovered && validateTcs != null)
            {
                WaitForValidationAsync(payTcs, validateTcs).Forget();
            }
        }

        /// <summary>
        /// 处理 PendingOrder：缓存票据、解析 tableId、登记待确认平台订单，并入队服务端验单。
        /// </summary>
        /// <param name="order">平台待确认订单。</param>
        /// <param name="product">订单内第一个商品。</param>
        private void HandlePendingOrder(PendingOrder order, Product product)
        {
            m_Hub.ProductService.CacheReceipt(product.definition.id, order.Info.Receipt);

            string encodedUuid = order.Info.Google?.ObfuscatedAccountId ?? order.Info.Apple?.AppAccountToken?.ToString();
            bool hasPurchaseContext = TryParseTableId(encodedUuid, out long tableId);
            if (!hasPurchaseContext)
            {
                tableId = ResolveTableIdFromTable(product.definition.id);
                if (tableId == 0L)
                {
                    Log.Warning(LogTag.IAPMobile, $"平台待确认购买回调无法解析商品表ID，商品ID={product.definition.id}");
                    CompleteActivePayFailure(InPayTableId, IAPMobileErrorCode.ProductNotFound, $"平台待确认回调无法解析 tableId，productId={product.definition.id}", m_CurrentCustomData, true);
                    return;
                }
            }

            bool isCurrentPayOrder = m_PayTcs != null && tableId == InPayTableId;
            bool isRecovered = !isCurrentPayOrder;
            var payTcs = isCurrentPayOrder ? m_PayTcs : null;
            var customData = isCurrentPayOrder ? m_CurrentCustomData : null;
            if (isCurrentPayOrder)
            {
                InPayTableId = 0;
                m_CurrentCustomData = null;
                m_PayTcs = null;
            }

            m_Hub.ProductService.GetReceiptInfo(product.definition.id, out string orderId, out string googleToken);
            Log.Debug(LogTag.IAPMobile, $"平台待确认购买回调：商品表ID={tableId}，商品ID={product.definition.id}，订单号={orderId}，是否补单={isRecovered}");

            var record = new MobileOrderRecord
            {
                TransactionId = orderId ?? string.Empty,
                TableId = tableId,
                GoogleToken = googleToken,
                Status = MobileOrderStatus.PendingValidate,
                IsReplenish = isRecovered,
                CustomDataParam = customData,
            };

            m_Hub.ValidationService.RegisterPendingPlatformOrder(tableId, order);

            UniTaskCompletionSource<IAPResult> validateTcs = null;
            if (!isRecovered)
            {
                validateTcs = new UniTaskCompletionSource<IAPResult>();
                m_Hub.ValidationService.CurrentPayTcs = validateTcs;
            }

            m_Hub.ValidationService.AddAndEnqueue(record);
            m_Hub.Store.TrackLocalPaySuccessInternal(record, product);

            if (!isRecovered && validateTcs != null)
            {
                WaitForValidationAsync(payTcs, validateTcs).Forget();
            }
        }

        /// <summary>
        /// ConfirmPurchase 返回 FailedOrder 时的处理入口；确认失败不等同于购买失败。
        /// </summary>
        /// <param name="order">确认失败订单。</param>
        private void HandleConfirmFailed(FailedOrder order)
        {
            long tableId = ResolveTableIdFromFailedOrder(order);
            bool hasActivePay = m_PayTcs != null || InPayTableId != 0;
            bool hasLocalOrder = tableId > 0L && m_Hub.ValidationService.HasOrderRecord(tableId);
            if (!hasActivePay && !hasLocalOrder)
            {
                Log.Debug(LogTag.IAPMobile, $"平台订单确认失败但本地订单已终结，忽略回调，商品表ID={tableId}，原因={order.FailureReason}");
                return;
            }

            Log.Warning(LogTag.IAPMobile, $"平台订单确认失败，保留本地订单等待后续补单，商品表ID={tableId}，原因={order.FailureReason}");
        }

        /// <summary>
        /// 统一处理购买失败：解析 tableId，清理支付状态，将失败原因映射为 IAPMobileErrorCode 后通过 m_PayTcs 返回结果。
        /// </summary>
        /// <param name="order">失败订单。</param>
        private void HandlePurchaseFailed(FailedOrder order)
        {
            long tableId = ResolveTableIdFromFailedOrder(order);
            Product product = m_Hub.ProductService.GetFirstProductInOrder(order);

            if (m_PayTcs == null && InPayTableId == 0 && !m_Hub.ValidationService.HasOrderRecord(tableId))
            {
                Log.Warning(LogTag.IAPMobile, $"平台订单确认失败但本地验单流程已结束，忽略回调，商品表ID={tableId}，原因={order.FailureReason}");
                return;
            }

            m_Hub.Store.SubWaitingRef(true);
            m_Hub.ValidationService.MarkLocalPayFailedAndRemove(tableId);

            string customData = m_CurrentCustomData;
            IAPMobileErrorCode code = MapPurchaseFailureReason(order.FailureReason);
            InPayTableId = 0;
            m_CurrentCustomData = null;
            var payTcs = m_PayTcs;
            m_PayTcs = null;

            Log.Warning(LogTag.IAPMobile, $"平台购买失败，商品表ID={tableId}，原因={order.FailureReason}");
            string failReason = $"平台购买失败：{order.FailureReason}";
            m_Hub.Store.TrackLocalPayFailInternal(tableId, product, code, failReason, customData);
            var failResult = new IAPResult(tableId, (int)code, failReason, customData);
            m_Hub.Context.EventBridge?.RaisePayFailed(failResult);
            payTcs?.TrySetResult(failResult);
        }

        /// <summary>
        /// 从失败订单中解析 tableId；UUID 解码失败时，回退到活跃支付或商品表反查。
        /// </summary>
        /// <param name="order">失败订单。</param>
        /// <returns>解析出的 tableId；无法解析时返回 0。</returns>
        private long ResolveTableIdFromFailedOrder(FailedOrder order)
        {
            Product product = m_Hub.ProductService.GetFirstProductInOrder(order);
            string encodedUuid = order.Info?.Google?.ObfuscatedAccountId ?? order.Info?.Apple?.AppAccountToken?.ToString();
            if (TryParseTableId(encodedUuid, out long tableId))
            {
                return tableId;
            }

            // UUID 透传参数解码失败时，回退到 InPayTableId（正常购买）或商品表反查（补单）
            return InPayTableId != 0 ? InPayTableId : ResolveTableIdFromTable(product?.definition.id);
        }

        /// <summary>
        /// 将当前活跃支付强制结束为失败；仅当前确实存在等待中的支付时才广播，防止补单或迟到回调重复通知。
        /// </summary>
        /// <param name="tableId">失败订单 tableId，传 0 时回退到 InPayTableId。</param>
        /// <param name="code">移动支付错误码。</param>
        /// <param name="message">失败说明。</param>
        /// <param name="customData">业务透传数据。</param>
        /// <param name="removeLocalOrder">是否按本地支付失败清理 Purchasing 占位订单。</param>
        /// <returns>构造出的失败结果；没有活跃支付时返回 null。</returns>
        private IAPResult CompleteActivePayFailure(long tableId, IAPMobileErrorCode code, string message, string customData, bool removeLocalOrder)
        {
            var payTcs = m_PayTcs;
            bool hasActivePay = payTcs != null || InPayTableId != 0;
            if (!hasActivePay)
            {
                return null;
            }

            if (tableId == 0L)
            {
                tableId = InPayTableId;
            }

            InPayTableId = 0;
            m_CurrentCustomData = null;
            m_PayTcs = null;

            if (removeLocalOrder)
            {
                m_Hub.ValidationService.MarkLocalPayFailedAndRemove(tableId);
            }

            m_Hub.Store.SubWaitingRef(true);

            m_Hub.Store.TrackLocalPayFailInternal(tableId, null, code, message, customData);
            var failResult = new IAPResult(tableId, (int)code, message, customData);
            m_Hub.Context.EventBridge?.RaisePayFailed(failResult);
            payTcs?.TrySetResult(failResult);
            return failResult;
        }

        /// <summary>
        /// 尝试从平台透传 UUID 字符串解码出 tableId；失败或空串时返回 false。
        /// </summary>
        /// <param name="encodedUuid">平台透传的编码 UUID 字符串（ObfuscatedAccountId 或 AppAccountToken）。</param>
        /// <param name="tableId">输出：解码得到的配置表行 ID；失败时为 0。</param>
        /// <returns>解码成功且 tableId > 0 时返回 true，否则返回 false。</returns>
        private static bool TryParseTableId(string encodedUuid, out long tableId)
        {
            tableId = 0;
            if (string.IsNullOrEmpty(encodedUuid))
            {
                return false;
            }

            tableId = MobileStoreParameterCodec.DecodeTableId(encodedUuid);
            return tableId > 0;
        }

        /// <summary>
        /// 降级兜底：通过商品表按 productId 反查 tableId；UUID 解码失败时使用。
        /// </summary>
        /// <param name="productId">平台商品 ID。</param>
        /// <returns>找到时返回对应 tableId，否则返回 0。</returns>
        private long ResolveTableIdFromTable(string productId)
        {
            if (m_Hub.Table?.Products == null || string.IsNullOrEmpty(productId))
            {
                return 0L;
            }

            foreach (IAPProductEntry entry in m_Hub.Table.Products)
            {
                if (entry?.ProductID == productId)
                {
                    return entry.TableId;
                }
            }

            return 0L;
        }

        /// <summary>
        /// 在订阅组内查找除当前商品外仍在有效期内的其他订阅 tableId；未找到时返回 0。
        /// </summary>
        /// <param name="tableId">当前待购买的订阅商品配置表行 ID。</param>
        /// <returns>组内有效订阅商品的 tableId；无有效订阅时返回 0。</returns>
        private long FindActiveSubscriptionInGroup(long tableId)
        {
            if (m_Hub.Table?.Products == null)
            {
                return 0;
            }

            IAPProductEntry self = m_Hub.Table.FindByTableId(tableId);
            if (self == null || self.SubGroupID == 0)
            {
                return 0;
            }

            var expireMap = m_Hub.Store?.PersistData?.SubscriptionExpireMs;
            if (expireMap == null)
            {
                return 0;
            }

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            foreach (IAPProductEntry entry in m_Hub.Table.Products)
            {
                if (entry == null || entry.TableId == tableId || entry.SubGroupID != self.SubGroupID)
                {
                    continue;
                }

                if (expireMap.TryGetValue(entry.TableId, out long stored) && stored > 0L && stored >= nowMs)
                {
                    return entry.TableId;
                }
            }

            return 0;
        }

        /// <summary>
        /// 异步等待验单完成，将验单结果转发到购买等待点 payTcs，实现 PayAsync → 验单结果的端到端传递。
        /// </summary>
        /// <param name="payTcs">购买流程完成信号，用于向 PayAsync 调用方返回结果。</param>
        /// <param name="validateTcs">验单服务完成信号，验单结束后触发。</param>
        private static async UniTaskVoid WaitForValidationAsync(UniTaskCompletionSource<IAPResult> payTcs, UniTaskCompletionSource<IAPResult> validateTcs)
        {
            IAPResult result = await validateTcs.Task;
            payTcs?.TrySetResult(result);
        }

        /// <summary>
        /// 将 Unity IAP PurchaseFailureReason 映射为 IAPMobileErrorCode。
        /// </summary>
        /// <param name="reason">Unity IAP 平台购买失败原因。</param>
        /// <returns>对应的 Mobile 错误码。</returns>
        private static IAPMobileErrorCode MapPurchaseFailureReason(PurchaseFailureReason reason)
        {
            return reason switch
            {
                PurchaseFailureReason.PurchasingUnavailable => IAPMobileErrorCode.PurchaseFailurePurchasingUnavailable,
                PurchaseFailureReason.ExistingPurchasePending => IAPMobileErrorCode.PurchaseFailureExistingPurchasePending,
                PurchaseFailureReason.ProductUnavailable => IAPMobileErrorCode.PurchaseFailureProductUnavailable,
                PurchaseFailureReason.SignatureInvalid => IAPMobileErrorCode.PurchaseFailureSignatureInvalid,
                PurchaseFailureReason.UserCancelled => IAPMobileErrorCode.PurchaseFailureUserCancelled,
                PurchaseFailureReason.PaymentDeclined => IAPMobileErrorCode.PurchaseFailurePaymentDeclined,
                PurchaseFailureReason.DuplicateTransaction => IAPMobileErrorCode.PurchaseFailureDuplicateTransaction,
                PurchaseFailureReason.ValidationFailure => IAPMobileErrorCode.PurchaseFailureValidationFailure,
                PurchaseFailureReason.StoreNotConnected => IAPMobileErrorCode.PurchaseFailureStoreNotConnected,
                PurchaseFailureReason.PurchaseMissing => IAPMobileErrorCode.PurchaseFailurePurchaseMissing,
                _ => IAPMobileErrorCode.PurchaseFailureUnknown,
            };
        }
    }
}
