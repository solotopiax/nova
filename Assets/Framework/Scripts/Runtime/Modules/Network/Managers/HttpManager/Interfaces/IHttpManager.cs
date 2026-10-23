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
using System.Collections.Generic;
using System.Threading;

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
        /// <param name="headerInfos">请求头内容（JSON 键值对格式），传 null 表示无额外请求头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        UniTask<HttpResponse> GetAsync(string url, float requestTimeout = -1f, string headerInfos = null);

        /// <summary>
        /// 异步发送 POST 请求（字符串 body）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentString">请求体字符串。</param>
        /// <param name="requestTimeout">请求超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="headerInfos">请求头内容（JSON 键值对格式），传 null 表示无额外请求头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout = -1f, string headerInfos = null);

        /// <summary>
        /// 异步发送 POST 请求（字节流 body，明文）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="contentBytes">请求体字节数组。</param>
        /// <param name="requestTimeout">请求超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="headerInfos">请求头内容（JSON 键值对格式），传 null 表示无额外请求头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout = -1f, string headerInfos = null);

        /// <summary>
        /// 异步发送 POST 请求（multipart 文件上传）。
        /// </summary>
        /// <param name="url">请求 URL。</param>
        /// <param name="bodyJsonData">表单字段 JSON 数据，可为 null。</param>
        /// <param name="fileBytes">文件字节数组。</param>
        /// <param name="fileName">文件名。</param>
        /// <param name="requestTimeout">请求超时时间（秒），传 -1 使用默认值。</param>
        /// <param name="headerInfos">请求头内容（JSON 键值对格式），传 null 表示无额外请求头。</param>
        /// <returns>包含响应数据的 HttpResponse。</returns>
        UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout = -1f, string headerInfos = null);

    }

    /// <summary>
    /// 可选的普通单URL物理取消能力；既有IHttpManager实现无需增加成员。
    /// 新重载的原参数全部必填，避免既有default字面量调用发生重载歧义。
    /// 取消返回失败HttpResponse，调用方仍须finally归池；不能撤销服务端已执行状态。
    /// </summary>
    public interface ICancellableHttpManager
    {
        /// <summary>发送可取消GET；请求前已取消则不发送，进行中取消在Update轮询后Abort并返回失败响应。</summary>
        /// <param name="url">单一请求URL。</param>
        /// <param name="requestTimeout">单次总超时，-1使用默认值。</param>
        /// <param name="headerInfos">请求头JSON，null不添加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌。</param>
        /// <returns>调用方必须归池的成功或失败响应，不以OperationCanceledException表示传输取消。</returns>
        UniTask<HttpResponse> GetAsync(string url, float requestTimeout, string headerInfos, CancellationToken cancellationToken);

        /// <summary>发送可取消字符串POST，沿用原UTF8与请求头语义；取消不代表服务端回滚。</summary>
        /// <param name="url">单一请求URL。</param>
        /// <param name="contentString">UTF8请求体，null按原语义为空。</param>
        /// <param name="requestTimeout">单次总超时，-1使用默认值。</param>
        /// <param name="headerInfos">请求头JSON，null不添加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌。</param>
        /// <returns>调用方必须归池的响应，传输取消返回失败而非抛取消异常。</returns>
        UniTask<HttpResponse> PostAsync(string url, string contentString, float requestTimeout, string headerInfos, CancellationToken cancellationToken);

        /// <summary>发送可取消原始字节POST；同一调用只有一个物理请求，不增加主备或重试。</summary>
        /// <param name="url">单一请求URL。</param>
        /// <param name="contentBytes">原始请求体字节。</param>
        /// <param name="requestTimeout">单次总超时，-1使用默认值。</param>
        /// <param name="headerInfos">请求头JSON，null不添加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌。</param>
        /// <returns>调用方必须归池的成功或失败响应。</returns>
        UniTask<HttpResponse> PostRawDataAsync(string url, byte[] contentBytes, float requestTimeout, string headerInfos, CancellationToken cancellationToken);

        /// <summary>发送可取消multipart上传；字段编码和文件语义与原入口一致。</summary>
        /// <param name="url">单一请求URL。</param>
        /// <param name="bodyJsonData">一级表单字段JSON，可为null。</param>
        /// <param name="fileBytes">文件字节。</param>
        /// <param name="fileName">文件名。</param>
        /// <param name="requestTimeout">单次总超时，-1使用默认值。</param>
        /// <param name="headerInfos">请求头JSON，null不添加额外头。</param>
        /// <param name="cancellationToken">调用方拥有的取消令牌。</param>
        /// <returns>调用方必须归池的响应，取消后保留既有交付状态与已收到内容。</returns>
        UniTask<HttpResponse> PostFileAsync(string url, string bodyJsonData, byte[] fileBytes, string fileName, float requestTimeout, string headerInfos, CancellationToken cancellationToken);
    }

    /// <summary>
    /// HostKey + NetCmd 业务协议专用的主备路由入口，不暴露给普通 HTTP、资源或第三方 SDK 调用。
    /// </summary>
    internal interface IBusinessHttpManager
    {
        /// <summary>
        /// 使用同一份请求数据按主备域名顺序发送原始字节 POST。
        /// </summary>
        /// <param name="routeUrls">主域名、备用域名完整 URL，已按顺序去重。</param>
        /// <param name="routeKey">主备偏好隔离键，通常为 HostKey。</param>
        /// <param name="operationName">稳定业务操作名，通常为 NetCmd 名称。</param>
        /// <param name="contentBytes">整条尝试链复用的原始请求字节。</param>
        /// <param name="requestTimeout">每次尝试独享的请求超时。</param>
        /// <param name="headerInfos">整条尝试链复用的请求头 JSON。</param>
        /// <param name="cancellationToken">取消令牌；取消后立即终止当前请求且不再切换候选。</param>
        UniTask<HttpResponse> PostBusinessRawDataAsync(
            IReadOnlyList<string> routeUrls,
            string routeKey,
            string operationName,
            byte[] contentBytes,
            float requestTimeout,
            string headerInfos,
            CancellationToken cancellationToken
        );
    }

    /// <summary>
    /// 模块内部使用的单次物理 HTTP 入口；不自动创建独立埋点链，供上层自行编排主备链。
    /// </summary>
    internal interface IPhysicalHttpManager
    {
        /// <summary>
        /// 发送一次可取消 GET，并原样返回该次物理请求结果。
        /// </summary>
        UniTask<HttpResponse> GetPhysicalAsync(
            string url,
            float requestTimeout,
            string headerInfos,
            CancellationToken cancellationToken);
    }
}
