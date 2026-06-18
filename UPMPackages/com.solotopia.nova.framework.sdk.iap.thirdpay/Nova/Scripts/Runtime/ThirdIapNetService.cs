/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdIapNetService.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   第三方支付业务网络 Service，封装商品列表、创建订单、验单三个协议
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付业务网络 Service。
    /// 封装 game_recharge 三个协议的发送逻辑，通过 NetService.SendAsync 完成
    /// Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// </summary>
    public sealed class ThirdIapNetService
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
        /// 拉取第三方支付商品列表。
        /// </summary>
        /// <param name="cmdName">拉取商品列表 NetCmd 协议名，由调用方从 ThirdPayStoreConfig.GetProductListCmdName 取出后传入。</param>
        /// <param name="request">商品列表请求。</param>
        /// <returns>包含商品列表响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetThirdProductListResp>> GetProductListAsync(string cmdName, PbNetThirdProductListReq request)
        {
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, request, PbNetThirdProductListResp.Parser, m_DebugModeOverride);
        }

        /// <summary>
        /// 创建第三方支付订单。
        /// </summary>
        /// <param name="cmdName">创建订单 NetCmd 协议名，由调用方从 ThirdPayStoreConfig.CreateOrderCmdName 取出后传入。</param>
        /// <param name="request">创建订单请求。</param>
        /// <returns>包含创建订单响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetThirdCreateOrderResp>> CreateOrderAsync(string cmdName, PbNetThirdCreateOrderReq request)
        {
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, request, PbNetThirdCreateOrderResp.Parser, m_DebugModeOverride);
        }

        /// <summary>
        /// 验证第三方支付订单。
        /// </summary>
        /// <param name="cmdName">验单 NetCmd 协议名，由调用方从 ThirdPayStoreConfig.CheckOrderCmdName 取出后传入。</param>
        /// <param name="request">验单请求。</param>
        /// <returns>包含验单响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetThirdCheckOrderResp>> CheckOrderAsync(string cmdName, PbNetThirdCheckOrderReq request)
        {
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            return await NetService.SendAsync(cmdRow, request, PbNetThirdCheckOrderResp.Parser, m_DebugModeOverride);
        }
    }
}
