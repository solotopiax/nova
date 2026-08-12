/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPVoucherRequest.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   由 Ready Voucher 报价创建的支付请求
 ***************************************************************/

using System;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// Voucher 支付请求；业务层不能手工拼装券码或赠币用量。
    /// </summary>
    public sealed class IAPVoucherRequest : IAPRequest
    {
        /// <summary>
        /// 当前请求对应的渠道类型，固定为 Voucher。
        /// </summary>
        public override IAPStoreType StoreType => IAPStoreType.Voucher;

        /// <summary>
        /// 创建请求时绑定的不可变可支付报价。
        /// </summary>
        public VoucherQuote Quote { get; }

        /// <summary>
        /// 从可提交的 Ready 报价创建 Voucher 支付请求。
        /// </summary>
        /// <param name="quote">当前 capability 生成的 Ready 报价。</param>
        /// <exception cref="ArgumentNullException">quote 为空时抛出。</exception>
        /// <exception cref="ArgumentException">quote 不是 Ready 时抛出。</exception>
        public IAPVoucherRequest(VoucherQuote quote)
        {
            if (quote == null)
            {
                throw new ArgumentNullException(nameof(quote));
            }
            if (quote.Status != VoucherQuoteStatus.Ready)
            {
                throw new ArgumentException("只有 Ready 状态的 VoucherQuote 可以创建支付请求。", nameof(quote));
            }

            Quote = quote;
            TableId = quote.TableId;
        }
    }
}
