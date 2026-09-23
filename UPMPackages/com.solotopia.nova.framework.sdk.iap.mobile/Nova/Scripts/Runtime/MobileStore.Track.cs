/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStore.Track.cs
 * author:    yingzheng
 * created:   2026/6/10
 * descrip:   MobileStore 埋点转发
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    public sealed partial class MobileStore
    {
        /// <summary>
        /// 当前账号持久化的验单成功订单键最大数量，超过后淘汰最早记录。
        /// </summary>
        private const int c_MaxValidateSuccessOrderKeys = 300;

        /// <summary>
        /// 上报移动内购初始化成功。
        /// </summary>
        internal void TrackInitSuccessInternal()
        {
            TrackInitSuccess();
        }

        /// <summary>
        /// 上报移动内购初始化失败。
        /// </summary>
        /// <param name="reason">初始化失败原因。</param>
        internal void TrackInitFailedInternal(MobileStoreInitFailureReason reason)
        {
            TrackInitFailed(reason);
        }

        /// <summary>
        /// 上报用户发起移动内购。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <param name="customData">业务透传数据。</param>
        internal void TrackBuyInternal(long tableId, Product product, string customData)
        {
            TrackBuy(tableId, ResolveProductId(tableId, product), IsTrackDebugMode(), ResolvePrice(tableId), customData);
        }

        /// <summary>
        /// 上报平台本地支付成功。同一平台订单打点 key 在当前运行期只上报一次。
        /// </summary>
        /// <param name="record">本地订单记录。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        internal void TrackLocalPaySuccessInternal(MobileOrderRecord record, Product product)
        {
            string trackKey = ResolveLocalPaySuccessTrackKey(record);
            if (record == null || HasRuntimeHandledTransactionInternal(trackKey))
            {
                return;
            }

            MarkRuntimeHandledTransactionInternal(trackKey);
            Dictionary<string, object> properties = CreateMobileTrackProperties(record.TableId, product, record.CustomDataParam);
            properties[IAPTrackFields.OrderId] = ResolveLocalPaySuccessOrderId(record, product);
            properties[IAPTrackFields.AddOrder] = record.IsReplenish;
            EmitTrackEvent(IAPTrackEvents.LocalPaySuccess, properties);
        }

        /// <summary>
        /// 上报平台本地支付失败。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <param name="reason">Mobile 支付错误码。</param>
        /// <param name="reasonDetail">平台失败回调或返回结果提供的失败详情。</param>
        /// <param name="customData">业务透传数据。</param>
        internal void TrackLocalPayFailInternal(long tableId, Product product, IAPMobileErrorCode reason, string reasonDetail, string customData)
        {
            Dictionary<string, object> properties = CreateMobileTrackProperties(tableId, product, customData);
            properties[IAPTrackFields.Reason] = (int)reason;
            properties[IAPTrackFields.ReasonDetail] = string.IsNullOrEmpty(reasonDetail) ? reason.ToString() : reasonDetail;
            EmitTrackEvent(IAPTrackEvents.LocalPayFail, properties);
        }

        /// <summary>
        /// 上报服务端验单可重试失败。
        /// </summary>
        /// <param name="record">本地订单记录。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <param name="validateCount">验单尝试次数。</param>
        /// <param name="netError">是否网络错误。</param>
        /// <param name="protocolCode">服务端错误码。</param>
        /// <param name="reason">失败原因。</param>
        /// <param name="reasonDetail">失败原因补充描述。</param>
        internal void TrackValidateFailInternal(MobileOrderRecord record, Product product, int validateCount, bool netError, int protocolCode, IAPMobileErrorCode reason, string reasonDetail)
        {
            if (record == null)
            {
                return;
            }

            Dictionary<string, object> properties = CreateMobileValidationTrackProperties(record, product, validateCount, netError);
            properties[IAPTrackFields.ProtocolCode] = protocolCode;
            properties[IAPTrackFields.Reason] = (int)reason;
            properties[IAPTrackFields.ReasonDetail] = reasonDetail ?? string.Empty;
            EmitTrackEvent(IAPTrackEvents.ValidateFail, properties);
        }

        /// <summary>
        /// 上报服务端验单最终失败。
        /// </summary>
        /// <param name="record">本地订单记录。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <param name="validateCount">最终验单次数。</param>
        /// <param name="netError">是否网络错误。</param>
        /// <param name="protocolCode">服务端错误码。</param>
        /// <param name="protocolMessage">服务端错误信息。</param>
        /// <param name="reason">失败原因。</param>
        /// <param name="reasonDetail">失败原因补充描述。</param>
        internal void TrackValidateFailFinishInternal(MobileOrderRecord record, Product product, int validateCount, bool netError, int protocolCode, string protocolMessage, IAPMobileErrorCode reason, string reasonDetail)
        {
            if (record == null)
            {
                return;
            }

            Dictionary<string, object> properties = CreateMobileValidationTrackProperties(record, product, validateCount, netError);
            properties[IAPTrackFields.ProtocolCode] = protocolCode;
            properties[IAPTrackFields.ProtocolMessage] = protocolMessage ?? string.Empty;
            properties[IAPTrackFields.Reason] = (int)reason;
            properties[IAPTrackFields.ReasonDetail] = reasonDetail ?? string.Empty;
            EmitTrackEvent(IAPTrackEvents.ValidateFailFinish, properties);
        }

        /// <summary>
        /// 上报服务端验单成功。
        /// </summary>
        /// <param name="record">本地订单记录。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <param name="validateCount">验单尝试次数。</param>
        /// <param name="orderId">服务端确认的订单号；为空时回退平台交易号。</param>
        internal void TrackValidateSuccessInternal(MobileOrderRecord record, Product product, int validateCount, string orderId)
        {
            if (record == null)
            {
                return;
            }

            string trackOrderId = !string.IsNullOrEmpty(orderId) ? orderId : record.TransactionId ?? string.Empty;
            if (!TryMarkValidateSuccess(record, trackOrderId))
            {
                return;
            }

            Dictionary<string, object> properties = CreateMobileTrackProperties(record.TableId, product, record.CustomDataParam);
            properties[IAPTrackFields.OrderId] = trackOrderId;
            properties[IAPTrackFields.AddOrder] = record.IsReplenish;
            properties[IAPTrackFields.ValidateCount] = validateCount;
            EmitTrackEvent(IAPTrackEvents.ValidateSuccess, properties);
        }

        /// <summary>
        /// 按平台订单号持久化验单成功打点去重，避免重启后 FetchPurchases 或补单再次上报同一订单。
        /// </summary>
        /// <param name="record">已完成服务端验单的订单记录。</param>
        /// <param name="orderId">验单成功事件使用的平台订单号。</param>
        /// <returns>首次上报该订单时返回 true。</returns>
        private bool TryMarkValidateSuccess(MobileOrderRecord record, string orderId)
        {
            string trackKey = ResolveValidateSuccessTrackKey(record, orderId);
            if (string.IsNullOrEmpty(trackKey))
            {
                return true;
            }

            string legacyTrackKey = ResolveLegacyValidateSuccessTrackKey(record);
            if (TryMigrateLegacyValidateSuccessTrackKey(trackKey, legacyTrackKey))
            {
                LogDebug($"订单已通过旧版 purchase token 上报过验单成功事件，已迁移为订单号并跳过重复打点，订单键={trackKey}");
                return false;
            }

            if (m_PersistData != null && m_PersistData.ValidateSuccessOrderKeys.Contains(trackKey))
            {
                LogDebug($"订单已在历史运行期上报过验单成功事件，本次跳过重复打点，订单键={trackKey}");
                return false;
            }

            if (m_RuntimeValidateSuccessOrderKeys.Contains(trackKey))
            {
                LogDebug($"订单已上报过验单成功事件，本次跳过重复打点，订单键={trackKey}");
                return false;
            }

            m_RuntimeValidateSuccessOrderKeys.Add(trackKey);
            TrimValidateSuccessOrderKeys(m_RuntimeValidateSuccessOrderKeys);
            if (m_PersistData != null)
            {
                m_PersistData.ValidateSuccessOrderKeys.Add(trackKey);
                TrimValidateSuccessOrderKeys(m_PersistData.ValidateSuccessOrderKeys);
                SavePersistDataInternal();
            }

            return true;
        }

        /// <summary>
        /// 将当前订单命中的旧版 Google purchase token 去重键迁移为订单号键。
        /// </summary>
        /// <param name="trackKey">当前订单号去重键。</param>
        /// <param name="legacyTrackKey">旧版 purchase token 去重键。</param>
        /// <returns>旧版键已存在并完成迁移时返回 true。</returns>
        private bool TryMigrateLegacyValidateSuccessTrackKey(string trackKey, string legacyTrackKey)
        {
            if (string.IsNullOrEmpty(legacyTrackKey) || string.Equals(trackKey, legacyTrackKey, StringComparison.Ordinal))
            {
                return false;
            }

            bool migrated = false;
            if (m_PersistData?.ValidateSuccessOrderKeys != null)
            {
                int legacyIndex = m_PersistData.ValidateSuccessOrderKeys.IndexOf(legacyTrackKey);
                if (legacyIndex >= 0)
                {
                    if (m_PersistData.ValidateSuccessOrderKeys.Contains(trackKey))
                    {
                        m_PersistData.ValidateSuccessOrderKeys.RemoveAt(legacyIndex);
                    }
                    else
                    {
                        m_PersistData.ValidateSuccessOrderKeys[legacyIndex] = trackKey;
                    }

                    TrimValidateSuccessOrderKeys(m_PersistData.ValidateSuccessOrderKeys);
                    SavePersistDataInternal();
                    migrated = true;
                }
            }

            if (m_RuntimeValidateSuccessOrderKeys.Remove(legacyTrackKey))
            {
                migrated = true;
            }

            if (migrated && !m_RuntimeValidateSuccessOrderKeys.Contains(trackKey))
            {
                m_RuntimeValidateSuccessOrderKeys.Add(trackKey);
                TrimValidateSuccessOrderKeys(m_RuntimeValidateSuccessOrderKeys);
            }

            return migrated;
        }

        /// <summary>
        /// 将验单成功订单键集合限制在固定容量内，按插入顺序淘汰最老记录。
        /// </summary>
        /// <param name="orderKeys">按时间顺序保存的验单成功订单键。</param>
        private static void TrimValidateSuccessOrderKeys(List<string> orderKeys)
        {
            if (orderKeys == null)
            {
                return;
            }

            while (orderKeys.Count > c_MaxValidateSuccessOrderKeys)
            {
                orderKeys.RemoveAt(0);
            }
        }

        /// <summary>
        /// 上报首次支付订单验单失败。
        /// </summary>
        /// <param name="record">本地订单记录。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <param name="validateCount">验单尝试次数。</param>
        /// <param name="netError">是否网络错误。</param>
        internal void TrackFirstPayOrderValidateInternal(MobileOrderRecord record, Product product, int validateCount, bool netError)
        {
            if (record == null || record.IsReplenish)
            {
                return;
            }

            Dictionary<string, object> properties = CreateMobileValidationTrackProperties(record, product, validateCount, netError);
            EmitTrackEvent(IAPTrackEvents.FirstPayOrderValidate, properties);
        }

        /// <summary>
        /// 构造 Mobile 渠道共有的商品打点字段。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <param name="customData">业务透传数据。</param>
        /// <returns>包含通用字段的 Mobile 打点属性。</returns>
        private Dictionary<string, object> CreateMobileTrackProperties(long tableId, Product product, string customData)
        {
            return CreateTrackProperties(tableId, ResolveProductId(tableId, product), IsTrackDebugMode(), ResolvePrice(tableId), customData);
        }

        /// <summary>
        /// 构造 Mobile 验单事件共有的订单和尝试次数字段。
        /// </summary>
        /// <param name="record">本地订单记录。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <param name="validateCount">验单尝试次数。</param>
        /// <param name="netError">是否为网络错误。</param>
        /// <returns>包含 Mobile 订单语义的验单属性。</returns>
        private Dictionary<string, object> CreateMobileValidationTrackProperties(MobileOrderRecord record, Product product, int validateCount, bool netError)
        {
            Dictionary<string, object> properties = CreateMobileTrackProperties(record.TableId, product, record.CustomDataParam);
            properties[IAPTrackFields.OrderId] = record.TransactionId ?? string.Empty;
            properties[IAPTrackFields.AddOrder] = record.IsReplenish;
            properties[IAPTrackFields.ValidateCount] = validateCount;
            properties[IAPTrackFields.NetError] = netError;
            return properties;
        }

        /// <summary>
        /// 清理当前运行期的移动内购订单去重缓存。
        /// </summary>
        internal void ClearTrackRuntimeCacheInternal()
        {
            m_RuntimeHandledTransactionIds.Clear();
            m_RuntimeValidateSuccessOrderKeys.Clear();
        }

        /// <summary>
        /// 判断指定订单号在当前运行期是否已经处理过。
        /// </summary>
        /// <param name="transactionId">平台交易订单号。</param>
        /// <returns>已处理过时返回 true；订单号为空时返回 false。</returns>
        internal bool HasRuntimeHandledTransactionInternal(string transactionId)
        {
            return !string.IsNullOrEmpty(transactionId) && m_RuntimeHandledTransactionIds.Contains(transactionId);
        }

        /// <summary>
        /// 记录指定订单号在当前运行期已经处理过。
        /// </summary>
        /// <param name="transactionId">平台交易订单号。</param>
        internal void MarkRuntimeHandledTransactionInternal(string transactionId)
        {
            if (!string.IsNullOrEmpty(transactionId))
            {
                m_RuntimeHandledTransactionIds.Add(transactionId);
            }
        }

        /// <summary>
        /// 解析本地支付成功打点去重 key：Apple 使用 TransactionId，Google 使用 purchase token。
        /// </summary>
        /// <param name="record">本地订单记录。</param>
        /// <returns>可用于运行期打点去重的平台订单 key。</returns>
        private static string ResolveLocalPaySuccessTrackKey(MobileOrderRecord record)
        {
            if (record == null)
            {
                return string.Empty;
            }

#if UNITY_ANDROID
            return record.GoogleToken ?? string.Empty;
#else
            return !string.IsNullOrEmpty(record.TransactionId) ? record.TransactionId : record.GoogleToken ?? string.Empty;
#endif
        }

        /// <summary>
        /// 解析验单成功打点去重 key：Apple 使用 transaction id，Google 使用订单号。
        /// </summary>
        /// <param name="record">移动端订单记录。</param>
        /// <param name="orderId">验单响应确认的订单号；为空时回退记录中的平台订单号。</param>
        /// <returns>平台订单去重 key。</returns>
        private static string ResolveValidateSuccessTrackKey(MobileOrderRecord record, string orderId)
        {
            string platformOrderKey;
#if UNITY_ANDROID
            platformOrderKey = !string.IsNullOrEmpty(orderId) ? orderId : record?.TransactionId ?? string.Empty;
#else
            platformOrderKey = ResolveLocalPaySuccessTrackKey(record);
#endif
            if (string.IsNullOrEmpty(platformOrderKey))
            {
                return string.Empty;
            }

#if UNITY_ANDROID
            return $"google:{platformOrderKey}";
#elif UNITY_IOS
            return $"ios:{platformOrderKey}";
#else
            return $"mobile:{platformOrderKey}";
#endif
        }

        /// <summary>
        /// 解析旧版验单成功打点去重 key，仅用于把 Android purchase token 记录迁移为订单号记录。
        /// </summary>
        /// <param name="record">移动端订单记录。</param>
        /// <returns>旧版 Google purchase token 去重 key；非 Android 或 token 为空时返回空字符串。</returns>
        private static string ResolveLegacyValidateSuccessTrackKey(MobileOrderRecord record)
        {
#if UNITY_ANDROID
            return !string.IsNullOrEmpty(record?.GoogleToken) ? $"google:{record.GoogleToken}" : string.Empty;
#else
            return string.Empty;
#endif
        }

        /// <summary>
        /// 解析本地支付成功打点使用的平台订单 ID：Google 从 receipt 取 OrderId，Apple 回退 TransactionId。
        /// </summary>
        /// <param name="record">本地订单记录。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <returns>打点使用的平台订单 ID。</returns>
        private string ResolveLocalPaySuccessOrderId(MobileOrderRecord record, Product product)
        {
            if (!string.IsNullOrEmpty(product?.definition?.id))
            {
                string orderId = string.Empty;
                m_Hub?.ProductService?.GetReceiptInfo(product.definition.id, out orderId, out _);
                if (!string.IsNullOrEmpty(orderId))
                {
                    return orderId;
                }
            }

            return record?.TransactionId ?? string.Empty;
        }

        /// <summary>
        /// 解析埋点使用的平台商品 ID，优先使用 Unity IAP Product，缺失时回退商品表。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <returns>平台商品 ID；无法解析时返回空字符串。</returns>
        private string ResolveProductId(long tableId, Product product)
        {
            if (!string.IsNullOrEmpty(product?.definition?.id))
            {
                return product.definition.id;
            }

            return Table?.FindByTableId(tableId)?.ProductID ?? string.Empty;
        }

        /// <summary>
        /// 解析埋点使用的商品价格，固定使用支付表配置价格。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>商品价格；无法解析时返回 0。</returns>
        private float ResolvePrice(long tableId)
        {
            string price = Table?.FindByTableId(tableId)?.Price;
            return float.TryParse(price, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }

        /// <summary>
        /// 判断当前支付打点是否应标记为 Debug。
        /// </summary>
        /// <returns>当前运行时 DevelopMode 为 Debug 时返回 true，否则返回 false。</returns>
        private bool IsTrackDebugMode()
        {
            return Context?.DevelopMode == DevelopMode.Debug;
        }
    }
}
