/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherStore.Visitors.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   VoucherStore 字段与属性
 ***************************************************************/

using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherStore 字段与属性。
    /// </summary>
    public sealed partial class VoucherStore
    {
        /// <summary>
        /// 当前 Store 的渠道类型，固定为 Voucher。
        /// </summary>
        public override IAPStoreType StoreType => IAPStoreType.Voucher;

        /// <summary>
        /// 当前账号的不可变钱包快照；账号未就绪时返回空快照。
        /// </summary>
        public VoucherWalletSnapshot Wallet => m_Session?.Wallet ?? VoucherWalletSnapshot.CreateNotReady();

        /// <summary>
        /// 支付打点使用的 Voucher 渠道标识。
        /// </summary>
        protected override string TrackChannel => "voucher";

        /// <summary>
        /// Voucher Store 使用的日志标签。
        /// </summary>
        protected override string StoreLogTag => LogTag.IAPVoucher;

        /// <summary>
        /// 当前 Voucher Store 配置。
        /// </summary>
        private VoucherStoreConfig m_Config;

        /// <summary>
        /// 当前账号的钱包作用域与 generation 管理器。
        /// </summary>
        private VoucherWalletSession m_Session;

        /// <summary>
        /// Voucher 领域命令与网络协议之间的网关。
        /// </summary>
        private IVoucherGateway m_Gateway;

        /// <summary>
        /// 当前账号的 Voucher 交易持久化数据。
        /// </summary>
        private VoucherStorePersistData m_PersistData;

        /// <summary>
        /// 当前账号按订单号索引的交易日志。
        /// </summary>
        private VoucherTransactionJournal m_Journal;

        /// <summary>
        /// 当前账号的可恢复 Voucher 交易协调器。
        /// </summary>
        private VoucherTransactionCoordinator m_Coordinator;
    }
}
