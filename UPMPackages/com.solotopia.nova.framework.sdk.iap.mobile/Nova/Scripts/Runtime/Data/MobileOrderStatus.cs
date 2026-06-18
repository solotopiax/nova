/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileOrderStatus.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   移动端订单状态枚举
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 移动端官方内购订单生命周期状态。
    /// 只保留驱动补单与本地清理所需的持久化状态。
    /// </summary>
    public enum MobileOrderStatus
    {
        /// <summary>
        /// 已发起平台购买，等待平台回调。
        /// </summary>
        Purchasing = 0,

        /// <summary>
        /// 平台回调成功，待服务端验单。
        /// </summary>
        PendingValidate = 1,

        /// <summary>
        /// 验单网络或 HTTP 失败，保留存档等待下次补单重试。
        /// </summary>
        ValidateFailed = 2,

        /// <summary>
        /// 平台本地支付失败，终态清理标记；启动扫描时直接删除，不进入验单。
        /// </summary>
        LocalPayFailed = 3,
    }
}
