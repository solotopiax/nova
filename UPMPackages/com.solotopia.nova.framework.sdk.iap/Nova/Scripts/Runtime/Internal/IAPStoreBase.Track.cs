/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreBase.Track.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   IAPStoreBase 打点封装，为每个 IAP 埋点事件提供具名方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    public abstract partial class IAPStoreBase
    {
        /// <summary>
        /// 当前 store 的渠道标识，用于 Init / Buy 等事件的 Channel 字段。
        /// 子类必须返回固定字符串（如 "mobile" / "thirdpay" / "voucher"）。
        /// </summary>
        protected abstract string TrackChannel { get; }

        /// <summary>
        /// 上报 nova_iap_init 成功事件。
        /// </summary>
        protected void TrackInitSuccess()
        {
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.Init, new Dictionary<string, object>
            {
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.InitResult, "success" },
            });
        }

        /// <summary>
        /// 上报 nova_iap_init 失败事件。
        /// </summary>
        /// <param name="failureReason">平台返回的失败原因枚举。</param>
        protected void TrackInitFailed(Enum failureReason)
        {
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.Init, new Dictionary<string, object>
            {
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.InitResult, "failed" },
                { IAPTrackFields.InitFailureReason, ToTrackEnumValue(failureReason) },
            });
        }

        /// <summary>
        /// 上报 nova_iap_buy 事件（用户发起购买）。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式（AlwaysPaySucceed）。</param>
        /// <param name="price">商品本地化价格数值。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackBuy(long tableId, string productId, bool debug, float price, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.Buy, properties);
        }

        /// <summary>
        /// 上报 nova_iap_local_pay_success 事件（平台侧确认支付，验单前）。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式。</param>
        /// <param name="price">商品本地化价格数值。</param>
        /// <param name="orderId">平台订单 ID。</param>
        /// <param name="isRecoveredOrder">是否补单。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackLocalPaySuccess(long tableId, string productId, bool debug, float price, string orderId, bool isRecoveredOrder, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.OrderId, orderId },
                { IAPTrackFields.AddOrder, isRecoveredOrder },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.LocalPaySuccess, properties);
        }

        /// <summary>
        /// 上报 nova_iap_local_pay_fail 事件（平台侧拒绝支付）。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式。</param>
        /// <param name="price">商品本地化价格数值。</param>
        /// <param name="reason">失败原因枚举。</param>
        /// <param name="reasonDetail">失败原因补充描述。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackLocalPayFail(long tableId, string productId, bool debug, float price, Enum reason, string reasonDetail, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.Reason, ToTrackEnumValue(reason) },
                { IAPTrackFields.ReasonDetail, reasonDetail ?? string.Empty },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.LocalPayFail, properties);
        }

        /// <summary>
        /// 上报 nova_iap_validate_fail 事件（验单失败可重试一档）。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式。</param>
        /// <param name="price">商品本地化价格数值。</param>
        /// <param name="orderId">订单 ID。</param>
        /// <param name="isRecoveredOrder">是否补单。</param>
        /// <param name="validateCount">本次验单尝试次数。</param>
        /// <param name="netError">是否网络错误导致。</param>
        /// <param name="protocolCode">服务端返回错误码。</param>
        /// <param name="reason">失败原因枚举。</param>
        /// <param name="reasonDetail">失败原因补充描述。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackValidateFail(long tableId, string productId, bool debug, float price, string orderId, bool isRecoveredOrder, int validateCount, bool netError, int protocolCode, Enum reason, string reasonDetail, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.OrderId, orderId },
                { IAPTrackFields.AddOrder, isRecoveredOrder },
                { IAPTrackFields.ValidateCount, validateCount },
                { IAPTrackFields.NetError, netError },
                { IAPTrackFields.ProtocolCode, protocolCode },
                { IAPTrackFields.Reason, ToTrackEnumValue(reason) },
                { IAPTrackFields.ReasonDetail, reasonDetail ?? string.Empty },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.ValidateFail, properties);
        }

        /// <summary>
        /// 上报 nova_iap_validate_fail_finish 事件（验单彻底失败，超出重试次数）。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式。</param>
        /// <param name="price">商品本地化价格数值。</param>
        /// <param name="orderId">订单 ID。</param>
        /// <param name="isRecoveredOrder">是否补单。</param>
        /// <param name="validateCount">最终重试次数上限。</param>
        /// <param name="netError">是否网络错误导致。</param>
        /// <param name="protocolCode">服务端返回错误码。</param>
        /// <param name="protocolMessage">服务端返回错误信息描述。</param>
        /// <param name="reason">失败原因枚举。</param>
        /// <param name="reasonDetail">失败原因补充描述。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackValidateFailFinish(long tableId, string productId, bool debug, float price, string orderId, bool isRecoveredOrder, int validateCount, bool netError, int protocolCode, string protocolMessage, Enum reason, string reasonDetail, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.OrderId, orderId },
                { IAPTrackFields.AddOrder, isRecoveredOrder },
                { IAPTrackFields.ValidateCount, validateCount },
                { IAPTrackFields.NetError, netError },
                { IAPTrackFields.ProtocolCode, protocolCode },
                { IAPTrackFields.ProtocolMessage, protocolMessage ?? string.Empty },
                { IAPTrackFields.Reason, ToTrackEnumValue(reason) },
                { IAPTrackFields.ReasonDetail, reasonDetail ?? string.Empty },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.ValidateFailFinish, properties);
        }

        /// <summary>
        /// 上报 nova_iap_validate_success 事件（服务端验单成功）。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式。</param>
        /// <param name="price">商品本地化价格数值。</param>
        /// <param name="orderId">订单 ID。</param>
        /// <param name="isRecoveredOrder">是否补单。</param>
        /// <param name="validateCount">验单尝试次数（含本次）。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackValidateSuccess(long tableId, string productId, bool debug, float price, string orderId, bool isRecoveredOrder, int validateCount, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.OrderId, orderId },
                { IAPTrackFields.AddOrder, isRecoveredOrder },
                { IAPTrackFields.ValidateCount, validateCount },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.ValidateSuccess, properties);
        }

        /// <summary>
        /// 上报 nova_iap_first_pay_order_validate 事件（首次下单验单失败专用，区分首验 vs 重试统计）。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式。</param>
        /// <param name="price">商品本地化价格数值。</param>
        /// <param name="orderId">订单 ID。</param>
        /// <param name="isRecoveredOrder">是否补单。</param>
        /// <param name="validateCount">验单尝试次数。</param>
        /// <param name="netError">是否网络错误导致。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackFirstPayOrderValidate(long tableId, string productId, bool debug, float price, string orderId, bool isRecoveredOrder, int validateCount, bool netError, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.OrderId, orderId },
                { IAPTrackFields.AddOrder, isRecoveredOrder },
                { IAPTrackFields.ValidateCount, validateCount },
                { IAPTrackFields.NetError, netError },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.FirstPayOrderValidate, properties);
        }

        /// <summary>
        /// 上报 nova_iap_deliver_fail 事件（验单已成功但发货失败）。
        /// </summary>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式。</param>
        /// <param name="orderId">订单 ID。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackDeliverFail(string productId, bool debug, string orderId, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.OrderId, orderId },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.DeliverFail, properties);
        }

        /// <summary>
        /// 上报 nova_iap_create_order_fail 事件（第三方订单创建失败）。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式。</param>
        /// <param name="price">商品本地化价格数值。</param>
        /// <param name="reason">失败原因枚举。</param>
        /// <param name="reasonDetail">失败原因补充描述。</param>
        /// <param name="netError">是否网络错误导致。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackCreateOrderFail(long tableId, string productId, bool debug, float price, Enum reason, string reasonDetail, bool netError, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.Reason, ToTrackEnumValue(reason) },
                { IAPTrackFields.ReasonDetail, reasonDetail ?? string.Empty },
                { IAPTrackFields.NetError, netError },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.CreateOrderFail, properties);
        }

        /// <summary>
        /// 上报 nova_iap_create_order_success 事件（第三方订单创建成功）。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式。</param>
        /// <param name="price">商品本地化价格数值。</param>
        /// <param name="orderId">服务端返回的订单 ID。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackCreateOrderSuccess(long tableId, string productId, bool debug, float price, string orderId, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.OrderId, orderId },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.CreateOrderSuccess, properties);
        }

        /// <summary>
        /// 上报 nova_iap_third_pay_close_order 事件（第三方收银台被用户关闭）。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">平台商品 ID。</param>
        /// <param name="debug">是否测试模式。</param>
        /// <param name="price">商品本地化价格数值。</param>
        /// <param name="orderId">订单 ID。</param>
        /// <param name="thirdPayTypeId">第三方支付渠道 ID。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        protected void TrackThirdPayCloseOrder(long tableId, string productId, bool debug, float price, string orderId, int thirdPayTypeId, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.OrderId, orderId },
                { IAPTrackFields.ThirdPayTypeId, thirdPayTypeId },
            };
            AppendCustomData(properties, customData);
            Context?.TrackPlugin?.TrackEvent(IAPTrackEvents.ThirdPayCloseOrder, properties);
        }
        
        /// <summary>
        /// 将透传字符串数据挂入打点参数字典；为空时跳过。
        /// </summary>
        /// <param name="properties">打点参数字典。</param>
        /// <param name="customData">业务层透传字符串数据。</param>
        private static void AppendCustomData(Dictionary<string, object> properties, string customData)
        {
            if (string.IsNullOrEmpty(customData))
            {
                return;
            }

            properties[IAPTrackFields.CustomData] = customData;
        }

        /// <summary>
        /// 将 Store 内部失败枚举转换为埋点可接受的整数值。
        /// </summary>
        /// <param name="value">Store 内部失败枚举。</param>
        /// <returns>枚举对应的 int 值；空值返回 0。</returns>
        private static int ToTrackEnumValue(Enum value)
        {
            return value == null ? 0 : Convert.ToInt32(value);
        }
    }
}
