/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponent.Http.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network组件 —— HTTP接口
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 组件 —— HTTP 短连接接口。
    /// </summary>
    public sealed partial class NetworkComponent : FrameworkComponent
    {
        /// <summary>
        /// 异步发送 GET 请求。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public UniTask<HttpResponse> GetAsync(string url, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            }

            return m_HttpManager.GetAsync(url, requestTimeout, connectTimeout, headerInfos);
        }

        /// <summary>
        /// 异步发送 POST 请求（字符串 body）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentString">请求体字符串。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            }

            return m_HttpManager.PostAsync(url, contentString, requestTimeout, connectTimeout, headerInfos);
        }

        /// <summary>
        /// 异步发送 POST 请求（字节流 body）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentBytes">请求体字节数组。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            }

            if (contentBytes == null)
            {
                throw new ArgumentNullException(nameof(contentBytes));
            }

            return m_HttpManager.PostRawDataAsync(url, contentBytes, requestTimeout, connectTimeout, headerInfos);
        }

        /// <summary>
        /// 异步发送 POST 请求（multipart 文件上传）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="bodyJsonData">表单字段 JSON 数据，可为 null。</param>
        /// <param name="fileBytes">文件字节数组。</param>
        /// <param name="fileName">文件名。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="connectTimeout">连接超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout = -1f, float connectTimeout = -1f, string headerInfos = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            }

            if (fileBytes == null)
            {
                throw new ArgumentNullException(nameof(fileBytes));
            }

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(fileName));
            }

            return m_HttpManager.PostFileAsync(url, bodyJsonData, fileBytes, fileName, requestTimeout, connectTimeout, headerInfos);
        }
    }
}
