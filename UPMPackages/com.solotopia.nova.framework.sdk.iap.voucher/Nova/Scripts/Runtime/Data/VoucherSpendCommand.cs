/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherSpendCommand.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   可持久化重放的不可变 Voucher 扣减命令
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// 一次 Voucher 交易的完整不可变业务命令；重试不得重新计算其任何字段。
    /// </summary>
    internal sealed class VoucherSpendCommand
    {
        /// <summary>
        /// 当前命令 schema 版本。
        /// </summary>
        internal int SchemaVersion { get; }

        /// <summary>
        /// 命令所属账号。
        /// </summary>
        internal string AccountId { get; }

        /// <summary>
        /// 客户端生成并由服务端用于幂等的游戏订单号。
        /// </summary>
        internal string GameOrderId { get; }

        /// <summary>
        /// 商品配置表行 ID。
        /// </summary>
        internal long TableId { get; }

        /// <summary>
        /// 冻结的精确券码。
        /// </summary>
        internal IReadOnlyList<string> VoucherCodes { get; }

        /// <summary>
        /// 冻结的赠币用量。
        /// </summary>
        internal IReadOnlyList<CoinUsageData> CoinUsages { get; }

        /// <summary>
        /// 下单国家或地区代码。
        /// </summary>
        internal string Country { get; }

        /// <summary>
        /// 调用方自定义数据。
        /// </summary>
        internal string CustomData { get; }

        /// <summary>
        /// 命令创建时间，Unix 毫秒时间戳。
        /// </summary>
        internal long CreatedAtUnixTimeMs { get; }

        /// <summary>
        /// 创建完整不可变扣减命令，并防御性复制所有集合。
        /// </summary>
        /// <param name="schemaVersion">命令结构版本。</param>
        /// <param name="accountId">命令所属账号 ID。</param>
        /// <param name="gameOrderId">客户端生成的稳定游戏订单号。</param>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="voucherCodes">冻结的精确券码。</param>
        /// <param name="coinUsages">冻结的赠币用量。</param>
        /// <param name="country">下单国家或地区代码。</param>
        /// <param name="customData">调用方自定义数据。</param>
        /// <param name="createdAtUnixTimeMs">命令创建时的 Unix 毫秒时间戳。</param>
        /// <exception cref="ArgumentException">游戏订单号为空时抛出。</exception>
        internal VoucherSpendCommand(int schemaVersion, string accountId, string gameOrderId, long tableId, IEnumerable<string> voucherCodes, IEnumerable<CoinUsageData> coinUsages, string country, string customData, long createdAtUnixTimeMs)
        {
            if (string.IsNullOrEmpty(gameOrderId))
                throw new ArgumentException("Voucher game_order_id 不能为空。", nameof(gameOrderId));

            SchemaVersion = schemaVersion;
            AccountId = accountId ?? string.Empty;
            GameOrderId = gameOrderId;
            TableId = tableId;
            VoucherCodes = new ReadOnlyCollection<string>(voucherCodes?.ToArray() ?? Array.Empty<string>());
            CoinUsages = new ReadOnlyCollection<CoinUsageData>(coinUsages?.Select(item => new CoinUsageData(item.CoinId, item.Quantity)).ToArray() ?? Array.Empty<CoinUsageData>());
            Country = country ?? string.Empty;
            CustomData = customData ?? string.Empty;
            CreatedAtUnixTimeMs = createdAtUnixTimeMs;
        }

        /// <summary>
        /// 判断两个同订单命令的全部业务 payload 是否一致。
        /// </summary>
        /// <param name="other">待比较命令。</param>
        /// <returns>所有持久化业务字段完全相同时返回 true。</returns>
        internal bool PayloadEquals(VoucherSpendCommand other)
        {
            if (other == null || SchemaVersion != other.SchemaVersion || !string.Equals(AccountId, other.AccountId, StringComparison.Ordinal) || !string.Equals(GameOrderId, other.GameOrderId, StringComparison.Ordinal) || TableId != other.TableId || !string.Equals(Country, other.Country, StringComparison.Ordinal) || !string.Equals(CustomData, other.CustomData, StringComparison.Ordinal) || CreatedAtUnixTimeMs != other.CreatedAtUnixTimeMs || !VoucherCodes.SequenceEqual(other.VoucherCodes, StringComparer.Ordinal) || CoinUsages.Count != other.CoinUsages.Count)
            {
                return false;
            }

            for (int i = 0; i < CoinUsages.Count; i++)
            {
                if (CoinUsages[i].CoinId != other.CoinUsages[i].CoinId || CoinUsages[i].Quantity != other.CoinUsages[i].Quantity)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 将不可变命令转换为与 protobuf 解耦的持久化记录。
        /// </summary>
        /// <param name="state">记录初始状态。</param>
        /// <returns>可由 PersistManager 序列化的记录。</returns>
        internal VoucherTransactionRecord ToRecord(VoucherTransactionState state)
        {
            return new VoucherTransactionRecord
            {
                SchemaVersion = SchemaVersion,
                AccountId = AccountId,
                GameOrderId = GameOrderId,
                TableId = TableId,
                VoucherCodes = new List<string>(VoucherCodes),
                CoinUsages = CoinUsages.Select(item => new VoucherCoinUsageRecord { CoinId = item.CoinId, Quantity = item.Quantity }).ToList(),
                Country = Country,
                CustomData = CustomData,
                CreatedAtUnixTimeMs = CreatedAtUnixTimeMs,
                State = state,
            };
        }

        /// <summary>
        /// 从持久化记录重建不可变命令。
        /// </summary>
        /// <param name="record">已通过基础结构校验的记录。</param>
        /// <returns>供重放使用的原始命令。</returns>
        internal static VoucherSpendCommand FromRecord(VoucherTransactionRecord record)
        {
            return new VoucherSpendCommand(record.SchemaVersion, record.AccountId, record.GameOrderId, record.TableId, record.VoucherCodes, record.CoinUsages?.Select(item => new CoinUsageData(item.CoinId, item.Quantity)), record.Country, record.CustomData, record.CreatedAtUnixTimeMs);
        }
    }
}
