/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  HybridClrGenerateReloadLease.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   仅当前WebGL Generate持有至Operation持久化尝试后的公开域重载租约
 ***************************************************************/
using System;
using System.Threading;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>只配对本实例成功取得的一个公开重载锁；取消计时器不会解锁，不等待重载自身。</summary>
    internal sealed class HybridClrGenerateReloadLease : IDisposable
    {
        internal static readonly TimeSpan MaximumCooperativeLifetime = TimeSpan.FromMinutes(30);
        private readonly Action m_Unlock;
        private readonly CancellationTokenSource m_Lifetime;
        private readonly int m_OwnerThread;
        private int m_OwnsLock;
        private int m_Disposed;

        /// <summary>只保存已分配好的清理设施；构造本身不调用Unity锁。</summary>
        /// <param name="unlock">与本次获取对应的一次解锁动作，生产固定公开Unity API。</param>
        /// <param name="lifetime">本实例拥有的有限协作令牌源。</param>
        /// <returns>尚未持有原生锁的内部实例。</returns>
        private HybridClrGenerateReloadLease(Action unlock, CancellationTokenSource lifetime)
        {
            m_Unlock = unlock; m_Lifetime = lifetime; m_OwnerThread = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>仅在Unity主线程为当前正式Generate取得一个公开重载锁，正常编译与Refresh保持。</summary>
        /// <param name="token">当前Action取消令牌；30分钟协作期限只设置取消，不从计时线程解锁。</param>
        /// <returns>唯一实例，必须由持有它的State在Dispatcher持久化尝试后的finally释放。</returns>
        internal static HybridClrGenerateReloadLease Acquire(CancellationToken token)
        {
            if (!AgentActionRuntime.IsMainThread) throw new InvalidOperationException("Generate 重载租约只能在Unity主线程获取。");
            return Acquire(EditorApplication.LockReloadAssemblies, EditorApplication.UnlockReloadAssemblies, token, MaximumCooperativeLifetime);
        }

        /// <summary>隔离公开锁的配对所有权以供永久纯回归；生产只由上方固定API入口调用。</summary>
        /// <param name="lockReload">生产固定LockReloadAssemblies；fixture只用自有计数动作，不调用Unity。</param>
        /// <param name="unlockReload">生产固定UnlockReloadAssemblies；只为成功取得的本实例调用一次。</param>
        /// <param name="token">调用方取消令牌，不能成为自动解锁回调。</param>
        /// <param name="cooperativeLifetime">大于零且最多30分钟；不是强杀同步Build的硬期限。</param>
        /// <returns>成功取得一个锁的实例；取得前异常只清理自己CTS，不释放他人锁。</returns>
        internal static HybridClrGenerateReloadLease Acquire(Action lockReload, Action unlockReload, CancellationToken token, TimeSpan cooperativeLifetime)
        {
            if (lockReload == null) throw new ArgumentNullException(nameof(lockReload));
            if (unlockReload == null) throw new ArgumentNullException(nameof(unlockReload));
            if (cooperativeLifetime <= TimeSpan.Zero || cooperativeLifetime > MaximumCooperativeLifetime) throw new ArgumentOutOfRangeException(nameof(cooperativeLifetime));
            token.ThrowIfCancellationRequested();
            CancellationTokenSource lifetime = null; HybridClrGenerateReloadLease lease = null;
            try
            {
                lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
                lifetime.CancelAfter(cooperativeLifetime);
                lease = new HybridClrGenerateReloadLease(unlockReload, lifetime);
                lifetime.Token.ThrowIfCancellationRequested();
                lockReload();
                lease.m_OwnsLock = 1;
                return lease;
            }
            catch
            {
                if (lease != null) lease.Dispose(); else lifetime?.Dispose();
                throw;
            }
        }

        /// <summary>当前取消或协作超时令牌；同步原生Build只能返回后再观察它。</summary>
        /// <returns>本实例有限令牌；释放后不再读取。</returns>
        internal CancellationToken Token => m_Lifetime.Token;

        /// <summary>仅在原获取主线程幂等释放自己的一次锁，绝不循环解锁或由取消timer提前释放。</summary>
        /// <returns>无返回值；错误线程在未消费所有权前拒绝，原线程仍可正确释放。</returns>
        public void Dispose()
        {
            if (Volatile.Read(ref m_Disposed) != 0) return;
            if (Thread.CurrentThread.ManagedThreadId != m_OwnerThread) throw new InvalidOperationException("Generate 重载租约必须在原主线程释放。");
            if (Interlocked.Exchange(ref m_Disposed, 1) != 0) return;
            try { m_Lifetime.Dispose(); }
            finally { if (Interlocked.Exchange(ref m_OwnsLock, 0) == 1) m_Unlock(); }
        }
    }
}
