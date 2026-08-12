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
    internal sealed class ThirdPayPersistData : IIAPStorePersistData
    {
        /// <summary>
        /// 进行中的第三方订单字典：key = 客户端订单号，value = 订单上下文。
        /// 同一个商品可以同时存在多个待处理订单。
        /// </summary>
        public Dictionary<string, ThirdPayOrderRecord> Orders;

        /// <summary>
        /// 当前账号的渠道参数（CID），登录成功后从服务端拉取一次或由业务层手动注入。
        /// </summary>
        public string ChannelParams;

        /// <summary>
        /// 反序列化后或新建空容器后由 IAPStoreBase 调用一次，确保引用类型字段非 null。
        /// </summary>
        public void EnsureInitialized()
        {
            if (Orders == null)
            {
                Orders = new Dictionary<string, ThirdPayOrderRecord>();
            }

            if (ChannelParams == null)
            {
                ChannelParams = string.Empty;
            }
        }
    }
}
