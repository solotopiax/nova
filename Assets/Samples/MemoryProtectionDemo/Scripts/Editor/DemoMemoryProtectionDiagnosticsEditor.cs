/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoMemoryProtectionDiagnosticsEditor.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   仅编辑器的内存防护演示诊断，不进入 Player
 ***************************************************************/

using System;
using System.Reflection;
using NovaFramework.Runtime;
using NovaFramework.Memory.Protection.Samples.Runtime;
using UnityEditor;

namespace NovaFramework.Memory.Protection.Samples.Editor
{
    /// <summary>只反射 Nova 自有状态并操作明确传入的演示字段，不读取原厂私有成员或操作真实存档。</summary>
    internal sealed class DemoMemoryProtectionDiagnosticsEditor : IDemoMemoryProtectionDiagnostics
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>Editor 重载后注册可选桥；Runtime 无需引用本程序集。</summary>
        [InitializeOnLoadMethod]
        private static void Register() => DemoMemoryProtectionDiagnostics.Editor = new DemoMemoryProtectionDiagnosticsEditor();

        /// <summary>替换指定演示金币的存储装饰器，仅让下一次读取返回校验失败。</summary>
        public bool InjectValidationFailure(ProtectedLong gold)
        {
            if (gold == null || gold.FieldId != DemoMemoryProtectionWallet.GoldFieldId || !gold.IsProtected) return false;
            try
            {
                object state = GetState(gold);
                FieldInfo field = state.GetType().GetField("Storage", Hidden);
                var storage = field?.GetValue(state) as IMemoryProtectionStorage;
                if (storage == null) return false;
                field.SetValue(state, new FailedReadStorage(storage));
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>轮换当前页面的两个明确字段并比较前后值；不改变防护包自动调度与轮换埋点。</summary>
        public bool RotateFields(ProtectedLong gold, ProtectedInt diamonds)
        {
            if (!MemoryProtection.IsActive || !MemoryProtection.KeyRotationEnabled || gold == null || diamonds == null ||
                gold.FieldId != DemoMemoryProtectionWallet.GoldFieldId || diamonds.FieldId != DemoMemoryProtectionWallet.DiamondsFieldId ||
                !gold.IsProtected || !diamonds.IsProtected || !gold.TryRead(out long beforeGold) || !diamonds.TryRead(out int beforeDiamonds)) return false;
            try
            {
                string batchId = "memory-protection-demo-editor-" + Guid.NewGuid().ToString("N");
                object goldState = GetState(gold);
                object diamondState = GetState(diamonds);
                if (!(bool)goldState.GetType().GetMethod("TryRotate", Hidden).Invoke(goldState, new object[] { batchId }) ||
                    !(bool)diamondState.GetType().GetMethod("TryRotate", Hidden).Invoke(diamondState, new object[] { batchId })) return false;
                return gold.TryRead(out long afterGold) && diamonds.TryRead(out int afterDiamonds) &&
                    beforeGold == afterGold && beforeDiamonds == afterDiamonds;
            }
            catch (Exception) { return false; }
        }

        /// <summary>仅在 Editor 诊断层取得当前 Nova 包的自有状态；结构变化时由调用者安全返回失败。</summary>
        private static object GetState(object value) => value.GetType().GetField("m_State", Hidden)?.GetValue(value);

        /// <summary>模拟一次后端校验失败；原始存储仍封装内部，不输出数值、密钥或摘要。</summary>
        private sealed class FailedReadStorage : IMemoryProtectionStorage
        {
            private readonly IMemoryProtectionStorage m_Inner;
            private bool m_FailNextRead = true;
            /// <summary>包装当前演示字段的存储，不重新建立余额基线。</summary>
            internal FailedReadStorage(IMemoryProtectionStorage inner) => m_Inner = inner;
            /// <summary>首读返回失败，由防护包执行实际隔离；以后是否允许读取由字段故障锁决定。</summary>
            public bool TryRead(out long value)
            {
                value = 0;
                if (!m_FailNextRead) return m_Inner.TryRead(out value);
                m_FailNextRead = false;
                return false;
            }
            /// <summary>保留原存储写入口；字段隔离后不会调用此入口。</summary>
            public void Write(long value) => m_Inner.Write(value);
            /// <summary>保留原存储轮换入口；不从失败状态重新解码。</summary>
            public void RotateKey(long verifiedValue) => m_Inner.RotateKey(verifiedValue);
        }
    }
}
