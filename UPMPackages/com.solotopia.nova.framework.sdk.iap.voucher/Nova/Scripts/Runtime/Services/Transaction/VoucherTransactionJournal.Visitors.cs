/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherTransactionJournal.Visitors.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   VoucherTransactionJournal 字段与属性
 ***************************************************************/

using System;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherTransactionJournal 字段与属性。
    /// </summary>
    internal sealed partial class VoucherTransactionJournal
    {
        /// <summary>
        /// 当前账号持久化容器。
        /// </summary>
        private readonly VoucherStorePersistData m_Data;

        /// <summary>
        /// 原子保存当前持久化容器的回调。
        /// </summary>
        private readonly Action<VoucherStorePersistData> m_Save;
    }
}
