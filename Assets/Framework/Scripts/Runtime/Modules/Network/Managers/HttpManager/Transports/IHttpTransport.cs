using System;
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 后端扩展点。供可选传输程序集实现，业务层应继续通过 Nova.Network 调用 HTTP API。
    /// </summary>
    public interface IHttpTransport
    {
        /// <summary>
        /// 初始化传输后端。
        /// </summary>
        /// <param name="requestTimeout">默认请求超时时间（秒）。</param>
        /// <param name="connectTimeout">默认连接超时时间（秒）。</param>
        void Initialize(float requestTimeout, float connectTimeout);

        /// <summary>
        /// 当前传输是否能在保持原始 Host / SNI 语义的前提下使用 IP 候选地址。
        /// </summary>
        bool CanUseIpCandidate(Uri uri);

        /// <summary>
        /// 异步发送 GET 请求。
        /// </summary>
        UniTask<HttpResponse> GetAsync(string url, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader);

        /// <summary>
        /// 异步发送 POST 请求（字符串 body 已由调用方转为字节数组）。
        /// </summary>
        UniTask<HttpResponse> PostAsync(string url, byte[] bodyBytes, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader);

        /// <summary>
        /// 异步发送 POST 请求（原始字节 body）。
        /// </summary>
        UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader);

        /// <summary>
        /// 异步发送 multipart 文件上传请求。
        /// </summary>
        UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout, float connectTimeout, string headerInfos, string hostHeader);

        /// <summary>
        /// 异步下载二进制数据。
        /// </summary>
        UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout, Action<HttpResponse> progressCallback, CancellationToken cancellationToken, string hostHeader);

        /// <summary>
        /// 异步下载文本内容。
        /// </summary>
        UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout, Action<HttpResponse> progressCallback, CancellationToken cancellationToken, string hostHeader);

        /// <summary>
        /// 关闭传输后端并释放后端持有的状态。
        /// </summary>
        void Shutdown();
    }

    /// <summary>
    /// 可选的业务 HTTPS 指定连接 IP 能力；URL 始终保留原域名，IP 仅用于底层 TCP 连接。
    /// 未实现该接口的传输继续使用系统 DNS，不影响原有 IHttpTransport 兼容性。
    /// </summary>
    public interface IHttpIPAddressTransport
    {
        /// <summary>
        /// 当前运行环境是否具备指定连接 IP 的能力。
        /// </summary>
        bool IsIPAddressRoutingAvailable { get; }

        /// <summary>
        /// 能力不可用时供 Nova 输出的一次性中文原因。
        /// </summary>
        string IPAddressRoutingUnavailableReason { get; }

        /// <summary>
        /// 以原域名 URL 发送业务原始字节 POST，并仅把 TCP 连接目标指定为给定 IPv4。
        /// </summary>
        UniTask<HttpResponse> PostRawDataAsync(
            string url,
            IPAddress connectIPAddress,
            byte[] contentBytes,
            float requestTimeout,
            float connectTimeout,
            string headerInfos);
    }

    /// <summary>
    /// 可选的业务整链遥测扩展点。
    /// 仅 HostKey + NetCmd 业务请求会使用它；未实现时普通 HTTP、UnityWebRequest 和第三方传输保持原有行为。
    /// </summary>
    public interface IBusinessHttpTelemetryTransport
    {
        /// <summary>
        /// 创建一条业务请求链的遥测作用域。
        /// </summary>
        /// <param name="operationName">不含参数和敏感数据的业务操作名。</param>
        /// <returns>支持时返回作用域；不支持时返回 null。</returns>
        IBusinessHttpTelemetryScope BeginBusinessHttpTelemetry(string operationName);

        /// <summary>
        /// 在指定业务遥测作用域中发送一个主备/IP 候选请求。
        /// URL 始终保留原域名；connectIPAddress 为 null 时按系统 DNS 发送。
        /// </summary>
        /// <param name="telemetryScope">由 <see cref="BeginBusinessHttpTelemetry"/> 创建的作用域。</param>
        /// <param name="url">原域名业务 URL。</param>
        /// <param name="connectIPAddress">指定 TCP 连接 IP；null 表示系统 DNS。</param>
        /// <param name="contentBytes">冻结后的原始请求字节。</param>
        /// <param name="requestTimeout">本候选独享的请求超时。</param>
        /// <param name="connectTimeout">本候选独享的连接超时。</param>
        /// <param name="headerInfos">冻结后的请求头 JSON。</param>
        /// <returns>本候选的网络响应。</returns>
        UniTask<HttpResponse> PostBusinessRawDataAsync(
            IBusinessHttpTelemetryScope telemetryScope,
            string url,
            IPAddress connectIPAddress,
            byte[] contentBytes,
            float requestTimeout,
            float connectTimeout,
            string headerInfos);
    }

    /// <summary>
    /// 业务整链遥测作用域。
    /// 调用方应在所有候选结束后调用 <see cref="Complete"/>；<see cref="Dispose"/> 也必须自动收口，避免 return 或异常出口遗漏最终事件。
    /// </summary>
    public interface IBusinessHttpTelemetryScope : IDisposable
    {
        /// <summary>
        /// 标记当前业务请求链已结束，并由传输适配层输出唯一最终 end 事件。
        /// </summary>
        void Complete();
    }
}
