/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryProtection.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   独立核心包的内存防护生命周期、异常通知和分帧密钥轮换
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>只含逻辑上下文的异常通知，不包含余额、密钥、摘要或原始插件对象。</summary>
    public sealed class MemoryTamperInfo
    {
        /// <summary>逻辑字段名称。</summary>
        public string ProtectedFieldId { get; }
        /// <summary>数值类型，int32 或 int64。</summary>
        public string ValueType { get; }
        /// <summary>发现异常的阶段。</summary>
        public string DetectionStage { get; }
        /// <summary>受控异常原因码。</summary>
        public string DetectionReason { get; }
        /// <summary>固定处理为字段隔离，不自动封号或恢复可疑值。</summary>
        public string ResponseAction => "quarantine_field";
        /// <summary>从故障状态创建脱敏通知。</summary>
        internal MemoryTamperInfo(ProtectedIntegerState state)
        {
            ProtectedFieldId = state.FieldId;
            ValueType = state.UseInt32 ? "int32" : "int64";
            DetectionStage = state.FaultStage;
            DetectionReason = state.FaultReason;
        }
    }

    /// <summary>可选核心包基础能力；通过包内管理器接入配置、轮询及关闭，无需场景组件。</summary>
    public static class MemoryProtection
    {
        private const long RotationIntervalMs = 60000;
        private const long SuppressionIntervalMs = 30000;
        private const int RotationFieldsPerFrame = 32;
        private static readonly List<WeakReference<ProtectedIntegerState>> s_Fields = new List<WeakReference<ProtectedIntegerState>>();
        private static readonly Stopwatch s_Clock = Stopwatch.StartNew();
        private static RotationBatch s_Batch;
        private static bool s_Initialized;
        private static bool s_AutomaticInitializationAllowed = true;
        private static long s_NextRotationMs;
        private static long s_NextMaintenanceMs;
        private static int s_MainThreadId;

        /// <summary>实际注册且完成 API 适配检查的后端；无插件时为 null。</summary>
        internal static IMemoryProtectionProvider Provider { get; private set; }
        /// <summary>当前进程内存防护会话标识，不持久化。</summary>
        public static string SessionId { get; private set; } = Guid.NewGuid().ToString("N");
        /// <summary>当前是否实际启用防护；无插件或配置禁用时为 false。</summary>
        public static bool IsActive
        {
            get
            {
                TryInitializeFromLoadedConfig();
                return s_Initialized && DetectionEnabled && Provider != null;
            }
        }
        /// <summary>隐私配置中的异常检测开关。</summary>
        public static bool DetectionEnabled { get; private set; }
        /// <summary>隐私配置中的定期密钥轮换开关；仅实际防护启用时执行。</summary>
        public static bool KeyRotationEnabled { get; private set; }
        /// <summary>当前实际插件版本；无插件为空字符串。</summary>
        public static string ActkVersion => Provider?.Version ?? "";
        /// <summary>当前进程时钟，避免系统时间调整影响轮换及耗时。</summary>
        internal static long ElapsedMs => s_Clock.ElapsedMilliseconds;
        /// <summary>首次发现字段异常时发出脱敏通知；监听者应停止结算并请求可信数据。</summary>
        public static event Action<MemoryTamperInfo> TamperDetected;

        /// <summary>在静态重置时结束旧字段生命周期，支持关闭 Domain Reload 的再次 Play。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Shutdown();
            s_MainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Provider = null;
            s_AutomaticInitializationAllowed = true;
            s_Clock.Restart();
            SessionId = Guid.NewGuid().ToString("N");
        }

        /// <summary>由自写适配程序集注册已核实可用的后端；不加载业务 DLL，不更改配置。</summary>
        public static void RegisterProvider(IMemoryProtectionProvider provider)
        {
            EnsureMainThread();
            if (s_Initialized) throw new InvalidOperationException("内存防护后端必须在 Config 初始化前注册。");
            Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>使用包拥有的配置初始化一次；正常启动自动调用，未安装商业插件不阻塞游戏。</summary>
        public static void Initialize(MemoryProtectionConfigs configs)
        {
            EnsureMainThread();
            if (s_Initialized) return;
            DetectionEnabled = configs?.EnableMemoryTamperDetection ?? true;
            KeyRotationEnabled = configs?.EnableMemoryKeyRotation ?? true;
            s_Initialized = true;
            s_NextRotationMs = ElapsedMs + RotationIntervalMs;
            // 初始化前建立的字段在此统一迁移，持久化格式始终是普通数值。
            var fields = s_Fields.ToArray();
            foreach (var reference in fields)
                if (reference.TryGetTarget(out var field)) field.Activate();
        }

        /// <summary>字段只保留弱引用，业务对象不再持有时允许自动回收。</summary>
        internal static void Register(ProtectedIntegerState field)
        {
            EnsureMainThread();
            TryInitializeFromLoadedConfig();
            s_Fields.Add(new WeakReference<ProtectedIntegerState>(field));
        }

        /// <summary>在配置已加载后的首个访问或管理器轮询中完成初始化，主框架无需调用本包。</summary>
        internal static void TryInitializeFromLoadedConfig()
        {
            EnsureMainThread();
            if (s_Initialized || !s_AutomaticInitializationAllowed) return;
            var provider = FrameworkManagersGroup.GetManager<IPrivacyConfigProvider>();
            PrivacyConfigs privacy = provider?.PrivacyConfigs;
            if (privacy != null) Initialize(MemoryProtectionConfigs.FromPrivacy(privacy));
        }

        /// <summary>校验主线程契约，防止读写与轮换并发造成误判或状态覆盖。</summary>
        internal static void EnsureMainThread()
        {
            int current = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (s_MainThreadId == 0) s_MainThreadId = current;
            if (s_MainThreadId != current) throw new InvalidOperationException("内存防护 API 只能在 Unity 主线程调用。");
        }

        /// <summary>由框架 Update 轮询，分帧处理轮换、重复异常汇总及待上报快照。</summary>
        internal static void Update()
        {
            TryInitializeFromLoadedConfig();
            if (!s_Initialized) return;
            long now = ElapsedMs;
            if (now >= s_NextMaintenanceMs)
            {
                s_NextMaintenanceMs = now + 1000;
                for (int i = s_Fields.Count - 1; i >= 0; i--)
                {
                    if (!s_Fields[i].TryGetTarget(out var field) || field.Disposed) s_Fields.RemoveAt(i);
                    else if (field.Faulted && field.SuppressedCount > 0 && now - field.LastReportMs >= SuppressionIntervalMs)
                        ReportTamper(field, false);
                }
            }
            if (IsActive && KeyRotationEnabled)
            {
                if (s_Batch == null && now >= s_NextRotationMs) BeginRotation();
                if (s_Batch != null) AdvanceRotation();
            }
            MemoryProtectionTelemetry.Flush();
        }

        /// <summary>冻结当批实际受保护且可轮换字段；无目标时不伪造轮换结果。</summary>
        private static void BeginRotation()
        {
            s_NextRotationMs = ElapsedMs + RotationIntervalMs;
            var targets = new List<WeakReference<ProtectedIntegerState>>();
            foreach (var reference in s_Fields)
                if (reference.TryGetTarget(out var field) && !field.Disposed && !field.Faulted && field.Storage != null)
                    targets.Add(reference);
            if (targets.Count != 0) s_Batch = new RotationBatch(targets.ToArray());
        }

        /// <summary>每帧处理有限字段，目标失效计入跳过；每批恰好一个结果事件。</summary>
        private static void AdvanceRotation()
        {
            RotationBatch batch = s_Batch;
            int end = Math.Min(batch.Index + RotationFieldsPerFrame, batch.Targets.Length);
            while (batch.Index < end)
            {
                var reference = batch.Targets[batch.Index++];
                if (!reference.TryGetTarget(out var field) || field.Disposed || field.Faulted || field.Storage == null) batch.Skipped++;
                else if (field.TryRotate(batch.Id)) batch.Completed++;
                else batch.Failed++;
                // 异常通知可能同步触发框架关闭；旧批次已由 Shutdown 终结，不得再次上报。
                if (!ReferenceEquals(s_Batch, batch)) return;
            }
            if (batch.Index != batch.Targets.Length) return;
            s_Batch = null;
            string status = batch.Failed == 0 ? "success" : batch.Completed == 0 ? "failure" : "partial_failure";
            MemoryProtectionTelemetry.Rotation(batch.Id, status, batch.Targets.Length, batch.Completed, batch.Failed, batch.Skipped,
                ElapsedMs - batch.StartMs, batch.Failed == 0 ? "" : "field_validation_failed");
        }

        /// <summary>首次通知业务，重复检测只定期汇总；监听者异常不污染崩溃统计。</summary>
        internal static void ReportTamper(ProtectedIntegerState field, bool notify)
        {
            var info = new MemoryTamperInfo(field);
            MemoryProtectionTelemetry.Tamper(field, info);
            field.SuppressedCount = 0;
            field.LastReportMs = ElapsedMs;
            if (!notify) return;
            Log.Warning(LogTag.Config, "受保护内存字段已隔离，请从可信数据源恢复。");
            if (TamperDetected == null) return;
            foreach (Action<MemoryTamperInfo> callback in TamperDetected.GetInvocationList())
            {
                try { callback(info); }
                catch (Exception) { Log.Warning(LogTag.Config, "内存异常通知监听者执行失败，已隔离。"); }
            }
        }

        /// <summary>在 SDK 关闭前结束未完成批次并尝试上报；释放字段、通知与所有待发快照。</summary>
        internal static void Shutdown()
        {
            s_AutomaticInitializationAllowed = false;
            if (s_Batch != null)
            {
                RotationBatch batch = s_Batch;
                MemoryProtectionTelemetry.Rotation(batch.Id, "cancelled", batch.Targets.Length, batch.Completed, batch.Failed,
                    batch.Targets.Length - batch.Completed - batch.Failed, ElapsedMs - batch.StartMs, "shutdown");
                s_Batch = null;
            }
            MemoryProtectionTelemetry.Flush(true);
            foreach (var reference in s_Fields)
                if (reference.TryGetTarget(out var field)) field.Dispose();
            s_Fields.Clear();
            TamperDetected = null;
            s_Initialized = false;
            DetectionEnabled = false;
            KeyRotationEnabled = false;
            s_NextMaintenanceMs = 0;
            MemoryProtectionTelemetry.Clear();
        }

        /// <summary>单批次的有限轮换状态，不持有业务字段强引用或任何明文值。</summary>
        private sealed class RotationBatch
        {
            internal readonly string Id = Guid.NewGuid().ToString("N");
            internal readonly long StartMs = ElapsedMs;
            internal readonly WeakReference<ProtectedIntegerState>[] Targets;
            internal int Index;
            internal int Completed;
            internal int Failed;
            internal int Skipped;
            /// <summary>保存批次目标快照和计数。</summary>
            internal RotationBatch(WeakReference<ProtectedIntegerState>[] targets) => Targets = targets;
        }
    }
}
