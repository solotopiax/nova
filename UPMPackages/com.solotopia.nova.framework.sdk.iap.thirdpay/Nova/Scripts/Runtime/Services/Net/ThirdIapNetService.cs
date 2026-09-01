/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdIapNetService.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   第三方支付商品、渠道参数、补单与验单协议服务
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付业务网络 Service。
    /// 封装 third_pay 系列协议的请求构造与发送，通过 NetService.SendAsync 完成
    /// Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// </summary>
    public sealed class ThirdIapNetService : ThirdPayLogOwner
    {
        /// <summary>
        /// 拉取指定国家或地区的第三方支付商品列表。
        /// </summary>
        /// <param name="cmdName">ThirdPayStoreConfig.GetProductListCmdName。</param>
        /// <param name="countryCode">ISO 3166-1 alpha-2 国家或地区代码。</param>
        /// <returns>商品列表响应。</returns>
        public async UniTask<NetResponse<PbNetThirdProductListResp>> GetProductListAsync(string cmdName, string countryCode)
        {
            var request = new PbNetThirdProductListReq { Head = NetBuilder.BuildHeader(), Country = countryCode ?? string.Empty };
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            LogRequest("GetProductList", cmdName, request);
            NetResponse<PbNetThirdProductListResp> response = await NetService.SendAsync(cmdRow, request, PbNetThirdProductListResp.Parser);
            LogResponse("GetProductList", cmdName, response);
            return response;
        }

        /// <summary>
        /// 查询支付成功、但客户端尚未校验的第三方支付订单。
        /// </summary>
        /// <param name="cmdName">ThirdPayStoreConfig.QueryPendingOrderCmdName。</param>
        /// <returns>未校验订单列表响应。</returns>
        public async UniTask<NetResponse<PbNetThirdQueryPendingOrderResp>> QueryPendingOrderAsync(string cmdName)
        {
            var request = new PbNetThirdQueryPendingOrderReq { Head = NetBuilder.BuildHeader() };
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            LogRequest("QueryPendingOrder", cmdName, request);
            NetResponse<PbNetThirdQueryPendingOrderResp> response = await NetService.SendAsync(cmdRow, request, PbNetThirdQueryPendingOrderResp.Parser);
            LogResponse("QueryPendingOrder", cmdName, response);
            return response;
        }

        /// <summary>
        /// 查询当前用户需要透传到第三方支付页的渠道参数。
        /// </summary>
        /// <param name="cmdName">ThirdPayStoreConfig.PayChannelParamsCmdName。</param>
        /// <returns>渠道参数响应。</returns>
        public async UniTask<NetResponse<PbNetThirdPayChannelParamsResp>> GetPayChannelParamsAsync(string cmdName)
        {
            var request = new PbNetThirdPayChannelParamsReq { Head = NetBuilder.BuildHeader() };
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            LogRequest("GetPayChannelParams", cmdName, request);
            NetResponse<PbNetThirdPayChannelParamsResp> response = await NetService.SendAsync(cmdRow, request, PbNetThirdPayChannelParamsResp.Parser);
            LogResponse("GetPayChannelParams", cmdName, response);
            return response;
        }

        /// <summary>
        /// 按客户端订单号批量验证第三方支付订单。
        /// </summary>
        /// <param name="cmdName">ThirdPayStoreConfig.VerifyIapCmdName。</param>
        /// <param name="clientOrderIds">待校验的客户端订单号。</param>
        /// <returns>订单状态列表响应。</returns>
        public async UniTask<NetResponse<PbNetThirdVerifyIapResp>> VerifyIapAsync(string cmdName, IReadOnlyList<string> clientOrderIds)
        {
            if (clientOrderIds == null || clientOrderIds.Count == 0)
            {
                return NetResponse<PbNetThirdVerifyIapResp>.Fail(0, "第三方支付验单列表为空。");
            }

            var request = new PbNetThirdVerifyIapReq { Head = NetBuilder.BuildHeader() };
            foreach (string clientOrderId in clientOrderIds)
            {
                request.ClientOrderIds.Add(clientOrderId ?? string.Empty);
            }

            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            LogRequest("VerifyIap", cmdName, request);
            NetResponse<PbNetThirdVerifyIapResp> response = await NetService.SendAsync(cmdRow, request, PbNetThirdVerifyIapResp.Parser);
            LogResponse("VerifyIap", cmdName, response);
            return response;
        }

        /// <summary>
        /// 记录第三方支付协议请求调试日志。
        /// </summary>
        /// <typeparam name="TReq">Protobuf 请求类型。</typeparam>
        /// <param name="protocol">协议职责名称。</param>
        /// <param name="cmdName">NetCmd 名称。</param>
        /// <param name="request">Protobuf 请求对象。</param>
        private void LogRequest<TReq>(string protocol, string cmdName, TReq request)
            where TReq : class, IMessage<TReq>
        {
            LogDebug($"第三方支付协议请求：协议={protocol}，命令={cmdName}，请求={request}");
        }

        /// <summary>
        /// 记录第三方支付协议响应调试日志。
        /// </summary>
        /// <typeparam name="TResp">Protobuf 响应类型。</typeparam>
        /// <param name="protocol">协议职责名称。</param>
        /// <param name="cmdName">NetCmd 名称。</param>
        /// <param name="response">统一网络响应。</param>
        private void LogResponse<TResp>(string protocol, string cmdName, NetResponse<TResp> response)
            where TResp : class, IMessage<TResp>
        {
            string data = response?.Data == null ? "null" : response.Data.ToString();
            LogDebug($"第三方支付协议响应：协议={protocol}，命令={cmdName}，是否成功={response?.IsSuccess}，错误码={response?.ErrorCode}，错误信息={response?.ErrorMessage}，数据={data}");
        }
    }
}
