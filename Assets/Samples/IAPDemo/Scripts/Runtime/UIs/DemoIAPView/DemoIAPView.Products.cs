/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPView.Products.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   三个商店 Panel 共用的演示商品目录
 ***************************************************************/

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 集中维护三个商店 Panel 使用的演示商品 ID 与分组文案。
    /// </summary>
    internal static class DemoIAPProductCatalog
    {
        /// <summary>
        /// 全部演示商品表行 ID。
        /// </summary>
        internal static readonly long[] AllProductIds = { 1L, 2L, 5L, 3L, 4L };

        /// <summary>
        /// ThirdPay Demo 展示的消耗品商品表行 ID。
        /// </summary>
        internal static readonly long[] ThirdPayProductIds = { 1L, 2L };

        /// <summary>
        /// ThirdPay 奖励参数测试支付使用的商品表行 ID。
        /// </summary>
        internal const long ThirdPayRewardTableId = 1L;

        /// <summary>
        /// ThirdPay 首充奖励测试物品 ID。
        /// </summary>
        internal const long ThirdPayFirstRechargeRewardItemId = 1001L;

        /// <summary>
        /// ThirdPay 首充奖励测试数量。
        /// </summary>
        internal const int ThirdPayFirstRechargeRewardQuantity = 1;

        /// <summary>
        /// ThirdPay 额外赠送测试物品 ID。
        /// </summary>
        internal const long ThirdPayExtraBonusRewardItemId = 1002L;

        /// <summary>
        /// ThirdPay 额外赠送测试数量。
        /// </summary>
        internal const int ThirdPayExtraBonusRewardQuantity = 2;

        /// <summary>
        /// 获取商品所属的演示分组。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>普通、非消耗或订阅。</returns>
        internal static string GetGroupLabel(long tableId)
        {
            if (tableId == 5L)
            {
                return "非消耗";
            }

            return tableId == 3L || tableId == 4L ? "订阅" : "普通";
        }
    }
}
