/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherStorePersistData.cs
 * author:    yingzheng
 * created:   2026/5/27
 * descrip:   VoucherStore 本地存档统一容器
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherStore 本地存档统一容器。
    /// 通过 IAPStoreBase 统一模板按 classify=iap_voucher / item=data_{uid} 单原子读写。
    /// </summary>
    [Serializable]
    public sealed class VoucherStorePersistData : IIAPStorePersistData
    {
        /// <summary>
        /// 进行中的抵扣存档字典，键为配置表行 ID，用于跨会话补单。
        /// </summary>
        public Dictionary<long, VoucherPendingDeduct> PendingDeductStates;

        /// <summary>
        /// 反序列化后或新建空容器后由 IAPStoreBase 调用一次，确保引用类型字段非 null。
        /// </summary>
        public void EnsureInitialized()
        {
            if (PendingDeductStates == null) PendingDeductStates = new Dictionary<long, VoucherPendingDeduct>();
        }
    }
}
