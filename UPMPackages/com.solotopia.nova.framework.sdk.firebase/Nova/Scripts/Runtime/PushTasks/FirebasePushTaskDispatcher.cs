/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePushTaskDispatcher.cs
 * author:    Codex
 * created:   2026/8/14
 * descrip:   Firebase push task dispatch state and flush logic
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase push task 调度器。
    /// 集中管理本地缓存、flush 状态、计时器和发送门槛，避免 FirebasePlugin 字段区承载过多 push 细节。
    /// </summary>
    internal sealed class FirebasePushTaskDispatcher : IDisposable
    {
        /// <summary>
        /// push task 默认发送时间阈值，单位秒。
        /// </summary>
        private const float c_DefaultFlushIntervalSeconds = 100f;

        /// <summary>
        /// push task 默认发送数量阈值。
        /// </summary>
        private const int c_DefaultFlushBatchSize = 5;

        /// <summary>
        /// Firebase push task 本地缓存仓储。
        /// </summary>
        private FirebasePushTaskRepository m_Repository;

        /// <summary>
        /// Firebase push task 协议 Service。
        /// </summary>
        private FirebasePushTaskNetService m_NetService;

        /// <summary>
        /// 运行时配置，由宿主插件初始化时透传。
        /// </summary>
        private FirebasePluginConfig m_Config;

        /// <summary>
        /// 后台发送任务取消源。
        /// </summary>
        private CancellationTokenSource m_Cts;

        /// <summary>
        /// 本地缓存读写锁。
        /// </summary>
        private readonly SemaphoreSlim m_CacheLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// flush 调度状态锁。
        /// </summary>
        private readonly object m_StateLock = new object();

        /// <summary>
        /// 当前进程内的缓存版本。
        /// </summary>
        private int m_CacheVersion;

        /// <summary>
        /// Firebase 初始化是否已经完成。
        /// </summary>
        private bool m_FirebaseReady;

        /// <summary>
        /// 是否已经有可用于协议 Header 的用户身份。
        /// </summary>
        private bool m_UserReady;

        /// <summary>
        /// 是否已有 flush 循环正在执行。
        /// </summary>
        private bool m_FlushRunning;

        /// <summary>
        /// flush 执行中是否又收到新的发送请求。
        /// </summary>
        private bool m_FlushRequested;

        /// <summary>
        /// 是否已有按时间阈值触发的计时器正在等待。
        /// </summary>
        private bool m_TimerRunning;

        /// <summary>
        /// 初始化 dispatcher 依赖。
        /// </summary>
        /// <param name="config">Firebase 运行时配置。</param>
        public void Initialize(FirebasePluginConfig config)
        {
            m_Config = config;
            EnsureServices();
        }

        /// <summary>
        /// 设置 Firebase 初始化门槛。
        /// </summary>
        /// <param name="ready">是否已初始化完成。</param>
        public void SetFirebaseReady(bool ready)
        {
            lock (m_StateLock)
            {
                m_FirebaseReady = ready;
            }

            if (ready)
            {
                RequestFlush();
            }
        }

        /// <summary>
        /// 确认用户身份已就绪。
        /// </summary>
        public void SetUserReady()
        {
            lock (m_StateLock)
            {
                m_UserReady = true;
            }

            RequestFlush();
        }

        /// <summary>
        /// 请求发送当前所有本地缓存的 push task。
        /// 调用方用于显式触发场景（例如应用恢复前台）；实际发送仍会复用 FirebaseReady/UserReady/单飞门槛。
        /// </summary>
        public void FlushAllCachedTasks()
        {
            RequestFlush();
        }

        /// <summary>
        /// 释放后台任务和状态。
        /// </summary>
        public void Dispose()
        {
            lock (m_StateLock)
            {
                m_FirebaseReady = false;
                m_UserReady = false;
                m_FlushRequested = false;
                m_FlushRunning = false;
                m_TimerRunning = false;
            }

            if (m_Cts == null)
            {
                return;
            }

            m_Cts.Cancel();
            m_Cts.Dispose();
            m_Cts = null;
        }

        /// <summary>
        /// 写入或覆盖 push task 缓存，并按配置决定是否触发批量发送。
        /// </summary>
        /// <param name="task">待推送任务。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>缓存成功返回 true。</returns>
        public async UniTask<bool> QueueAsync(FirebasePushTask task, CancellationToken ct)
        {
            if (task == null || !task.HasValidTaskKey())
            {
                Log.Warning(LogTag.Firebase, "Firebase push task task_key 为空，已跳过缓存。");
                return false;
            }

            EnsureServices();
            if (m_Repository == null)
            {
                Log.Warning(LogTag.Firebase, "Firebase push task 持久化管理器不可用，缓存失败。");
                return false;
            }

            try
            {
                FirebasePushTask normalizedTask = task.CloneNormalized();
                int cacheCount;
                await m_CacheLock.WaitAsync(ct);
                try
                {
                    int cacheVersion = NextCacheVersion();
                    if (!m_Repository.Upsert(normalizedTask, cacheVersion))
                    {
                        return false;
                    }

                    cacheCount = m_Repository.Count();
                }
                finally
                {
                    m_CacheLock.Release();
                }

                Log.Info(LogTag.Firebase, $"Firebase push task 已缓存：TaskKey={normalizedTask.TaskKey}，CacheCount={cacheCount}，FlushBatchSize={GetFlushBatchSize()}，FlushIntervalSeconds={GetFlushInterval().TotalSeconds}。");
                OnQueued(cacheCount);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(LogTag.Firebase, $"Firebase push task 缓存异常：{ex}");
                return false;
            }
        }

        /// <summary>
        /// 确保依赖对象已经创建。
        /// </summary>
        private void EnsureServices()
        {
            if (m_Repository == null)
            {
                m_Repository = FirebasePushTaskRepository.TryCreate();
            }

            if (m_NetService == null)
            {
                m_NetService = new FirebasePushTaskNetService();
            }

            if (m_Cts == null)
            {
                m_Cts = new CancellationTokenSource();
            }
        }

        /// <summary>
        /// 生成下一次缓存版本。
        /// 达到 int.MaxValue 后回绕到 1，版本只需要区分同一进程内发送快照和当前缓存是否一致。
        /// </summary>
        /// <returns>新缓存版本。</returns>
        private int NextCacheVersion()
        {
            if (m_CacheVersion == int.MaxValue)
            {
                m_CacheVersion = 0;
            }

            m_CacheVersion++;
            return m_CacheVersion;
        }

        /// <summary>
        /// 处理缓存写入后的发送调度。
        /// </summary>
        /// <param name="cacheCount">当前缓存数量。</param>
        private void OnQueued(int cacheCount)
        {
            bool flushRunning;
            lock (m_StateLock)
            {
                flushRunning = m_FlushRunning;
                if (flushRunning)
                {
                    m_FlushRequested = true;
                }
            }

            if (flushRunning || cacheCount >= GetFlushBatchSize())
            {
                RequestFlush();
                return;
            }

            EnsureFlushTimer();
        }

        /// <summary>
        /// 请求一次 push task flush。
        /// Firebase 初始化和用户身份都就绪后才会真正启动发送。
        /// </summary>
        private void RequestFlush()
        {
            EnsureServices();
            CancellationTokenSource cts = m_Cts;
            if (cts == null)
            {
                return;
            }

            bool shouldStart = false;
            lock (m_StateLock)
            {
                m_FlushRequested = true;
                if (!m_FirebaseReady || !m_UserReady || m_FlushRunning)
                {
                    Log.Info(LogTag.Firebase, $"Firebase push task 等待发送条件：FirebaseReady={m_FirebaseReady}，UserReady={m_UserReady}，FlushRunning={m_FlushRunning}。");
                    return;
                }

                m_FlushRunning = true;
                shouldStart = true;
            }

            if (shouldStart)
            {
                RunFlushLoopAsync(cts.Token).Forget();
            }
        }

        /// <summary>
        /// 确保按时间阈值触发的 flush 计时器已启动。
        /// 计时器从首条缓存写入开始计时，后续写入不会重置时间。
        /// </summary>
        private void EnsureFlushTimer()
        {
            EnsureServices();
            TimeSpan interval = GetFlushInterval();
            if (interval <= TimeSpan.Zero)
            {
                RequestFlush();
                return;
            }

            CancellationTokenSource cts = m_Cts;
            if (cts == null)
            {
                return;
            }

            lock (m_StateLock)
            {
                if (m_TimerRunning)
                {
                    return;
                }

                m_TimerRunning = true;
            }

            Log.Info(LogTag.Firebase, $"Firebase push task 未达到数量阈值，将在 {interval.TotalSeconds} 秒后请求发送。");
            DelayFlushAsync(interval, cts.Token).Forget();
        }

        /// <summary>
        /// 等待时间阈值后请求 flush。
        /// </summary>
        /// <param name="interval">等待时长。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async UniTaskVoid DelayFlushAsync(TimeSpan interval, CancellationToken ct)
        {
            bool canceled = false;
            try
            {
                int delayMs = (int)Math.Min(interval.TotalMilliseconds, int.MaxValue);
                await UniTask.Delay(delayMs, cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }
            finally
            {
                lock (m_StateLock)
                {
                    m_TimerRunning = false;
                }
            }

            if (!canceled)
            {
                RequestFlush();
            }
        }

        /// <summary>
        /// push task 单飞发送循环。
        /// 每轮只发送当前缓存快照；若发送过程中有新增或覆盖，会在本轮成功后继续下一轮。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async UniTaskVoid RunFlushLoopAsync(CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    lock (m_StateLock)
                    {
                        m_FlushRequested = false;
                    }

                    FirebasePushTaskFlushResult flushResult = await FlushSnapshotOnceAsync(ct);
                    int remainingCount = await GetCacheCountAsync(ct);

                    if (flushResult == FirebasePushTaskFlushResult.Failed)
                    {
                        FinishFlushLoop();
                        if (remainingCount > 0)
                        {
                            EnsureFlushTimer();
                        }

                        return;
                    }

                    if (flushResult == FirebasePushTaskFlushResult.Empty)
                    {
                        FinishFlushLoop();
                        return;
                    }

                    bool shouldContinue;
                    lock (m_StateLock)
                    {
                        shouldContinue = m_FirebaseReady && m_UserReady && m_FlushRequested;
                        if (!shouldContinue)
                        {
                            m_FlushRunning = false;
                        }
                    }

                    if (!shouldContinue)
                    {
                        if (remainingCount > 0)
                        {
                            EnsureFlushTimer();
                        }

                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                FinishFlushLoop();
            }
            catch (Exception ex)
            {
                Log.Error(LogTag.Firebase, $"Firebase push task flush 异常：{ex}");
                FinishFlushLoop();
            }
        }

        /// <summary>
        /// 标记发送循环结束。
        /// </summary>
        private void FinishFlushLoop()
        {
            lock (m_StateLock)
            {
                m_FlushRunning = false;
            }
        }

        /// <summary>
        /// 发送一轮当前缓存快照。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>本轮发送结果。</returns>
        private async UniTask<FirebasePushTaskFlushResult> FlushSnapshotOnceAsync(CancellationToken ct)
        {
            List<FirebasePushTaskSnapshotItem> snapshot = await TakeSnapshotAsync(ct);
            if (snapshot.Count == 0)
            {
                return FirebasePushTaskFlushResult.Empty;
            }

            if (m_Config == null || string.IsNullOrWhiteSpace(m_Config.PushCmdName))
            {
                Log.Warning(LogTag.Firebase, "Firebase push task 协议名为空，缓存保留等待下次发送。");
                return FirebasePushTaskFlushResult.Failed;
            }

            var tasks = new List<FirebasePushTask>(snapshot.Count);
            for (int i = 0; i < snapshot.Count; i++)
            {
                tasks.Add(snapshot[i].Task);
            }

            NetResponse<PbNetCreatePushTasksResp> response = await m_NetService.Async(m_Config.PushCmdName, tasks);
            if (response == null || !response.IsSuccess)
            {
                string error = response == null ? "null response" : response.ErrorMessage;
                Log.Warning(LogTag.Firebase, $"Firebase push task 协议发送失败，缓存保留等待重试：{error}");
                return FirebasePushTaskFlushResult.Failed;
            }

            Log.Info(LogTag.Firebase, $"Firebase push task 协议响应成功：PushCmdName={m_Config.PushCmdName}，TaskCount={snapshot.Count}，准备删除本地缓存。");
            await RemoveSucceededSnapshotAsync(snapshot, ct);
            return FirebasePushTaskFlushResult.Success;
        }

        /// <summary>
        /// 读取当前缓存快照。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>缓存快照。</returns>
        private async UniTask<List<FirebasePushTaskSnapshotItem>> TakeSnapshotAsync(CancellationToken ct)
        {
            await m_CacheLock.WaitAsync(ct);
            try
            {
                return m_Repository == null
                    ? new List<FirebasePushTaskSnapshotItem>()
                    : m_Repository.GetSnapshot();
            }
            finally
            {
                m_CacheLock.Release();
            }
        }

        /// <summary>
        /// 删除发送成功且版本未被覆盖的缓存快照。
        /// </summary>
        /// <param name="snapshot">发送快照。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private async UniTask RemoveSucceededSnapshotAsync(
            IReadOnlyList<FirebasePushTaskSnapshotItem> snapshot,
            CancellationToken ct)
        {
            await m_CacheLock.WaitAsync(ct);
            try
            {
                int removedCount = m_Repository?.RemoveSucceededSnapshotItems(snapshot) ?? 0;
                Log.Info(LogTag.Firebase, $"Firebase push task 已删除发送成功缓存：RemovedCount={removedCount}，SnapshotCount={snapshot.Count}。");
            }
            finally
            {
                m_CacheLock.Release();
            }
        }

        /// <summary>
        /// 获取当前 push task 缓存数量。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>缓存数量。</returns>
        private async UniTask<int> GetCacheCountAsync(CancellationToken ct)
        {
            await m_CacheLock.WaitAsync(ct);
            try
            {
                return m_Repository?.Count() ?? 0;
            }
            finally
            {
                m_CacheLock.Release();
            }
        }

        /// <summary>
        /// 获取 push task 发送时间阈值。
        /// </summary>
        /// <returns>发送时间阈值。</returns>
        private TimeSpan GetFlushInterval()
        {
            float intervalSeconds = m_Config == null ? c_DefaultFlushIntervalSeconds : m_Config.PushFlushIntervalSeconds;
            if (float.IsNaN(intervalSeconds) || float.IsInfinity(intervalSeconds))
            {
                intervalSeconds = c_DefaultFlushIntervalSeconds;
            }

            if (intervalSeconds <= 0f)
            {
                return TimeSpan.Zero;
            }

            double intervalMilliseconds = Math.Min(intervalSeconds * 1000d, int.MaxValue);
            return TimeSpan.FromMilliseconds(intervalMilliseconds);
        }

        /// <summary>
        /// 获取 push task 数量阈值。
        /// </summary>
        /// <returns>数量阈值。</returns>
        private int GetFlushBatchSize()
        {
            int batchSize = m_Config?.PushFlushBatchSize ?? c_DefaultFlushBatchSize;
            return Math.Max(1, batchSize);
        }
    }
}
#endif
