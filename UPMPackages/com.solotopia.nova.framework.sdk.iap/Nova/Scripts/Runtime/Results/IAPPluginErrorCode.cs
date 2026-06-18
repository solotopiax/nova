/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPPluginErrorCode.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   IAPPlugin 路由层错误码，与 store 层错误码独立
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAPPlugin 路由层错误码。
    /// 在 store 被找到之前就已经失败时使用；ErrorCode int 值由此枚举强转。
    /// </summary>
    public enum IAPPluginErrorCode
    {
        /// <summary>
        /// 无错误（保留，未使用）。
        /// </summary>
        None = 0,

        /// <summary>
        /// 目标 store 不可用、未找到或已被禁用。
        /// </summary>
        StoreNotAvailable = 1,

        /// <summary>
        /// store 尚未完成初始化。
        /// </summary>
        StoreInitFailed = 2,

        /// <summary>
        /// 当前已有支付进行中，拒绝重入。
        /// </summary>
        AlreadyPurchasing = 3,

        /// <summary>
        /// 商品 ID 在配置表中未找到。
        /// </summary>
        ProductNotFound = 4,
    }
}
