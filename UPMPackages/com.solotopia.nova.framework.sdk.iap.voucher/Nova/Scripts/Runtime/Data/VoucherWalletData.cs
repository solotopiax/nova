/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherWalletData.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 内部钱包资产、冻结扣减草案与金额解析
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// 包含精确券码的内部礼券资产。
    /// </summary>
    internal sealed class VoucherAssetData
    {
        /// <summary>
        /// 礼券档位 ID。
        /// </summary>
        internal int VoucherTierId { get; }

        /// <summary>
        /// 原始面值字符串。
        /// </summary>
        internal string FaceValue { get; }

        /// <summary>
        /// 单张面值，单位为 mills。
        /// </summary>
        internal long FaceValueMills { get; }

        /// <summary>
        /// 稳定排序、去重后的券码只读列表。
        /// </summary>
        internal IReadOnlyList<string> VoucherCodes { get; }

        /// <summary>
        /// 当前有效券数量。
        /// </summary>
        internal int Quantity => VoucherCodes.Count;

        /// <summary>
        /// 创建内部礼券资产，并对券码去空、去重和稳定排序。
        /// </summary>
        /// <param name="voucherTierId">礼券档位 ID。</param>
        /// <param name="faceValue">原始面值字符串。</param>
        /// <param name="faceValueMills">面值 mills。</param>
        /// <param name="voucherCodes">精确券码集合。</param>
        internal VoucherAssetData(int voucherTierId, string faceValue, long faceValueMills, IEnumerable<string> voucherCodes)
        {
            VoucherTierId = voucherTierId;
            FaceValue = faceValue ?? string.Empty;
            FaceValueMills = faceValueMills;
            string[] codes = voucherCodes == null ? Array.Empty<string>() : voucherCodes.Where(code => !string.IsNullOrEmpty(code)).Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToArray();
            VoucherCodes = new ReadOnlyCollection<string>(codes);
        }
    }

    /// <summary>
    /// 内部赠币资产。
    /// </summary>
    internal sealed class CoinAssetData
    {
        /// <summary>
        /// 赠币类型 ID。
        /// </summary>
        internal int CoinId { get; }

        /// <summary>
        /// 原始面值字符串。
        /// </summary>
        internal string FaceValue { get; }

        /// <summary>
        /// 单枚面值，单位为 mills。
        /// </summary>
        internal long FaceValueMills { get; }

        /// <summary>
        /// 当前有效数量。
        /// </summary>
        internal int Quantity { get; }

        /// <summary>
        /// 创建内部赠币资产。
        /// </summary>
        /// <param name="coinId">赠币类型 ID。</param>
        /// <param name="faceValue">原始面值字符串。</param>
        /// <param name="faceValueMills">面值 mills。</param>
        /// <param name="quantity">当前数量。</param>
        internal CoinAssetData(int coinId, string faceValue, long faceValueMills, int quantity)
        {
            CoinId = coinId;
            FaceValue = faceValue ?? string.Empty;
            FaceValueMills = faceValueMills;
            Quantity = Math.Max(0, quantity);
        }
    }

    /// <summary>
    /// 冻结草案中的单种赠币用量。
    /// </summary>
    internal sealed class CoinUsageData
    {
        /// <summary>
        /// 赠币类型 ID。
        /// </summary>
        internal int CoinId { get; }

        /// <summary>
        /// 使用数量。
        /// </summary>
        internal int Quantity { get; }

        /// <summary>
        /// 创建冻结赠币用量。
        /// </summary>
        /// <param name="coinId">赠币类型 ID。</param>
        /// <param name="quantity">使用数量。</param>
        internal CoinUsageData(int coinId, int quantity)
        {
            CoinId = coinId;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// 报价内部冻结的精确扣减草案。
    /// </summary>
    internal sealed class VoucherFrozenSpend
    {
        /// <summary>
        /// 不包含任何扣减明细的共享冻结草案。
        /// </summary>
        private static readonly VoucherFrozenSpend s_Empty = new VoucherFrozenSpend(Array.Empty<string>(), Array.Empty<CoinUsageData>());

        /// <summary>
        /// 不包含任何扣减明细的共享冻结草案。
        /// </summary>
        internal static VoucherFrozenSpend Empty => s_Empty;

        /// <summary>
        /// 稳定排序后的精确券码。
        /// </summary>
        internal IReadOnlyList<string> VoucherCodes { get; }

        /// <summary>
        /// 稳定排序后的赠币用量。
        /// </summary>
        internal IReadOnlyList<CoinUsageData> CoinUsages { get; }

        /// <summary>
        /// 创建不可变冻结草案，并防御性复制集合。
        /// </summary>
        /// <param name="voucherCodes">精确券码。</param>
        /// <param name="coinUsages">赠币用量。</param>
        internal VoucherFrozenSpend(IEnumerable<string> voucherCodes, IEnumerable<CoinUsageData> coinUsages)
        {
            string[] codes = voucherCodes?.ToArray() ?? Array.Empty<string>();
            CoinUsageData[] usages = coinUsages?.ToArray() ?? Array.Empty<CoinUsageData>();
            VoucherCodes = new ReadOnlyCollection<string>(codes);
            CoinUsages = new ReadOnlyCollection<CoinUsageData>(usages);
        }
    }

    /// <summary>
    /// 绑定账号和版本、包含精确资产的内部不可变钱包。
    /// </summary>
    internal sealed class VoucherWalletData
    {
        /// <summary>
        /// 钱包所属账号。
        /// </summary>
        internal string AccountId { get; }

        /// <summary>
        /// 钱包所属账号 generation。
        /// </summary>
        internal long AccountGeneration { get; }

        /// <summary>
        /// 账号作用域内的钱包版本。
        /// </summary>
        internal long Version { get; }

        /// <summary>
        /// 刷新时间，Unix 毫秒时间戳。
        /// </summary>
        internal long RefreshedAtUnixTimeMs { get; }

        /// <summary>
        /// 是否已经成功刷新。
        /// </summary>
        internal bool IsReady { get; }

        /// <summary>
        /// 包含精确券码的礼券资产。
        /// </summary>
        internal IReadOnlyList<VoucherAssetData> Vouchers { get; }

        /// <summary>
        /// 赠币资产。
        /// </summary>
        internal IReadOnlyList<CoinAssetData> Coins { get; }

        /// <summary>
        /// 创建内部不可变钱包。
        /// </summary>
        /// <param name="accountId">钱包所属账号 ID。</param>
        /// <param name="accountGeneration">钱包所属账号 generation。</param>
        /// <param name="version">账号作用域内的钱包版本。</param>
        /// <param name="refreshedAtUnixTimeMs">刷新完成时的 Unix 毫秒时间戳。</param>
        /// <param name="isReady">钱包是否已经成功刷新。</param>
        /// <param name="vouchers">包含精确券码的礼券资产。</param>
        /// <param name="coins">赠币资产。</param>
        private VoucherWalletData(string accountId, long accountGeneration, long version, long refreshedAtUnixTimeMs, bool isReady, IEnumerable<VoucherAssetData> vouchers, IEnumerable<CoinAssetData> coins)
        {
            AccountId = accountId ?? string.Empty;
            AccountGeneration = accountGeneration;
            Version = version;
            RefreshedAtUnixTimeMs = refreshedAtUnixTimeMs;
            IsReady = isReady;
            Vouchers = new ReadOnlyCollection<VoucherAssetData>(vouchers?.ToArray() ?? Array.Empty<VoucherAssetData>());
            Coins = new ReadOnlyCollection<CoinAssetData>(coins?.ToArray() ?? Array.Empty<CoinAssetData>());
        }

        /// <summary>
        /// 创建未就绪钱包，用于账号切换后立即隐藏旧余额。
        /// </summary>
        /// <param name="accountId">新账号 ID。</param>
        /// <param name="generation">新账号 generation。</param>
        /// <returns>不包含任何资产的内部钱包。</returns>
        internal static VoucherWalletData NotReady(string accountId, long generation)
        {
            return new VoucherWalletData(accountId, generation, 0, 0, false, null, null);
        }

        /// <summary>
        /// 创建已就绪内部钱包。
        /// </summary>
        /// <param name="accountId">账号 ID。</param>
        /// <param name="generation">账号 generation。</param>
        /// <param name="version">钱包版本。</param>
        /// <param name="refreshedAtUnixTimeMs">刷新时间。</param>
        /// <param name="vouchers">礼券资产。</param>
        /// <param name="coins">赠币资产。</param>
        /// <returns>不可变内部钱包。</returns>
        internal static VoucherWalletData Ready(string accountId, long generation, long version, long refreshedAtUnixTimeMs, IEnumerable<VoucherAssetData> vouchers, IEnumerable<CoinAssetData> coins)
        {
            return new VoucherWalletData(accountId, generation, version, refreshedAtUnixTimeMs, true, vouchers, coins);
        }

        /// <summary>
        /// 生成不含精确券码的公共钱包快照。
        /// </summary>
        /// <returns>只包含聚合余额的不可变快照。</returns>
        internal VoucherWalletSnapshot ToPublicSnapshot()
        {
            if (!IsReady)
                return VoucherWalletSnapshot.CreateNotReady();

            VoucherWalletBalance[] vouchers = Vouchers.Select(item => new VoucherWalletBalance(item.VoucherTierId, item.FaceValue, item.FaceValueMills, item.Quantity)).ToArray();
            VoucherCoinBalance[] coins = Coins.Select(item => new VoucherCoinBalance(item.CoinId, item.FaceValue, item.FaceValueMills, item.Quantity)).ToArray();
            return new VoucherWalletSnapshot(true, Version, RefreshedAtUnixTimeMs, vouchers, coins);
        }
    }

    /// <summary>
    /// Voucher 金额字符串与 mills 的精确转换工具。
    /// </summary>
    internal static class VoucherMoney
    {
        /// <summary>
        /// 把美元十进制字符串精确转换为 mills；超过三位小数、非正数或溢出时返回 0。
        /// </summary>
        /// <param name="value">InvariantCulture 十进制金额字符串。</param>
        /// <returns>精确 mills；无效时为 0。</returns>
        internal static long ParseMills(string value)
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) || amount <= 0)
            {
                return 0;
            }

            decimal scaled;
            try
            {
                scaled = amount * 1000m;
            }
            catch (OverflowException)
            {
                return 0;
            }

            if (scaled != decimal.Truncate(scaled) || scaled > long.MaxValue)
                return 0;
            return (long)scaled;
        }
    }
}
