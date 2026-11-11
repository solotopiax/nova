/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPMobileSubscriptionCapable.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   具备移动端订阅与非消耗品查询能力的 store 扩展接口
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 具备订阅商品到期时间查询与非消耗品持有判断能力的移动端 store 扩展接口。
    /// 实现此接口的商店提供 Google Play / iOS App Store 的本地订阅到期与非消耗品持有查询。
    /// 通过 IAPPlugin.TryGetCapability<IIAPMobileSubscriptionCapable> 取用。
    /// </summary>
    public interface IIAPMobileSubscriptionCapable : IIAPCapable
    {
        /// <summary>
        /// 获取指定订阅商品的到期时间戳（毫秒）。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID。</param>
        /// <returns>订阅到期的 Unix 时间戳（毫秒）；未订阅时返回 0。</returns>
        long GetSubscriptionExpireTime(long tableId);

        /// <summary>
        /// 查询当前账号存档中的非消耗品持有标记，不刷新平台或服务端权益。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>存档标记已持有时返回 true，缺少标记时返回 false；不表示有待发货订单。</returns>
        bool HasNonConsumeProduct(long tableId);

        /// <summary>
        /// 判断指定订阅商品当前是否在订阅有效期内。
        /// 由 store 从持久化层读取上次验单写入的到期时间戳，与当前 UTC 时间比较；
        /// 未订阅、已过期或无存档时均返回 false。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID。</param>
        /// <returns>订阅有效期内返回 true，否则返回 false。</returns>
        bool InSubscriptionPeriod(long tableId);
    }
}
