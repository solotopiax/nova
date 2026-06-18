/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPRequest.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAP 支付请求基类，所有渠道请求均继承此类
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 支付请求抽象基类。
    /// 实现 IIAPRequest 接口，所有渠道具体请求类均继承此类并实现 StoreType。
    /// </summary>
    public abstract class IAPRequest : IIAPRequest
    {
        /// <inheritdoc/>
        public abstract IAPStoreType StoreType { get; }

        /// <inheritdoc/>
        public long TableId { get; set; }

        /// <summary>
        /// 调用方传入的自定义数据，支付完成后原样回传至 IAPResult.CustomData。
        /// </summary>
        public string CustomData;

        /// <summary>
        /// 埋点附加属性，由业务层填充后随请求传递给 store 层上报埋点。
        /// </summary>
        public Dictionary<string, object> TrackProperties;
    }
}
