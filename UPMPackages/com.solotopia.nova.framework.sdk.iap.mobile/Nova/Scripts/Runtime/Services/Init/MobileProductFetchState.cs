/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileProductFetchState.cs
 * author:    yingzheng
 * created:   2026/8/11
 * descrip:   Mobile IAP 商品拉取状态
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// Unity IAP 商品拉取状态。
    /// </summary>
    internal enum MobileProductFetchState
    {
        /// <summary>
        /// 尚未发起商品信息拉取。
        /// </summary>
        None,

        /// <summary>
        /// 商品信息正在拉取中，不允许发起并发请求。
        /// </summary>
        Fetching,

        /// <summary>
        /// 商品信息已经成功拉取，无需重复请求。
        /// </summary>
        Succeeded,

        /// <summary>
        /// 商品信息拉取失败，后续商店连接回调允许重试。
        /// </summary>
        Failed,
    }
}
