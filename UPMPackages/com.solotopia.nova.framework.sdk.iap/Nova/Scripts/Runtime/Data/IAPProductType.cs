/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductType.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   IAP 商品类型枚举，从 Mobile 包上移到核心 IAP 包以供商品表共用
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 商品类型枚举，与 Unity IAP ProductType 对齐。
    /// 从 MobileStoreConfig 上移到核心 IAP 包，供 IAPPluginConfig 与所有 store 共用，
    /// 避免 Voucher / ThirdPay 包反向引用 Mobile 包。
    /// </summary>
    public enum IAPProductType
    {
        /// <summary>
        /// 消耗品：购买后需消耗才可再次购买（如金币、道具）。
        /// </summary>
        Consumable = 0,

        /// <summary>
        /// 非消耗品：一次性永久购买（如去广告、永久解锁）。
        /// </summary>
        NonConsumable = 1,

        /// <summary>
        /// 订阅商品：周期性自动续费（如月卡、年卡）。
        /// </summary>
        Subscription = 2,
    }
}
