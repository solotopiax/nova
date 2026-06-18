/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HttpResponse.cs
 * author:    taoye
 * created:   2026/4/7
 * descrip:   HTTP响应数据
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 响应数据，封装请求完成后的状态码、正文、原始字节、响应头及错误信息。
    /// 🔴 红线：本类是 IReference，由 Create 从 ReferencePool 获取。
    /// 调用方持有 return value 的 IHttpManager.XxxAsync 必须用 try/finally 在使用完后
    /// ReferencePool.Put(response) 归还；归还前后字段值会被 Clear() 清空，
    /// 需要的字段必须在 Put 之前拷贝到局部变量。
    /// </summary>
    public sealed class HttpResponse : IReference
    {
        /// <summary>
        /// 初始化 HttpResponse 的新实例（ReferencePool 要求的公开空参构造器）。
        /// </summary>
        public HttpResponse()
        {
            TotalBytes = -1;
        }

        /// <summary>
        /// 从引用池中获取 HttpResponse 并初始化所有字段。
        /// </summary>
        /// <param name="statusCode">HTTP 状态码。</param>
        /// <param name="body">响应正文字符串。</param>
        /// <param name="rawData">响应原始字节数组。</param>
        /// <param name="headers">响应头键值对字典。</param>
        /// <param name="error">错误描述，无错误时为 null。</param>
        /// <param name="isSuccess">请求是否成功。</param>
        /// <param name="downloadedBytes">已下载的字节数。</param>
        /// <param name="totalBytes">总字节数，未知时为 -1。</param>
        /// <returns>初始化完成的 HttpResponse 实例。</returns>
        public static HttpResponse Create(int statusCode, string body, byte[] rawData, Dictionary<string, string> headers, string error, bool isSuccess, long downloadedBytes, long totalBytes)
        {
            HttpResponse resp = ReferencePool.Get<HttpResponse>();
            resp.StatusCode = statusCode;
            resp.Body = body;
            resp.RawData = rawData;
            resp.Headers = headers;
            resp.Error = error;
            resp.IsSuccess = isSuccess;
            resp.DownloadedBytes = downloadedBytes;
            resp.TotalBytes = totalBytes;
            return resp;
        }

        /// <summary>
        /// HTTP 状态码（如 200、404、500 等）。
        /// </summary>
        public int StatusCode { get; private set; }

        /// <summary>
        /// 响应正文字符串，请求失败或无正文时为 null。
        /// </summary>
        public string Body { get; private set; }

        /// <summary>
        /// 响应原始字节数组，请求失败或无正文时为 null。
        /// </summary>
        public byte[] RawData { get; private set; }

        /// <summary>
        /// 响应头键值对字典，请求失败或无响应头时可能为 null。
        /// </summary>
        public Dictionary<string, string> Headers { get; private set; }

        /// <summary>
        /// 错误描述文本，请求成功时为 null。
        /// </summary>
        public string Error { get; private set; }

        /// <summary>
        /// 请求是否成功完成。
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// 已下载的字节数。
        /// </summary>
        public long DownloadedBytes { get; private set; }

        /// <summary>
        /// 总字节数，未知时为 -1。
        /// </summary>
        public long TotalBytes { get; private set; }

        /// <summary>
        /// 下载进度比例，范围 0 到 1，总字节数未知或为零时返回 0。
        /// </summary>
        public float DownloadProgress => TotalBytes > 0 ? Math.Max(0f, Math.Min(1f, (float)DownloadedBytes / TotalBytes)) : 0f;

        /// <summary>
        /// 清理引用，将所有字段重置为默认值。
        /// </summary>
        public void Clear()
        {
            StatusCode = 0;
            Body = null;
            RawData = null;
            Headers = null;
            Error = null;
            IsSuccess = false;
            DownloadedBytes = 0;
            TotalBytes = -1;
        }
    }
}
