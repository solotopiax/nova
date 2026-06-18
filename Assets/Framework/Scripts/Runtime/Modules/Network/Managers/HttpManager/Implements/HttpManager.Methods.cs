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
        /// 按 DoH 缓存结果构造请求候选地址，并在单次调用内按 IP -> 原始域名顺序自动重试。
        /// </summary>
        /// <param name="originalUrl">原始请求 URL。</param>
        /// <param name="sendRequestAsync">执行单次请求的委托。</param>
        /// <param name="requestTag">请求类型标签，用于日志。</param>
        /// <returns>最终请求结果；若全部候选都失败，返回最后一次失败响应。</returns>
        private async UniTask<HttpResponse> ExecuteDoHResilientAsync(string originalUrl, Func<string, UniTask<HttpResponse>> sendRequestAsync, string requestTag)
        {
            List<string> candidateUrls = await BuildRequestUrlCandidatesAsync(originalUrl);
            HttpResponse lastFailedResponse = null;

            for (int i = 0; i < candidateUrls.Count; i++)
            {
                string requestUrl = candidateUrls[i];
                HttpResponse response = await sendRequestAsync(requestUrl);
                if (response != null && response.IsSuccess)
                {
                    if (lastFailedResponse != null)
                    {
                        ReferencePool.Put(lastFailedResponse);
                    }

                    return response;
                }

                if (i < candidateUrls.Count - 1)
                {
                    Log.Warning(
                        LogTag.Http,
                        "{0} 请求失败，准备尝试下一个候选地址。原始 URL：{1}，当前 URL：{2}，错误：{3}。",
                        requestTag,
                        originalUrl,
                        requestUrl,
                        response?.Error ?? "Unknown");
                }

                if (lastFailedResponse != null)
                {
                    ReferencePool.Put(lastFailedResponse);
                }

                lastFailedResponse = response;
            }

            return lastFailedResponse ?? HttpResponse.Create(0, null, null, null, Txt.Format("{0} 请求失败，且未生成可用响应。", requestTag), false, 0, -1L);
        }

        /// <summary>
        /// 根据 DoH 缓存与即时查询结果构造请求候选 URL，顺序为“缓存 IP 列表 -> 原始 URL”。
        /// </summary>
        /// <param name="originalUrl">原始请求 URL。</param>
        /// <returns>候选 URL 列表。</returns>
        private async UniTask<List<string>> BuildRequestUrlCandidatesAsync(string originalUrl)
        {
            List<string> candidateUrls = new List<string>();
            HashSet<string> uniqueUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!CanUseDoH(originalUrl, out string hostName))
            {
                uniqueUrls.Add(originalUrl);
                candidateUrls.Add(originalUrl);
                return candidateUrls;
            }

            IPAddress[] cachedIPs = m_DoHManager.GetIPAddresses(hostName);
            if (cachedIPs == null || cachedIPs.Length == 0)
            {
                await m_DoHManager.DNSQuery(originalUrl);
                cachedIPs = m_DoHManager.GetIPAddresses(hostName);
            }

            if (cachedIPs != null)
            {
                for (int i = 0; i < cachedIPs.Length; i++)
                {
                    if (TryBuildUrlWithIPAddress(originalUrl, cachedIPs[i], out string resolvedUrl) && uniqueUrls.Add(resolvedUrl))
                    {
                        candidateUrls.Add(resolvedUrl);
                    }
                }
            }

            if (uniqueUrls.Add(originalUrl))
            {
                candidateUrls.Add(originalUrl);
            }

            return candidateUrls;
        }

        /// <summary>
        /// 判断当前 URL 是否适合走 DoH 解析。
        /// </summary>
        /// <param name="url">原始请求 URL。</param>
        /// <param name="hostName">解析出的主机名。</param>
        /// <returns>是否允许尝试 DoH。</returns>
        private bool CanUseDoH(string url, out string hostName)
        {
            hostName = null;
            if (m_DoHManager == null || string.IsNullOrEmpty(url))
            {
                return false;
            }

            hostName = m_DoHManager.GetHostName(url);
            if (string.IsNullOrEmpty(hostName) || string.Equals(hostName, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !IPAddress.TryParse(hostName, out _);
        }

        /// <summary>
        /// 用指定 IP 替换请求 URL 的 host 部分，保留协议、端口、路径与查询字符串不变。
        /// </summary>
        /// <param name="url">原始请求 URL。</param>
        /// <param name="ipAddress">目标 IP。</param>
        /// <param name="resolvedUrl">替换后的 URL。</param>
        /// <returns>是否替换成功。</returns>
        private static bool TryBuildUrlWithIPAddress(string url, IPAddress ipAddress, out string resolvedUrl)
        {
            resolvedUrl = null;
            if (ipAddress == null || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return false;
            }

            UriBuilder builder = new UriBuilder(uri)
            {
                Host = ipAddress.ToString()
            };

            resolvedUrl = builder.Uri.AbsoluteUri;
            return true;
        }
    }
}
