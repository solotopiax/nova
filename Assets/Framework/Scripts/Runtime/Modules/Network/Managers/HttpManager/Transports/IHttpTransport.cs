using System;
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
}
