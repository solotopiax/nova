/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProtobufVoucherGateway.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 领域命令与 protobuf 协议映射入口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// Voucher protobuf 网关。
    /// 负责构建公共协议头、发送请求并将协议结果转换为领域结果。
    /// </summary>
    internal sealed partial class ProtobufVoucherGateway : IVoucherGateway
    {
        /// <summary>
        /// 创建 Voucher protobuf 网关。
        /// </summary>
        /// <param name="netService">Voucher 网络发送服务。</param>
        /// <param name="listCommand">查询钱包的网络命令名。</param>
        /// <param name="deductCommand">扣减资产的网络命令名。</param>
        /// <param name="testGrantCommand">测试发放资产的网络命令名。</param>
        /// <exception cref="ArgumentNullException">网络发送服务为空时抛出。</exception>
        internal ProtobufVoucherGateway(VoucherIapNetService netService, string listCommand, string deductCommand, string testGrantCommand)
        {
            m_NetService = netService ?? throw new ArgumentNullException(nameof(netService));
            m_ListCommand = listCommand ?? string.Empty;
            m_DeductCommand = deductCommand ?? string.Empty;
            m_TestGrantCommand = testGrantCommand ?? string.Empty;
        }

        /// <summary>
        /// 拉取服务端钱包，并将协议数据转换为内部不可变资产。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>钱包拉取结果。</returns>
        public async UniTask<VoucherGatewayWalletResult> FetchWalletAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var request = new PbNetGiftVoucherListReq { Head = NetBuilder.BuildHeader() };
            NetResponse<PbNetGiftVoucherListResp> response = await m_NetService.GetVoucherListAsync(m_ListCommand, request);
            ct.ThrowIfCancellationRequested();

            if (response == null || !response.IsSuccess || response.Data == null)
            {
                return new VoucherGatewayWalletResult(false, IAPVoucherErrorCode.NetworkError, response?.ErrorMessage ?? "Voucher 钱包响应为空。", null, null);
            }

            if (!TryMapWallet(response.Data.VoucherGroups, response.Data.CoinBalances, out List<VoucherAssetData> vouchers, out List<CoinAssetData> coins))
            {
                return new VoucherGatewayWalletResult(false, IAPVoucherErrorCode.ProtocolError, "Voucher 钱包包含无法精确表示的资产面值。", null, null);
            }

            return new VoucherGatewayWalletResult(true, IAPVoucherErrorCode.None, string.Empty, vouchers, coins);
        }

        /// <summary>
        /// 使用不可变交易命令构造并发送 Voucher 扣减请求。
        /// </summary>
        /// <param name="command">已经持久化的不可变交易命令。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>经过有限状态分类的扣减结果。</returns>
        public async UniTask<VoucherGatewayDeductResult> DeductAsync(VoucherSpendCommand command, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            PbNetGiftVoucherDeductReq request = BuildDeductRequest(command, NetBuilder.BuildHeader());
            NetResponse<PbNetGiftVoucherDeductResp> response = await m_NetService.DeductVoucherAsync(m_DeductCommand, request);
            ct.ThrowIfCancellationRequested();
            return Classify(response);
        }

        /// <summary>
        /// 向当前网络会话对应账号测试发放礼券和赠币。
        /// </summary>
        /// <param name="request">测试发放请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>测试发放后的服务端钱包结果。</returns>
        public async UniTask<VoucherGatewayWalletResult> TestGrantAsync(VoucherTestGrantRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            PbNetGiftVoucherTestGrantReq protocolRequest = BuildTestGrantRequest(request, NetBuilder.BuildHeader());
            NetResponse<PbNetGiftVoucherTestGrantResp> response = await m_NetService.TestGrantVoucherAsync(m_TestGrantCommand, protocolRequest);
            ct.ThrowIfCancellationRequested();
            return ClassifyTestGrant(response);
        }
    }
}
