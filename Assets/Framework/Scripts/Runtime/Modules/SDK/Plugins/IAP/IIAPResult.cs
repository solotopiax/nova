/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPResult.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   IAP 支付结果最小接口，供外部业务层感知结果
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// IAP 支付结果最小接口。
    /// 外部业务层通过 IIAPPlugin 获得此接口，只感知是否成功、错误码与商品 ID；
    /// 具体字段（OrderId / CustomData 等）由各包内的具体实现类提供，需转型后访问。
    /// ErrorCode 为 int 类型，具体含义由各 store 自定义错误码枚举定义，从 0 起编。
    /// </summary>
    public interface IIAPResult
    {
        /// <summary>
        /// 支付是否成功。
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// 错误码。支付成功时为 0；失败时由各 store 自定义错误码枚举强转。
        /// </summary>
        int ErrorCode { get; }

        /// <summary>
        /// 商品配置表行 ID，与请求中的 TableId 一致。
        /// </summary>
        long TableId { get; }
    }
}
