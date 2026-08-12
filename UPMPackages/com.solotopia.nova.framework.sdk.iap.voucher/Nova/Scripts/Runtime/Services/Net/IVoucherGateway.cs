/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IVoucherGateway.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 领域协议访问端口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher 领域协议访问端口。
    /// 交易协调器不感知 protobuf 或网络响应包装类型。
    /// </summary>
    internal interface IVoucherGateway
    {
        /// <summary>
        /// 拉取当前网络会话对应账号的钱包。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>钱包查询结果。</returns>
        UniTask<VoucherGatewayWalletResult> FetchWalletAsync(CancellationToken ct);

        /// <summary>
        /// 发送已经持久化的完整扣减命令。
        /// </summary>
        /// <param name="command">不可变交易命令。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>经过有限状态分类的扣减结果。</returns>
        UniTask<VoucherGatewayDeductResult> DeductAsync(VoucherSpendCommand command, CancellationToken ct);

        /// <summary>
        /// 向当前网络会话对应账号测试发放礼券和赠币。
        /// </summary>
        /// <param name="request">测试发放请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>测试发放后的服务端钱包结果。</returns>
        UniTask<VoucherGatewayWalletResult> TestGrantAsync(VoucherTestGrantRequest request, CancellationToken ct);
    }
}
