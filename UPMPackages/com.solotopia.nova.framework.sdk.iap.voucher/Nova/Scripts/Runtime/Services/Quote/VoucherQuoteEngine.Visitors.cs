/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherQuoteEngine.Visitors.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   VoucherQuoteEngine 内部候选类型
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherQuoteEngine 内部候选类型。
    /// </summary>
    internal static partial class VoucherQuoteEngine
    {
        /// <summary>
        /// 报价候选资产类型。
        /// </summary>
        private enum AssetKind
        {
            /// <summary>
            /// 候选资产为礼券。
            /// </summary>
            Voucher = 0,

            /// <summary>
            /// 候选资产为赠币。
            /// </summary>
            Coin = 1,
        }

        /// <summary>
        /// 一个有界资产类型候选。
        /// </summary>
        private sealed partial class AssetOption
        {
            /// <summary>
            /// 候选资产类型。
            /// </summary>
            internal AssetKind Kind { get; private set; }

            /// <summary>
            /// 用于确定性排序的资产 ID。
            /// </summary>
            internal int StableId { get; private set; }

            /// <summary>
            /// 单份资产面值，单位为 mills。
            /// </summary>
            internal long FaceValueMills { get; private set; }

            /// <summary>
            /// 当前钱包可用数量。
            /// </summary>
            internal int Quantity { get; private set; }

            /// <summary>
            /// 礼券资产；当前候选为赠币时为空。
            /// </summary>
            internal VoucherAssetData Voucher { get; private set; }

            /// <summary>
            /// 赠币资产；当前候选为礼券时为空。
            /// </summary>
            internal CoinAssetData Coin { get; private set; }

        }

        /// <summary>
        /// 动态规划中某一已覆盖金额的最优候选。
        /// </summary>
        private sealed partial class Candidate
        {
            /// <summary>
            /// 每种候选资产的使用数量。
            /// </summary>
            internal int[] Counts { get; }

            /// <summary>
            /// 候选中礼券抵扣的总金额。
            /// </summary>
            internal long VoucherAmountMills { get; }

            /// <summary>
            /// 候选使用的资产总件数。
            /// </summary>
            internal int ItemCount { get; }

        }
    }
}
