/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherTransactionJournal.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 交易日志公开操作
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 交易日志。
    /// 封装按订单号读写、幂等冲突检查和状态持久化。
    /// </summary>
    internal sealed partial class VoucherTransactionJournal : VoucherLogOwner
    {
        /// <summary>
        /// 创建 Voucher 交易日志，并确保持久化容器完成初始化。
        /// </summary>
        /// <param name="data">当前账号持久化容器。</param>
        /// <param name="save">原子保存当前容器的回调。</param>
        /// <exception cref="ArgumentNullException">持久化容器或保存回调为空时抛出。</exception>
        internal VoucherTransactionJournal(VoucherStorePersistData data, Action<VoucherStorePersistData> save)
        {
            m_Data = data ?? throw new ArgumentNullException(nameof(data));
            m_Save = save ?? throw new ArgumentNullException(nameof(save));
            m_Data.EnsureInitialized();
        }

        /// <summary>
        /// 在网络发送前写入完整交易命令，并检测同订单不同 payload 冲突。
        /// </summary>
        /// <param name="command">不可变完整交易命令。</param>
        /// <returns>新增、同 payload 幂等命中或冲突。</returns>
        /// <exception cref="ArgumentNullException">交易命令为空时抛出。</exception>
        internal VoucherJournalAddResult TryAdd(VoucherSpendCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (m_Data.Transactions.TryGetValue(command.GameOrderId, out VoucherTransactionRecord existing))
            {
                VoucherSpendCommand existingCommand = VoucherSpendCommand.FromRecord(existing);
                return existingCommand.PayloadEquals(command) ? VoucherJournalAddResult.AlreadyExists : VoucherJournalAddResult.Conflict;
            }

            m_Data.Transactions.Add(command.GameOrderId, command.ToRecord(VoucherTransactionState.Prepared));
            m_Save(m_Data);
            return VoucherJournalAddResult.Added;
        }

        /// <summary>
        /// 按订单号读取不可变交易命令和状态。
        /// </summary>
        /// <param name="orderId">游戏订单号。</param>
        /// <param name="entry">读取到的交易日志条目。</param>
        /// <returns>找到有效交易记录时返回 true，否则返回 false。</returns>
        internal bool TryGet(string orderId, out VoucherTransactionEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(orderId) || !m_Data.Transactions.TryGetValue(orderId, out VoucherTransactionRecord record) || record == null)
            {
                return false;
            }

            entry = ToEntry(record);
            return true;
        }

        /// <summary>
        /// 返回指定账号的全部交易，并按创建时间和订单号稳定排序。
        /// </summary>
        /// <param name="accountId">账号 ID。</param>
        /// <returns>指定账号的交易日志条目。</returns>
        internal IReadOnlyList<VoucherTransactionEntry> GetAll(string accountId)
        {
            return m_Data.Transactions.Values.Where(record => record != null && string.Equals(record.AccountId, accountId ?? string.Empty, StringComparison.Ordinal)).OrderBy(record => record.CreatedAtUnixTimeMs).ThenBy(record => record.GameOrderId, StringComparer.Ordinal).Select(ToEntry).ToArray();
        }

        /// <summary>
        /// 判断指定账号是否仍有未派发或结果未知的交易。
        /// </summary>
        /// <param name="accountId">账号 ID。</param>
        /// <returns>存在交易日志时返回 true，否则返回 false。</returns>
        internal bool HasAny(string accountId)
        {
            return m_Data.Transactions.Values.Any(record => record != null && string.Equals(record.AccountId, accountId ?? string.Empty, StringComparison.Ordinal));
        }

        /// <summary>
        /// 将发送结果未知的订单标记为等待恢复，并保留原始交易命令。
        /// </summary>
        /// <param name="orderId">游戏订单号。</param>
        /// <param name="message">本次发送失败原因。</param>
        /// <returns>找到并更新交易记录时返回 true，否则返回 false。</returns>
        internal bool MarkPendingRecovery(string orderId, string message)
        {
            return Update(orderId, record =>
            {
                record.State = VoucherTransactionState.PendingRecovery;
                record.ErrorMessage = message ?? string.Empty;
            });
        }

        /// <summary>
        /// 将成功订单标记为待派发，保证事件前终态已经落盘。
        /// </summary>
        /// <param name="orderId">游戏订单号。</param>
        /// <returns>找到并更新交易记录时返回 true，否则返回 false。</returns>
        internal bool MarkSucceededPendingDispatch(string orderId)
        {
            return Update(orderId, record =>
            {
                record.State = VoucherTransactionState.SucceededPendingDispatch;
                record.ErrorCode = 0;
                record.ErrorMessage = string.Empty;
            });
        }

        /// <summary>
        /// 将明确拒绝订单标记为待派发失败事件。
        /// </summary>
        /// <param name="orderId">游戏订单号。</param>
        /// <param name="errorCode">终态错误码。</param>
        /// <param name="errorMessage">终态错误信息。</param>
        /// <returns>找到并更新交易记录时返回 true，否则返回 false。</returns>
        internal bool MarkRejectedPendingDispatch(string orderId, int errorCode, string errorMessage)
        {
            return Update(orderId, record =>
            {
                record.State = VoucherTransactionState.RejectedPendingDispatch;
                record.ErrorCode = errorCode;
                record.ErrorMessage = errorMessage ?? string.Empty;
            });
        }

        /// <summary>
        /// 在终态事件派发完成后删除指定订单。
        /// </summary>
        /// <param name="orderId">游戏订单号。</param>
        /// <returns>删除并保存成功时返回 true，否则返回 false。</returns>
        internal bool Remove(string orderId)
        {
            if (string.IsNullOrEmpty(orderId) || !m_Data.Transactions.Remove(orderId))
            {
                return false;
            }

            m_Save(m_Data);
            return true;
        }
    }
}
