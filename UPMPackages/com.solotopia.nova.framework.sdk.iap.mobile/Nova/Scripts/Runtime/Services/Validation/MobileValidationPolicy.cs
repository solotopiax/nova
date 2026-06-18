/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileValidationPolicy.cs
 * author:    yingzheng
 * created:   2026/6/16
 * descrip:   Mobile IAP 验单策略辅助
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// Mobile IAP 验单策略辅助。
    /// </summary>
    internal static class MobileValidationPolicy
    {
        /// <summary>
        /// Apple 验单协议必须携带 order_id；缺失时本地订单不可恢复，应删除本地待验记录。
        /// </summary>
        /// <param name="orderId">Apple transaction id / order_id。</param>
        /// <returns>缺少 order_id 时返回 true。</returns>
        internal static bool ShouldDropAppleOrderMissingCredential(string orderId)
        {
            return string.IsNullOrWhiteSpace(orderId);
        }

        /// <summary>
        /// 订阅 Restore 事件只收集服务端已确认有效的订阅结果。
        /// </summary>
        /// <param name="status">服务端验单订单状态整数值。</param>
        /// <param name="isSubscription">当前订单是否为订阅。</param>
        /// <returns>需要进入 SubscriptionRestored 结果列表时返回 true。</returns>
        internal static bool ShouldCollectSubscriptionRestored(int status, bool isSubscription)
        {
            if (!isSubscription)
            {
                return false;
            }

            return status == (int)PbNetMobileVerifyOrderStatus.Verified ||
                   status == (int)PbNetMobileVerifyOrderStatus.Delivered;
        }
    }
}
