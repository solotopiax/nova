/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherWalletSnapshot.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 对外不可变钱包快照与刷新结果
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 单个礼券档位的聚合余额；不包含券唯一码。
    /// </summary>
    public sealed class VoucherWalletBalance
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
        /// 单张礼券面值，单位为 mills。
        /// </summary>
        public long FaceValueMills { get; }

        /// <summary>
        /// 当前可用数量。
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// 创建不可变礼券聚合余额。
        /// </summary>
        /// <param name="voucherTierId">礼券档位 ID。</param>
        /// <param name="faceValue">原始面值字符串。</param>
        /// <param name="faceValueMills">面值 mills。</param>
        /// <param name="quantity">可用数量。</param>
        internal VoucherWalletBalance(int voucherTierId, string faceValue, long faceValueMills, int quantity)
        {
            VoucherTierId = voucherTierId;
            FaceValue = faceValue ?? string.Empty;
            FaceValueMills = faceValueMills;
            Quantity = Math.Max(0, quantity);
        }
    }

    /// <summary>
    /// 单种赠币的聚合余额。
    /// </summary>
    public sealed class VoucherCoinBalance
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
        /// 单枚赠币面值，单位为 mills。
        /// </summary>
        public long FaceValueMills { get; }

        /// <summary>
        /// 当前可用数量。
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// 创建不可变赠币聚合余额。
        /// </summary>
        /// <param name="coinId">赠币类型 ID。</param>
        /// <param name="faceValue">原始面值字符串。</param>
        /// <param name="faceValueMills">面值 mills。</param>
        /// <param name="quantity">可用数量。</param>
        internal VoucherCoinBalance(int coinId, string faceValue, long faceValueMills, int quantity)
        {
            CoinId = coinId;
            FaceValue = faceValue ?? string.Empty;
            FaceValueMills = faceValueMills;
            Quantity = Math.Max(0, quantity);
        }
    }

    /// <summary>
    /// 当前账号可见的不可变 Voucher 钱包快照。
    /// </summary>
    public sealed class VoucherWalletSnapshot
    {
        /// <summary>
        /// 不包含任何礼券余额的共享只读集合。
        /// </summary>
        private static readonly IReadOnlyList<VoucherWalletBalance> s_EmptyVouchers = new ReadOnlyCollection<VoucherWalletBalance>(Array.Empty<VoucherWalletBalance>());

        /// <summary>
        /// 不包含任何赠币余额的共享只读集合。
        /// </summary>
        private static readonly IReadOnlyList<VoucherCoinBalance> s_EmptyCoins = new ReadOnlyCollection<VoucherCoinBalance>(Array.Empty<VoucherCoinBalance>());

        /// <summary>
        /// 钱包是否已经成功刷新并可用于报价。
        /// </summary>
        public bool IsReady { get; }

        /// <summary>
        /// 当前账号作用域内单调递增的钱包版本。
        /// </summary>
        public long Version { get; }

        /// <summary>
        /// 成功刷新时间，Unix 毫秒时间戳；未就绪时为 0。
        /// </summary>
        public long RefreshedAtUnixTimeMs { get; }

        /// <summary>
        /// 礼券聚合余额只读列表。
        /// </summary>
        public IReadOnlyList<VoucherWalletBalance> VoucherBalances { get; }

        /// <summary>
        /// 赠币聚合余额只读列表。
        /// </summary>
        public IReadOnlyList<VoucherCoinBalance> CoinBalances { get; }

        /// <summary>
        /// 创建不可变钱包快照，并防御性复制聚合余额集合。
        /// </summary>
        /// <param name="isReady">是否已经就绪。</param>
        /// <param name="version">钱包版本。</param>
        /// <param name="refreshedAtUnixTimeMs">刷新时间。</param>
        /// <param name="voucherBalances">礼券聚合余额。</param>
        /// <param name="coinBalances">赠币聚合余额。</param>
        internal VoucherWalletSnapshot(bool isReady, long version, long refreshedAtUnixTimeMs, VoucherWalletBalance[] voucherBalances, VoucherCoinBalance[] coinBalances)
        {
            IsReady = isReady;
            Version = version;
            RefreshedAtUnixTimeMs = refreshedAtUnixTimeMs;
            VoucherBalances = voucherBalances == null || voucherBalances.Length == 0 ? s_EmptyVouchers : new ReadOnlyCollection<VoucherWalletBalance>((VoucherWalletBalance[])voucherBalances.Clone());
            CoinBalances = coinBalances == null || coinBalances.Length == 0 ? s_EmptyCoins : new ReadOnlyCollection<VoucherCoinBalance>((VoucherCoinBalance[])coinBalances.Clone());
        }

        /// <summary>
        /// 创建未就绪的空钱包快照。
        /// </summary>
        /// <returns>不携带任何旧账号余额的快照。</returns>
        internal static VoucherWalletSnapshot CreateNotReady()
        {
            return new VoucherWalletSnapshot(false, 0, 0, null, null);
        }
    }

    /// <summary>
    /// Voucher 钱包刷新结果。
    /// </summary>
    public sealed class VoucherRefreshResult
    {
        /// <summary>
        /// 本次刷新是否成功发布了新快照。
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 失败错误码；成功时为 None。
        /// </summary>
        public IAPVoucherErrorCode ErrorCode { get; }

        /// <summary>
        /// 失败描述；成功时为空字符串。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 刷新完成后当前可见的钱包快照。
        /// </summary>
        public VoucherWalletSnapshot Wallet { get; }

        /// <summary>
        /// 创建钱包刷新结果。
        /// </summary>
        /// <param name="isSuccess">是否成功。</param>
        /// <param name="errorCode">Voucher 错误码。</param>
        /// <param name="errorMessage">错误描述。</param>
        /// <param name="wallet">刷新后的当前快照。</param>
        internal VoucherRefreshResult(bool isSuccess, IAPVoucherErrorCode errorCode, string errorMessage, VoucherWalletSnapshot wallet)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage ?? string.Empty;
            Wallet = wallet ?? VoucherWalletSnapshot.CreateNotReady();
        }
    }
}
