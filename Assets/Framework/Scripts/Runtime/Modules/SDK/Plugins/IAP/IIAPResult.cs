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
    /// 外部业务层通过 IIAPPlugin 获得此接口，无需转型即可读取定位问题所需的核心字段：
    /// TableId、IsSuccess、ErrorSource、ErrorCode、ErrorDesc、OrderId；
    /// 其余实现细节字段（CustomData / ReceiptParam 等）仍由各包内的具体实现类提供，需转型后访问。
    /// ErrorCode 为 int 类型，具体含义由 ErrorSource 指向的错误码枚举定义，须结合 (ErrorSource, ErrorCode) 才能唯一解码。
    /// </summary>
    public interface IIAPResult
    {

        /// <summary>
        /// 商品配置表行 ID，与请求中的 TableId 一致。
        /// </summary>
        long TableId { get; }

        /// <summary>
        /// 支付是否成功。
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// 错误码来源。标识 ErrorCode 属于哪个渠道/层的错误码枚举，
        /// 业务层须结合 (ErrorSource, ErrorCode) 才能唯一解码；支付成功时为 None。
        /// </summary>
        IAPErrorSource ErrorSource { get; }

        /// <summary>
        /// 错误码。支付成功时为 0；失败时由各 store 自定义错误码枚举强转。
        /// </summary>
        int ErrorCode { get; }

        /// <summary>
        /// 错误描述。支付成功时为 null；失败时为该错误码对应的可读原因，便于结合 ErrorCode 精确定位问题。
        /// </summary>
        string ErrorDesc { get; }

        /// <summary>
        /// 订单唯一 ID，用于跨客户端日志 / 服务端订单 / 渠道后台关联定位；
        /// 尚未生成订单号的早期失败（如渠道不可用、商品未找到）为 null。
        /// </summary>
        string OrderId { get; }

    }
}
