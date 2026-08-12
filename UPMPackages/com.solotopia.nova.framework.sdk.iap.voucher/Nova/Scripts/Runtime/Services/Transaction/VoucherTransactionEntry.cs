/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherTransactionEntry.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 交易日志只读查询结果
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 交易日志中的不可变命令与当前状态。
    /// </summary>
    internal sealed class VoucherTransactionEntry
    {
        /// <summary>
        /// 原始不可变交易命令。
        /// </summary>
        internal VoucherSpendCommand Command { get; }

        /// <summary>
        /// 当前持久化交易状态。
        /// </summary>
        internal VoucherTransactionState State { get; }

        /// <summary>
        /// 当前终态错误码。
        /// </summary>
        internal int ErrorCode { get; }

        /// <summary>
        /// 当前错误信息。
        /// </summary>
        internal string ErrorMessage { get; }

        /// <summary>
        /// 创建 Voucher 交易日志查询结果。
        /// </summary>
        /// <param name="command">原始不可变交易命令。</param>
        /// <param name="state">当前持久化交易状态。</param>
        /// <param name="errorCode">当前终态错误码。</param>
        /// <param name="errorMessage">当前错误信息。</param>
        internal VoucherTransactionEntry(VoucherSpendCommand command, VoucherTransactionState state, int errorCode, string errorMessage)
        {
            Command = command;
            State = state;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage ?? string.Empty;
        }
    }
}
