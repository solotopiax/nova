/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherQuote.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 不可变报价及对外展示明细
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NovaFramework.SDK.IAP.Voucher.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// Voucher 报价状态。
    /// </summary>
    public enum VoucherQuoteStatus
    {
        /// <summary>
        /// 资产可以精确覆盖价格，可创建支付请求。
        /// </summary>
        Ready,

        /// <summary>
        /// 当前账号钱包尚未准备完成。
        /// </summary>
        WalletNotReady,

        /// <summary>
        /// 价格小于等于零或无法安全表示。
        /// </summary>
        InvalidPrice,

        /// <summary>
        /// 当前资产不存在精确覆盖价格的组合。
        /// </summary>
        InsufficientBalance,
    }

    /// <summary>
    /// 报价中的礼券聚合展示明细；不包含券唯一码。
    /// </summary>
    public sealed class VoucherSpendLine
    {
        /// <summary>
        /// 礼券档位 ID。
        /// </summary>
        public int VoucherTierId { get; }

        /// <summary>
        /// 服务端返回的原始面值字符串。
        /// </summary>
        public string FaceValue { get; }

        /// <summary>
        /// 单张面值，单位为 mills。
        /// </summary>
        public long FaceValueMills { get; }

        /// <summary>
        /// 本次使用数量。
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// 创建礼券展示明细。
        /// </summary>
        /// <param name="voucherTierId">礼券档位 ID。</param>
        /// <param name="faceValue">原始面值字符串。</param>
        /// <param name="faceValueMills">面值 mills。</param>
        /// <param name="quantity">使用数量。</param>
        internal VoucherSpendLine(int voucherTierId, string faceValue, long faceValueMills, int quantity)
        {
            VoucherTierId = voucherTierId;
            FaceValue = faceValue ?? string.Empty;
            FaceValueMills = faceValueMills;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// 报价中的赠币聚合展示明细。
    /// </summary>
    public sealed class CoinSpendLine
    {
        /// <summary>
        /// 赠币类型 ID。
        /// </summary>
        public int CoinId { get; }

        /// <summary>
        /// 服务端返回的原始面值字符串。
        /// </summary>
        public string FaceValue { get; }

        /// <summary>
        /// 单枚面值，单位为 mills。
        /// </summary>
        public long FaceValueMills { get; }

        /// <summary>
        /// 本次使用数量。
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// 创建赠币展示明细。
        /// </summary>
        /// <param name="coinId">赠币类型 ID。</param>
        /// <param name="faceValue">原始面值字符串。</param>
        /// <param name="faceValueMills">面值 mills。</param>
        /// <param name="quantity">使用数量。</param>
        internal CoinSpendLine(int coinId, string faceValue, long faceValueMills, int quantity)
        {
            CoinId = coinId;
            FaceValue = faceValue ?? string.Empty;
            FaceValueMills = faceValueMills;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// 绑定账号 generation、钱包版本和冻结扣减草案的不可变 Voucher 报价。
    /// </summary>
    public sealed class VoucherQuote
    {
        /// <summary>
        /// 不包含任何礼券明细的共享只读集合。
        /// </summary>
        private static readonly IReadOnlyList<VoucherSpendLine> s_EmptyVouchers = new ReadOnlyCollection<VoucherSpendLine>(Array.Empty<VoucherSpendLine>());

        /// <summary>
        /// 不包含任何赠币明细的共享只读集合。
        /// </summary>
        private static readonly IReadOnlyList<CoinSpendLine> s_EmptyCoins = new ReadOnlyCollection<CoinSpendLine>(Array.Empty<CoinSpendLine>());

        /// <summary>
        /// 报价状态。
        /// </summary>
        public VoucherQuoteStatus Status { get; }

        /// <summary>
        /// 商品配置表行 ID。
        /// </summary>
        public long TableId { get; }

        /// <summary>
        /// 商品价格，单位为 mills。
        /// </summary>
        public long PriceMills { get; }

        /// <summary>
        /// 报价基于的钱包版本。
        /// </summary>
        public long WalletVersion { get; }

        /// <summary>
        /// 礼券抵扣总额，单位为 mills。
        /// </summary>
        public long VoucherAmountMills { get; }

        /// <summary>
        /// 赠币抵扣总额，单位为 mills。
        /// </summary>
        public long CoinAmountMills { get; }

        /// <summary>
        /// 面向 UI 的礼券聚合展示明细。
        /// </summary>
        public IReadOnlyList<VoucherSpendLine> VoucherSpend { get; }

        /// <summary>
        /// 面向 UI 的赠币聚合展示明细。
        /// </summary>
        public IReadOnlyList<CoinSpendLine> CoinSpend { get; }

        /// <summary>
        /// 报价所属账号，仅供 Store 校验，不向业务层公开。
        /// </summary>
        internal string AccountId { get; }

        /// <summary>
        /// 报价所属账号 generation，仅供 Store 校验。
        /// </summary>
        internal long AccountGeneration { get; }

        /// <summary>
        /// 包含精确券码的冻结草案，仅供交易协调器创建命令。
        /// </summary>
        internal VoucherFrozenSpend FrozenSpend { get; }

        /// <summary>
        /// 创建不可变 Voucher 报价，并防御性复制所有展示集合。
        /// </summary>
        /// <param name="status">报价状态。</param>
        /// <param name="tableId">商品表 ID。</param>
        /// <param name="priceMills">商品价格。</param>
        /// <param name="walletVersion">钱包版本。</param>
        /// <param name="voucherAmountMills">礼券抵扣总额。</param>
        /// <param name="coinAmountMills">赠币抵扣总额。</param>
        /// <param name="voucherSpend">礼券展示明细。</param>
        /// <param name="coinSpend">赠币展示明细。</param>
        /// <param name="accountId">账号 ID。</param>
        /// <param name="accountGeneration">账号 generation。</param>
        /// <param name="frozenSpend">冻结扣减草案。</param>
        internal VoucherQuote(VoucherQuoteStatus status, long tableId, long priceMills, long walletVersion, long voucherAmountMills, long coinAmountMills, VoucherSpendLine[] voucherSpend, CoinSpendLine[] coinSpend, string accountId, long accountGeneration, VoucherFrozenSpend frozenSpend)
        {
            Status = status;
            TableId = tableId;
            PriceMills = priceMills;
            WalletVersion = walletVersion;
            VoucherAmountMills = voucherAmountMills;
            CoinAmountMills = coinAmountMills;
            VoucherSpend = voucherSpend == null || voucherSpend.Length == 0 ? s_EmptyVouchers : new ReadOnlyCollection<VoucherSpendLine>((VoucherSpendLine[])voucherSpend.Clone());
            CoinSpend = coinSpend == null || coinSpend.Length == 0 ? s_EmptyCoins : new ReadOnlyCollection<CoinSpendLine>((CoinSpendLine[])coinSpend.Clone());
            AccountId = accountId ?? string.Empty;
            AccountGeneration = accountGeneration;
            FrozenSpend = frozenSpend ?? VoucherFrozenSpend.Empty;
        }
    }
}
