/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayPersistData.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   第三方支付本地存档统一容器
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付本地存档统一容器。
    /// 通过 IAPStoreBase 统一模板按 classify=iap_thirdpay / item=data_{uid} 单原子读写。
    /// </summary>
    [Serializable]
    public sealed class ThirdPayPersistData : IIAPStorePersistData
    {
        /// <summary>
        /// 进行中的第三方订单字典：key = TableId，value = 订单上下文。
        /// </summary>
        public Dictionary<long, ThirdOrderData> OrderingStates;

        /// <summary>
        /// 上次成功支付的 PayMethod，仅用于 GetPayTypeList 排序排首位，不入请求体。
        /// </summary>
        public string LastPayMethod;

        /// <summary>
        /// 渠道参数（CID），SetAccountID 后异步拉取写入。
        /// </summary>
        public string ChannelParams;

        /// <summary>
        /// 反序列化后或新建空容器后由 IAPStoreBase 调用一次，确保引用类型字段非 null。
        /// </summary>
        public void EnsureInitialized()
        {
            if (OrderingStates == null) OrderingStates = new Dictionary<long, ThirdOrderData>();
            if (LastPayMethod == null) LastPayMethod = string.Empty;
            if (ChannelParams == null) ChannelParams = string.Empty;
        }
    }
}
