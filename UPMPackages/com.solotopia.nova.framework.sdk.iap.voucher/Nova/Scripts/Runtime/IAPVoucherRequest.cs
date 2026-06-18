/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPVoucherRequest.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   代金券/金币兑换渠道支付请求
 ***************************************************************/

using System.Collections.Generic;

using NovaFramework.Runtime;
namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 代金券/金币兑换渠道支付请求。
    /// 适用于通过持有的代金券码或金币抵扣商品价格的支付流程。
    /// VoucherCodes 和 CoinUsages 至少提供其一；store 层按 DeductPlan 决定实际扣减组合。
    /// </summary>
    public sealed class IAPVoucherRequest : IAPRequest
    {
        /// <summary>
        /// 当前请求对应的渠道类型，固定为 Voucher。
        /// </summary>
        public override IAPStoreType StoreType => IAPStoreType.Voucher;

        /// <summary>
        /// 参与本次抵扣的代金券激活码列表。
        /// 为 null 或空表示不使用代金券。
        /// </summary>
        public List<string> VoucherCodes;

        /// <summary>
        /// 参与本次抵扣的金币用量列表，每项指定金币类型及消耗数量。
        /// 为 null 或空表示不使用金币。
        /// </summary>
        public List<VoucherCoinUsage> CoinUsages;

        /// <summary>
        /// 是否向服务端提交对账订单。
        /// true 时 store 层在扣减成功后发起订单上报；false 时仅本地扣减，不上报。
        /// </summary>
        public bool AddOrder;
    }
}
