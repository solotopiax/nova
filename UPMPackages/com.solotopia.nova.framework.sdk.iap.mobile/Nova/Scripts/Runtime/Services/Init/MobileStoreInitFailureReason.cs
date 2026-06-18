/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStoreInitFailureReason.cs
 * author:    yingzheng
 * created:   2026/6/11
 * descrip:   MobileStore 初始化失败原因
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// MobileStore 初始化失败原因。
    /// </summary>
    internal enum MobileStoreInitFailureReason
    {
        /// <summary>
        /// 未发生初始化失败。
        /// </summary>
        None = 0,

        /// <summary>
        /// 平台内购服务不可用的通用兜底原因。
        /// </summary>
        PurchasingUnavailable = 1,

        /// <summary>
        /// Unity IAP StoreController 创建失败。
        /// </summary>
        StoreControllerUnavailable = 2,

        /// <summary>
        /// Unity IAP Connect 调用抛出异常。
        /// </summary>
        StoreConnectException = 3,

        /// <summary>
        /// 初始化期间商店连接断开。
        /// </summary>
        StoreDisconnected = 4,

        /// <summary>
        /// 初始化流程被取消。
        /// </summary>
        InitializationCanceled = 5,
    }
}
