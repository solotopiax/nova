/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileOrderKey.cs
 * author:    yingzheng
 * created:   2026/8/11
 * descrip:   移动端官方内购未完成订单唯一键工具
 ***************************************************************/

using System;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 移动端官方内购未完成订单唯一键工具。
    /// 订单仓库只保存未完成订单，唯一身份由商品表行 ID 与票据透传参数共同组成；
    /// 票据透传参数为空时保持旧版 tableId-only 语义。
    /// </summary>
    internal static class MobileOrderKey
    {
        /// <summary>
        /// 存储键内部字段分隔符。
        /// </summary>
        private const string c_Separator = "|";

        /// <summary>
        /// 根据商品表行 ID 和票据透传参数生成稳定存储键。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <param name="receiptParam">票据透传参数；为空时按旧版 tableId-only 订单处理。</param>
        /// <returns>用于本地未完成订单仓库的稳定键。</returns>
        internal static string Build(long tableId, string receiptParam)
        {
            return tableId <= 0L ? string.Empty : $"{tableId}{c_Separator}{NormalizeReceiptParam(receiptParam)}";
        }

        /// <summary>
        /// 根据订单记录生成稳定存储键。
        /// </summary>
        /// <param name="record">订单记录。</param>
        /// <returns>用于本地未完成订单仓库的稳定键；记录无效时返回空字符串。</returns>
        internal static string Build(MobileOrderRecord record)
        {
            return record == null ? string.Empty : Build(record.TableId, record.ReceiptParam);
        }

        /// <summary>
        /// 将票据透传参数归一化为订单键使用的形式。
        /// </summary>
        /// <param name="receiptParam">原始票据透传参数。</param>
        /// <returns>归一化后的票据透传参数；空值返回空字符串。</returns>
        internal static string NormalizeReceiptParam(string receiptParam)
        {
            return string.IsNullOrEmpty(receiptParam) ? string.Empty : receiptParam.ToUpperInvariant();
        }

        /// <summary>
        /// 判断订单键是否可用于未完成订单仓库。
        /// </summary>
        /// <param name="orderKey">待检查的订单键。</param>
        /// <returns>订单键非空时返回 true。</returns>
        internal static bool IsValid(string orderKey)
        {
            return !string.IsNullOrEmpty(orderKey);
        }

        /// <summary>
        /// 生成面向日志和调试的订单键说明。
        /// </summary>
        /// <param name="record">订单记录。</param>
        /// <returns>中文订单键说明。</returns>
        internal static string Describe(MobileOrderRecord record)
        {
            return record == null ? "订单记录为空" : Describe(record.TableId, record.ReceiptParam);
        }

        /// <summary>
        /// 生成面向日志和调试的订单键说明。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <param name="receiptParam">票据透传参数。</param>
        /// <returns>中文订单键说明。</returns>
        internal static string Describe(long tableId, string receiptParam)
        {
            string normalizedReceiptParam = NormalizeReceiptParam(receiptParam);
            return string.IsNullOrEmpty(normalizedReceiptParam)
                ? $"商品表ID={tableId}，票据透传=空"
                : $"商品表ID={tableId}，票据透传={normalizedReceiptParam}";
        }
    }
}
