/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPMobileRequest.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   Google Play / Apple App Store 移动端渠道支付请求
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// Google Play / Apple App Store 移动端渠道支付请求。
    /// 继承 IAPRequest，使用平台官方 IAP SDK 完成购买；
    /// 无需额外字段，渠道所需的 ProductId 由 store 层按 TableId 从配置表查取。
    /// </summary>
    public sealed class IAPMobileRequest : IAPRequest
    {
        /// <summary>
        /// 当前请求对应的渠道类型，固定为 Mobile。
        /// </summary>
        public override IAPStoreType StoreType => IAPStoreType.Mobile;
    }
}
