/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileSubscriptionService.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   MobileSubscriptionService 字段与属性
 ***************************************************************/

using System.Threading;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobileSubscriptionService
    {
        /// <summary>
        /// 服务容器，持有共享外部依赖与其他服务引用。
        /// </summary>
        private readonly MobileServiceHub m_Hub;

        /// <summary>
        /// 按 tableId 存储的倒计时取消令牌源；切换 UID 或 Dispose 时取消。
        /// </summary>
        private CancellationTokenSource m_CountdownCts;
    }
}
