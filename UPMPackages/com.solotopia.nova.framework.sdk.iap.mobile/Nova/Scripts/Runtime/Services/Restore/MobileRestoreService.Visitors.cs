/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileRestoreService.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   MobileRestoreService 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobileRestoreService
    {
        /// <summary>
        /// 是否正在进行 Restore 流程，防止并发重复发起。
        /// </summary>
        private bool m_IsInRestore;

        /// <summary>
        /// 商店、商品或平台已有购买尚未就绪时收到过权益刷新请求；依赖就绪后补跑一次。
        /// </summary>
        private bool m_PendingEntitlementRefresh;

        /// <summary>
        /// 是否正在通过 Unity IAP 拉取平台已有购买；拉取完成前不启动权益汇总，避免先生成缺少票据的恢复记录。
        /// </summary>
        private bool m_IsExistingPurchasesFetchInProgress;

        /// <summary>
        /// Restore 操作完成信号，所有验单结果收集完成后触发。
        /// </summary>
        private UniTaskCompletionSource<IReadOnlyList<IAPResult>> m_RestoreTcs;

        /// <summary>
        /// Restore 期间收集的已恢复订阅结果列表。
        /// </summary>
        private List<IAPResult> m_SubscriptionResults;

        /// <summary>
        /// Restore 期间收集的已恢复非消耗品结果列表。
        /// </summary>
        private List<IAPResult> m_NonConsumeResults;

        /// <summary>
        /// Restore 完成协调器，追踪订阅和非消耗品验单是否均已完成。
        /// </summary>
        private readonly MobileRestoreCoordinator m_RestoreCoordinator = new MobileRestoreCoordinator();
    }
}
