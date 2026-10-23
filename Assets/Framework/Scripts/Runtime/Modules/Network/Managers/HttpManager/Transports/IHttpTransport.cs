using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 传输内部契约。
    /// </summary>
    internal interface IHttpTransport
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

        /// <summary>发送带物理取消令牌的已编码字符串POST，保留旧四参数内部调用。</summary>
        /// <param name="url">单一请求 URL。</param>
        /// <param name="bodyBytes">已编码的请求体字节，沿用原 Content-Type。</param>
        /// <param name="requestTimeout">单次总超时（秒），负数使用当前默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 不附加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌；传输取消返回失败响应。</param>
        UniTask<HttpResponse> PostAsync(string url, byte[] bodyBytes, float requestTimeout, string headerInfos, CancellationToken cancellationToken);

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

        /// <summary>发送带物理取消令牌的multipart上传，保留旧六参数内部调用。</summary>
        /// <param name="url">单一请求 URL。</param>
        /// <param name="bodyJsonData">一级表单字段 JSON，可为 null。</param>
        /// <param name="fileBytes">文件内容字节。</param>
        /// <param name="fileName">上传文件名。</param>
        /// <param name="requestTimeout">单次总超时（秒），负数使用当前默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 不附加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌；传输取消返回失败响应。</param>
        UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout, string headerInfos, CancellationToken cancellationToken);

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
