/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileValidationLocalOrderScanner.cs
 * author:    yingzheng
 * created:   2026/8/11
 * descrip:   移动端官方内购本地订单扫描器
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 移动端官方内购本地订单扫描器。
    /// 负责把本地存档订单按状态筛选为待验单订单键列表，并清理应删除的终态或无效订单。
    /// </summary>
    internal sealed class MobileValidationLocalOrderScanner
    {
        /// <summary>
        /// 扫描本地订单字典，返回本轮应进入验单队列的订单键列表。
        /// </summary>
        /// <param name="orderRecords">当前账号的本地订单字典。</param>
        /// <param name="canEnqueueLocalRecord">判断订单凭据是否足以进入验单队列的委托。</param>
        /// <param name="shouldRemoveLocalRecordWithoutCredential">判断缺少凭据的订单是否应删除的委托。</param>
        /// <returns>本轮需要进入验单队列的订单键列表。</returns>
        internal List<string> CollectValidationOrderKeys(
            Dictionary<string, MobileOrderRecord> orderRecords,
            Func<MobileOrderRecord, bool> canEnqueueLocalRecord,
            Func<MobileOrderRecord, bool> shouldRemoveLocalRecordWithoutCredential)
        {
            var validationOrderKeys = new List<string>();
            if (orderRecords == null)
            {
                return validationOrderKeys;
            }

            var toRemove = new List<string>();
            foreach (KeyValuePair<string, MobileOrderRecord> kv in orderRecords)
            {
                string orderKey = kv.Key;
                MobileOrderRecord record = kv.Value;
                if (record == null)
                {
                    // 空记录没有可恢复凭据，直接清理，避免坏存档中断整轮补单扫描
                    toRemove.Add(orderKey);
                    continue;
                }

                switch (record.Status)
                {
                    case MobileOrderStatus.Purchasing:
                        if (!CanEnqueue(record, canEnqueueLocalRecord))
                        {
                            if (ShouldRemove(record, shouldRemoveLocalRecordWithoutCredential))
                            {
                                toRemove.Add(orderKey);
                            }

                            continue;
                        }

                        record.Status = MobileOrderStatus.PendingValidate;
                        break;
                    case MobileOrderStatus.LocalPayFailed:
                        toRemove.Add(orderKey);
                        continue;
                    case MobileOrderStatus.AwaitingConfirm:
                        continue;
                }

                if (record.Status == MobileOrderStatus.PendingValidate || record.Status == MobileOrderStatus.ValidateFailed)
                {
                    if (!CanEnqueue(record, canEnqueueLocalRecord))
                    {
                        if (ShouldRemove(record, shouldRemoveLocalRecordWithoutCredential))
                        {
                            toRemove.Add(orderKey);
                        }

                        continue;
                    }

                    record.IsReplenish = true;
                    if (!validationOrderKeys.Contains(orderKey))
                    {
                        validationOrderKeys.Add(orderKey);
                    }
                }
            }

            foreach (string orderKey in toRemove)
            {
                orderRecords.Remove(orderKey);
            }

            return validationOrderKeys;
        }

        /// <summary>
        /// 执行可入队判断委托；委托缺失时按不可入队处理。
        /// </summary>
        /// <param name="record">待检查的本地订单记录。</param>
        /// <param name="canEnqueueLocalRecord">可入队判断委托。</param>
        /// <returns>订单可以进入验单队列时返回 true。</returns>
        private static bool CanEnqueue(MobileOrderRecord record, Func<MobileOrderRecord, bool> canEnqueueLocalRecord)
        {
            return record != null && canEnqueueLocalRecord != null && canEnqueueLocalRecord(record);
        }

        /// <summary>
        /// 执行缺凭据订单删除判断委托；委托缺失时保留订单。
        /// </summary>
        /// <param name="record">待检查的本地订单记录。</param>
        /// <param name="shouldRemoveLocalRecordWithoutCredential">缺凭据删除判断委托。</param>
        /// <returns>订单应从本地字典删除时返回 true。</returns>
        private static bool ShouldRemove(MobileOrderRecord record, Func<MobileOrderRecord, bool> shouldRemoveLocalRecordWithoutCredential)
        {
            return record != null &&
                   shouldRemoveLocalRecordWithoutCredential != null &&
                   shouldRemoveLocalRecordWithoutCredential(record);
        }
    }
}
