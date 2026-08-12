/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.Track.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   ThirdPayStore 支付打点转发
 ***************************************************************/

using System.Collections.Generic;
using System.Globalization;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    public sealed partial class ThirdPayStore
    {
        /// <summary>
        /// 上报用户发起第三方支付。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        private void TrackBuyInternal(IAPThirdPayRequest request)
        {
            TrackBuy(request.TableId, ResolveThirdProductId(request.TableId), IsTrackDebugMode(), ResolvePrice(request.TableId), request.CustomData);
        }

        /// <summary>
        /// 上报客户端第三方订单创建成功。
        /// </summary>
        /// <param name="order">已保存的本地订单。</param>
        private void TrackCreateOrderSuccessInternal(ThirdPayOrderRecord order)
        {
            TrackCreateOrderSuccess(order.TableId, ResolveThirdProductId(order.TableId), IsTrackDebugMode(), ResolvePrice(order.TableId), order.ClientOrderId, order.CustomData);
        }

        /// <summary>
        /// 上报第三方订单创建或支付 URL 构造失败。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="reasonDetail">失败原因详情。</param>
        private void TrackCreateOrderFailInternal(IAPThirdPayRequest request, string reasonDetail)
        {
            TrackCreateOrderFail(request.TableId, ResolveThirdProductId(request.TableId), IsTrackDebugMode(), ResolvePrice(request.TableId), IAPThirdPayErrorCode.StoreInitFailed, reasonDetail, false, request.CustomData);
        }

        /// <summary>
        /// 上报用户关闭第三方支付页。
        /// </summary>
        /// <param name="order">当前本地订单。</param>
        private void TrackThirdPayCloseOrderInternal(ThirdPayOrderRecord order)
        {
            TrackThirdPayCloseOrder(order.TableId, ResolveThirdProductId(order.TableId), IsTrackDebugMode(), ResolvePrice(order.TableId), order.ClientOrderId, 0, order.CustomData);
        }

        /// <summary>
        /// 上报第三方支付页返回支付完成。
        /// </summary>
        /// <param name="order">当前本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        private void TrackLocalPaySuccessInternal(ThirdPayOrderRecord order, bool isRecovered)
        {
            TrackLocalPaySuccess(order.TableId, ResolveThirdProductId(order.TableId), IsTrackDebugMode(), ResolvePrice(order.TableId), order.ClientOrderId, isRecovered, order.CustomData);
        }

        /// <summary>
        /// 上报第三方支付页打开或支付失败。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="reason">失败错误码。</param>
        /// <param name="reasonDetail">失败原因详情。</param>
        private void TrackLocalPayFailInternal(IAPThirdPayRequest request, IAPThirdPayErrorCode reason, string reasonDetail)
        {
            TrackLocalPayFail(request.TableId, ResolveThirdProductId(request.TableId), IsTrackDebugMode(), ResolvePrice(request.TableId), reason, reasonDetail, request.CustomData);
        }

        /// <summary>
        /// 上报一批订单的可重试或最终验单失败。
        /// </summary>
        /// <param name="orders">验单订单列表。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="reasonDetail">失败原因详情。</param>
        /// <param name="isFinal">是否为最终失败。</param>
        private void TrackValidationFailureBatchInternal(IReadOnlyList<ThirdPayOrderRecord> orders, bool isRecovered, int validateCount, bool netError, int protocolCode, string reasonDetail, bool isFinal)
        {
            foreach (ThirdPayOrderRecord order in orders)
            {
                if (isFinal)
                {
                    TrackValidateFailFinishInternal(order, isRecovered, validateCount, netError, protocolCode, reasonDetail);
                }
                else
                {
                    TrackValidateFailInternal(order, isRecovered, validateCount, netError, protocolCode, reasonDetail);
                }
            }
        }

        /// <summary>
        /// 上报首次支付订单的第一次验单失败。
        /// </summary>
        /// <param name="orders">首次支付订单列表。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        private void TrackFirstValidationFailureBatchInternal(IReadOnlyList<ThirdPayOrderRecord> orders, int validateCount, bool netError)
        {
            foreach (ThirdPayOrderRecord order in orders)
            {
                TrackFirstPayOrderValidate(order.TableId, ResolveThirdProductId(order.TableId), IsTrackDebugMode(), ResolvePrice(order.TableId), order.ClientOrderId, false, validateCount, netError, order.CustomData);
            }
        }

        /// <summary>
        /// 上报单笔订单可重试的验单失败。
        /// </summary>
        /// <param name="order">本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="reasonDetail">失败原因详情。</param>
        private void TrackValidateFailInternal(ThirdPayOrderRecord order, bool isRecovered, int validateCount, bool netError, int protocolCode, string reasonDetail)
        {
            TrackValidateFail(order.TableId, ResolveThirdProductId(order.TableId), IsTrackDebugMode(), ResolvePrice(order.TableId), order.ClientOrderId, isRecovered, validateCount, netError, protocolCode, IAPThirdPayErrorCode.ServerValidationFailed, reasonDetail, order.CustomData);
        }

        /// <summary>
        /// 上报单笔订单最终验单失败。
        /// </summary>
        /// <param name="order">本地订单。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        /// <param name="netError">是否为网络链路失败。</param>
        /// <param name="protocolCode">协议错误码。</param>
        /// <param name="reasonDetail">失败原因详情。</param>
        private void TrackValidateFailFinishInternal(ThirdPayOrderRecord order, bool isRecovered, int validateCount, bool netError, int protocolCode, string reasonDetail)
        {
            TrackValidateFailFinish(order.TableId, ResolveThirdProductId(order.TableId), IsTrackDebugMode(), ResolvePrice(order.TableId), order.ClientOrderId, isRecovered, validateCount, netError, protocolCode, reasonDetail, IAPThirdPayErrorCode.ServerValidationFailed, reasonDetail, order.CustomData);
        }

        /// <summary>
        /// 上报单笔订单服务端验单成功。
        /// </summary>
        /// <param name="order">本地订单。</param>
        /// <param name="tableId">服务端确认的支付表行 ID。</param>
        /// <param name="orderId">服务端订单号。</param>
        /// <param name="isRecovered">是否为补单订单。</param>
        /// <param name="validateCount">验单次数。</param>
        private void TrackValidateSuccessInternal(ThirdPayOrderRecord order, long tableId, string orderId, bool isRecovered, int validateCount)
        {
            TrackValidateSuccess(tableId, ResolveThirdProductId(tableId), IsTrackDebugMode(), ResolvePrice(tableId), orderId, isRecovered, validateCount, order.CustomData);
        }

        /// <summary>
        /// 解析第三方商品 ID，未配置时返回空字符串。
        /// </summary>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <returns>第三方商品 ID。</returns>
        private string ResolveThirdProductId(long tableId)
        {
            return Table?.FindByTableId(tableId)?.ThirdProductID ?? string.Empty;
        }

        /// <summary>
        /// 解析支付表配置价格，解析失败时返回 0。
        /// </summary>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <returns>支付表配置价格。</returns>
        private float ResolvePrice(long tableId)
        {
            string price = Table?.FindByTableId(tableId)?.Price;
            return float.TryParse(price, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }

        /// <summary>
        /// 判断当前运行环境是否为 Debug 开发模式。
        /// </summary>
        /// <returns>当前配置为 Debug 时返回 true。</returns>
        private bool IsTrackDebugMode()
        {
            return Context?.DevelopMode == DevelopMode.Debug;
        }
    }
}
