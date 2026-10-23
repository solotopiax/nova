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
using System.Collections.Generic;
using System.Threading;
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
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public UniTask<HttpResponse> GetAsync(string url, float requestTimeout = -1f, string headerInfos = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            }

            return m_HttpManager.GetAsync(url, requestTimeout, headerInfos);
        }

        /// <summary>
        /// 异步发送 POST 请求（字符串 body）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentString">请求体字符串。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout = -1f, string headerInfos = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            }

            return m_HttpManager.PostAsync(url, contentString, requestTimeout, headerInfos);
        }

        /// <summary>
        /// 异步发送 POST 请求（字节流 body）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentBytes">请求体字节数组。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout = -1f, string headerInfos = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            }

            if (contentBytes == null)
            {
                throw new ArgumentNullException(nameof(contentBytes));
            }

            return m_HttpManager.PostRawDataAsync(url, contentBytes, requestTimeout, headerInfos);
        }

        /// <summary>发送可取消单URL GET；旧自定义HttpManager不支持此能力时在发送前明确拒绝。</summary>
        /// <param name="url">单一请求 URL。</param>
        /// <param name="requestTimeout">单次总超时（秒），负数使用当前默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 不附加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌；传输取消返回失败响应。</param>
        /// <returns>取消为失败响应，调用方须finally归池；不会取消已完成的服务端业务。</returns>
        public UniTask<HttpResponse> GetAsync(string url, float requestTimeout, string headerInfos, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            return RequireCancellableHttpManager().GetAsync(url, requestTimeout, headerInfos, cancellationToken);
        }

        /// <summary>发送可取消字符串POST，保留旧URL校验与编码；原参数必填以避免default调用歧义。</summary>
        /// <param name="url">单一请求 URL。</param>
        /// <param name="contentString">按 UTF-8 编码的请求体，null 按旧语义为空。</param>
        /// <param name="requestTimeout">单次总超时（秒），负数使用当前默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 不附加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌；传输取消返回失败响应。</param>
        /// <returns>调用方负责归池的成功或失败响应。</returns>
        public UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout, string headerInfos, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            return RequireCancellableHttpManager().PostAsync(url, contentString, requestTimeout, headerInfos, cancellationToken);
        }

        /// <summary>发送可取消原始字节POST，保留URL及非null字节校验，不增加主备重放。</summary>
        /// <param name="url">单一请求 URL。</param>
        /// <param name="contentBytes">原始请求体字节。</param>
        /// <param name="requestTimeout">单次总超时（秒），负数使用当前默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 不附加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌；传输取消返回失败响应。</param>
        /// <returns>调用方负责归池的成功或失败响应。</returns>
        public UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout, string headerInfos, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            if (contentBytes == null) throw new ArgumentNullException(nameof(contentBytes));
            return RequireCancellableHttpManager().PostRawDataAsync(url, contentBytes, requestTimeout, headerInfos, cancellationToken);
        }

        /// <summary>发送可取消multipart上传，字段与文件处理沿用原传输；不支持能力时不发送。</summary>
        /// <param name="url">单一请求 URL。</param>
        /// <param name="bodyJsonData">一级表单字段 JSON，可为 null。</param>
        /// <param name="fileBytes">文件内容字节。</param>
        /// <param name="fileName">上传文件名。</param>
        /// <param name="requestTimeout">单次总超时（秒），负数使用当前默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 不附加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌；传输取消返回失败响应。</param>
        /// <returns>调用方负责归池的成功或取消失败响应。</returns>
        public UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout, string headerInfos, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("Value cannot be null or empty.", nameof(url));
            if (fileBytes == null) throw new ArgumentNullException(nameof(fileBytes));
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("Value cannot be null or empty.", nameof(fileName));
            return RequireCancellableHttpManager().PostFileAsync(url, bodyJsonData, fileBytes, fileName, requestTimeout, headerInfos, cancellationToken);
        }

        /// <summary>只通过公开可选能力接口取得取消入口，不更改旧IHttpManager契约，也不以停止等待假装物理取消。</summary>
        /// <returns>支持物理取消的当前HTTP管理器。</returns>
        /// <exception cref="NotSupportedException">旧自定义HTTP管理器未实现ICancellableHttpManager，发送前拒绝。</exception>
        private ICancellableHttpManager RequireCancellableHttpManager()
        {
            if (m_HttpManager is ICancellableHttpManager cancellable) return cancellable;
            throw new NotSupportedException("当前 HTTP 管理器未实现 ICancellableHttpManager，无法取消正在进行的请求。");
        }

        /// <summary>
        /// 使用冻结的请求数据按主备地址执行业务协议 POST，切换与重试策略完全由网络底层负责。
        /// </summary>
        /// <param name="routeUrls">主域名、备用域名完整 URL，已按顺序去重。</param>
        /// <param name="routeKey">主备偏好隔离键，通常为 HostKey。</param>
        /// <param name="operationName">稳定业务操作名，通常为 NetCmd 名称。</param>
        /// <param name="contentBytes">整条尝试链复用的请求字节。</param>
        /// <param name="requestTimeout">每次尝试独享的请求超时，-1 使用默认值。</param>
        /// <param name="headerInfos">整条尝试链复用的请求头 JSON。</param>
        /// <param name="cancellationToken">取消令牌；取消后不再进入后续候选。</param>
        /// <returns>服务器正式响应，或全部网络尝试结束后的最终失败响应。</returns>
        internal UniTask<HttpResponse> PostBusinessRawDataAsync(
            IReadOnlyList<string> routeUrls,
            string routeKey,
            string operationName,
            byte[] contentBytes,
            float requestTimeout = -1f,
            string headerInfos = null,
            CancellationToken cancellationToken = default
        )
        {
            if (contentBytes == null)
            {
                throw new ArgumentNullException(nameof(contentBytes));
            }

            if (!(m_HttpManager is IBusinessHttpManager businessHttpManager))
            {
                throw new InvalidOperationException("当前 HTTP 管理器不支持业务主备路由。");
            }

            return businessHttpManager.PostBusinessRawDataAsync(
                routeUrls,
                routeKey,
                operationName,
                contentBytes,
                requestTimeout,
                headerInfos,
                cancellationToken
            );
        }

        /// <summary>
        /// 异步发送 POST 请求（multipart 文件上传）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="bodyJsonData">表单字段 JSON 数据，可为 null。</param>
        /// <param name="fileBytes">文件字节数组。</param>
        /// <param name="fileName">文件名。</param>
        /// <param name="requestTimeout">请求超时时间（秒），-1 使用默认值。</param>
        /// <param name="headerInfos">请求头 JSON 键值对，null 表示无额外头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        public UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout = -1f, string headerInfos = null)
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

            return m_HttpManager.PostFileAsync(url, bodyJsonData, fileBytes, fileName, requestTimeout, headerInfos);
        }
    }
}
