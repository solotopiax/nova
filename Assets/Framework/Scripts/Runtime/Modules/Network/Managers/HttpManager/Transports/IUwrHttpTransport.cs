using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UnityWebRequest 传输内部契约。
    /// </summary>
    internal interface IUwrHttpTransport
    {
        /// <summary>
        /// 初始化传输后端。
        /// </summary>
        /// <param name="requestTimeout">默认请求超时时间（秒）。</param>
        void Initialize(float requestTimeout);

        /// <summary>
        /// 异步发送 GET 请求。
        /// </summary>
        UniTask<HttpResponse> GetAsync(
            string url,
            float requestTimeout,
            string headerInfos,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步发送 POST 请求（字符串 body 已由调用方转为字节数组）。
        /// </summary>
        UniTask<HttpResponse> PostAsync(string url, byte[] bodyBytes, float requestTimeout, string headerInfos);

        /// <summary>
        /// 异步发送 POST 请求（原始字节 body）。
        /// </summary>
        UniTask<HttpResponse> PostRawDataAsync(
            string url,
            byte[] contentBytes,
            float requestTimeout,
            string headerInfos,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步发送 multipart 文件上传请求。
        /// </summary>
        UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout, string headerInfos);

        /// <summary>
        /// 异步下载二进制数据。
        /// </summary>
        UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout, Action<HttpResponse> progressCallback, CancellationToken cancellationToken);

        /// <summary>
        /// 异步下载文本内容。
        /// </summary>
        UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout, Action<HttpResponse> progressCallback, CancellationToken cancellationToken);

        /// <summary>
        /// 关闭传输后端并释放后端持有的状态。
        /// </summary>
        void Shutdown();
    }
}
