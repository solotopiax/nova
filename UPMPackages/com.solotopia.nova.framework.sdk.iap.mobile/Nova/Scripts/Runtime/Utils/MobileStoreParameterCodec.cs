/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStoreParameterCodec.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   uid + tableId ↔ GUID 编解码，用于购买时透传参数
 ***************************************************************/

using System;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// uid + tableId 与 GUID 字符串互转工具类。
    /// 购买时将 uid 和 tableId 编码为 GUID 写入平台透传字段（ObfuscatedAccountId / AppAccountToken），
    /// 回调时解码还原 tableId，用于精确路由订单；对齐 IAP3Helper IAP3StoreParameterCodec。
    /// </summary>
    internal static class MobileStoreParameterCodec
    {
        /// <summary>
        /// 将 uid 字符串和 tableId 编码为 GUID 格式字符串。
        /// uid 转 long 失败时 uid 部分填 0，tableId 仍正常编码，保证 tableId 解析不受影响。
        /// </summary>
        /// <param name="uid">用户唯一 ID（数字字符串）。</param>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>形如 "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" 的 GUID 字符串。</returns>
        internal static string Encode(string uid, long tableId)
        {
            long uidLong = 0L;
            if (!string.IsNullOrEmpty(uid))
                long.TryParse(uid, out uidLong);

            string uidHex = uidLong.ToString("X16");
            string tableHex = tableId.ToString("X16");
            string raw = uidHex + tableHex;
            return $"{raw.Substring(0, 8)}-{raw.Substring(8, 4)}-{raw.Substring(12, 4)}-{raw.Substring(16, 4)}-{raw.Substring(20, 12)}";
        }

        /// <summary>
        /// 从 GUID 字符串中解码 tableId。
        /// 取 GUID 去掉连接符后的后 16 个 hex 字符解析为 long。
        /// 解码失败时返回 0。
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
            string tableHex = raw.Substring(16, 16);
            return long.TryParse(tableHex, System.Globalization.NumberStyles.HexNumber, null, out long tableId) ? tableId : 0L;
        }
    }
}
