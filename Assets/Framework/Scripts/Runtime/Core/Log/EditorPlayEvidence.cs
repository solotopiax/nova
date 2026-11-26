/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename: EditorPlayEvidence.cs
 * descrip:  向编辑器提供 Play 模式的框架运行证据。
 ***************************************************************/

#if UNITY_EDITOR
using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal enum EditorPlayModule
    {
        Asset,
        Procedure,
        Table,
        Localization,
        UI,
        Network
    }

    /// <summary>只在 Editor Play 中归集实际使用，供编辑器定期取走计数。</summary>
    internal static class EditorPlayEvidence
    {
        private static readonly (long Count, long ObservedAt)[] s_Calls =
            new (long Count, long ObservedAt)[Enum.GetValues(typeof(EditorPlayModule)).Length];
        private static bool s_TablesReady;
        internal static event Action<string, PlatformType, ChannelType, DevelopMode?> ConfigLoadFinished;
        internal static event Action TablesReady;

        /// <summary>每次进入 Play 重新观察表注册，兼容禁用域重载的项目。</summary>
        internal static void ResetTableRegistration()
        {
            s_TablesReady = false;
        }

        /// <summary>每次成功使用计一次，调用路径只更新内存中的计数和时间。</summary>
        internal static void Use(EditorPlayModule module)
        {
            try
            {
                if (!Application.isPlaying)
                    return;
                AddCalls(module, 1, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            catch (Exception) { /* 观察不参与运行结果。 */ }
        }

        /// <summary>原子取出本批次数和最后一次调用时间。后续调用归入下一批。</summary>
        internal static (long Count, long ObservedAt) TakeCalls(EditorPlayModule module)
        {
            lock (s_Calls)
            {
                int index = (int)module;
                var batch = s_Calls[index];
                long count = Math.Min(batch.Count, int.MaxValue);
                s_Calls[index] = batch.Count == count ? default : (batch.Count - count, batch.ObservedAt);
                return (count, batch.ObservedAt);
            }
        }

        /// <summary>累加调用或放回入队失败的批次，保留最新发生时间。</summary>
        internal static void AddCalls(EditorPlayModule module, long count, long observedAt)
        {
            lock (s_Calls)
            {
                ref var pending = ref s_Calls[(int)module];
                pending.Count += count;
                pending.ObservedAt = Math.Max(pending.ObservedAt, observedAt);
            }
        }

        /// <summary>已注册真实表数据，独立于业务取表次数。</summary>
        internal static void RegisterTableEvidence()
        {
            try
            {
                if (s_TablesReady || TablesReady == null || !Application.isPlaying)
                    return;
                s_TablesReady = true;
                TablesReady.Invoke();
            }
            catch (Exception) { /* 观察不参与表注册结果。 */ }
        }

        /// <summary>在实际加载结算时通知 Editor，仅复制可公开的配置坐标。</summary>
        internal static void FinishConfigLoad(ConfigRuntimeSO config, Exception failure)
        {
            try
            {
                if (!Application.isPlaying)
                    return;
                string outcome = failure == null ? "success" : failure is OperationCanceledException ? "cancelled" : "failed";
                ConfigLoadFinished?.Invoke(outcome, config?.Platform ?? PlatformType.None,
                    config?.Channel ?? ChannelType.None, config?.DevelopMode);
            }
            catch (Exception) { /* 观察不参与配置加载结果。 */ }
        }
    }
}
#endif
