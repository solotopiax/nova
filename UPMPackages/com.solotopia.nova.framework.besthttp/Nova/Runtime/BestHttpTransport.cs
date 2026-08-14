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
using System.Net;
using System.Reflection;
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
    internal sealed partial class BestHttpTransport : IHttpTransport, IHttpIPAddressTransport, IBusinessHttpTelemetryTransport
    {
        private const string MissingSetIPAddressWarning =
            "DoH 已配置为启用，但当前 BestHTTP 未提供 SetIPAddress，无法指定连接 IP，运行时已自动禁用 DoH 并改用系统 DNS。";

#if NOVA_BEST_HTTP
        private static readonly object s_IPAddressCapabilityLock = new object();
        private static bool s_IPAddressCapabilityChecked;
        private static Action<HTTPRequest, IPAddress[]> s_SetIPAddress;
        private static string s_IPAddressRoutingUnavailableReason;
        private static readonly object s_BusinessTelemetryCapabilityLock = new object();
        private static bool s_BusinessTelemetryCapabilityChecked;
        private static Func<string, string, object> s_CreateBusinessTelemetryChain;
        private static Action<HTTPRequest, object> s_SetBusinessTelemetryChain;
        private static Action<object> s_CompleteBusinessTelemetryChain;
#endif

        private float m_RequestTimeout = 60f;
        private float m_ConnectTimeout = 20f;

        /// <summary>
        /// 当前 BestHTTP 是否具备保留原域名并指定 TCP 连接 IP 的能力。
        /// </summary>
        public bool IsIPAddressRoutingAvailable
        {
            get
            {
#if NOVA_BEST_HTTP
                EnsureIPAddressCapability();
                return s_SetIPAddress != null;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// 指定连接 IP 能力不可用时供框架输出的中文原因。
        /// </summary>
        public string IPAddressRoutingUnavailableReason
        {
            get
            {
#if NOVA_BEST_HTTP
                EnsureIPAddressCapability();
                return s_IPAddressRoutingUnavailableReason ?? MissingSetIPAddressWarning;
#else
                return MissingSetIPAddressWarning;
#endif
            }
        }

        public void Initialize(float requestTimeout, float connectTimeout)
        {
            m_RequestTimeout = requestTimeout;
            m_ConnectTimeout = connectTimeout;

            Best.TLSSecurity.SecurityOptions.OCSP.EnableOCSPQueries = false;
            Best.TLSSecurity.TLSSecurity.Setup();
#if NOVA_BEST_HTTP
            EnsureIPAddressCapability();
#endif
        }

        public bool CanUseIpCandidate(Uri uri)
        {
            return uri != null &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                   IsIPAddressRoutingAvailable;
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

        /// <summary>
        /// 保留原域名 URL，并只把底层 TCP 连接目标指定为给定 IPv4。
        /// </summary>
        public UniTask<HttpResponse> PostRawDataAsync(
            string url,
            IPAddress connectIPAddress,
            byte[] contentBytes,
            float requestTimeout,
            float connectTimeout,
            string headerInfos)
        {
#if NOVA_BEST_HTTP
            EnsureIPAddressCapability();
            if (s_SetIPAddress == null)
            {
                return UniTask.FromResult(HttpResponse.Create(
                    0,
                    null,
                    null,
                    null,
                    IPAddressRoutingUnavailableReason,
                    false,
                    0,
                    -1L,
                    HttpDeliveryState.NotReachedServer));
            }

            HTTPRequest request = HTTPRequest.CreatePost(url);
            try
            {
                s_SetIPAddress(request, new[] { connectIPAddress });
            }
            catch (Exception exception)
            {
                DisableIPAddressRouting(exception);
                return UniTask.FromResult(HttpResponse.Create(
                    0,
                    null,
                    null,
                    null,
                    s_IPAddressRoutingUnavailableReason,
                    false,
                    0,
                    -1L,
                    HttpDeliveryState.NotReachedServer));
            }

            ApplyTimeoutSettings(request, requestTimeout, connectTimeout);
            request.UploadSettings.UploadStream = new MemoryStream(contentBytes);
            request.AddHeader("Content-Type", "application/octet-stream");
            ApplyHeaderInfos(request, headerInfos, null);
            return ExecuteRequestAsync(request, url, "POST RawData 请求已取消。", "POST RawData 请求异常");
#else
            return UniTask.FromResult(HttpResponse.Create(
                0,
                null,
                null,
                null,
                MissingSetIPAddressWarning,
                false,
                0,
                -1L,
                HttpDeliveryState.NotReachedServer));
#endif
        }

        /// <summary>
        /// 创建业务主备/IP 轮换专用的整链遥测作用域。
        /// 官方 BestHTTP 缺少内部链扩展时返回 null，业务请求会保持原有传输行为。
        /// </summary>
        /// <param name="operationName">不含请求参数的业务指令名。</param>
        /// <returns>可用的整链遥测作用域；当前 BestHTTP 不支持时返回 null。</returns>
        public IBusinessHttpTelemetryScope BeginBusinessHttpTelemetry(string operationName)
        {
#if NOVA_BEST_HTTP
            EnsureBusinessTelemetryCapability();
            if (s_CreateBusinessTelemetryChain == null ||
                s_SetBusinessTelemetryChain == null ||
                s_CompleteBusinessTelemetryChain == null)
            {
                return null;
            }

            try
            {
                object chain = s_CreateBusinessTelemetryChain(Guid.NewGuid().ToString("N"), operationName ?? string.Empty);
                return chain == null
                    ? null
                    : new BestHttpBusinessTelemetryScope(chain, s_CompleteBusinessTelemetryChain);
            }
            catch
            {
                // 内部扩展异常时保持业务请求可用，按无整链遥测降级。
                return null;
            }
#else
            return null;
#endif
        }

        /// <summary>
        /// 在整链遥测作用域中发送一个主备/IP 候选请求。
        /// </summary>
        /// <param name="telemetryScope">由本传输创建的整链遥测作用域。</param>
        /// <param name="url">保留原域名的业务 URL。</param>
        /// <param name="connectIPAddress">指定 TCP 连接 IP；null 时使用系统 DNS。</param>
        /// <param name="contentBytes">冻结后的业务请求字节。</param>
        /// <param name="requestTimeout">本候选独享的请求超时。</param>
        /// <param name="connectTimeout">本候选独享的连接超时。</param>
        /// <param name="headerInfos">冻结后的请求头 JSON。</param>
        /// <returns>本候选的网络响应。</returns>
        public UniTask<HttpResponse> PostBusinessRawDataAsync(
            IBusinessHttpTelemetryScope telemetryScope,
            string url,
            IPAddress connectIPAddress,
            byte[] contentBytes,
            float requestTimeout,
            float connectTimeout,
            string headerInfos)
        {
#if NOVA_BEST_HTTP
            return PostBusinessRawDataWithTelemetryAsync(
                telemetryScope as BestHttpBusinessTelemetryScope,
                url,
                connectIPAddress,
                contentBytes,
                requestTimeout,
                connectTimeout,
                headerInfos);
#else
            return UniTask.FromResult(HttpResponse.Create(
                0,
                null,
                null,
                null,
                "BestHTTP（com.tivadar.best.http）未安装，网络传输不可用。",
                false,
                0,
                -1L));
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
