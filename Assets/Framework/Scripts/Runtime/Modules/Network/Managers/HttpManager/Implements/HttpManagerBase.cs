/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpManagerBase.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   HTTP管理器基类
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器基类。
    /// </summary>
    internal abstract class HttpManagerBase : FrameworkManager, IHttpManager
    {
        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        /// <remarks>值越小优先级越高，越先 Update、越后 Shutdown。</remarks>
        public override int Priority => 8;

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public abstract void Initialize(HttpManagerConfig config);

        /// <summary>
        /// 异步发送 GET 请求。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="requestTimeout">请求超时时间（秒）。</param>
        /// <param name="connectTimeout">连接超时时间（秒）。</param>
        /// <param name="headerInfos">请求头（JSON 键值对格式）。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public abstract UniTask<HttpResponse> GetAsync(string url, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null);

        /// <summary>
        /// 异步发送 POST 请求（字符串 body）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentString">请求体字符串。</param>
        /// <param name="requestTimeout">请求超时时间（秒）。</param>
        /// <param name="connectTimeout">连接超时时间（秒）。</param>
        /// <param name="headerInfos">请求头（JSON 键值对格式）。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public abstract UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null);

        /// <summary>
        /// 异步发送 POST 请求（字节流 body）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentBytes">请求体字节数组。</param>
        /// <param name="requestTimeout">请求超时时间（秒）。</param>
        /// <param name="connectTimeout">连接超时时间（秒）。</param>
        /// <param name="headerInfos">请求头（JSON 键值对格式）。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public abstract UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null);

        /// <summary>
        /// 异步发送 POST 请求（multipart 文件上传）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="bodyJsonData">表单字段 JSON 数据。</param>
        /// <param name="fileBytes">文件字节数组。</param>
        /// <param name="fileName">文件名。</param>
        /// <param name="requestTimeout">请求超时时间（秒）。</param>
        /// <param name="connectTimeout">连接超时时间（秒）。</param>
        /// <param name="headerInfos">请求头（JSON 键值对格式）。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public abstract UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null);

        /// <summary>
        /// 异步下载二进制数据。
        /// </summary>
        /// <param name="url">下载地址。</param>
        /// <param name="idleTimeout">空闲超时时间（秒）。</param>
        /// <param name="progressCallback">下载进度回调。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含下载结果的 HttpResponse。</returns>
        public abstract UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步下载文本内容，支持空闲超时与进度回调。
        /// </summary>
        /// <param name="url">下载地址。</param>
        /// <param name="idleTimeout">空闲超时时间（秒）。</param>
        /// <param name="progressCallback">下载进度回调。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含下载结果的 HttpResponse。</returns>
        public abstract UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();
    }
}
