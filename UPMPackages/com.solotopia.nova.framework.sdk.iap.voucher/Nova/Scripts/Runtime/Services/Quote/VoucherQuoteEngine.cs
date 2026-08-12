/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherQuoteEngine.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 有界精确组合报价入口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 纯报价引擎。
    /// 不访问网络、持久化或 Unity 运行时状态。
    /// </summary>
    internal static partial class VoucherQuoteEngine
    {
        /// <summary>
        /// 使用有界组合优化寻找精确覆盖价格的确定性资产组合。
        /// </summary>
        /// <param name="wallet">当前账号内部钱包。</param>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="priceMills">商品价格，单位为 mills。</param>
        /// <returns>绑定钱包版本和冻结扣减草案的不可变报价。</returns>
        internal static VoucherQuote Quote(VoucherWalletData wallet, long tableId, long priceMills)
        {
            if (priceMills <= 0)
            {
                return CreateFailure(wallet, tableId, priceMills, VoucherQuoteStatus.InvalidPrice);
            }

            if (wallet == null || !wallet.IsReady)
            {
                return CreateFailure(wallet, tableId, priceMills, VoucherQuoteStatus.WalletNotReady);
            }

            AssetOption[] options = BuildOptions(wallet);
            if (options.Length == 0)
            {
                return CreateFailure(wallet, tableId, priceMills, VoucherQuoteStatus.InsufficientBalance);
            }

            var states = new Dictionary<long, Candidate>
            {
                [0] = Candidate.Empty(options.Length),
            };

            for (int optionIndex = 0; optionIndex < options.Length; optionIndex++)
            {
                AssetOption option = options[optionIndex];
                long usableLong = Math.Min(option.Quantity, priceMills / option.FaceValueMills);
                int remaining = usableLong > int.MaxValue ? int.MaxValue : (int)usableLong;
                int chunkSize = 1;
                while (remaining > 0)
                {
                    int take = Math.Min(chunkSize, remaining);
                    KeyValuePair<long, Candidate>[] snapshot = states.ToArray();
                    foreach (KeyValuePair<long, Candidate> state in snapshot)
                    {
                        long addedAmount = option.FaceValueMills * take;
                        if (state.Key > priceMills - addedAmount)
                        {
                            continue;
                        }

                        long nextAmount = state.Key + addedAmount;
                        Candidate next = state.Value.Add(optionIndex, option, take);
                        if (!states.TryGetValue(nextAmount, out Candidate current) || IsBetter(next, current))
                        {
                            states[nextAmount] = next;
                        }
                    }

                    remaining -= take;
                    chunkSize = remaining > chunkSize ? chunkSize * 2 : remaining;
                }
            }

            return states.TryGetValue(priceMills, out Candidate best) ? BuildReadyQuote(wallet, tableId, priceMills, options, best) : CreateFailure(wallet, tableId, priceMills, VoucherQuoteStatus.InsufficientBalance);
        }
    }
}
