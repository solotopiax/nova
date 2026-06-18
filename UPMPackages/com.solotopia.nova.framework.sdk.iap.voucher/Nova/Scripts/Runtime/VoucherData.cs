/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherData.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   代金券/金币 store 数据结构（协议 DTO、余额快照、错误码）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// 礼券档位分组（按 voucherTierId 聚合）。
    /// </summary>
    public sealed class GiftVoucherGroup
    {
        /// <summary>
        /// 档位 ID。
        /// </summary>
        public int VoucherTierId;

        /// <summary>
        /// 面值原始字符串（美元，如 "4.99"），用于构造服务端请求体。
        /// </summary>
        public string FaceValue;

        /// <summary>
        /// 面值毫分（FaceValue 解析后 ×1000，向下取整），用于整型贪心计算，避免浮点误差。
        /// </summary>
        public long FaceValueMilliCents;

        /// <summary>
        /// 持有数量。
        /// </summary>
        public int Quantity;

        /// <summary>
        /// 券激活码列表。
        /// </summary>
        public List<string> VoucherCodes;
    }

    /// <summary>
    /// 单种金币余额。
    /// </summary>
    public sealed class CoinBalance
    {
        /// <summary>
        /// 金币类型 ID。
        /// </summary>
        public int CoinId;

        /// <summary>
        /// 单个面值原始字符串（美元）。
        /// </summary>
        public string FaceValue;

        /// <summary>
        /// 单个面值毫分（FaceValue 解析后 ×1000，向下取整），用于整型贪心计算，避免浮点误差。
        /// </summary>
        public long FaceValueMilliCents;

        /// <summary>
        /// 持有数量。
        /// </summary>
        public int Quantity;
    }

    /// <summary>
    /// GiftVoucherDeduct 请求的抵扣明细（含礼券与金币用量，序列化后放入请求体）。
    /// </summary>
    public sealed class DeductDetail
    {
        /// <summary>
        /// 礼券使用明细列表。
        /// </summary>
        public List<DeductVoucherItem> VoucherUsed;

        /// <summary>
        /// 金币使用明细列表。
        /// </summary>
        public List<DeductCoinItem> CoinUsed;
    }

    /// <summary>
    /// 本地存档的进行中代金券抵扣数据，用于跨会话补单。
    /// </summary>
    public sealed class VoucherPendingDeduct
    {
        /// <summary>
        /// 商品配置表行 ID。
        /// </summary>
        public long TableId;

        /// <summary>
        /// 用户 UID（string 形式，与 IAPStoreBase.m_GameUID 类型一致）。
        /// </summary>
        public string Uid = string.Empty;

        /// <summary>
        /// 服务端返回的抵扣订单 ID。
        /// </summary>
        public string OrderId;

        /// <summary>
        /// 透传的业务自定义数据。
        /// </summary>
        public string CustomString;
    }

    /// <summary>
    /// 持有礼券档位与赠币余额两份字典，支持全量覆盖与增量补丁两种更新模式。
    /// 提供贪心算法计算最优抵扣方案。
    /// </summary>
    public sealed class VoucherBalanceSnapshot
    {
        private readonly Dictionary<int, GiftVoucherGroup> m_VoucherGroups = new Dictionary<int, GiftVoucherGroup>();
        private readonly Dictionary<int, CoinBalance> m_CoinBalances = new Dictionary<int, CoinBalance>();

        /// <summary>
        /// 清空本地余额快照。
        /// </summary>
        public void Clear()
        {
            m_VoucherGroups.Clear();
            m_CoinBalances.Clear();
        }

        /// <summary>
        /// 全量覆盖礼券档位列表。
        /// </summary>
        /// <param name="groups">礼券档位列表。</param>
        public void SetVoucherGroups(IEnumerable<GiftVoucherGroup> groups)
        {
            if (groups == null) return;
            foreach (var g in groups)
            {
                if (g != null)
                    m_VoucherGroups[g.VoucherTierId] = g;
            }
        }

        /// <summary>
        /// 全量覆盖金币余额列表。
        /// </summary>
        /// <param name="balances">金币余额列表。</param>
        public void SetCoinBalances(IEnumerable<CoinBalance> balances)
        {
            if (balances == null) return;
            foreach (var b in balances)
            {
                if (b != null)
                    m_CoinBalances[b.CoinId] = b;
            }
        }

        /// <summary>
        /// 增量补丁更新：仅覆盖已存在的档位/币种余额。
        /// 由抵扣回调触发，不新增条目，防止快照膨胀。
        /// </summary>
        /// <param name="groups">需要更新的礼券档位列表（可为 null）。</param>
        /// <param name="coinBalances">需要更新的金币余额列表（可为 null）。</param>
        public void Patch(IEnumerable<GiftVoucherGroup> groups, IEnumerable<CoinBalance> coinBalances)
        {
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    if (g != null && m_VoucherGroups.ContainsKey(g.VoucherTierId))
                        m_VoucherGroups[g.VoucherTierId] = g;
                }
            }
            if (coinBalances != null)
            {
                foreach (var b in coinBalances)
                {
                    if (b != null && m_CoinBalances.ContainsKey(b.CoinId))
                        m_CoinBalances[b.CoinId] = b;
                }
            }
        }

        /// <summary>
        /// 获取指定金币类型当前持有数量。
        /// </summary>
        /// <param name="coinId">金币类型 ID。</param>
        /// <returns>持有数量；无记录时返回 0。</returns>
        public int GetCoinQuantity(int coinId)
        {
            return m_CoinBalances.TryGetValue(coinId, out var b) ? b.Quantity : 0;
        }

        /// <summary>
        /// 获取指定档位礼券持有数量。
        /// </summary>
        /// <param name="voucherTierId">档位 ID。</param>
        /// <returns>持有数量；无记录时返回 0。</returns>
        public int GetVoucherQuantity(int voucherTierId)
        {
            return m_VoucherGroups.TryGetValue(voucherTierId, out var g) ? g.Quantity : 0;
        }

        /// <summary>
        /// 获取指定档位的券激活码列表。
        /// </summary>
        /// <param name="voucherTierId">档位 ID。</param>
        /// <returns>激活码列表；无记录时返回空列表。</returns>
        public List<string> GetVoucherCodes(int voucherTierId)
        {
            return m_VoucherGroups.TryGetValue(voucherTierId, out var g) ? g.VoucherCodes ?? new List<string>() : new List<string>();
        }

        /// <summary>
        /// 根据激活码反查所属档位 ID。
        /// </summary>
        /// <param name="voucherCode">激活码字符串。</param>
        /// <returns>对应的档位 ID；未找到时返回 0。</returns>
        public int FindTierIdByVoucherCode(string voucherCode)
        {
            if (string.IsNullOrEmpty(voucherCode)) return 0;
            foreach (var kv in m_VoucherGroups)
            {
                if (kv.Value?.VoucherCodes == null) continue;
                if (kv.Value.VoucherCodes.Contains(voucherCode))
                    return kv.Key;
            }
            return 0;
        }

        /// <summary>
        /// 获取指定档位的面值字符串（美元）。
        /// </summary>
        /// <param name="voucherTierId">档位 ID。</param>
        /// <returns>面值字符串；无记录时返回空字符串。</returns>
        public string GetVoucherFaceValue(int voucherTierId)
        {
            return m_VoucherGroups.TryGetValue(voucherTierId, out var g) ? g.FaceValue ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// 根据商品价格（毫分）贪心计算最优扣费方案。
        /// 优先礼券→赠币→现金，按面值降序贪心消耗，不拆分单张。
        /// 剩余量不超过容差（99 毫分 ≈ $0.10）时视为完全覆盖，避免浮点误差导致判断失误。
        /// </summary>
        /// <param name="priceMilliCents">商品价格，单位毫分（price_usd × 1000），避免浮点误差。</param>
        /// <returns>推荐扣费方案，含各资产明细。</returns>
        public DeductPlan CalcDeductPlan(long priceMilliCents)
        {
            const long c_Tolerance = 99L;

            if (priceMilliCents <= 0)
                return new DeductPlan(VoucherDeductMode.Cash, 0, 0, 0);

            long remaining = priceMilliCents;
            long voucherDeducted = 0L;
            int totalCoinQty = 0;
            var voucherUsed = new List<DeductVoucherItem>();
            var coinUsed = new List<DeductCoinItem>();

            // 阶段一：贪心消耗礼券（按面值降序，不拆分单张）
            var sortedVouchers = new List<GiftVoucherGroup>(m_VoucherGroups.Values);
            sortedVouchers.Sort((a, b) => b.FaceValueMilliCents.CompareTo(a.FaceValueMilliCents));
            foreach (var group in sortedVouchers)
            {
                if (remaining <= 0) break;
                if (group.Quantity <= 0 || group.FaceValueMilliCents <= 0) continue;
                if (group.FaceValueMilliCents > remaining) continue;
                int use = (int)Math.Min(remaining / group.FaceValueMilliCents, group.Quantity);
                if (use <= 0) continue;
                voucherUsed.Add(new DeductVoucherItem { VoucherTierId = group.VoucherTierId, FaceValue = group.FaceValue, Quantity = use });
                long deducted = use * group.FaceValueMilliCents;
                remaining -= deducted;
                voucherDeducted += deducted;
            }

            if (remaining <= c_Tolerance)
                return new DeductPlan(VoucherDeductMode.Voucher, voucherDeducted, 0, 0, voucherUsed, coinUsed);

            // 阶段二：赠币补差（按面值降序，不拆分单枚）
            var sortedCoins = new List<CoinBalance>(m_CoinBalances.Values);
            sortedCoins.Sort((a, b) => b.FaceValueMilliCents.CompareTo(a.FaceValueMilliCents));
            foreach (var coin in sortedCoins)
            {
                if (remaining <= 0) break;
                if (coin.Quantity <= 0 || coin.FaceValueMilliCents <= 0) continue;
                if (coin.FaceValueMilliCents > remaining) continue;
                int use = (int)Math.Min(remaining / coin.FaceValueMilliCents, coin.Quantity);
                if (use <= 0) continue;
                coinUsed.Add(new DeductCoinItem { CoinId = coin.CoinId, Quantity = use });
                remaining -= use * coin.FaceValueMilliCents;
                totalCoinQty += use;
            }

            if (remaining <= c_Tolerance)
            {
                bool hasVoucher = voucherUsed.Count > 0;
                bool hasCoin = coinUsed.Count > 0;
                var mode = hasVoucher && hasCoin ? VoucherDeductMode.VoucherCoin
                         : hasCoin ? VoucherDeductMode.Coin
                         : VoucherDeductMode.Voucher;
                return new DeductPlan(mode, voucherDeducted, totalCoinQty, 0, voucherUsed, coinUsed);
            }

            // 阶段三：礼券/赠币不足，返回 Cash 模式（不携带部分消耗列表，避免上层误用）
            return new DeductPlan(VoucherDeductMode.Cash, 0, 0, remaining);
        }

        /// <summary>
        /// 将面值字符串（美元，如 "4.99"）解析为毫分整型（×1000，向下取整）。
        /// </summary>
        /// <param name="faceValue">面值字符串。</param>
        /// <returns>毫分整型；无法解析时返回 0。</returns>
        public static long ParseFaceValueToMilliCents(string faceValue)
        {
            if (string.IsNullOrEmpty(faceValue)) return 0L;
            if (decimal.TryParse(faceValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return (long)Math.Floor((double)(d * 1000));
            return 0L;
        }
    }
}
