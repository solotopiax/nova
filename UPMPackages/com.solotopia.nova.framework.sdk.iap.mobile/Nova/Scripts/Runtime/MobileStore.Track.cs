/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStore.Track.cs
 * author:    yingzheng
 * created:   2026/6/10
 * descrip:   MobileStore 埋点转发
 ***************************************************************/

using System.Globalization;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    public sealed partial class MobileStore
    {
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
            TrackLocalPaySuccess(record.TableId, ResolveProductId(record.TableId, product), IsTrackDebugMode(), ResolvePrice(record.TableId), ResolveLocalPaySuccessOrderId(record, product), record.IsReplenish, record.CustomDataParam);
        }

        /// <summary>
        /// 上报平台本地支付失败。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <param name="reason">Mobile 支付错误码。</param>
        /// <param name="customData">业务透传数据。</param>
        internal void TrackLocalPayFailInternal(long tableId, Product product, IAPMobileErrorCode reason, string reasonDetail, string customData)
        {
            TrackLocalPayFail(tableId, ResolveProductId(tableId, product), IsTrackDebugMode(), ResolvePrice(tableId), reason, string.IsNullOrEmpty(reasonDetail) ? reason.ToString() : reasonDetail, customData);
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
        internal void TrackValidateFailInternal(MobileOrderRecord record, Product product, int validateCount, bool netError, int protocolCode, IAPMobileErrorCode reason, string reasonDetail)
        {
            if (record == null)
            {
                return;
            }

            TrackValidateFail(record.TableId, ResolveProductId(record.TableId, product), IsTrackDebugMode(), ResolvePrice(record.TableId), record.TransactionId ?? string.Empty, record.IsReplenish, validateCount, netError, protocolCode, reason, reasonDetail, record.CustomDataParam);
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
        internal void TrackValidateFailFinishInternal(MobileOrderRecord record, Product product, int validateCount, bool netError, int protocolCode, string protocolMessage, IAPMobileErrorCode reason, string reasonDetail)
        {
            if (record == null)
            {
                return;
            }

            TrackValidateFailFinish(record.TableId, ResolveProductId(record.TableId, product), IsTrackDebugMode(), ResolvePrice(record.TableId), record.TransactionId ?? string.Empty, record.IsReplenish, validateCount, netError, protocolCode, protocolMessage, reason, reasonDetail, record.CustomDataParam);
        }

        /// <summary>
        /// 上报服务端验单成功。
        /// </summary>
        /// <param name="record">本地订单记录。</param>
        /// <param name="product">Unity IAP 商品对象。</param>
        /// <param name="validateCount">验单尝试次数。</param>
        internal void TrackValidateSuccessInternal(MobileOrderRecord record, Product product, int validateCount, string orderId)
        {
            if (record == null)
            {
                return;
            }

            string trackOrderId = !string.IsNullOrEmpty(orderId) ? orderId : record.TransactionId ?? string.Empty;
            TrackValidateSuccess(record.TableId, ResolveProductId(record.TableId, product), IsTrackDebugMode(), ResolvePrice(record.TableId), trackOrderId, record.IsReplenish, validateCount, record.CustomDataParam);
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

            TrackFirstPayOrderValidate(record.TableId, ResolveProductId(record.TableId, product), IsTrackDebugMode(), ResolvePrice(record.TableId), record.TransactionId ?? string.Empty, false, validateCount, netError, record.CustomDataParam);
        }

        /// <summary>
        /// 清理当前运行期的移动内购订单去重缓存。
        /// </summary>
        internal void ClearTrackRuntimeCacheInternal()
        {
            m_RuntimeHandledTransactionIds.Clear();
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
