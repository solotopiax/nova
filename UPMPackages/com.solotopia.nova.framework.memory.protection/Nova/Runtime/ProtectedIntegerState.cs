/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProtectedIntegerState.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   受保护整数的引用状态、校验和异常隔离
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>独立引用拥有实际存储，避免可变结构体复制导致轮换只修改副本。</summary>
    internal sealed class ProtectedIntegerState
    {
        internal readonly string FieldId;
        internal readonly bool UseInt32;
        internal IMemoryProtectionStorage Storage;
        internal long LastWriteMs;
        internal long LastRotationMs = -1;
        internal string LastRotationBatchId = "";
        internal bool Faulted;
        internal bool Disposed;
        internal long SuppressedCount;
        internal long LastReportMs;
        internal string FaultStage;
        internal string FaultReason;
        private long m_PlainValue;

        /// <summary>登记逻辑字段标识，并按当前可用能力建立存储；初始化前允许普通数值兼容。</summary>
        internal ProtectedIntegerState(string fieldId, long value, bool useInt32)
        {
            if (string.IsNullOrWhiteSpace(fieldId) || fieldId.Length > 128)
                throw new ArgumentException("受保护字段标识必须为 1～128 个字符的稳定逻辑名称。", nameof(fieldId));
            FieldId = fieldId;
            UseInt32 = useInt32;
            m_PlainValue = value;
            LastWriteMs = MemoryProtection.ElapsedMs;
            MemoryProtection.Register(this);
            Activate();
        }

        /// <summary>配置和可选后端就绪后，将初始化前保存的普通数值转为受保护状态。</summary>
        internal void Activate()
        {
            if (Disposed || Faulted || Storage != null || !MemoryProtection.IsActive) return;
            try
            {
                Storage = MemoryProtection.Provider.Create(m_PlainValue, UseInt32);
                if (Storage == null) throw new InvalidOperationException();
                m_PlainValue = 0;
            }
            catch (Exception)
            {
                Fail("initialize", "backend_error");
            }
        }

        /// <summary>每次读、覆盖写和轮换前校验；故障锁定后不再读取插件数值。</summary>
        internal bool TryRead(string stage, out long value)
        {
            MemoryProtection.EnsureMainThread();
            value = 0;
            if (Disposed) return false;
            if (Storage == null)
            {
                MemoryProtection.TryInitializeFromLoadedConfig();
                Activate();
                if (Disposed || Faulted) return false;
            }
            if (Faulted)
            {
                SuppressedCount++;
                return false;
            }
            if (Storage == null)
            {
                value = m_PlainValue;
                return true;
            }
            try
            {
                if (Storage.TryRead(out long candidate) && (!UseInt32 || (candidate >= int.MinValue && candidate <= int.MaxValue)))
                {
                    value = candidate;
                    return true;
                }
                Fail(stage, "integrity_mismatch");
            }
            catch (Exception)
            {
                Fail(stage, "backend_error");
            }
            return false;
        }

        /// <summary>先校验旧值，再接受新值；禁止覆盖可疑状态后把异常数据重新变成合法基线。</summary>
        internal bool TryWrite(long value)
        {
            if (!TryRead("write", out _)) return false;
            try
            {
                if (Storage != null) Storage.Write(value);
                else m_PlainValue = value;
                LastWriteMs = MemoryProtection.ElapsedMs;
                if (!TryRead("write", out long actual)) return false;
                if (actual == value) return true;
                Fail("write", "write_value_mismatch");
                return false;
            }
            catch (Exception)
            {
                Fail("write", "backend_error");
                return false;
            }
        }

        /// <summary>验证旧值、原地轮换并复核值未改变；任一步失败均锁定字段。</summary>
        internal bool TryRotate(string batchId)
        {
            if (!TryRead("rotation", out long before)) return false;
            try
            {
                Storage.RotateKey(before);
                if (!TryRead("rotation", out long after)) return false;
                if (before != after)
                {
                    Fail("rotation", "rotation_value_mismatch");
                    return false;
                }
                LastRotationMs = MemoryProtection.ElapsedMs;
                LastRotationBatchId = batchId;
                return true;
            }
            catch (Exception)
            {
                Fail("rotation", "backend_error");
                return false;
            }
        }

        /// <summary>只上报逻辑上下文；不暴露业务值、插件对象、密钥或原始异常。</summary>
        private void Fail(string stage, string reason)
        {
            if (Faulted || Disposed) return;
            Faulted = true;
            FaultStage = stage;
            FaultReason = reason;
            LastReportMs = MemoryProtection.ElapsedMs;
            MemoryProtection.ReportTamper(this, true);
        }

        /// <summary>释放状态，使生命周期结束后的持有者无法继续读写旧数据。</summary>
        internal void Dispose()
        {
            MemoryProtection.EnsureMainThread();
            Disposed = true;
            Storage = null;
            m_PlainValue = 0;
        }
    }
}
