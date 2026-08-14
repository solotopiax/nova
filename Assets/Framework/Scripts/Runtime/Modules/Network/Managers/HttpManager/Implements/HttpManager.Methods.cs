/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpManager.Methods.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   HTTP管理器 —— 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器。
    /// </summary>
    internal sealed partial class HttpManager : HttpManagerBase
    {
        /// <summary>
        /// 按当前传输能力决定 DoH 是否能在本进程安全启用，并输出一次明确的中文原因。
        /// </summary>
        private void ConfigureDoHTransportCapability()
        {
            if (m_DoHManager == null || !m_DoHManager.IsEnabled)
            {
                return;
            }

#if UNITY_WEBGL
            DisableDoHWithWarning(
                "DoH 已配置为启用，但当前运行平台为 WebGL，WebGL 不支持指定连接 IP，运行时已自动禁用 DoH 并改用系统 DNS。");
#else
            m_IPAddressTransport = m_Transport as IHttpIPAddressTransport;
            if (m_IPAddressTransport == null)
            {
                DisableDoHWithWarning(
                    "DoH 已配置为启用，但当前未安装 BestHTTP，所以回退为 UnityWebRequest 方案，但因 UnityWebRequest 原生不支持指定连接 IP，故运行时已自动禁用 DoH 并改用系统 DNS。");
                return;
            }

            if (!m_IPAddressTransport.IsIPAddressRoutingAvailable)
            {
                DisableDoHWithWarning(m_IPAddressTransport.IPAddressRoutingUnavailableReason);
            }
#endif
        }

        /// <summary>
        /// 禁用当前进程的 DoH，并输出一条 Nova Warning 日志。
        /// </summary>
        /// <param name="message">明确说明禁用原因的中文文案。</param>
        private void DisableDoHWithWarning(string message)
        {
            m_DoHManager?.DisableForRuntime();
            m_IPAddressTransport = null;
            Log.Warning(LogTag.Http, string.IsNullOrWhiteSpace(message)
                ? "DoH 已配置为启用，但当前网络传输无法指定连接 IP，运行时已自动禁用 DoH 并改用系统 DNS。"
                : message);
        }

        /// <summary>
        /// 使用冻结的请求数据按主备域名执行业务 POST；DoH 可用时先交错尝试各域名 IP，最后再按主备域名走系统 DNS。
        /// </summary>
        /// <param name="routeUrls">主域名、备用域名完整 URL，已按顺序去重。</param>
        /// <param name="contentBytes">整条重试链复用的原始请求字节。</param>
        /// <param name="requestTimeout">每次尝试独享的请求超时。</param>
        /// <param name="connectTimeout">每次尝试独享的连接超时。</param>
        /// <param name="headerInfos">整条重试链复用的请求头。</param>
        /// <returns>服务器正式响应，或全部网络尝试结束后的最后一份失败响应。</returns>
        public async UniTask<HttpResponse> PostBusinessRawDataAsync(
            IReadOnlyList<string> routeUrls,
            byte[] contentBytes,
            float requestTimeout,
            float connectTimeout,
            string headerInfos)
        {
            if (routeUrls == null || routeUrls.Count == 0)
            {
                Log.Error(LogTag.Http, "【通信失败】所有请求均未到达服务器，本次请求已结束。");
                return HttpResponse.Create(
                    0,
                    null,
                    null,
                    null,
                    "没有可用的主域名或备用域名。",
                    false,
                    0,
                    -1L,
                    HttpDeliveryState.NotReachedServer);
            }

            List<BusinessRequestCandidate> candidates = await BuildBusinessCandidatesAsync(routeUrls);
            HttpResponse lastFailedResponse = null;
            bool mayHaveReachedServer = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                BusinessRequestCandidate candidate = candidates[i];
                HttpResponse response;
                try
                {
                    if (candidate.ConnectIPAddress != null &&
                        m_IPAddressTransport != null &&
                        m_IPAddressTransport.IsIPAddressRoutingAvailable)
                    {
                        response = await m_IPAddressTransport.PostRawDataAsync(
                            candidate.Url,
                            candidate.ConnectIPAddress,
                            contentBytes,
                            requestTimeout,
                            connectTimeout,
                            headerInfos);

                        if (!m_IPAddressTransport.IsIPAddressRoutingAvailable)
                        {
                            DisableDoHWithWarning(m_IPAddressTransport.IPAddressRoutingUnavailableReason);
                        }
                    }
                    else if (candidate.ConnectIPAddress != null)
                    {
                        continue;
                    }
                    else
                    {
                        response = await m_Transport.PostRawDataAsync(
                            candidate.Url,
                            contentBytes,
                            requestTimeout,
                            connectTimeout,
                            headerInfos,
                            null);
                    }
                }
                catch (Exception exception)
                {
                    response = HttpResponse.Create(
                        0,
                        null,
                        null,
                        null,
                        exception.GetBaseException().Message,
                        false,
                        0,
                        -1L,
                        DetermineExceptionDeliveryState(exception));
                }

                if (response != null && response.HasServerResponse)
                {
                    if (lastFailedResponse != null)
                    {
                        ReferencePool.Put(lastFailedResponse);
                    }

                    return response;
                }

                mayHaveReachedServer |= response == null || response.DeliveryState != HttpDeliveryState.NotReachedServer;
                if (HasRemainingUsableCandidate(candidates, i + 1))
                {
                    Log.Warning(
                        LogTag.Http,
                        "【继续重试】{0}，正在尝试下一个地址。当前地址：{1}。",
                        DescribeFailure(response),
                        candidate.DisplayName);
                }

                if (lastFailedResponse != null)
                {
                    ReferencePool.Put(lastFailedResponse);
                }

                lastFailedResponse = response;
            }

            if (mayHaveReachedServer)
            {
                Log.Error(LogTag.Http, "【结果未确认】请求可能已到达服务器，但未获得可确认的响应，本次请求已结束。");
            }
            else
            {
                Log.Error(LogTag.Http, "【通信失败】所有请求均未到达服务器，本次请求已结束。");
            }

            HttpDeliveryState finalDeliveryState = mayHaveReachedServer
                ? HttpDeliveryState.Unknown
                : HttpDeliveryState.NotReachedServer;
            if (lastFailedResponse == null)
            {
                return HttpResponse.Create(
                    0,
                    null,
                    null,
                    null,
                    mayHaveReachedServer ? "请求结果未确认。" : "网络通信失败。",
                    false,
                    0,
                    -1L,
                    finalDeliveryState);
            }

            HttpResponse finalResponse = HttpResponse.Create(
                lastFailedResponse.StatusCode,
                lastFailedResponse.Body,
                lastFailedResponse.RawData,
                lastFailedResponse.Headers,
                lastFailedResponse.Error,
                lastFailedResponse.IsSuccess,
                lastFailedResponse.DownloadedBytes,
                lastFailedResponse.TotalBytes,
                finalDeliveryState);
            ReferencePool.Put(lastFailedResponse);
            return finalResponse;
        }

        /// <summary>
        /// 并行解析主备域名，并生成 P1、B1、P2、B2、主域名系统 DNS、备用域名系统 DNS 的候选顺序。
        /// </summary>
        private async UniTask<List<BusinessRequestCandidate>> BuildBusinessCandidatesAsync(
            IReadOnlyList<string> routeUrls)
        {
            var result = new List<BusinessRequestCandidate>();
            bool canUseDoH = m_DoHManager != null &&
                             m_DoHManager.IsEnabled &&
                             m_IPAddressTransport != null &&
                             m_IPAddressTransport.IsIPAddressRoutingAvailable;
            var ipLists = new List<IPAddress[]>(routeUrls.Count);

            if (canUseDoH)
            {
                await m_DoHManager.CollectAllIPAddresses(routeUrls);
            }

            int maxIPCount = 0;
            for (int routeIndex = 0; routeIndex < routeUrls.Count; routeIndex++)
            {
                IPAddress[] addresses = canUseDoH &&
                                        Uri.TryCreate(routeUrls[routeIndex], UriKind.Absolute, out Uri uri)
                    ? m_DoHManager.GetIPAddresses(uri.Host) ?? Array.Empty<IPAddress>()
                    : Array.Empty<IPAddress>();
                ipLists.Add(addresses);
                maxIPCount = Math.Max(maxIPCount, addresses.Length);
            }

            for (int ipIndex = 0; ipIndex < maxIPCount; ipIndex++)
            {
                for (int routeIndex = 0; routeIndex < routeUrls.Count; routeIndex++)
                {
                    IPAddress[] addresses = ipLists[routeIndex];
                    if (ipIndex < addresses.Length)
                    {
                        result.Add(new BusinessRequestCandidate(routeUrls[routeIndex], addresses[ipIndex], routeIndex));
                    }
                }
            }

            for (int routeIndex = 0; routeIndex < routeUrls.Count; routeIndex++)
            {
                result.Add(new BusinessRequestCandidate(routeUrls[routeIndex], null, routeIndex));
            }

            return result;
        }

        /// <summary>
        /// 判断后续是否还有当前运行能力可以执行的候选，避免在即将跳过的 IP 候选前误打印重试日志。
        /// </summary>
        private bool HasRemainingUsableCandidate(IReadOnlyList<BusinessRequestCandidate> candidates, int startIndex)
        {
            for (int i = startIndex; i < candidates.Count; i++)
            {
                if (candidates[i].ConnectIPAddress == null ||
                    (m_IPAddressTransport != null && m_IPAddressTransport.IsIPAddressRoutingAvailable))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 把底层错误转换为聚焦的中文日志说明，不改变原始响应内容。
        /// </summary>
        private static string DescribeFailure(HttpResponse response)
        {
            if (response == null)
            {
                return "未获得任何网络响应";
            }

            string error = response.Error ?? string.Empty;
            if (ContainsAny(error, "timeout", "timed out", "超时")) return "请求超时";
            if (ContainsAny(error, "certificate", "证书")) return "证书校验失败";
            if (ContainsAny(error, "tls", "ssl")) return "TLS 握手失败";
            if (ContainsAny(error, "dns", "resolve", "name resolution")) return "DNS 解析失败";
            if (ContainsAny(error, "connect", "connection")) return "网络连接失败";
            return string.IsNullOrWhiteSpace(error) ? "网络通信失败" : "网络通信失败：" + error;
        }

        /// <summary>
        /// 忽略大小写判断文本是否包含任一关键词。
        /// </summary>
        private static bool ContainsAny(string value, params string[] keywords)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (value.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 只将异常中明确属于 DNS、证书、TLS 或 TCP 建连阶段的错误判定为未到达服务器。
        /// </summary>
        private static HttpDeliveryState DetermineExceptionDeliveryState(Exception exception)
        {
            string message = exception?.GetBaseException().Message ?? string.Empty;
            return ContainsAny(
                message,
                "dns",
                "resolve",
                "name resolution",
                "certificate",
                "证书",
                "tls",
                "ssl",
                "failed to connect",
                "could not connect",
                "cannot connect",
                "connection refused",
                "no route to host")
                ? HttpDeliveryState.NotReachedServer
                : HttpDeliveryState.Unknown;
        }

        /// <summary>
        /// 单个业务请求候选；URL 始终保留域名，ConnectIPAddress 仅决定 TCP 连接目标。
        /// </summary>
        private readonly struct BusinessRequestCandidate
        {
            /// <summary>
            /// 初始化候选。
            /// </summary>
            internal BusinessRequestCandidate(string url, IPAddress connectIPAddress, int routeIndex)
            {
                Url = url;
                ConnectIPAddress = connectIPAddress;
                RouteIndex = routeIndex;
            }

            /// <summary>保留原域名的业务 URL。</summary>
            internal string Url { get; }

            /// <summary>指定的 TCP IPv4；null 表示使用系统 DNS。</summary>
            internal IPAddress ConnectIPAddress { get; }

            /// <summary>候选所属逻辑路线；0 为主路线，1 为备用路线。</summary>
            internal int RouteIndex { get; }

            /// <summary>供中文日志使用的主备路线名称。</summary>
            internal string RouteName => RouteIndex == 0 ? "主路线" : "备用路线";

            /// <summary>供日志显示的候选地址。</summary>
            internal string DisplayName => RouteName + "：" + (ConnectIPAddress == null
                ? Url + "（系统 DNS）"
                : Url + "（连接 IP：" + ConnectIPAddress + "）");
        }
    }
}
