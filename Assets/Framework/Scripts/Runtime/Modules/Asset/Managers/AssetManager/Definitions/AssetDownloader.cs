/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetDownloader.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   包装 YooAsset ResourceDownloaderOperation，实现 IAssetDownloader
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 资源下载器，包装 YooAsset ResourceDownloaderOperation 并暴露为 IAssetDownloader。
    /// </summary>
    internal sealed class AssetDownloader : IAssetDownloader
    {
        /// <summary>
        /// 速率采样最小间隔（秒）。
        /// </summary>
        private const float c_SpeedSampleInterval = 0.5f;

        /// <summary>
        /// 被包装的 YooAsset 下载操作实例。
        /// </summary>
        private readonly ResourceDownloaderOperation m_Operation;

        /// <summary>
        /// 切片来源描述，仅用于日志与 UI 展示。
        /// </summary>
        private readonly string m_Scope;

        /// <summary>
        /// 上次速率采样时刻（Time.realtimeSinceStartup）。
        /// </summary>
        private float m_LastSampleTime;

        /// <summary>
        /// 上次采样时已完成字节数，用于差分计算速率。
        /// </summary>
        private long m_LastSampleBytes;

        /// <summary>
        /// 当前下载速率缓存（bytes/s）。
        /// </summary>
        private long m_DownloadSpeed;

        /// <summary>
        /// 总文件数。
        /// </summary>
        public int TotalCount => m_Operation.TotalDownloadCount;

        /// <summary>
        /// 总字节数。
        /// </summary>
        public long TotalBytes => m_Operation.TotalDownloadBytes;

        /// <summary>
        /// 已完成文件数。
        /// </summary>
        public int FinishedCount => m_Operation.CurrentDownloadCount;

        /// <summary>
        /// 已完成字节数。
        /// </summary>
        public long FinishedBytes => m_Operation.CurrentDownloadBytes;

        /// <summary>
        /// 进度 0~1。
        /// </summary>
        public float Progress => m_Operation.Progress;

        /// <summary>
        /// 是否空下载（TotalCount == 0）。
        /// </summary>
        public bool IsEmpty => TotalCount <= 0;

        /// <summary>
        /// 当前下载速率（bytes/s），基于约 500ms 采样窗口差分估算。
        /// 下载未启动或已结束时返回 0。
        /// </summary>
        public long DownloadSpeed => m_DownloadSpeed;

        /// <summary>
        /// 切片来源描述，标识本下载器覆盖范围，仅用于日志与 UI 展示。
        /// 整包为 "all"；按 tag 为 "tags:a,b"；按 Asset 地址为 "locations:N"。
        /// </summary>
        public string Scope => m_Scope;

        /// <summary>
        /// 进度回调，参数 (finishedCount, totalCount, finishedBytes, totalBytes)。
        /// </summary>
        public event Action<int, int, long, long> OnProgress;

        /// <summary>
        /// 单文件开始下载回调，参数 (bundleName, fileName, fileSize)。
        /// </summary>
        public event Action<string, string, long> OnFileStarted;

        /// <summary>
        /// 构造资源下载器，注册进度回调与单文件开始事件。
        /// </summary>
        /// <param name="operation">YooAsset 下载操作实例。</param>
        /// <param name="scope">切片来源描述；整包传 "all"，按 tag 传 "tags:..."，按 Asset 地址传 "locations:N"。</param>
        public AssetDownloader(ResourceDownloaderOperation operation, string scope = "all")
        {
            m_Operation = operation;
            m_Scope = scope;
            m_Operation.DownloadProgressChanged += OnDownloadProgressChanged;
            m_Operation.DownloadFileStarted += OnDownloadFileStarted;
        }

        /// <summary>
        /// 启动下载并等待完成，返回是否成功。
        /// </summary>
        /// <remarks>
        /// ct 取消时会同步调用 CancelDownload() 停止底层下载，再重新抛出 OperationCanceledException。
        /// </remarks>
        /// <param name="ct">取消令牌。</param>
        /// <returns>true 表示下载成功，false 表示失败或被取消。</returns>
        public async UniTask<bool> RunAsync(CancellationToken ct = default)
        {
            if (IsEmpty)
            {
                return true;
            }

            m_LastSampleTime = Time.realtimeSinceStartup;
            m_LastSampleBytes = 0L;
            m_Operation.StartDownload();
            try
            {
                await UniTask.WaitUntil(() => m_Operation.IsDone, cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                Cancel();
                throw;
            }

            UnsubscribeOperationEvents();
            return m_Operation.Status == EOperationStatus.Succeeded;
        }

        /// <summary>
        /// 取消下载，取消订阅进度/文件开始回调并通知 YooAsset 中止操作。
        /// </summary>
        public void Cancel()
        {
            UnsubscribeOperationEvents();
            m_Operation.CancelDownload();
        }

        /// <summary>
        /// 处理 YooAsset 下载进度变化事件，差分估算瞬时速率并转发至 OnProgress。
        /// 采样窗口约 500ms，窗口内仅更新一次速率缓存。
        /// </summary>
        /// <param name="data">YooAsset 下载进度事件参数。</param>
        private void OnDownloadProgressChanged(DownloadProgressChangedEventArgs data)
        {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - m_LastSampleTime;
            if (elapsed >= c_SpeedSampleInterval)
            {
                long bytesDelta = data.CurrentDownloadBytes - m_LastSampleBytes;
                m_DownloadSpeed = elapsed > 0f ? (long)(bytesDelta / elapsed) : 0L;
                m_LastSampleTime = now;
                m_LastSampleBytes = data.CurrentDownloadBytes;
            }
            OnProgress?.Invoke(data.CurrentDownloadCount, data.TotalDownloadCount, data.CurrentDownloadBytes, data.TotalDownloadBytes);
        }

        /// <summary>
        /// 处理 YooAsset 单文件开始下载事件，转发至 OnFileStarted。
        /// </summary>
        /// <param name="data">YooAsset 单文件开始事件参数。</param>
        private void OnDownloadFileStarted(DownloadFileStartedEventArgs data)
        {
            OnFileStarted?.Invoke(data.BundleName, data.FileName, data.FileSize);
        }

        /// <summary>
        /// 取消订阅所有 YooAsset 下载事件，并将速率缓存归零。
        /// </summary>
        private void UnsubscribeOperationEvents()
        {
            m_Operation.DownloadProgressChanged -= OnDownloadProgressChanged;
            m_Operation.DownloadFileStarted -= OnDownloadFileStarted;
            m_DownloadSpeed = 0L;
        }
    }
}
