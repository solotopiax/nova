/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherGatewayDeductResult.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 网关资产扣减结果
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 网关返回的扣减分类与可选新钱包。
    /// </summary>
    internal sealed class VoucherGatewayDeductResult
    {
        /// <summary>
        /// 扣减结果分类。
        /// </summary>
        internal VoucherGatewayDisposition Disposition { get; }

        /// <summary>
        /// 网络或业务错误码。
        /// </summary>
        internal int ErrorCode { get; }

        /// <summary>
        /// 错误或诊断信息。
        /// </summary>
        internal string Message { get; }

        /// <summary>
        /// 成功响应携带的礼券资产。
        /// </summary>
        internal IReadOnlyList<VoucherAssetData> Vouchers { get; }

        /// <summary>
        /// 成功响应携带的赠币资产。
        /// </summary>
        internal IReadOnlyList<CoinAssetData> Coins { get; }

        /// <summary>
        /// 创建 Voucher 扣减网关结果并防御性复制响应钱包。
        /// </summary>
        /// <param name="disposition">扣减结果分类。</param>
        /// <param name="errorCode">网络或业务错误码。</param>
        /// <param name="message">错误或诊断信息。</param>
        /// <param name="vouchers">成功响应携带的礼券资产。</param>
        /// <param name="coins">成功响应携带的赠币资产。</param>
        internal VoucherGatewayDeductResult(VoucherGatewayDisposition disposition, int errorCode, string message, IEnumerable<VoucherAssetData> vouchers = null, IEnumerable<CoinAssetData> coins = null)
        {
            Disposition = disposition;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            Vouchers = new ReadOnlyCollection<VoucherAssetData>(vouchers == null ? Array.Empty<VoucherAssetData>() : new List<VoucherAssetData>(vouchers).ToArray());
            Coins = new ReadOnlyCollection<CoinAssetData>(coins == null ? Array.Empty<CoinAssetData>() : new List<CoinAssetData>(coins).ToArray());
        }
    }
}
