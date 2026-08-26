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
        /// 旧版进行中订单记录字典，key = tableId，value = 单条订单存档。
        /// 仅用于反序列化旧存档并迁移到 OrderRecordsByKey，新写入不再使用该字段。
        /// </summary>
        public Dictionary<long, MobileOrderRecord> OrderRecords;

        /// <summary>
        /// 进行中订单记录字典，key = tableId + ReceiptParam 组成的订单键，value = 单条订单存档。
        /// </summary>
        public Dictionary<string, MobileOrderRecord> OrderRecordsByKey;

        /// <summary>
        /// 订阅商品到期时间字典，key = tableId，value = Unix 毫秒时间戳；0 表示已过期或未订阅。
        /// </summary>
        public Dictionary<long, long> SubscriptionExpireMs;

        /// <summary>
        /// 非消耗品持有标记字典，key = tableId，value = 是否持有。
        /// </summary>
        public Dictionary<long, bool> NonConsumeOwnership;

        /// <summary>
        /// 当前账号已经上报过验单成功的平台注册订单键，用于跨进程去重。
        /// Apple 使用 transaction id，Google 使用 purchase token。
        /// </summary>
        public List<string> ValidateSuccessOrderKeys;

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
            if (OrderRecordsByKey == null)
            {
                OrderRecordsByKey = new Dictionary<string, MobileOrderRecord>();
            }

            MigrateLegacyOrderRecords();

            if (SubscriptionExpireMs == null)
            {
                SubscriptionExpireMs = new Dictionary<long, long>();
            }

            if (NonConsumeOwnership == null)
            {
                NonConsumeOwnership = new Dictionary<long, bool>();
            }

            if (ValidateSuccessOrderKeys == null)
            {
                ValidateSuccessOrderKeys = new List<string>();
            }
        }

        /// <summary>
        /// 将旧版 tableId 字典迁移到新版订单键字典。
        /// ReceiptParam 是新版本能力，旧记录通常为空透传，因此迁移为 tableId + 空 ReceiptParam。
        /// </summary>
        private void MigrateLegacyOrderRecords()
        {
            if (OrderRecords == null || OrderRecords.Count == 0)
            {
                OrderRecords = null;
                return;
            }

            foreach (KeyValuePair<long, MobileOrderRecord> kv in OrderRecords)
            {
                MobileOrderRecord record = kv.Value;
                if (record == null)
                {
                    continue;
                }

                if (record.TableId == 0L)
                {
                    record.TableId = kv.Key;
                }

                string orderKey = MobileOrderKey.Build(record);
                if (MobileOrderKey.IsValid(orderKey) && !OrderRecordsByKey.ContainsKey(orderKey))
                {
                    OrderRecordsByKey[orderKey] = record;
                }
            }

            OrderRecords = null;
        }
    }
}
