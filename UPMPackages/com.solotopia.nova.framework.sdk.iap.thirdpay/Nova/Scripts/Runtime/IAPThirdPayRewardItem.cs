/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPThirdPayRewardItem.cs
 * author:    yingzheng
 * created:   2026/10/21
 * descrip:   ThirdPay 网页展示奖励物品数据
 ***************************************************************/

using System;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 表示随第三方支付页面跳转参数发送的一项奖励物品。
    /// </summary>
    public sealed class IAPThirdPayRewardItem
    {
        /// <summary>
        /// 创建一项有效的第三方支付网页奖励物品。
        /// </summary>
        /// <param name="itemId">物品 ID，必须为正数。</param>
        /// <param name="quantity">物品数量，必须为正数。</param>
        public IAPThirdPayRewardItem(long itemId, int quantity)
        {
            if (itemId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(itemId), itemId, "物品 ID 必须为正数。");
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "物品数量必须为正数。");
            }

            ItemId = itemId;
            Quantity = quantity;
        }

        /// <summary>
        /// 获取奖励物品 ID。
        /// </summary>
        public long ItemId { get; }

        /// <summary>
        /// 获取奖励物品数量。
        /// </summary>
        public int Quantity { get; }
    }
}
