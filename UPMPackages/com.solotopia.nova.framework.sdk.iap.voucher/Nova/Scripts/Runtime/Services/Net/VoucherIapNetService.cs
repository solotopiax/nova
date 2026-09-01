/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherIapNetService.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher protobuf 请求发送适配器
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// 只负责解析 NetCmd 并发送已构造 protobuf 消息的内部网络适配器。
    /// </summary>
    internal sealed class VoucherIapNetService : VoucherLogOwner
    {
        /// <summary>
        /// 发送钱包列表请求。
        /// </summary>
        /// <param name="cmdName">钱包列表协议名。</param>
        /// <param name="request">已包含公共 Header 的请求。</param>
        /// <returns>网络响应。</returns>
        internal async UniTask<NetResponse<PbNetGiftVoucherListResp>> GetVoucherListAsync(string cmdName, PbNetGiftVoucherListReq request)
        {
            if (string.IsNullOrEmpty(cmdName))
            {
                return NetResponse<PbNetGiftVoucherListResp>.Fail(NetErrorCode.URL_NOT_FOUND, "Voucher 钱包协议名未配置。");
            }
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, request, PbNetGiftVoucherListResp.Parser);
        }

        /// <summary>
        /// 发送冻结命令对应的抵扣请求。
        /// </summary>
        /// <param name="cmdName">抵扣协议名。</param>
        /// <param name="request">已包含公共 Header 和稳定订单号的请求。</param>
        /// <returns>网络响应。</returns>
        internal async UniTask<NetResponse<PbNetGiftVoucherDeductResp>> DeductVoucherAsync(string cmdName, PbNetGiftVoucherDeductReq request)
        {
            if (string.IsNullOrEmpty(cmdName))
            {
                return NetResponse<PbNetGiftVoucherDeductResp>.Fail(NetErrorCode.URL_NOT_FOUND, "Voucher 抵扣协议名未配置。");
            }
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, request, PbNetGiftVoucherDeductResp.Parser);
        }

        /// <summary>
        /// 发送测试发放礼券和赠币请求。
        /// </summary>
        /// <param name="cmdName">测试发放协议名。</param>
        /// <param name="request">已包含公共 Header 的测试发放请求。</param>
        /// <returns>网络响应。</returns>
        internal async UniTask<NetResponse<PbNetGiftVoucherTestGrantResp>> TestGrantVoucherAsync(string cmdName, PbNetGiftVoucherTestGrantReq request)
        {
            if (string.IsNullOrEmpty(cmdName))
            {
                return NetResponse<PbNetGiftVoucherTestGrantResp>.Fail(NetErrorCode.URL_NOT_FOUND, "Voucher 测试发放协议名未配置。");
            }

            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, request, PbNetGiftVoucherTestGrantResp.Parser);
        }
    }
}
