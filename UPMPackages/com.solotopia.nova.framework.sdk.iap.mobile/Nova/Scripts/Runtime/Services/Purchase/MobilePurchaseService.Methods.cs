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
    /// <summary>
    /// MobilePurchaseService 的私有方法分部。
    /// </summary>
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
                return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.SubscriptionIsReady, IAPErrorSource.Mobile, $"当前订阅商品仍在有效期内（tableId={request.TableId}）。", request.CustomData, request.ReceiptParam));
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
                return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.ProductNotFound, IAPErrorSource.Mobile, $"订阅组内有效订阅 tableId={activeGroupTableId} 未在配置中找到。", request.CustomData, request.ReceiptParam));
            }

            return await DoUpgradePayAsync(request, entry, oldEntry);
#elif UNITY_IOS
            // iOS 不支持组内升降级，直接返回已订阅错误，让业务层提示用户
            return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.SubscriptionIsReady, IAPErrorSource.Mobile, $"订阅组内已有有效订阅（tableId={activeGroupTableId}），iOS 不支持组内升降级。", request.CustomData, request.ReceiptParam));
#else
            // 其他平台同样不支持升降级
            return BroadcastFail(new IAPResult(request.TableId, (int)IAPMobileErrorCode.SubscriptionIsReady, IAPErrorSource.Mobile, $"订阅组内已有有效订阅（tableId={activeGroupTableId}）。", request.CustomData, request.ReceiptParam));
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
            m_CurrentReceiptParam = request.ReceiptParam;
            var payTcs = new UniTaskCompletionSource<IAPResult>();
            m_PayTcs = payTcs;
            var replaceMode = (GooglePlayReplacementMode)m_SubscriptionReplaceMode;
            m_Hub.ValidationService.SubscriptionUpgradeTableId = oldEntry.TableId;
            m_Hub.ValidationService.RemoveAllPurchasingRecords();
            m_Hub.ValidationService.WritePurchasingRecord(request.TableId, request.CustomData, request.ReceiptParam);
            m_Hub.Store.AddWaitingRef();
            try
            {
                // IAP 5.x：直接通过 Controller 购买（升降级通过 PurchaseProduct 带旧商品 ID 参数不再直接暴露 UpgradeDowngrade API）
                ApplyPurchaseContext(request.TableId, request.ReceiptParam);
                Product product = m_Hub.ExtendedService.GetProductById(newEntry.ProductID);
                if (!m_Hub.ExtendedService.IsAttached || product == null)
                {
                    return CompleteActivePayFailure(request.TableId, IAPMobileErrorCode.ProductNotFound, $"平台商品 {newEntry.ProductID} 不可购买。", request.CustomData, true)
                           ?? new IAPResult(request.TableId, (int)IAPMobileErrorCode.ProductNotFound, IAPErrorSource.Mobile, $"平台商品 {newEntry.ProductID} 不可购买。", request.CustomData, request.ReceiptParam);
                }

                m_Hub.Store.TrackBuyInternal(request.TableId, product, request.CustomData);
                m_Hub.ExtendedService.PurchaseProduct(product);
                return await payTcs.Task;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.IAPMobile, $"平台发起订阅升降级购买异常，商品表ID={request.TableId}，详情={ex.Message}");
                return CompleteActivePayFailure(request.TableId, IAPMobileErrorCode.StoreNotAvailable, $"平台发起购买异常：{ex.Message}", request.CustomData, true)
                       ?? new IAPResult(request.TableId, (int)IAPMobileErrorCode.StoreNotAvailable, IAPErrorSource.Mobile, $"平台发起购买异常：{ex.Message}", request.CustomData, request.ReceiptParam);
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
            m_CurrentReceiptParam = request.ReceiptParam;
            var payTcs = new UniTaskCompletionSource<IAPResult>();
            m_PayTcs = payTcs;
            m_Hub.ValidationService.RemoveAllPurchasingRecords();
            m_Hub.ValidationService.WritePurchasingRecord(request.TableId, request.CustomData, request.ReceiptParam);
            m_Hub.Store.AddWaitingRef();
            try
            {
                ApplyPurchaseContext(request.TableId, request.ReceiptParam);
                if (!m_Hub.ExtendedService.IsAttached || product == null)
                {
                    return CompleteActivePayFailure(request.TableId, IAPMobileErrorCode.ProductNotFound, "平台商品不可购买。", request.CustomData, true)
                           ?? new IAPResult(request.TableId, (int)IAPMobileErrorCode.ProductNotFound, IAPErrorSource.Mobile, "平台商品不可购买。", request.CustomData, request.ReceiptParam);
                }

                m_Hub.Store.TrackBuyInternal(request.TableId, product, request.CustomData);
                m_Hub.ExtendedService.PurchaseProduct(product);
                return await payTcs.Task;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.IAPMobile, $"平台发起购买异常，商品表ID={request.TableId}，详情={ex.Message}");
                return CompleteActivePayFailure(request.TableId, IAPMobileErrorCode.StoreNotAvailable, $"平台发起购买异常：{ex.Message}", request.CustomData, true)
                       ?? new IAPResult(request.TableId, (int)IAPMobileErrorCode.StoreNotAvailable, IAPErrorSource.Mobile, $"平台发起购买异常：{ex.Message}", request.CustomData, request.ReceiptParam);
            }
            finally
            {
                m_Hub.Store.SubWaitingRef();
            }
        }

        /// <summary>
        /// tableId 的 8 位十进制上限（99,999,999）；ReceiptParam / uid 按 GUID 槽位规则校验。
        /// </summary>
        private const long MaxEncodableValue = 99_999_999L;

        /// <summary>
        /// 校验 tableId 是否在数值范围内，ReceiptParam / uid 是否能无损写入各自 GUID 字符串槽位。
        /// 非空字符串必须是十六进制且不能以 0 开头；否则要么无法成为 iOS AppAccountToken，要么会在左侧补零后丢失原始值，
        /// PayAsync 须在发起真实支付前调用本方法拦截，而不是仅在编码时告警。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="receiptParam">本次支付请求携带的平台票据透传字符串。</param>
        /// <param name="uid">当前登录账号 UID。</param>
        /// <param name="errorDesc">校验失败时的具体原因；全部通过时为 null。</param>
        /// <returns>三者均在合法编码范围内时返回 true。</returns>
        private static bool TryValidatePassthroughParams(long tableId, string receiptParam, string uid, out string errorDesc)
        {
            if (tableId < 0L || tableId > MaxEncodableValue)
            {
                errorDesc = $"tableId={tableId} 超出 8 位编码范围（0~{MaxEncodableValue}），平台透传参数无法正确回解，请检查商品表配置。";
                return false;
            }

            if (GetStringLength(receiptParam) > MobileStoreParameterCodec.ReceiptParamMaxLength)
            {
                errorDesc = $"ReceiptParam='{receiptParam}' 超出 {MobileStoreParameterCodec.ReceiptParamMaxLength} 字符上限，请检查业务传参。";
                return false;
            }

            if (!IsRoundTrippableGuidSlot(receiptParam))
            {
                errorDesc = "ReceiptParam 只能使用 1~16 位十六进制字符，且非空值不能以 0 开头；否则平台透传参数无法无损回解。";
                return false;
            }

            if (GetStringLength(uid) > MobileStoreParameterCodec.UidMaxLength)
            {
                errorDesc = $"uid='{uid}' 超出 {MobileStoreParameterCodec.UidMaxLength} 字符上限，平台透传参数无法正确编码，账号关联不可靠。";
                return false;
            }

            if (!IsRoundTrippableGuidSlot(uid))
            {
                errorDesc = "uid 只能使用 1~8 位十六进制字符，且非空值不能以 0 开头；否则平台透传参数无法无损回解。";
                return false;
            }

            errorDesc = null;
            return true;
        }

        /// <summary>
        /// 获取字符串长度；空串按 0 处理。
        /// </summary>
        /// <param name="value">待校验字符串。</param>
        /// <returns>字符串长度。</returns>
        private static int GetStringLength(string value)
        {
            return string.IsNullOrEmpty(value) ? 0 : value.Length;
        }

        /// <summary>
        /// 判断字符串能否在左补 0、解码时去除左侧 0 的 GUID 槽位布局中无损往返。
        /// 空值代表不透传；非空值只能由 ASCII 十六进制字符组成，且不能以 0 开头。
        /// </summary>
        /// <param name="value">待写入 GUID 固定槽位的业务值。</param>
        /// <returns>值可被当前固定槽位布局无损编码和解码时返回 true。</returns>
        private static bool IsRoundTrippableGuidSlot(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            if (value[0] == '0')
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool isDecimal = character >= '0' && character <= '9';
                bool isLowerHex = character >= 'a' && character <= 'f';
                bool isUpperHex = character >= 'A' && character <= 'F';
                if (!isDecimal && !isLowerHex && !isUpperHex)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 将 UID + tableId + receiptParam 编码为 UUID，写入平台透传参数（Android: ObfuscatedAccountId/ProfileId；iOS: AppAccountToken）。
        /// 调用前须已经过 <see cref="TryValidatePassthroughParams"/> 校验（PayAsync 入口保证），此处只做纯编码。
        /// </summary>
        /// <param name="tableId">当前购买商品的配置表行 ID。</param>
        /// <param name="receiptParam">当前购买请求携带的平台票据透传字符串。</param>
        private void ApplyPurchaseContext(long tableId, string receiptParam)
        {
            string uid = m_Hub.Store?.GameUID ?? string.Empty;
            string uuid = MobileStoreParameterCodec.Encode(uid, tableId, receiptParam);
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
        /// 判断平台回调是否属于当前活跃支付订单。
        /// tableId 与 ReceiptParam 都一致时才消费当前支付等待点，避免同 SKU 不同礼包的迟到回调串单。
        /// </summary>
        /// <param name="tableId">平台回调解析出的商品表行 ID。</param>
        /// <param name="receiptParam">平台回调解析出的票据透传参数。</param>
        /// <returns>属于当前活跃支付时返回 true。</returns>
        private bool IsCurrentPayOrder(long tableId, string receiptParam)
        {
            return m_PayTcs != null &&
                   tableId == InPayTableId &&
                   string.Equals(
                       MobileOrderKey.NormalizeReceiptParam(receiptParam),
                       MobileOrderKey.NormalizeReceiptParam(m_CurrentReceiptParam),
                       StringComparison.Ordinal);
        }

        /// <summary>
        /// 处理 ConfirmedOrder：缓存 receipt，解析 tableId，构建 MobileOrderRecord，加入验单队列；
        /// 非补单订单时桥接 WaitForValidationAsync 到 m_PayTcs。
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
            string receiptParam = MobileStoreParameterCodec.DecodeReceiptParam(encodedUuid);
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

            if (m_Hub.ValidationService.TryCompleteAwaitingConfirm(tableId, receiptParam))
            {
                Log.Debug(LogTag.IAPMobile, $"平台确认 ack 到达，补单确认完成并清理记录，商品表ID={tableId}，商品ID={product.definition.id}，平台交易ID={order.Info.TransactionID}，AppleOriginalTransactionID={order.Info.Apple?.OriginalTransactionID}，AppAccountToken={order.Info.Apple?.AppAccountToken}");
                return;
            }

            if (m_PayTcs == null && !m_Hub.ValidationService.HasOrderRecord(tableId, receiptParam))
            {
                Log.Debug(LogTag.IAPMobile, $"平台订单确认完成，商品表ID={tableId}，商品ID={product.definition.id}，平台交易ID={order.Info.TransactionID}，AppleOriginalTransactionID={order.Info.Apple?.OriginalTransactionID}，AppAccountToken={order.Info.Apple?.AppAccountToken}");
                return;
            }

            bool isCurrentPayOrder = IsCurrentPayOrder(tableId, receiptParam);
            bool isRecovered = !isCurrentPayOrder;
            var payTcs = isCurrentPayOrder ? m_PayTcs : null;
            var customData = isCurrentPayOrder ? m_CurrentCustomData : null;
            if (isCurrentPayOrder)
            {
                // 只允许当前点击的商品消费活跃支付状态；历史 PendingOrder 按补单处理。
                InPayTableId = 0;
                m_CurrentCustomData = null;
                m_CurrentReceiptParam = null;
                m_PayTcs = null;
            }

            m_Hub.ProductService.GetReceiptInfo(product.definition.id, out string orderId, out string googleToken);
            Log.Debug(LogTag.IAPMobile, $"平台订单确认回调：商品表ID={tableId}，商品ID={product.definition.id}，订单号={orderId}，平台交易ID={order.Info.TransactionID}，AppleOriginalTransactionID={order.Info.Apple?.OriginalTransactionID}，AppAccountToken={order.Info.Apple?.AppAccountToken}，透传UUID={encodedUuid}，是否补单={isRecovered}");

            var record = new MobileOrderRecord
            {
                TransactionId = orderId ?? string.Empty,
                TableId = tableId,
                GoogleToken = googleToken,
                Status = MobileOrderStatus.PendingValidate,
                IsReplenish = isRecovered,
                CustomDataParam = customData,
                ReceiptParam = receiptParam,
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
                m_Hub.RunBackgroundTask(token => WaitForValidationAsync(payTcs, validateTcs, tableId, customData, receiptParam, token), "支付验单结果桥接");
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
            string receiptParam = MobileStoreParameterCodec.DecodeReceiptParam(encodedUuid);
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

            if (m_Hub.ValidationService.TryReconfirmAwaitingOrder(tableId, receiptParam, order))
            {
                // 已验单发货但上次平台确认失败的订单：直接重试确认，跳过重复验单。
                Log.Debug(LogTag.IAPMobile, $"检测到待确认补单记录，直接重试平台确认并跳过重复验单，商品表ID={tableId}，商品ID={product.definition.id}");
                return;
            }

            bool isCurrentPayOrder = IsCurrentPayOrder(tableId, receiptParam);
            bool isRecovered = !isCurrentPayOrder;
            var payTcs = isCurrentPayOrder ? m_PayTcs : null;
            var customData = isCurrentPayOrder ? m_CurrentCustomData : null;
            if (isCurrentPayOrder)
            {
                InPayTableId = 0;
                m_CurrentCustomData = null;
                m_CurrentReceiptParam = null;
                m_PayTcs = null;
            }

            m_Hub.ProductService.GetReceiptInfo(product.definition.id, out string orderId, out string googleToken);
            Log.Debug(LogTag.IAPMobile, $"平台待确认购买回调：商品表ID={tableId}，商品ID={product.definition.id}，订单号={orderId}，平台交易ID={order.Info.TransactionID}，AppleOriginalTransactionID={order.Info.Apple?.OriginalTransactionID}，AppAccountToken={order.Info.Apple?.AppAccountToken}，透传UUID={encodedUuid}，是否补单={isRecovered}");

            var record = new MobileOrderRecord
            {
                TransactionId = orderId ?? string.Empty,
                TableId = tableId,
                GoogleToken = googleToken,
                Status = MobileOrderStatus.PendingValidate,
                IsReplenish = isRecovered,
                CustomDataParam = customData,
                ReceiptParam = receiptParam,
            };

            m_Hub.ValidationService.RegisterPendingPlatformOrder(tableId, receiptParam, order);

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
                m_Hub.RunBackgroundTask(token => WaitForValidationAsync(payTcs, validateTcs, tableId, customData, receiptParam, token), "支付验单结果桥接");
            }
        }

        /// <summary>
        /// ConfirmPurchase 返回 FailedOrder 时的处理入口；确认失败不等同于购买失败。
        /// </summary>
        /// <param name="order">确认失败订单。</param>
        private void HandleConfirmFailed(FailedOrder order)
        {
            long tableId = ResolveTableIdFromFailedOrder(order);
            string receiptParam = ResolveReceiptParamFromFailedOrder(order);
            Product product = m_Hub.ProductService.GetFirstProductInOrder(order);
            string diagnostic = BuildFailedOrderDiagnostic(order, tableId);
            bool hasActivePay = m_PayTcs != null || InPayTableId != 0;
            bool hasLocalOrder = tableId > 0L && m_Hub.ValidationService.HasOrderRecord(tableId, receiptParam);
            if (!hasActivePay && !hasLocalOrder)
            {
                Log.Debug(LogTag.IAPMobile, $"平台订单确认失败但本地订单已终结，忽略回调，商品表ID={tableId}，原因={order.FailureReason}，详情={order.Details}，{diagnostic}");
                return;
            }

            // order.Details 是 Unity IAP 附带的具体失败说明（如 "Order info is null" / "Transaction ID is null or empty"
            // / "Received invalid order type after confirmation" / 异常 message），是定位 Unknown 失败根因的关键信息，必须打出来。
            Log.Warning(LogTag.IAPMobile, $"平台确认失败，保留 AwaitingConfirm 记录，商品表ID={tableId}，原因={order.FailureReason}，详情={order.Details}，{diagnostic}");
            IAPMobileErrorCode code = MapPurchaseFailureReason(order.FailureReason);
            string errorDesc = $"平台确认失败：{order.FailureReason}，详情={order.Details}";
            m_Hub.Store.TrackLocalPayFailInternal(tableId, product, code, errorDesc, hasActivePay ? m_CurrentCustomData : null);
        }

        /// <summary>
        /// 统一处理购买失败：解析 tableId，清理支付状态，将失败原因映射为 IAPMobileErrorCode 后通过 m_PayTcs 返回结果。
        /// </summary>
        /// <param name="order">失败订单。</param>
        private void HandlePurchaseFailed(FailedOrder order)
        {
            long tableId = ResolveTableIdFromFailedOrder(order);
            string receiptParam = ResolveReceiptParamFromFailedOrder(order);
            Product product = m_Hub.ProductService.GetFirstProductInOrder(order);
            string diagnostic = BuildFailedOrderDiagnostic(order, tableId);

            if (m_PayTcs == null && InPayTableId == 0 && !m_Hub.ValidationService.HasOrderRecord(tableId, receiptParam))
            {
                Log.Warning(LogTag.IAPMobile, $"平台购买失败但本地验单流程已结束，忽略回调，商品表ID={tableId}，原因={order.FailureReason}，详情={order.Details}，{diagnostic}");
                return;
            }

            m_Hub.Store.SubWaitingRef(true);
            m_Hub.ValidationService.MarkLocalPayFailedAndRemove(tableId, receiptParam);

            string customData = m_CurrentCustomData;
            IAPMobileErrorCode code = MapPurchaseFailureReason(order.FailureReason);
            InPayTableId = 0;
            m_CurrentCustomData = null;
            m_CurrentReceiptParam = null;
            var payTcs = m_PayTcs;
            m_PayTcs = null;

            Log.Warning(LogTag.IAPMobile, $"平台购买失败，商品表ID={tableId}，原因={order.FailureReason}，详情={order.Details}，{diagnostic}");
            string errorDesc = $"平台购买失败：{order.FailureReason}";
            m_Hub.Store.TrackLocalPayFailInternal(tableId, product, code, errorDesc, customData);
            var failResult = new IAPResult(tableId, (int)code, IAPErrorSource.Mobile, errorDesc, customData, receiptParam);
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
        /// 从失败订单中解析票据透传参数；UUID 解码失败时回退到当前活跃支付参数。
        /// </summary>
        /// <param name="order">失败订单。</param>
        /// <returns>解析出的票据透传参数；无透传时返回 null。</returns>
        private string ResolveReceiptParamFromFailedOrder(FailedOrder order)
        {
            string encodedUuid = order.Info?.Google?.ObfuscatedAccountId ?? order.Info?.Apple?.AppAccountToken?.ToString();
            string receiptParam = MobileStoreParameterCodec.DecodeReceiptParam(encodedUuid);
            return string.IsNullOrEmpty(receiptParam) ? m_CurrentReceiptParam : receiptParam;
        }

        /// <summary>
        /// 构建 FailedOrder 诊断信息，用于区分平台失败是否能明确绑定到当前活跃支付。
        /// </summary>
        /// <param name="order">失败订单。</param>
        /// <param name="resolvedTableId">当前失败处理解析出的商品配置表行 ID。</param>
        /// <returns>可直接拼接到日志的诊断信息。</returns>
        private string BuildFailedOrderDiagnostic(FailedOrder order, long resolvedTableId)
        {
            Product product = m_Hub.ProductService.GetFirstProductInOrder(order);
            string productId = product?.definition.id;
            string googleObfuscatedAccountId = order.Info?.Google?.ObfuscatedAccountId;
            string appAccountToken = order.Info?.Apple?.AppAccountToken?.ToString();
            string encodedUuid = googleObfuscatedAccountId ?? appAccountToken;
            bool hasDecodedTableId = TryParseTableId(encodedUuid, out long decodedTableId);
            long productTableId = ResolveTableIdFromTable(productId);
            string tableIdSource = ResolveFailedOrderTableIdSource(resolvedTableId, hasDecodedTableId, decodedTableId, productTableId);
            string decodedUid = MobileStoreParameterCodec.DecodeUid(encodedUuid);
            string decodedReceiptParam = MobileStoreParameterCodec.DecodeReceiptParam(encodedUuid);
            bool hasLocalOrder = resolvedTableId > 0L && m_Hub.ValidationService.HasOrderRecord(resolvedTableId, decodedReceiptParam);
            bool hasPayTcs = m_PayTcs != null;
            bool hasActivePay = hasPayTcs || InPayTableId != 0;

            return $"诊断：商品ID={productId}，平台交易ID={order.Info?.TransactionID}，AppleOriginalTransactionID={order.Info?.Apple?.OriginalTransactionID}，AppAccountToken={appAccountToken}，GoogleObfuscatedAccountId={googleObfuscatedAccountId}，透传UUID={encodedUuid}，UUID解码TableId={decodedTableId}，UUID解码UID={decodedUid}，UUID解码ReceiptParam={decodedReceiptParam}，解析来源={tableIdSource}，ProductTableId={productTableId}，InPayTableId={InPayTableId}，HasPayTcs={hasPayTcs}，HasActivePay={hasActivePay}，HasLocalOrder={hasLocalOrder}";
        }

        /// <summary>
        /// 判断 FailedOrder tableId 的解析来源，暴露是否使用了当前活跃支付兜底。
        /// </summary>
        /// <param name="resolvedTableId">当前失败处理解析出的商品配置表行 ID。</param>
        /// <param name="hasDecodedTableId">是否从平台透传 UUID 成功解出 tableId。</param>
        /// <param name="decodedTableId">从平台透传 UUID 解出的 tableId。</param>
        /// <param name="productTableId">从商品 ID 反查出的 tableId。</param>
        /// <returns>tableId 解析来源。</returns>
        private string ResolveFailedOrderTableIdSource(long resolvedTableId, bool hasDecodedTableId, long decodedTableId, long productTableId)
        {
            if (hasDecodedTableId && resolvedTableId == decodedTableId)
            {
                return "PlatformAccountToken";
            }

            if (InPayTableId != 0L && resolvedTableId == InPayTableId)
            {
                return "InPayTableIdFallback";
            }

            if (productTableId != 0L && resolvedTableId == productTableId)
            {
                return "ProductIdFallback";
            }

            return "Unknown";
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

            string receiptParam = m_CurrentReceiptParam;
            InPayTableId = 0;
            m_CurrentCustomData = null;
            m_CurrentReceiptParam = null;
            m_PayTcs = null;

            if (removeLocalOrder)
            {
                m_Hub.ValidationService.MarkLocalPayFailedAndRemove(tableId, receiptParam);
            }

            m_Hub.Store.SubWaitingRef(true);

            var failResult = new IAPResult(tableId, (int)code, IAPErrorSource.Mobile, message, customData, receiptParam);
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
        /// <param name="tableId">当前支付订单的商品配置表行 ID，用于取消时构造失败结果。</param>
        /// <param name="customData">当前支付请求的业务透传数据，用于取消时保持返回上下文。</param>
        /// <param name="receiptParam">当前支付请求的票据透传参数，用于取消时保持返回上下文。</param>
        /// <param name="ct">移动端官方内购商店运行期取消令牌。</param>
        private async UniTask WaitForValidationAsync(
            UniTaskCompletionSource<IAPResult> payTcs,
            UniTaskCompletionSource<IAPResult> validateTcs,
            long tableId,
            string customData,
            string receiptParam,
            CancellationToken ct)
        {
            try
            {
                IAPResult result = await validateTcs.Task.AttachExternalCancellation(ct);
                payTcs?.TrySetResult(result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                var cancelResult = new IAPResult(tableId, (int)IAPMobileErrorCode.StoreNotAvailable, IAPErrorSource.Mobile, "移动端官方内购商店已释放，支付验单等待已取消。", customData, receiptParam);
                m_Hub.Context.EventBridge?.RaisePayFailed(cancelResult);
                payTcs?.TrySetResult(cancelResult);
                throw;
            }
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
