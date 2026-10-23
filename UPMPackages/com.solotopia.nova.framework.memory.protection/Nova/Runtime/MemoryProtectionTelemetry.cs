/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryProtectionTelemetry.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   内存防护脱敏埋点和有界延迟上报
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>独立有界快照队列，等待通用埋点能力就绪；不接管插件本身的网络重试。</summary>
    internal static class MemoryProtectionTelemetry
    {
        private const int Capacity = 64;
        private static readonly Queue<Snapshot> s_Pending = new Queue<Snapshot>();
        private static long s_NextFlushMs;

        /// <summary>采集固定公共上下文；版本来自框架常量和实际适配器，不读取用户数据。</summary>
        private static Dictionary<string, object> Common()
        {
            return new Dictionary<string, object>
            {
                ["nova_memory_protection_session_id"] = MemoryProtection.SessionId,
                ["nova_memory_protection_framework_version"] = Nova.Version,
                ["nova_memory_protection_actk_version"] = MemoryProtection.ActkVersion,
                ["nova_memory_protection_detection_enabled"] = MemoryProtection.DetectionEnabled,
                ["nova_memory_protection_key_rotation_enabled"] = MemoryProtection.KeyRotationEnabled,
                ["nova_memory_protection_elapsed_since_start_ms"] = MemoryProtection.ElapsedMs
            };
        }

        /// <summary>记录首次异常或被抑制的重复访问汇总，不采集数值、密钥、摘要或异常堆栈。</summary>
        internal static void Tamper(ProtectedIntegerState state, MemoryTamperInfo info)
        {
            var properties = Common();
            properties["nova_memory_tamper_protected_field_id"] = info.ProtectedFieldId;
            properties["nova_memory_tamper_value_type"] = info.ValueType;
            properties["nova_memory_tamper_detection_stage"] = info.DetectionStage;
            properties["nova_memory_tamper_detection_reason"] = info.DetectionReason;
            properties["nova_memory_tamper_response_action"] = info.ResponseAction;
            properties["nova_memory_tamper_last_rotation_batch_id"] = state.LastRotationBatchId;
            properties["nova_memory_tamper_since_last_rotation_ms"] = state.LastRotationMs < 0 ? -1L : MemoryProtection.ElapsedMs - state.LastRotationMs;
            properties["nova_memory_tamper_since_last_write_ms"] = MemoryProtection.ElapsedMs - state.LastWriteMs;
            properties["nova_memory_tamper_suppressed_detection_count"] = state.SuppressedCount;
            Enqueue("nova_memory_tamper_detected", properties);
        }

        /// <summary>每批一次结果，目标数始终等于完成、失败、跳过三项之和。</summary>
        internal static void Rotation(string batchId, string status, int target, int completed, int failed, int skipped, long duration, string reason)
        {
            var properties = Common();
            properties["nova_memory_key_rotation_batch_id"] = batchId;
            properties["nova_memory_key_rotation_status"] = status;
            properties["nova_memory_key_rotation_target_field_count"] = target;
            properties["nova_memory_key_rotation_completed_field_count"] = completed;
            properties["nova_memory_key_rotation_failed_field_count"] = failed;
            properties["nova_memory_key_rotation_skipped_field_count"] = skipped;
            properties["nova_memory_key_rotation_duration_ms"] = duration;
            properties["nova_memory_key_rotation_failure_reason"] = reason;
            Enqueue("nova_memory_key_rotation_result", properties);
        }

        /// <summary>保存完整且私有的快照；队列满时丢弃最旧事件，游戏流程不等待网络。</summary>
        private static void Enqueue(string name, Dictionary<string, object> properties)
        {
            if (s_Pending.Count == Capacity) s_Pending.Dequeue();
            s_Pending.Enqueue(new Snapshot(name, properties));
        }

        /// <summary>仅在存在实际 Ready 埋点插件时出队；单插件失败隔离且不输出异常内容。</summary>
        internal static void Flush(bool force = false)
        {
            if (s_Pending.Count == 0 || (!force && MemoryProtection.ElapsedMs < s_NextFlushMs)) return;
            s_NextFlushMs = MemoryProtection.ElapsedMs + 1000;
            var manager = FrameworkManagersGroup.GetManager<ISDKManager>();
            IReadOnlyList<ITrackPlugin> plugins = manager?.GetAll<ITrackPlugin>();
            if (plugins == null || plugins.Count == 0) return;
            // 每帧最多四条，避免延迟初始化后一次性清空队列造成卡顿。
            int count = Math.Min(force ? Capacity : 4, s_Pending.Count);
            while (count-- > 0 && s_Pending.Count > 0)
            {
                Snapshot snapshot = s_Pending.Dequeue();
                foreach (ITrackPlugin plugin in plugins)
                {
                    try { plugin.TrackEvent(snapshot.Name, new Dictionary<string, object>(snapshot.Properties)); }
                    catch (Exception) { Log.Warning(LogTag.SDK, "内存防护埋点插件上报失败，已隔离。"); }
                }
            }
        }

        /// <summary>生命周期结束时丢弃尚未发送的内存快照。</summary>
        internal static void Clear()
        {
            s_Pending.Clear();
            s_NextFlushMs = 0;
        }

        /// <summary>仅队列拥有的事件载荷，不保留任何业务对象引用。</summary>
        private sealed class Snapshot
        {
            internal readonly string Name;
            internal readonly Dictionary<string, object> Properties;
            /// <summary>接收已完成构造且不再修改的脱敏载荷。</summary>
            internal Snapshot(string name, Dictionary<string, object> properties)
            {
                Name = name;
                Properties = properties;
            }
        }
    }
}
