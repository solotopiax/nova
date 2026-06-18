/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DeductItems.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   代金券/金币抵扣明细数据结构
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 抵扣方案中单档位礼券的使用明细。
    /// </summary>
    public sealed class DeductVoucherItem
    {
        /// <summary>
        /// 档位 ID。
        /// </summary>
        public int VoucherTierId;

        /// <summary>
        /// 面值字符串（美元，如 "4.99"）。
        /// </summary>
        public string FaceValue;

        /// <summary>
        /// 使用数量。
        /// </summary>
        public int Quantity;
    }

    /// <summary>
    /// 抵扣方案中单种金币的使用明细。
    /// </summary>
    public sealed class DeductCoinItem
    {
        /// <summary>
        /// 金币类型 ID。
        /// </summary>
        public int CoinId;

        /// <summary>
        /// 使用数量。
        /// </summary>
        public int Quantity;
    }
}
