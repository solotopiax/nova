/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherJournalAddResult.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 交易日志新增结果
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 交易命令写入日志的结果。
    /// </summary>
    internal enum VoucherJournalAddResult
    {
        /// <summary>
        /// 新命令已经写入并完成持久化。
        /// </summary>
        Added,

        /// <summary>
        /// 相同订单号和相同 payload 已经存在。
        /// </summary>
        AlreadyExists,

        /// <summary>
        /// 相同订单号对应不同 payload，拒绝覆盖。
        /// </summary>
        Conflict,
    }
}
