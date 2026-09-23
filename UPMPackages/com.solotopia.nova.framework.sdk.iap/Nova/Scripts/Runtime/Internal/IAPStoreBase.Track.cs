/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreBase.Track.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   IAPStoreBase 通用打点入口与属性构造能力
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    public abstract partial class IAPStoreBase
    {
        /// <summary>
        /// 当前 Store 的稳定渠道标识。
        /// </summary>
        protected abstract string TrackChannel { get; }

        /// <summary>
        /// 上报 IAP 初始化成功事件。
        /// </summary>
        protected void TrackInitSuccess()
        {
            EmitTrackEvent(IAPTrackEvents.Init, new Dictionary<string, object>
            {
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.InitResult, "success" },
            });
        }

        /// <summary>
        /// 上报 IAP 初始化失败事件。
        /// </summary>
        /// <param name="failureReason">渠道定义的初始化失败原因。</param>
        protected void TrackInitFailed(Enum failureReason)
        {
            EmitTrackEvent(IAPTrackEvents.Init, new Dictionary<string, object>
            {
                { IAPTrackFields.Channel, TrackChannel },
                { IAPTrackFields.InitResult, "failed" },
                { IAPTrackFields.InitFailureReason, ToTrackEnumValue(failureReason) },
            });
        }

        /// <summary>
        /// 上报用户发起购买事件。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">渠道商品 ID。</param>
        /// <param name="debug">是否为调试环境。</param>
        /// <param name="price">支付表配置价格。</param>
        /// <param name="customData">业务透传数据。</param>
        protected void TrackBuy(long tableId, string productId, bool debug, float price, string customData)
        {
            EmitTrackEvent(IAPTrackEvents.Buy, CreateTrackProperties(tableId, productId, debug, price, customData));
        }

        /// <summary>
        /// 根据失败支付结果上报基类前置校验失败事件。
        /// </summary>
        /// <param name="result">失败支付结果。</param>
        /// <param name="reason">写入通用失败原因字段的错误码。</param>
        /// <param name="reasonDetail">失败原因补充描述。</param>
        protected void TrackPayFailureResult(IAPResult result, int reason, string reasonDetail)
        {
            if (result == null || result.IsSuccess)
            {
                return;
            }

            Dictionary<string, object> properties = CreateTrackProperties(result.TableId, ResolveTrackProductId(result.TableId), Context?.DevelopMode == DevelopMode.Debug, ResolveTrackPrice(result.TableId), result.CustomData);
            properties[IAPTrackFields.Reason] = reason;
            properties[IAPTrackFields.ReasonDetail] = FormatPayFailureReasonDetail(result, reasonDetail);
            EmitTrackEvent(IAPTrackEvents.LocalPayFail, properties);
        }

        /// <summary>
        /// 解析通用失败打点使用的渠道商品 ID。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>渠道商品 ID；无法解析时返回空字符串。</returns>
        protected virtual string ResolveTrackProductId(long tableId)
        {
            return Table?.FindByTableId(tableId)?.ProductID ?? string.Empty;
        }

        /// <summary>
        /// 解析通用失败打点使用的支付表配置价格。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>配置价格；无法解析时返回 0。</returns>
        protected virtual float ResolveTrackPrice(long tableId)
        {
            string price = Table?.FindByTableId(tableId)?.Price;
            return float.TryParse(price, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }

        /// <summary>
        /// 构造包含各渠道共有商品字段的打点属性。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="productId">渠道商品 ID。</param>
        /// <param name="debug">是否为调试环境。</param>
        /// <param name="price">支付表配置价格。</param>
        /// <param name="customData">业务透传数据。</param>
        /// <returns>可由具体 Store 继续追加渠道字段的属性字典。</returns>
        protected Dictionary<string, object> CreateTrackProperties(long tableId, string productId, bool debug, float price, string customData)
        {
            var properties = new Dictionary<string, object>
            {
                { IAPTrackFields.TableId, tableId },
                { IAPTrackFields.ProductId, productId ?? string.Empty },
                { IAPTrackFields.Debug, debug },
                { IAPTrackFields.Price, price },
                { IAPTrackFields.Channel, TrackChannel },
            };
            AppendCustomData(properties, customData);
            return properties;
        }

        /// <summary>
        /// 把渠道专属字段追加到目标属性字典，同名字段使用渠道提供的值。
        /// </summary>
        /// <param name="properties">目标打点属性字典。</param>
        /// <param name="additionalProperties">渠道专属字段；为空时不处理。</param>
        protected static void AppendTrackProperties(Dictionary<string, object> properties, IReadOnlyDictionary<string, object> additionalProperties)
        {
            if (properties == null || additionalProperties == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in additionalProperties)
            {
                if (!string.IsNullOrEmpty(pair.Key))
                {
                    properties[pair.Key] = pair.Value;
                }
            }
        }

        /// <summary>
        /// 通过当前 Store 上下文发送指定打点事件。
        /// </summary>
        /// <param name="eventName">事件名称。</param>
        /// <param name="properties">事件属性。</param>
        protected void EmitTrackEvent(string eventName, Dictionary<string, object> properties)
        {
            Context?.TrackPlugin?.TrackEvent(eventName, properties);
        }

        /// <summary>
        /// 在业务透传数据非空时追加通用自定义字段。
        /// </summary>
        /// <param name="properties">目标打点属性字典。</param>
        /// <param name="customData">业务透传数据。</param>
        private static void AppendCustomData(Dictionary<string, object> properties, string customData)
        {
            if (!string.IsNullOrEmpty(customData))
            {
                properties[IAPTrackFields.CustomData] = customData;
            }
        }

        /// <summary>
        /// 格式化 Store 前置校验失败详情，保留错误来源和错误码域。
        /// </summary>
        /// <param name="result">失败支付结果。</param>
        /// <param name="reasonDetail">失败原因补充描述。</param>
        /// <returns>包含错误来源、错误码和补充描述的字符串。</returns>
        private static string FormatPayFailureReasonDetail(IAPResult result, string reasonDetail)
        {
            string prefix = $"{result.ErrorSource}:{result.ErrorCode}";
            return string.IsNullOrEmpty(reasonDetail) ? prefix : $"{prefix} {reasonDetail}";
        }

        /// <summary>
        /// 将渠道枚举转换为打点使用的稳定整数值。
        /// </summary>
        /// <param name="value">渠道枚举；为空时返回 0。</param>
        /// <returns>枚举对应的 32 位整数。</returns>
        private static int ToTrackEnumValue(Enum value)
        {
            return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }
}
