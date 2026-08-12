/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherTransactionCoordinator.Visitors.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   VoucherTransactionCoordinator 常量与字段
 ***************************************************************/

using System;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherTransactionCoordinator 常量与字段。
    /// </summary>
    internal sealed partial class VoucherTransactionCoordinator
    {
        /// <summary>
        /// 当前不可变交易命令结构版本。
        /// </summary>
        private const int c_CommandSchemaVersion = 1;

        /// <summary>
        /// Voucher 协议网关。
        /// </summary>
        private readonly IVoucherGateway m_Gateway;

        /// <summary>
        /// 当前账号交易日志。
        /// </summary>
        private readonly VoucherTransactionJournal m_Journal;

        /// <summary>
        /// 当前账号钱包会话。
        /// </summary>
        private readonly VoucherWalletSession m_Session;

        /// <summary>
        /// 终态 IAP 结果派发端口。
        /// </summary>
        private readonly IVoucherResultDispatcher m_Dispatcher;

        /// <summary>
        /// 稳定订单号工厂。
        /// </summary>
        private readonly Func<string> m_OrderIdFactory;

        /// <summary>
        /// UTC 毫秒时间戳提供器。
        /// </summary>
        private readonly Func<long> m_UtcNowUnixTimeMs;

        /// <summary>
        /// 当前国家或地区提供器。
        /// </summary>
        private readonly Func<string> m_CountryProvider;

        /// <summary>
        /// 新支付与补单恢复共享的串行锁状态。
        /// </summary>
        private int m_IsBusy;
    }
}
