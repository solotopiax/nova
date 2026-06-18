/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileIapNetService.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   移动端官方内购业务网络 Service，封装查询未完成订单、验单等协议
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// 移动端官方内购业务网络 Service。
    /// 封装 mobile_pay 系列协议的发送逻辑，通过 NetService.SendAsync 完成
    /// Protobuf 序列化、AES 加密、HTTP 请求及解析全流程。
    /// </summary>
    public sealed class MobileIapNetService
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
        /// 查询谷歌平台未完成订单列表。
        /// 内部构造 PbNetGoogleQueryPendingOrderReq（含协议头），通过 NetService.SendAsync 发送。
        /// </summary>
        /// <param name="cmdName">查询未完成订单 NetCmd 协议名，由调用方从 MobileStoreConfig.GoogleQueryPendingOrderCmdName 取出后传入。</param>
        /// <returns>包含谷歌未完成订单响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetGoogleQueryPendingOrderResp>> QueryGooglePendingOrderAsync(string cmdName)
        {
            var req = new PbNetGoogleQueryPendingOrderReq { Head = NetBuilder.BuildHeader() };
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            LogRequest("QueryPendingOrder", "Google", cmdName, req);
            NetResponse<PbNetGoogleQueryPendingOrderResp> resp = await NetService.SendAsync(cmdRow, req, PbNetGoogleQueryPendingOrderResp.Parser, m_DebugModeOverride);
            LogResponse("QueryPendingOrder", "Google", cmdName, resp);
            return resp;
        }

        /// <summary>
        /// 查询苹果平台未完成订单列表。
        /// 内部构造 PbNetAppleQueryPendingOrderReq（含协议头），通过 NetService.SendAsync 发送。
        /// </summary>
        /// <param name="cmdName">查询未完成订单 NetCmd 协议名，由调用方从 MobileStoreConfig.AppleQueryPendingOrderCmdName 取出后传入。</param>
        /// <returns>包含苹果未完成订单响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetAppleQueryPendingOrderResp>> QueryApplePendingOrderAsync(string cmdName)
        {
            var req = new PbNetAppleQueryPendingOrderReq { Head = NetBuilder.BuildHeader() };
            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            LogRequest("QueryPendingOrder", "Apple", cmdName, req);
            NetResponse<PbNetAppleQueryPendingOrderResp> resp = await NetService.SendAsync(cmdRow, req, PbNetAppleQueryPendingOrderResp.Parser, m_DebugModeOverride);
            LogResponse("QueryPendingOrder", "Apple", cmdName, resp);
            return resp;
        }

        /// <summary>
        /// 发送谷歌验单请求：按 isSubscription 选择普通内购或订阅请求类型，内部构造订单项与协议头。
        /// 普通订单与订阅订单为独立协议，按 cmdName 路由到不同 URL。
        /// </summary>
        /// <param name="cmdName">验单 NetCmd 协议名，普通订单取 MobileStoreConfig.GoogleVerifyCmdName，订阅订单取 GoogleVerifySubscriptionCmdName。</param>
        /// <param name="productId">应用内商品 SKU。</param>
        /// <param name="token">Google Play purchase token（服务端校验凭据）。</param>
        /// <param name="price">商品本地化价格，取不到时为 0。</param>
        /// <param name="isSubscription">true = 订阅请求；false = 普通内购请求。</param>
        /// <returns>包含验单响应数据或错误信息的 NetResponse。</returns>
        public UniTask<NetResponse<PbNetMobileVerifyResp>> VerifyGoogleAsync(string cmdName, string productId, string token, float price, bool isSubscription)
        {
            var item = new PbNetGoogleVerifyIapOrderItem { ProductId = productId, Token = token, Price = price };
            return VerifyGoogleAsync(cmdName, new[] { item }, isSubscription);
        }

        /// <summary>
        /// 批量发送谷歌验单请求：按 isSubscription 选择普通内购或订阅请求类型，OrderList 一次性携带多笔订单。
        /// </summary>
        /// <param name="cmdName">验单 NetCmd 协议名。</param>
        /// <param name="items">待校验订单项列表。</param>
        /// <param name="isSubscription">true = 订阅请求；false = 普通内购请求。</param>
        /// <returns>包含验单响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetMobileVerifyResp>> VerifyGoogleAsync(string cmdName, IReadOnlyList<PbNetGoogleVerifyIapOrderItem> items, bool isSubscription)
        {
            if (items == null || items.Count == 0)
            {
                return NetResponse<PbNetMobileVerifyResp>.Fail(0, "谷歌验单订单列表为空。");
            }

            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            if (isSubscription)
            {
                var req = new PbNetGoogleVerifySubscribeReq { Head = NetBuilder.BuildHeader() };
                foreach (PbNetGoogleVerifyIapOrderItem item in items)
                {
                    req.OrderList.Add(item);
                }

                LogRequest("VerifyGoogleSubscribe", "Google", cmdName, req);
                NetResponse<PbNetMobileVerifyResp> resp = await NetService.SendAsync(cmdRow, req, PbNetMobileVerifyResp.Parser, m_DebugModeOverride);
                LogResponse("VerifyGoogleSubscribe", "Google", cmdName, resp);
                return resp;
            }

            var iapReq = new PbNetGoogleVerifyIapReq { Head = NetBuilder.BuildHeader() };
            foreach (PbNetGoogleVerifyIapOrderItem item in items)
            {
                iapReq.OrderList.Add(item);
            }

            LogRequest("VerifyGoogleIap", "Google", cmdName, iapReq);
            NetResponse<PbNetMobileVerifyResp> iapResp = await NetService.SendAsync(cmdRow, iapReq, PbNetMobileVerifyResp.Parser, m_DebugModeOverride);
            LogResponse("VerifyGoogleIap", "Google", cmdName, iapResp);
            return iapResp;
        }

        /// <summary>
        /// 发送苹果验单请求：按 isSubscription 选择普通内购或订阅请求类型，内部构造订单项与协议头。
        /// 订单项仅含订单号 + 价格，不再发送收据；普通与订阅为独立协议按 cmdName 路由不同 URL。
        /// </summary>
        /// <param name="cmdName">验单 NetCmd 协议名，普通订单取 MobileStoreConfig.AppleVerifyCmdName，订阅订单取 AppleVerifySubscriptionCmdName。</param>
        /// <param name="orderId">客户端订单号（透传到服务端，服务端按订单号校验）。</param>
        /// <param name="price">商品本地化价格，取不到时为 0。</param>
        /// <param name="isSubscription">true = 订阅请求；false = 普通内购请求。</param>
        /// <returns>包含验单响应数据或错误信息的 NetResponse。</returns>
        public UniTask<NetResponse<PbNetMobileVerifyResp>> VerifyAppleAsync(string cmdName, string orderId, float price, bool isSubscription)
        {
            var item = new PbNetAppleVerifyIapOrderItem { OrderId = orderId, Price = price };
            return VerifyAppleAsync(cmdName, new[] { item }, isSubscription);
        }

        /// <summary>
        /// 批量发送苹果验单请求：按 isSubscription 选择普通内购或订阅请求类型，OrderList 一次性携带多笔订单。
        /// </summary>
        /// <param name="cmdName">验单 NetCmd 协议名。</param>
        /// <param name="items">待校验订单项列表。</param>
        /// <param name="isSubscription">true = 订阅请求；false = 普通内购请求。</param>
        /// <returns>包含验单响应数据或错误信息的 NetResponse。</returns>
        public async UniTask<NetResponse<PbNetMobileVerifyResp>> VerifyAppleAsync(string cmdName, IReadOnlyList<PbNetAppleVerifyIapOrderItem> items, bool isSubscription)
        {
            if (items == null || items.Count == 0)
            {
                return NetResponse<PbNetMobileVerifyResp>.Fail(0, "苹果验单订单列表为空。");
            }

            INetworkCmdRow cmdRow = Nova.Network?.ResolveNetCmdRow(cmdName);
            if (isSubscription)
            {
                var req = new PbNetAppleVerifySubscribeReq { Head = NetBuilder.BuildHeader() };
                foreach (PbNetAppleVerifyIapOrderItem item in items)
                {
                    req.OrderList.Add(item);
                }

                LogRequest("VerifyAppleSubscribe", "Apple", cmdName, req);
                NetResponse<PbNetMobileVerifyResp> resp = await NetService.SendAsync(cmdRow, req, PbNetMobileVerifyResp.Parser, m_DebugModeOverride);
                LogResponse("VerifyAppleSubscribe", "Apple", cmdName, resp);
                return resp;
            }

            var iapReq = new PbNetAppleVerifyIapReq { Head = NetBuilder.BuildHeader() };
            foreach (PbNetAppleVerifyIapOrderItem item in items)
            {
                iapReq.OrderList.Add(item);
            }

            LogRequest("VerifyAppleIap", "Apple", cmdName, iapReq);
            NetResponse<PbNetMobileVerifyResp> iapResp = await NetService.SendAsync(cmdRow, iapReq, PbNetMobileVerifyResp.Parser, m_DebugModeOverride);
            LogResponse("VerifyAppleIap", "Apple", cmdName, iapResp);
            return iapResp;
        }

        /// <summary>
        /// 打印支付协议请求数据。
        /// </summary>
        /// <typeparam name="TReq">请求消息类型。</typeparam>
        /// <param name="protocol">协议用途标识。</param>
        /// <param name="platform">当前平台标识。</param>
        /// <param name="cmdName">NetCmd 协议名。</param>
        /// <param name="req">协议请求体。</param>
        private static void LogRequest<TReq>(string protocol, string platform, string cmdName, TReq req) where TReq : class, IMessage<TReq>
        {
            Log.Debug(LogTag.IAPMobile, $"支付协议请求数据：协议={protocol}，平台={platform}，命令={cmdName}，请求={req}");
        }

        /// <summary>
        /// 打印支付协议响应数据。
        /// </summary>
        /// <typeparam name="TResp">响应消息类型。</typeparam>
        /// <param name="protocol">协议用途标识。</param>
        /// <param name="platform">当前平台标识。</param>
        /// <param name="cmdName">NetCmd 协议名。</param>
        /// <param name="resp">网络层返回的响应包装。</param>
        private static void LogResponse<TResp>(string protocol, string platform, string cmdName, NetResponse<TResp> resp) where TResp : class, IMessage<TResp>
        {
            string data = resp == null || resp.Data == null ? "null" : resp.Data.ToString();
            Log.Debug(LogTag.IAPMobile, $"支付协议响应数据：协议={protocol}，平台={platform}，命令={cmdName}，是否成功={resp?.IsSuccess}，错误码={resp?.ErrorCode}，错误信息={resp?.ErrorMessage}，数据={data}");
        }
    }
}
