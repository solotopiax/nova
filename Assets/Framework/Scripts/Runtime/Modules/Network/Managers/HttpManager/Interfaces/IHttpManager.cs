/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IHttpManager.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   HTTP管理器接口
 ***************************************************************/

using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 管理器接口，负责 HTTP 短连接请求的发送与响应处理。
    /// 返回的 HttpResponse 是引用池对象，调用方使用完毕后必须 ReferencePool.Put 归还；
    /// Put 后 Clear() 会立即清空所有字段，使用前须将所需字段拷贝到局部变量。
    /// </summary>
    public interface IHttpManager : IDownloadService
    {
        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        void Initialize(HttpManagerConfig config);

        /// <summary>
        /// 异步发送 GET 请求。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="requestTimeout">请求超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="headerInfos">请求头内容（JSON 键值对格式），传 null 表示无额外请求头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        UniTask<HttpResponse> GetAsync(string url, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null);

        /// <summary>
        /// 异步发送 POST 请求（字符串 body）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentString">请求体字符串。</param>
        /// <param name="requestTimeout">请求超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="headerInfos">请求头内容（JSON 键值对格式），传 null 表示无额外请求头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null);

        /// <summary>
        /// 异步发送 POST 请求（字节流 body，明文）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentBytes">请求体字节数组。</param>
        /// <param name="requestTimeout">请求超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="headerInfos">请求头内容（JSON 键值对格式），传 null 表示无额外请求头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null);

        /// <summary>
        /// 异步发送 POST 请求（multipart 文件上传）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="bodyJsonData">表单字段 JSON 数据，可为 null。</param>
        /// <param name="fileBytes">文件字节数组。</param>
        /// <param name="fileName">文件名。</param>
        /// <param name="requestTimeout">请求超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="headerInfos">请求头内容（JSON 键值对格式），传 null 表示无额外请求头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null);

    }
}
