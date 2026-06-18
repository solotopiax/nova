/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherIapNetService.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   礼券/代金券业务网络 Service，封装礼券列表、代金券扣减、测试发放三个协议
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// 礼券/代金券业务网络 Service。
    /// 封装 gift_voucher 三个协议的发送逻辑，通过 NetService.SendAsync 完成
    /// Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// </summary>
    public sealed class VoucherIapNetService
    {
        /// <summary>
        /// 当前 Service 实例的调试模式覆盖值。
        /// 为 null 时沿用 NetService.IsDebugMode 全局开关。
        /// </summary>
        private bool? m_DebugModeOverride;

        /// <summary>
        /// 设置当前 Service 实例的调试模式覆盖。
        /// 设置后仅影响本实例发出的请求；传 null 可恢复沿用全局开关。
        /// </summary>
        /// <param name="debugMode">是否启用调试模式。</param>
        public void SetDebugMode(bool debugMode)
        {
            m_DebugModeOverride = debugMode;
        }

        /// <summary>
        /// 获取礼券列表。
        /// </summary>
        /// <param name="cmdName">获取礼券列表 NetCmd 协议名，由调用方从 VoucherStoreConfig.GetVoucherListCmdName 取出后传入。</param>
        /// <param name="request">礼券列表请求。</param>
        /// <returns>包含礼券列表响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetGiftVoucherListResp>> GetVoucherListAsync(string cmdName, PbNetGiftVoucherListReq request)
        {
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, request, PbNetGiftVoucherListResp.Parser, m_DebugModeOverride);
        }

        /// <summary>
        /// 扣减代金券/金币。
        /// </summary>
        /// <param name="cmdName">扣减 NetCmd 协议名，由调用方从 VoucherStoreConfig.DeductVoucherCmdName 取出后传入。</param>
        /// <param name="request">扣减请求。</param>
        /// <returns>包含扣减响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetGiftVoucherDeductResp>> DeductVoucherAsync(string cmdName, PbNetGiftVoucherDeductReq request)
        {
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, request, PbNetGiftVoucherDeductResp.Parser, m_DebugModeOverride);
        }

        /// <summary>
        /// 测试发放礼券（仅测试环境使用）。
        /// </summary>
        /// <param name="cmdName">测试发放 NetCmd 协议名，由调用方从 VoucherStoreConfig.TestGrantVoucherCmdName 取出后传入。</param>
        /// <param name="request">测试发放请求。</param>
        /// <returns>包含测试发放响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetGiftVoucherTestGrantResp>> TestGrantVoucherAsync(string cmdName, PbNetGiftVoucherTestGrantReq request)
        {
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, request, PbNetGiftVoucherTestGrantResp.Parser, m_DebugModeOverride);
        }
    }
}
