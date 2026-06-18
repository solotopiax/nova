/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStorePersistData.cs
 * author:    yingzheng
 * created:   2026/5/27
 * descrip:   MobileStore 本地存档统一容器（订单/订阅到期/非消耗品标记）
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// MobileStore 本地存档统一容器。
    /// 由 IAPStoreBase 通过 IPersistManager.GetObject/SetObject 单原子读写：
    /// classify = "iap_mobile"，item = "data_{uid}"。
    /// 非终态订单（Purchasing/PendingValidate/ValidateFailed）保留供下次启动补单扫描；
    /// LocalPayFailed 只作为平台失败后的终态清理标记，扫描时直接删除。
    /// </summary>
    [Serializable]
    public sealed class MobileStorePersistData : IIAPStorePersistData
    {
        /// <summary>
        /// 进行中订单记录字典，key = tableId，value = 单条订单存档。
        /// </summary>
        public Dictionary<long, MobileOrderRecord> OrderRecords;

        /// <summary>
        /// 订阅商品到期时间字典，key = tableId，value = Unix 毫秒时间戳；0 表示已过期或未订阅。
        /// </summary>
        public Dictionary<long, long> SubscriptionExpireMs;

        /// <summary>
        /// 非消耗品持有标记字典，key = tableId，value = 是否持有。
        /// </summary>
        public Dictionary<long, bool> NonConsumeOwnership;

        /// <summary>
        /// 当前账号是否已向服务端拉取过一次未发货补单列表。
        /// 首次登录拉取成功后置 true，切换 UID 时随整包存档重置。
        /// </summary>
        public bool HasQueriedPendingFromServer;

        /// <summary>
        /// 反序列化或新建后兜底初始化集合字段，避免后续读写空引用。
        /// </summary>
        public void EnsureInitialized()
        {
            if (OrderRecords == null)
            {
                OrderRecords = new Dictionary<long, MobileOrderRecord>();
            }

            if (SubscriptionExpireMs == null)
            {
                SubscriptionExpireMs = new Dictionary<long, long>();
            }

            if (NonConsumeOwnership == null)
            {
                NonConsumeOwnership = new Dictionary<long, bool>();
            }
        }
    }
}
