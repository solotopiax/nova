/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStoreParameterCodec.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   uid + tableId + receiptParam ↔ GUID 编解码，用于购买时透传参数
 ***************************************************************/

using System;
using System.Globalization;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// uid + tableId + receiptParam 与 GUID 字符串互转工具类。
    /// uid / receiptParam 为十六进制字符串槽位，由 MobilePurchaseService.TryValidatePassthroughParams 校验长度、字符集和前导零；
    /// tableId 为数值槽位，编码时按十六进制定长左补 0。
    /// 购买时把三者编码为 GUID 写入平台账号字段（Android: ObfuscatedAccountId/ProfileId；iOS: AppAccountToken），
    /// 随平台票据回传，支付回调 / 补单 / 恢复时解码还原，并可供服务端从票据解出。
    ///
    /// 布局（去连字符后 32 个 hex）：
    ///   [0,8)   uid          —— 8 字符，字符串左补 0
    ///   [8,16)  tableId      —— 8 hex，数值左补 0
    ///   [16,32) receiptParam —— 16 字符，字符串左补 0
    /// </summary>
    internal static class MobileStoreParameterCodec
    {
        /// <summary>
        /// uid 字符串编码进 GUID 的最大字符数。
        /// </summary>
        internal const int UidMaxLength = 8;

        /// <summary>
        /// receiptParam 字符串编码进 GUID 的最大字符数。
        /// </summary>
        internal const int ReceiptParamMaxLength = 16;

        /// <summary>
        /// 将 uid、tableId、receiptParam 编码为 GUID 格式字符串。
    /// uid / receiptParam 按已经校验的十六进制字符串写入定长槽；tableId 直接编码。
        /// </summary>
        /// <param name="uid">用户唯一 ID（≤8 位十六进制；非空值不能以 0 开头）。</param>
        /// <param name="tableId">商品配置表行 ID（数值，≤8 位）。</param>
        /// <param name="receiptParam">平台票据透传字符串（≤16 位十六进制；非空值不能以 0 开头）。</param>
        /// <returns>形如 "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" 的 GUID 字符串。</returns>
        internal static string Encode(string uid, long tableId, string receiptParam)
        {
            string uidHex = ToFixedSlot(uid, UidMaxLength);
            string tableHex = ToFixedHex(tableId, 8);
            string receiptHex = ToFixedSlot(receiptParam, ReceiptParamMaxLength);
            string raw = uidHex + tableHex + receiptHex; // 8 + 8 + 16 = 32
            return $"{raw.Substring(0, 8)}-{raw.Substring(8, 4)}-{raw.Substring(12, 4)}-{raw.Substring(16, 4)}-{raw.Substring(20, 12)}";
        }

        /// <summary>
        /// 从 GUID 字符串中解码 tableId（取 hex [8,16)）。解码失败时返回 0。
        /// </summary>
        /// <param name="guid">Encode 编码生成的 GUID 字符串。</param>
        /// <returns>tableId；解码失败时返回 0。</returns>
        internal static long DecodeTableId(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return 0L;
            string raw = guid.Replace("-", string.Empty);
            if (raw.Length != 32)
                return 0L;
            string tableHex = raw.Substring(8, 8);
            return long.TryParse(tableHex, NumberStyles.HexNumber, null, out long tableId) ? tableId : 0L;
        }

        /// <summary>
        /// 从 GUID 字符串中解码 receiptParam（取后 16 个字符，去掉左侧补 0）。
        /// 解码失败或全 0（无透传）时返回 null。
        /// </summary>
        /// <param name="guid">Encode 编码生成的 GUID 字符串。</param>
        /// <returns>receiptParam 字符串原文；无透传/解码失败时返回 null。</returns>
        internal static string DecodeReceiptParam(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;
            string raw = guid.Replace("-", string.Empty);
            if (raw.Length != 32)
                return null;
            return DecodeFixedSlot(raw.Substring(16, 16));
        }

        /// <summary>
        /// 从 GUID 字符串中解码 uid（取 hex [0,8)，去掉左侧补 0）。
        /// 解码失败或全 0 时返回 null。客户端不依赖此值，主要供服务端按同一布局解析对齐。
        /// </summary>
        /// <param name="guid">Encode 编码生成的 GUID 字符串。</param>
        /// <returns>uid 字符串原文；无值/解码失败时返回 null。</returns>
        internal static string DecodeUid(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;
            string raw = guid.Replace("-", string.Empty);
            if (raw.Length != 32)
                return null;
            return DecodeFixedSlot(raw.Substring(0, 8));
        }

        /// <summary>
        /// 把数值按指定宽度编码成定长十六进制字符（不足位左补 0）。
        /// </summary>
        /// <param name="value">待编码数值。</param>
        /// <param name="width">目标十六进制字符串长度。</param>
        /// <returns>定长十六进制字符串。</returns>
        private static string ToFixedHex(long value, int width)
        {
            if (value < 0L)
            {
                value = 0L;
            }

            return Convert.ToString(value, 16).ToUpperInvariant().PadLeft(width, '0');
        }

        /// <summary>
        /// 把字符串写入固定宽度槽位，不足位左补 0。
        /// </summary>
        /// <param name="value">待编码字符串。</param>
        /// <param name="width">目标槽位宽度。</param>
        /// <returns>定长字符串槽位。</returns>
        private static string ToFixedSlot(string value, int width)
        {
            return string.IsNullOrEmpty(value) ? new string('0', width) : value.ToUpperInvariant().PadLeft(width, '0');
        }

        /// <summary>
        /// 从固定宽度槽位还原字符串，去掉左侧补 0；全 0 返回 null。
        /// </summary>
        /// <param name="slot">固定宽度槽位。</param>
        /// <returns>字符串原文；全 0 返回 null。</returns>
        private static string DecodeFixedSlot(string slot)
        {
            string value = slot.TrimStart('0');
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
