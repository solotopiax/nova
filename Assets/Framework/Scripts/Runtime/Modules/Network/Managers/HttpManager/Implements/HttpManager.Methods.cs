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
            bool canUseIpCandidate = Uri.TryCreate(originalUrl, UriKind.Absolute, out Uri uri)
                                     && m_Transport != null
                                     && m_Transport.CanUseIpCandidate(uri);
            IReadOnlyList<string> candidateUrls = m_DoHManager == null
                ? new[] { originalUrl }
                : await m_DoHManager.BuildRequestUrlCandidatesAsync(originalUrl, canUseIpCandidate);
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
    }
}
