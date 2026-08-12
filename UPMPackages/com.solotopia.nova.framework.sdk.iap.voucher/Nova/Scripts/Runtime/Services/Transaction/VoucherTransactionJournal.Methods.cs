/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherTransactionJournal.Methods.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   VoucherTransactionJournal 非公开方法
 ***************************************************************/

using System;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherTransactionJournal 非公开方法。
    /// </summary>
    internal sealed partial class VoucherTransactionJournal
    {
        /// <summary>
        /// 更新单条交易记录并立即持久化。
        /// </summary>
        /// <param name="orderId">游戏订单号。</param>
        /// <param name="update">交易记录更新操作。</param>
        /// <returns>找到并更新交易记录时返回 true，否则返回 false。</returns>
        private bool Update(string orderId, Action<VoucherTransactionRecord> update)
        {
            if (string.IsNullOrEmpty(orderId) || !m_Data.Transactions.TryGetValue(orderId, out VoucherTransactionRecord record) || record == null)
            {
                return false;
            }

            update(record);
            m_Save(m_Data);
            return true;
        }

        /// <summary>
        /// 将持久化记录转换为只读交易日志条目。
        /// </summary>
        /// <param name="record">持久化交易记录。</param>
        /// <returns>只读交易日志条目。</returns>
        private static VoucherTransactionEntry ToEntry(VoucherTransactionRecord record)
        {
            return new VoucherTransactionEntry(VoucherSpendCommand.FromRecord(record), record.State, record.ErrorCode, record.ErrorMessage);
        }
    }
}
