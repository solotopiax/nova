/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAssetDownloader.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   通用资源下载器接口
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 资源下载器，封装底层资源框架的下载操作。
    /// </summary>
    public interface IAssetDownloader
    {
        /// <summary>
        /// 总文件数。
        /// </summary>
        int TotalCount { get; }

        /// <summary>
        /// 总字节数。
        /// </summary>
        long TotalBytes { get; }

        /// <summary>
        /// 已完成文件数。
        /// </summary>
        int FinishedCount { get; }

        /// <summary>
        /// 已完成字节数。
        /// </summary>
        long FinishedBytes { get; }

        /// <summary>
        /// 进度 0~1。
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// 是否空下载（TotalCount == 0）。
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// 当前下载速率（bytes/s），基于约 500ms 采样窗口差分估算。
        /// 下载未启动或已结束时返回 0。
        /// </summary>
        long DownloadSpeed { get; }

        /// <summary>
        /// 切片来源描述，标识本下载器覆盖范围，仅用于日志与 UI 展示。
        /// 整包为 "all"；按 tag 为 "tags:a,b"；按 Asset 地址为 "locations:N"。
        /// </summary>
        string Scope { get; }

        /// <summary>
        /// 进度回调，参数 (finishedCount, totalCount, finishedBytes, totalBytes)。
        /// </summary>
        event Action<int, int, long, long> OnProgress;

        /// <summary>
        /// 单文件开始下载回调，参数 (bundleName, fileName, fileSize)。
        /// </summary>
        event Action<string, string, long> OnFileStarted;

        /// <summary>
        /// 启动下载并等待完成。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 成功，false 失败。</returns>
        UniTask<bool> RunAsync(CancellationToken ct = default);

        /// <summary>
        /// 取消下载。
        /// </summary>
        void Cancel();
    }
}
