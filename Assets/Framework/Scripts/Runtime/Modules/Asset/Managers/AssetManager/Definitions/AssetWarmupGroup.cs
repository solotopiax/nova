/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetWarmupGroup.cs
 * author:    taoye
 * created:   2026/9/16
 * descrip:   YooAsset Bundle 预热组实现
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 批量创建并集中持有 YooAsset BundleFileHandle 的预热组。
    /// </summary>
    internal sealed class AssetWarmupGroup : IAssetWarmupGroup
    {
        private readonly ResourcePackage m_Package;
        private readonly AssetInfo[] m_AssetInfos;
        private readonly List<BundleFileHandle> m_Handles = new();
        private CancellationTokenSource m_RunCts;
        private bool m_IsRunning;

        /// <inheritdoc />
        public int TotalCount => m_AssetInfos.Length;

        /// <inheritdoc />
        public int FinishedCount { get; private set; }

        /// <inheritdoc />
        public float Progress => TotalCount <= 0 ? 1f : (float)FinishedCount / TotalCount;

        /// <inheritdoc />
        public bool IsDone { get; private set; }

        /// <inheritdoc />
        public bool Succeeded { get; private set; }

        /// <inheritdoc />
        public bool IsReleased { get; private set; }

        /// <inheritdoc />
        public string Error { get; private set; }

        /// <inheritdoc />
        public string Scope { get; }

        /// <inheritdoc />
        public event Action<int, int> OnProgress;

        /// <summary>
        /// 创建一个尚未启动的预热组。
        /// </summary>
        /// <param name="package">已经初始化并加载 Manifest 的 YooAsset 包。</param>
        /// <param name="assetInfos">需要预热的资源信息集合。</param>
        /// <param name="scope">用于日志与 UI 的范围描述。</param>
        public AssetWarmupGroup(ResourcePackage package, AssetInfo[] assetInfos, string scope)
        {
            m_Package = package ?? throw new ArgumentNullException(nameof(package));
            m_AssetInfos = assetInfos ?? Array.Empty<AssetInfo>();
            Scope = scope ?? string.Empty;
        }

        /// <inheritdoc />
        public async UniTask<bool> RunAsync(CancellationToken ct = default)
        {
            if (IsReleased)
            {
                throw new ObjectDisposedException(nameof(AssetWarmupGroup));
            }
            if (m_IsRunning)
            {
                throw new InvalidOperationException("同一个 WarmupGroup 不能重复并发执行。");
            }
            if (IsDone)
            {
                return Succeeded;
            }

            m_IsRunning = true;
            m_RunCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                for (int i = 0; i < m_AssetInfos.Length; i++)
                {
                    m_RunCts.Token.ThrowIfCancellationRequested();
                    m_Handles.Add(m_Package.LoadBundleFileAsync(m_AssetInfos[i]));
                }

                NotifyProgress(0);
                while (true)
                {
                    m_RunCts.Token.ThrowIfCancellationRequested();
                    int finished = 0;
                    BundleFileHandle failedHandle = null;
                    for (int i = 0; i < m_Handles.Count; i++)
                    {
                        BundleFileHandle handle = m_Handles[i];
                        if (!handle.IsDone)
                        {
                            continue;
                        }

                        finished++;
                        if (failedHandle == null && handle.Status != EOperationStatus.Succeeded)
                        {
                            failedHandle = handle;
                        }
                    }

                    NotifyProgress(finished);
                    if (finished >= m_Handles.Count)
                    {
                        IsDone = true;
                        Succeeded = failedHandle == null;
                        if (!Succeeded)
                        {
                            Error = string.IsNullOrEmpty(failedHandle.Error)
                                ? "Bundle 预热失败。"
                                : failedHandle.Error;
                            ReleaseHandles();
                        }
                        return Succeeded;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, m_RunCts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Error = "Bundle 预热已取消。";
                IsDone = true;
                Succeeded = false;
                ReleaseHandles();
                throw;
            }
            catch (Exception e)
            {
                Error = e.Message;
                IsDone = true;
                Succeeded = false;
                ReleaseHandles();
                return false;
            }
            finally
            {
                m_IsRunning = false;
                m_RunCts?.Dispose();
                m_RunCts = null;
            }
        }

        /// <inheritdoc />
        public void Cancel()
        {
            m_RunCts?.Cancel();
            Release();
        }

        /// <inheritdoc />
        public void Release()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            m_RunCts?.Cancel();
            ReleaseHandles();
            OnProgress = null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Release();
        }

        /// <summary>
        /// 更新已完成数量，并仅在数值变化时通知监听方。
        /// </summary>
        /// <param name="finished">当前完成数量。</param>
        private void NotifyProgress(int finished)
        {
            if (FinishedCount == finished && !(finished == 0 && TotalCount == 0))
            {
                return;
            }

            FinishedCount = finished;
            Action<int, int> callbacks = OnProgress;
            if (callbacks == null)
            {
                return;
            }

            foreach (Action<int, int> callback in callbacks.GetInvocationList())
            {
                try
                {
                    callback(FinishedCount, TotalCount);
                }
                catch (Exception e)
                {
                    // 进度展示属于旁路观察，监听方异常不得改变资源预热结果。
                    Log.Warning(LogTag.Asset, "Warmup 进度监听异常，已忽略：{0}", e.Message);
                }
            }
        }

        /// <summary>
        /// 幂等释放已经创建的全部 YooAsset BundleFileHandle。
        /// </summary>
        private void ReleaseHandles()
        {
            for (int i = 0; i < m_Handles.Count; i++)
            {
                BundleFileHandle handle = m_Handles[i];
                if (handle != null && handle.IsValid)
                {
                    handle.Release();
                }
            }
            m_Handles.Clear();
        }
    }
}
