/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherStorePersistData.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   按游戏订单号索引的 Voucher 交易日志存档
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// 可持久化的 Voucher 交易状态。
    /// </summary>
    internal enum VoucherTransactionState
    {
        /// <summary>
        /// 命令已经持久化但尚未取得明确服务端结果。
        /// </summary>
        Prepared = 0,

        /// <summary>
        /// 上次请求结果未知，需要使用原命令恢复。
        /// </summary>
        PendingRecovery = 1,

        /// <summary>
        /// 成功终态已经持久化，等待派发成功事件。
        /// </summary>
        SucceededPendingDispatch = 2,

        /// <summary>
        /// 拒绝终态已经持久化，等待派发失败事件。
        /// </summary>
        RejectedPendingDispatch = 3,
    }

    /// <summary>
    /// 持久化用赠币明细 DTO。
    /// </summary>
    [Serializable]
    internal sealed class VoucherCoinUsageRecord
    {
        /// <summary>
        /// 赠币类型 ID。
        /// </summary>
        public int CoinId;

        /// <summary>
        /// 使用数量。
        /// </summary>
        public int Quantity;
    }

    /// <summary>
    /// 单个订单的完整持久化命令和状态；不直接序列化 protobuf 类型。
    /// </summary>
    [Serializable]
    internal sealed class VoucherTransactionRecord
    {
        /// <summary>
        /// 命令结构版本。
        /// </summary>
        public int SchemaVersion;

        /// <summary>
        /// 命令所属账号 ID。
        /// </summary>
        public string AccountId = string.Empty;

        /// <summary>
        /// 客户端生成的稳定游戏订单号。
        /// </summary>
        public string GameOrderId = string.Empty;

        /// <summary>
        /// 商品配置表行 ID。
        /// </summary>
        public long TableId;

        /// <summary>
        /// 冻结的精确券码。
        /// </summary>
        public List<string> VoucherCodes = new List<string>();

        /// <summary>
        /// 冻结的赠币用量。
        /// </summary>
        public List<VoucherCoinUsageRecord> CoinUsages = new List<VoucherCoinUsageRecord>();

        /// <summary>
        /// 下单国家或地区代码。
        /// </summary>
        public string Country = string.Empty;

        /// <summary>
        /// 调用方自定义数据。
        /// </summary>
        public string CustomData = string.Empty;

        /// <summary>
        /// 命令创建时的 Unix 毫秒时间戳。
        /// </summary>
        public long CreatedAtUnixTimeMs;

        /// <summary>
        /// 当前交易状态。
        /// </summary>
        public VoucherTransactionState State;

        /// <summary>
        /// 终态错误码。
        /// </summary>
        public int ErrorCode;

        /// <summary>
        /// 当前错误信息。
        /// </summary>
        public string ErrorMessage = string.Empty;
    }

    /// <summary>
    /// Voucher Store 当前账号的交易日志容器。
    /// </summary>
    [Serializable]
    internal sealed class VoucherStorePersistData : IIAPStorePersistData
    {
        /// <summary>
        /// 按 game_order_id 索引的交易记录。
        /// </summary>
        public Dictionary<string, VoucherTransactionRecord> Transactions;

        /// <summary>
        /// 确保反序列化后的交易字典可用，并丢弃空订单键。
        /// </summary>
        public void EnsureInitialized()
        {
            if (Transactions == null)
                Transactions = new Dictionary<string, VoucherTransactionRecord>(StringComparer.Ordinal);
            else if (!(Transactions.Comparer is StringComparer))
                Transactions = new Dictionary<string, VoucherTransactionRecord>(Transactions, StringComparer.Ordinal);

            Transactions.Remove(string.Empty);
        }
    }
}
