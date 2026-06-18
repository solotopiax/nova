/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherCoinUsage.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   代金券支付请求中单种金币的用量描述
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 代金券支付请求中单种金币的消耗用量。
    /// 用于 IAPVoucherRequest.CoinUsages 列表，描述本次支付中
    /// 某一类型金币参与抵扣的数量。
    /// </summary>
    public sealed class VoucherCoinUsage
    {
        /// <summary>
        /// 金币类型 ID，对应配置表中的金币种类主键。
        /// </summary>
        public int CoinId;

        /// <summary>
        /// 本次支付中消耗该类型金币的数量，必须大于 0。
        /// </summary>
        public int Quantity;
    }
}
