/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DeductPlan.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   代金券扣费方案及扣费模式枚举
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 代金券扣费模式，描述本次支付采用哪种资产组合完成抵扣。
    /// </summary>
    public enum VoucherDeductMode
    {
        /// <summary>仅使用代金券余额抵扣。</summary>
        Voucher,

        /// <summary>仅使用金币抵扣。</summary>
        Coin,

        /// <summary>代金券与金币混合抵扣。</summary>
        VoucherCoin,

        /// <summary>代金券/金币不足，需补充现金支付差价。</summary>
        Cash,
    }

    /// <summary>
    /// 代金券/金币扣费方案。
    /// 由实现 IIAPVoucherCapable 的 store 计算得出，经 IAPPlugin.TryGetCapability 取用，
    /// 用于在支付确认 UI 中展示扣减明细，也可直接转换为服务端请求的抵扣明细。
    /// 所有属性只读，创建后不可变更。
    /// </summary>
    public sealed class DeductPlan
    {
        /// <summary>
        /// 本次推荐的扣费模式。
        /// </summary>
        public VoucherDeductMode Mode { get; }

        /// <summary>
        /// 本次消耗的代金券金额（毫分，price_usd × 1000）。
        /// Mode 为 Coin 时此值为 0。
        /// </summary>
        public long VoucherAmount { get; }

        /// <summary>
        /// 本次消耗的金币数量（通用计数，不区分金币类型）。
        /// Mode 为 Voucher 时此值为 0。
        /// </summary>
        public int CoinQuantity { get; }

        /// <summary>
        /// 需额外支付的现金金额（毫分）。
        /// Mode 不为 Cash 时此值为 0。
        /// </summary>
        public long CashAmount { get; }

        /// <summary>
        /// 本次消耗的礼券明细列表，可直接用于构造服务端抵扣请求体。
        /// 未使用礼券时为空列表。
        /// </summary>
        public IReadOnlyList<DeductVoucherItem> VoucherUsed { get; }

        /// <summary>
        /// 本次消耗的金币明细列表，可直接用于构造服务端抵扣请求体。
        /// 未使用金币时为空列表。
        /// </summary>
        public IReadOnlyList<DeductCoinItem> CoinUsed { get; }

        /// <summary>
        /// 构造代金券扣费方案。
        /// </summary>
        /// <param name="mode">推荐扣费模式。</param>
        /// <param name="voucherAmount">消耗代金券金额（毫分）；Mode 为 Coin 时传 0。</param>
        /// <param name="coinQuantity">消耗金币数量；Mode 为 Voucher 时传 0。</param>
        /// <param name="cashAmount">需额外支付的现金金额（毫分）；Mode 不为 Cash 时传 0。</param>
        /// <param name="voucherUsed">礼券使用明细列表；为 null 时视为空列表。</param>
        /// <param name="coinUsed">金币使用明细列表；为 null 时视为空列表。</param>
        public DeductPlan(VoucherDeductMode mode, long voucherAmount, int coinQuantity, long cashAmount,
            IReadOnlyList<DeductVoucherItem> voucherUsed = null,
            IReadOnlyList<DeductCoinItem> coinUsed = null)
        {
            Mode = mode;
            VoucherAmount = voucherAmount;
            CoinQuantity = coinQuantity;
            CashAmount = cashAmount;
            VoucherUsed = voucherUsed ?? new List<DeductVoucherItem>();
            CoinUsed = coinUsed ?? new List<DeductCoinItem>();
        }
    }
}
