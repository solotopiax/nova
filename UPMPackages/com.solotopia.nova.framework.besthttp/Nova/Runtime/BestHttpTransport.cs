/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BestHttpTransport.cs
 * author:    taoye
 * created:   2026/6/15
 * descrip:   BestHTTP transport adapter
 ***************************************************************/

using System;
using System.IO;
using System.Threading;
#if NOVA_BEST_HTTP
using Best.HTTP;
using Best.HTTP.Request.Upload.Forms;
#endif
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;

namespace NovaFramework.BestHTTP.Runtime
{
    internal sealed partial class BestHttpTransport : IHttpTransport
    {
        private float m_RequestTimeout = 60f;
        private float m_ConnectTimeout = 20f;

        public void Initialize(float requestTimeout, float connectTimeout)
        {
            m_RequestTimeout = requestTimeout;
            m_ConnectTimeout = connectTimeout;

            Best.TLSSecurity.SecurityOptions.OCSP.EnableOCSPQueries = false;
            Best.TLSSecurity.TLSSecurity.Setup();
        }

        public bool CanUseIpCandidate(Uri uri)
        {
            return uri != null && uri.Scheme == Uri.UriSchemeHttp;
        }

        public UniTask<HttpResponse> GetAsync(string url, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
#if NOVA_BEST_HTTP
            HTTPRequest request = HTTPRequest.CreateGet(url);
            request.DownloadSettings.DisableCache = true;
            ApplyTimeoutSettings(request, requestTimeout, connectTimeout);
            ApplyHeaderInfos(request, headerInfos, hostHeader);
            return ExecuteRequestAsync(request, url, "GET 请求已取消。", "GET 请求异常");
#else
            return UniTask.FromResult(HttpResponse.Create(0, null, null, null, "BestHTTP（com.tivadar.best.http）未安装，网络传输不可用。", false, 0, -1L));
#endif
        }

        public UniTask<HttpResponse> PostAsync(string url, byte[] bodyBytes, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
#if NOVA_BEST_HTTP
            HTTPRequest request = HTTPRequest.CreatePost(url);
            ApplyTimeoutSettings(request, requestTimeout, connectTimeout);
            request.UploadSettings.UploadStream = new MemoryStream(bodyBytes ?? Array.Empty<byte>());
            request.AddHeader("Content-Type", "application/octet-stream");
            ApplyHeaderInfos(request, headerInfos, hostHeader);
            return ExecuteRequestAsync(request, url, "POST 请求已取消。", "POST 请求异常");
#else
            return UniTask.FromResult(HttpResponse.Create(0, null, null, null, "BestHTTP（com.tivadar.best.http）未安装，网络传输不可用。", false, 0, -1L));
#endif
        }

        public UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
#if NOVA_BEST_HTTP
            HTTPRequest request = HTTPRequest.CreatePost(url);
            ApplyTimeoutSettings(request, requestTimeout, connectTimeout);
            request.UploadSettings.UploadStream = new MemoryStream(contentBytes);
            request.AddHeader("Content-Type", "application/octet-stream");
            ApplyHeaderInfos(request, headerInfos, hostHeader);
            return ExecuteRequestAsync(request, url, "POST RawData 请求已取消。", "POST RawData 请求异常");
#else
            return UniTask.FromResult(HttpResponse.Create(0, null, null, null, "BestHTTP（com.tivadar.best.http）未安装，网络传输不可用。", false, 0, -1L));
#endif
        }

        public UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader)
        {
#if NOVA_BEST_HTTP
            HTTPRequest request = HTTPRequest.CreatePost(url);
            ApplyTimeoutSettings(request, requestTimeout, connectTimeout);

            MultipartFormDataStream formStream = new MultipartFormDataStream();
            if (!string.IsNullOrEmpty(bodyJsonData))
            {
                JObject jObject = JObject.Parse(bodyJsonData);
                foreach (var kvp in jObject)
                {
                    formStream.AddField(kvp.Key, kvp.Value.ToString());
                }
            }

            formStream.AddStreamField("file", new MemoryStream(fileBytes), fileName, "multipart/form-data");
            request.UploadSettings.UploadStream = formStream;
            ApplyHeaderInfos(request, headerInfos, hostHeader);
            return ExecuteRequestAsync(request, url, "POST File 请求已取消。", "POST File 请求异常");
#else
            return UniTask.FromResult(HttpResponse.Create(0, null, null, null, "BestHTTP（com.tivadar.best.http）未安装，网络传输不可用。", false, 0, -1L));
#endif
        }

        public UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout, Action<HttpResponse> progressCallback, CancellationToken cancellationToken, string hostHeader)
        {
#if NOVA_BEST_HTTP
            return DownloadCoreAsync(url, idleTimeout, progressCallback, cancellationToken, "Download cancelled.", "下载二进制异常", hostHeader);
#else
            return UniTask.FromResult(HttpResponse.Create(0, null, null, null, "BestHTTP（com.tivadar.best.http）未安装，网络传输不可用。", false, 0, -1L));
#endif
        }

        public UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout, Action<HttpResponse> progressCallback, CancellationToken cancellationToken, string hostHeader)
        {
#if NOVA_BEST_HTTP
            return DownloadCoreAsync(url, idleTimeout, progressCallback, cancellationToken, "Download text cancelled.", "DownloadTextAsync 失败", hostHeader);
#else
            return UniTask.FromResult(HttpResponse.Create(0, null, null, null, "BestHTTP（com.tivadar.best.http）未安装，网络传输不可用。", false, 0, -1L));
#endif
        }

        public void Shutdown()
        {
        }
    }
}
