/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IDownloadService.cs
 * author:    taoye
 * created:   2026/4/7
 * descrip:   下载服务接口
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 下载服务接口，提供二进制文件下载与文本下载能力。
    /// </summary>
    public interface IDownloadService
    {
        /// <summary>
        /// 异步下载二进制数据。
        /// </summary>
        /// <param name="url">下载地址。</param>
        /// <param name="idleTimeout">空闲超时时间（秒），连续 N 秒无新字节时中止，传 -1 使用默认值。</param>
        /// <param name="progressCallback">下载进度回调，参数为包含已下载字节数与总字节数的 HttpResponse（中间态，IsSuccess 为 false）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含下载结果的 HttpResponse。</returns>
        UniTask<HttpResponse> DownloadBinaryAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步下载文本内容，支持空闲超时与进度回调。
        /// </summary>
        /// <param name="url">下载地址。</param>
        /// <param name="idleTimeout">空闲超时时间（秒），连续 N 秒无新字节时中止，传 -1 使用默认值。</param>
        /// <param name="progressCallback">下载进度回调，参数为包含已下载字节数与总字节数的 HttpResponse（中间态，IsSuccess 为 false）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>包含下载结果的 HttpResponse。</returns>
        UniTask<HttpResponse> DownloadTextAsync(string url, int idleTimeout = -1, Action<HttpResponse> progressCallback = null, CancellationToken cancellationToken = default);
    }
}
