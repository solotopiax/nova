/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherGatewayWalletResult.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 网关钱包查询结果
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 网关返回的钱包资产结果。
    /// </summary>
    internal sealed class VoucherGatewayWalletResult
    {
        /// <summary>
        /// 钱包查询是否成功。
        /// </summary>
        internal bool IsSuccess { get; }

        /// <summary>
        /// Voucher 业务错误码。
        /// </summary>
        internal IAPVoucherErrorCode ErrorCode { get; }

        /// <summary>
        /// 错误或诊断信息。
        /// </summary>
        internal string Message { get; }

        /// <summary>
        /// 查询到的礼券资产。
        /// </summary>
        internal IReadOnlyList<VoucherAssetData> Vouchers { get; }

        /// <summary>
        /// 查询到的赠币资产。
        /// </summary>
        internal IReadOnlyList<CoinAssetData> Coins { get; }

        /// <summary>
        /// 创建 Voucher 钱包网关结果并防御性复制资产集合。
        /// </summary>
        /// <param name="isSuccess">钱包查询是否成功。</param>
        /// <param name="errorCode">Voucher 业务错误码。</param>
        /// <param name="message">错误或诊断信息。</param>
        /// <param name="vouchers">礼券资产集合。</param>
        /// <param name="coins">赠币资产集合。</param>
        internal VoucherGatewayWalletResult(bool isSuccess, IAPVoucherErrorCode errorCode, string message, IEnumerable<VoucherAssetData> vouchers, IEnumerable<CoinAssetData> coins)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            Vouchers = new ReadOnlyCollection<VoucherAssetData>(vouchers == null ? Array.Empty<VoucherAssetData>() : new List<VoucherAssetData>(vouchers).ToArray());
            Coins = new ReadOnlyCollection<CoinAssetData>(coins == null ? Array.Empty<CoinAssetData>() : new List<CoinAssetData>(coins).ToArray());
        }
    }
}
