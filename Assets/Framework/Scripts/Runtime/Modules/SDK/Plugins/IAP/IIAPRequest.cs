/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPRequest.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   IAP 支付请求最小接口，供 IIAPPlugin 路由使用
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// IAP 支付请求最小接口。
    /// 各渠道具体请求类（IAPMobileRequest 等）继承 IAPRequest 抽象类实现此接口。
    /// IIAPPlugin.PayAsync 接收此接口，路由逻辑由实现层处理。
    /// </summary>
    public interface IIAPRequest
    {
        /// <summary>
        /// 商品配置表行 ID，用于查询商品定价与平台 ProductId。
        /// </summary>
        long TableId { get; }
    }
}
