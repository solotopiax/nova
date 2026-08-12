/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherQuoteEngine.Methods.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   VoucherQuoteEngine 非公开报价方法
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherQuoteEngine 非公开报价方法。
    /// </summary>
    internal static partial class VoucherQuoteEngine
    {
        /// <summary>
        /// 将钱包资产转换为稳定排序的报价候选项。
        /// </summary>
        /// <param name="wallet">已就绪的内部钱包。</param>
        /// <returns>过滤无效面值和空余额后的候选项。</returns>
        private static AssetOption[] BuildOptions(VoucherWalletData wallet)
        {
            var options = new List<AssetOption>();
            foreach (VoucherAssetData voucher in wallet.Vouchers)
            {
                if (voucher != null && voucher.FaceValueMills > 0 && voucher.Quantity > 0)
                {
                    options.Add(AssetOption.FromVoucher(voucher));
                }
            }

            foreach (CoinAssetData coin in wallet.Coins)
            {
                if (coin != null && coin.FaceValueMills > 0 && coin.Quantity > 0)
                {
                    options.Add(AssetOption.FromCoin(coin));
                }
            }

            return options.OrderByDescending(item => item.FaceValueMills).ThenBy(item => item.Kind).ThenBy(item => item.StableId).ToArray();
        }

        /// <summary>
        /// 比较同一总金额下的两个候选解。
        /// </summary>
        /// <param name="left">待评估候选。</param>
        /// <param name="right">当前候选。</param>
        /// <returns>待评估候选更符合确定性优先级时返回 true。</returns>
        private static bool IsBetter(Candidate left, Candidate right)
        {
            if (left.VoucherAmountMills != right.VoucherAmountMills)
            {
                return left.VoucherAmountMills > right.VoucherAmountMills;
            }

            if (left.ItemCount != right.ItemCount)
            {
                return left.ItemCount < right.ItemCount;
            }

            for (int i = 0; i < left.Counts.Length; i++)
            {
                if (left.Counts[i] != right.Counts[i])
                {
                    return left.Counts[i] > right.Counts[i];
                }
            }

            return false;
        }

        /// <summary>
        /// 将最优内部候选转换为公开展示明细和冻结扣减草案。
        /// </summary>
        /// <param name="wallet">报价使用的内部钱包。</param>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="priceMills">商品价格，单位为 mills。</param>
        /// <param name="options">稳定排序后的候选项。</param>
        /// <param name="candidate">最优候选。</param>
        /// <returns>可以提交支付的 Voucher 报价。</returns>
        private static VoucherQuote BuildReadyQuote(VoucherWalletData wallet, long tableId, long priceMills, AssetOption[] options, Candidate candidate)
        {
            var voucherLines = new List<VoucherSpendLine>();
            var coinLines = new List<CoinSpendLine>();
            var voucherCodes = new List<string>();
            var coinUsages = new List<CoinUsageData>();
            long coinAmountMills = 0;

            for (int i = 0; i < options.Length; i++)
            {
                int quantity = candidate.Counts[i];
                if (quantity <= 0)
                {
                    continue;
                }

                AssetOption option = options[i];
                if (option.Kind == AssetKind.Voucher)
                {
                    VoucherAssetData voucher = option.Voucher;
                    voucherLines.Add(new VoucherSpendLine(voucher.VoucherTierId, voucher.FaceValue, voucher.FaceValueMills, quantity));
                    voucherCodes.AddRange(voucher.VoucherCodes.Take(quantity));
                }
                else
                {
                    CoinAssetData coin = option.Coin;
                    coinLines.Add(new CoinSpendLine(coin.CoinId, coin.FaceValue, coin.FaceValueMills, quantity));
                    coinUsages.Add(new CoinUsageData(coin.CoinId, quantity));
                    coinAmountMills += coin.FaceValueMills * quantity;
                }
            }

            return new VoucherQuote(VoucherQuoteStatus.Ready, tableId, priceMills, wallet.Version, candidate.VoucherAmountMills, coinAmountMills, voucherLines.ToArray(), coinLines.ToArray(), wallet.AccountId, wallet.AccountGeneration, new VoucherFrozenSpend(voucherCodes, coinUsages));
        }

        /// <summary>
        /// 创建不携带任何部分扣减信息的失败报价。
        /// </summary>
        /// <param name="wallet">当前内部钱包，可为空。</param>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="priceMills">商品价格，单位为 mills。</param>
        /// <param name="status">报价失败状态。</param>
        /// <returns>不可提交的 Voucher 报价。</returns>
        private static VoucherQuote CreateFailure(VoucherWalletData wallet, long tableId, long priceMills, VoucherQuoteStatus status)
        {
            return new VoucherQuote(status, tableId, priceMills, wallet?.Version ?? 0, 0, 0, null, null, wallet?.AccountId, wallet?.AccountGeneration ?? 0, VoucherFrozenSpend.Empty);
        }

        /// <summary>
        /// 报价资产候选的构造方法。
        /// </summary>
        private sealed partial class AssetOption
        {
            /// <summary>
            /// 从礼券资产创建候选项。
            /// </summary>
            /// <param name="voucher">礼券资产。</param>
            /// <returns>礼券候选项。</returns>
            internal static AssetOption FromVoucher(VoucherAssetData voucher)
            {
                return new AssetOption
                {
                    Kind = AssetKind.Voucher,
                    StableId = voucher.VoucherTierId,
                    FaceValueMills = voucher.FaceValueMills,
                    Quantity = voucher.Quantity,
                    Voucher = voucher,
                };
            }

            /// <summary>
            /// 从赠币资产创建候选项。
            /// </summary>
            /// <param name="coin">赠币资产。</param>
            /// <returns>赠币候选项。</returns>
            internal static AssetOption FromCoin(CoinAssetData coin)
            {
                return new AssetOption
                {
                    Kind = AssetKind.Coin,
                    StableId = coin.CoinId,
                    FaceValueMills = coin.FaceValueMills,
                    Quantity = coin.Quantity,
                    Coin = coin,
                };
            }
        }

        /// <summary>
        /// 动态规划候选的构造与派生方法。
        /// </summary>
        private sealed partial class Candidate
        {
            /// <summary>
            /// 创建动态规划候选。
            /// </summary>
            /// <param name="counts">每种候选资产的使用数量。</param>
            /// <param name="voucherAmountMills">礼券抵扣总金额。</param>
            /// <param name="itemCount">资产总件数。</param>
            private Candidate(int[] counts, long voucherAmountMills, int itemCount)
            {
                Counts = counts;
                VoucherAmountMills = voucherAmountMills;
                ItemCount = itemCount;
            }

            /// <summary>
            /// 创建尚未选择任何资产的初始候选。
            /// </summary>
            /// <param name="optionCount">候选资产类型数量。</param>
            /// <returns>零金额候选。</returns>
            internal static Candidate Empty(int optionCount) => new Candidate(new int[optionCount], 0, 0);

            /// <summary>
            /// 返回在当前候选上增加指定资产块后的新候选。
            /// </summary>
            /// <param name="optionIndex">资产候选索引。</param>
            /// <param name="option">资产候选。</param>
            /// <param name="quantity">增加数量。</param>
            /// <returns>防御性复制计数数组后的新候选。</returns>
            internal Candidate Add(int optionIndex, AssetOption option, int quantity)
            {
                int[] counts = (int[])Counts.Clone();
                counts[optionIndex] += quantity;
                long voucherAmount = VoucherAmountMills;
                if (option.Kind == AssetKind.Voucher)
                {
                    voucherAmount += option.FaceValueMills * quantity;
                }

                return new Candidate(counts, voucherAmount, ItemCount + quantity);
            }
        }
    }
}
