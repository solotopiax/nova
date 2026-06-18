/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileValidationService.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   MobileValidationService 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobileValidationService
    {
        /// <summary>
        /// 待验单的 tableId 队列，防止并发多条同时请求。
        /// </summary>
        private readonly Queue<long> m_ValidateQueue = new Queue<long>();

        /// <summary>
        /// 已收到平台 PendingOrder 但尚未完成服务端验单确认的平台订单。
        /// 只保存在内存中，重启后的补单只做服务端验单，不尝试恢复 PendingOrder 引用。
        /// </summary>
        private readonly Dictionary<long, PendingOrder> m_PendingPlatformOrders = new Dictionary<long, PendingOrder>();

        /// <summary>
        /// 登录前平台回调收集到的待验订单。
        /// 未登录时不读写账号存档，也不发起验单协议；登录后补单扫描前合并到当前 UID 存档。
        /// </summary>
        private readonly Dictionary<long, MobileOrderRecord> m_PreLoginOrderRecords = new Dictionary<long, MobileOrderRecord>();

        /// <summary>
        /// 当前运行期已经派发过 PaySuccess 的商品表 ID，用于避免服务端补单与平台 PendingOrder 双回调重复通知业务。
        /// 发起同 tableId 的新支付时会清理对应标记。
        /// </summary>
        private readonly HashSet<long> m_DispatchedPaySuccessTableIds = new HashSet<long>();

        /// <summary>
        /// 当无法访问 Store.PersistData 时使用的兜底空字典，避免空引用。
        /// </summary>
        private static readonly Dictionary<long, MobileOrderRecord> s_EmptyOrderRecords = new Dictionary<long, MobileOrderRecord>();

        /// <summary>
        /// 进行中订单字典，路由到 MobileStore.PersistData.OrderRecords，
        /// 与统一存档共享同一份字典引用，避免双源不同步。
        /// </summary>
        private Dictionary<long, MobileOrderRecord> m_OrderRecords => m_Hub.Store?.PersistData?.OrderRecords ?? s_EmptyOrderRecords;

        /// <summary>
        /// 验单队列是否正在处理中，防止并发重入。
        /// </summary>
        private bool m_IsProcessingQueue;

        /// <summary>
        /// 当前正常支付完成信号（非 recovered order），验单结束后通知 PurchaseService。
        /// </summary>
        internal UniTaskCompletionSource<IAPResult> CurrentPayTcs;

        /// <summary>
        /// 订阅升级时被替换的旧订阅 tableId。
        /// 升级支付发起前由 PurchaseService 写入，验单成功后用于清零旧订阅到期时间，清零后重置为 0。
        /// </summary>
        internal long SubscriptionUpgradeTableId;
    }
}
