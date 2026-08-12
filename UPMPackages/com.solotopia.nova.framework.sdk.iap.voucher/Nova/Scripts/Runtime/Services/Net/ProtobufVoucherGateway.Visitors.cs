/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProtobufVoucherGateway.Visitors.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   ProtobufVoucherGateway 字段与属性
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// ProtobufVoucherGateway 字段与属性。
    /// </summary>
    internal sealed partial class ProtobufVoucherGateway
    {
        /// <summary>
        /// Voucher 网络发送服务。
        /// </summary>
        private readonly VoucherIapNetService m_NetService;

        /// <summary>
        /// 查询 Voucher 钱包使用的网络命令名。
        /// </summary>
        private readonly string m_ListCommand;

        /// <summary>
        /// 扣减 Voucher 资产使用的网络命令名。
        /// </summary>
        private readonly string m_DeductCommand;

        /// <summary>
        /// 测试发放 Voucher 资产使用的网络命令名。
        /// </summary>
        private readonly string m_TestGrantCommand;
    }
}
