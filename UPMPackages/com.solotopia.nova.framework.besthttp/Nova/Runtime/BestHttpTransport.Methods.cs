/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BestHttpTransport.Methods.cs
 * author:    taoye
 * created:   2026/6/15
 * descrip:   BestHTTP transport adapter methods
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Best.HTTP;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.BestHTTP.Runtime
{
    internal sealed partial class BestHttpTransport
    {
        private void ApplyTimeoutSettings(HTTPRequest request, float requestTimeout, float connectTimeout)
        {
            float effectiveRequestTimeout = requestTimeout < 0f ? m_RequestTimeout : requestTimeout;
            float effectiveConnectTimeout = connectTimeout < 0f ? m_ConnectTimeout : connectTimeout;
            request.TimeoutSettings.Timeout = TimeSpan.FromSeconds(effectiveRequestTimeout);
            request.TimeoutSettings.ConnectTimeout = TimeSpan.FromSeconds(effectiveConnectTimeout);
        }

        private static void ApplyHeaderInfos(HTTPRequest request, string headerInfos, string hostHeader)
        {
            bool hasHostHeader = false;
            if (!string.IsNullOrEmpty(headerInfos))
            {
                JObject headerJson = JObject.Parse(headerInfos);
                foreach (var kvp in headerJson)
                {
                    if (string.Equals(kvp.Key, "Host", StringComparison.OrdinalIgnoreCase))
                    {
                        hasHostHeader = true;
                    }

                    request.AddHeader(kvp.Key, kvp.Value.ToString());
                }
            }

            if (!hasHostHeader && !string.IsNullOrEmpty(hostHeader))
            {
                request.AddHeader("Host", hostHeader);
            }
        }

        private static void LogRequestResult(HTTPRequest request, Best.HTTP.HTTPResponse response)
        {
            if (response == null || !response.IsSuccess)
            {
                string error = response != null ? Txt.Format("{0} {1}", response.StatusCode, response.Message) : "No response";
                Log.Warning(LogTag.Http, "请求异常：{0}，URL：{1}。", error, request.CurrentUri);
            }
        }

        private async UniTask<HttpResponse> ExecuteRequestAsync(HTTPRequest request, string url, string cancelledMessage, string exceptionLogPrefix)
        {
            try
            {
                Best.HTTP.HTTPResponse response = await request.GetHTTPResponseAsync();
                LogRequestResult(request, response);
                return BuildHttpResponse(response);
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                return HttpResponse.Create(0, null, null, null, cancelledMessage, false, 0, -1L);
            }
            catch (AsyncHTTPException e)
            {
                Log.Warning(LogTag.Http, "{0}：{1}，URL：{2}。", exceptionLogPrefix, e.Message, url);
                return HttpResponse.Create(e.StatusCode, e.Content, null, null, e.Message, false, 0, -1L);
            }
        }

        private async UniTask<HttpResponse> DownloadCoreAsync(
            string url,
            int idleTimeout,
            Action<HttpResponse> progressCallback,
            CancellationToken cancellationToken,
            string cancelledMessage,
            string exceptionLogPrefix,
            string hostHeader)
        {
            int effectiveIdleTimeout = idleTimeout < 0 ? Mathf.CeilToInt(m_RequestTimeout) : idleTimeout;

            HTTPRequest request = HTTPRequest.CreateGet(url);
            request.TimeoutSettings.ConnectTimeout = TimeSpan.FromSeconds(m_ConnectTimeout);
            request.DownloadSettings.DisableCache = true;
            if (!string.IsNullOrEmpty(hostHeader))
            {
                request.AddHeader("Host", hostHeader);
            }

            long lastDownloadedBytes = 0;
            float lastProgressTime = Time.realtimeSinceStartup;

            request.DownloadSettings.OnDownloadProgress += (HTTPRequest req, long downloaded, long total) =>
            {
                lastDownloadedBytes = downloaded;
                lastProgressTime = Time.realtimeSinceStartup;
                if (progressCallback != null)
                {
                    HttpResponse progressResp = HttpResponse.Create(0, null, null, null, null, false, downloaded, total);
                    try
                    {
                        progressCallback.Invoke(progressResp);
                    }
                    finally
                    {
                        ReferencePool.Put(progressResp);
                    }
                }
            };

            CancellationTokenSource idleCts = new CancellationTokenSource();
            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idleCts.Token);

            try
            {
                UniTask<Best.HTTP.HTTPResponse> responseTask = request.GetHTTPResponseAsync(linkedCts.Token).AsUniTask();

                while (true)
                {
                    bool completed = responseTask.Status != UniTaskStatus.Pending;
                    if (completed)
                    {
                        break;
                    }

                    if (Time.realtimeSinceStartup - lastProgressTime > effectiveIdleTimeout)
                    {
                        idleCts.Cancel();
                        request.Abort();
                        return HttpResponse.Create(0, null, null, null, Txt.Format("Idle timeout ({0}s) exceeded for URL: {1}。", effectiveIdleTimeout, url), false, lastDownloadedBytes, -1L);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, linkedCts.Token);
                }

                Best.HTTP.HTTPResponse response = await responseTask;
                return BuildHttpResponse(response);
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                return HttpResponse.Create(0, null, null, null, cancelledMessage, false, lastDownloadedBytes, -1L);
            }
            catch (AsyncHTTPException e)
            {
                Log.Warning(LogTag.Http, "{0}：{1}，URL：{2}。", exceptionLogPrefix, e.Message, url);
                return HttpResponse.Create(e.StatusCode, e.Content, null, null, e.Message, false, lastDownloadedBytes, -1L);
            }
            finally
            {
                linkedCts.Dispose();
                idleCts.Dispose();
            }
        }

        private static HttpResponse BuildHttpResponse(Best.HTTP.HTTPResponse response)
        {
            if (response == null)
            {
                return HttpResponse.Create(0, null, null, null, "No response received.", false, 0, -1L);
            }

            Dictionary<string, string> headers = null;
            if (response.Headers != null)
            {
                headers = new Dictionary<string, string>();
                foreach (var kvp in response.Headers)
                {
                    headers[kvp.Key] = kvp.Value != null && kvp.Value.Count > 0 ? kvp.Value[0] : string.Empty;
                }
            }

            long downloadedBytes = response.Data != null ? response.Data.Length : 0;
            long totalBytes = response.Data != null ? response.Data.Length : -1L;
            return HttpResponse.Create(response.StatusCode, response.DataAsText, response.Data, headers, response.IsSuccess ? null : response.Message, response.IsSuccess, downloadedBytes, totalBytes);
        }
    }
}
