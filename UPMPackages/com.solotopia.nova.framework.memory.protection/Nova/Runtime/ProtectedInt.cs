/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProtectedInt.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   稳定的整型防护入口
 ***************************************************************/

using System;
using Newtonsoft.Json;

namespace NovaFramework.Runtime
{
    /// <summary>长期敏感 int 的引用存储，避免可变结构体复制及插件缺失时的编译差异。</summary>
    [JsonConverter(typeof(ProtectedIntegerJsonConverter))]
    public sealed class ProtectedInt : IDisposable
    {
        private readonly ProtectedIntegerState m_State;
        /// <summary>业务逻辑字段名称。</summary>
        public string FieldId => m_State.FieldId;
        /// <summary>检测异常后是否已隔离。</summary>
        public bool IsQuarantined => m_State.Faulted;
        /// <summary>当前字段是否实际使用防护后端。</summary>
        public bool IsProtected => m_State.Storage != null && !m_State.Disposed && !m_State.Faulted;
        /// <summary>简便读写属性；业务边界建议使用 Try 方法避免未处理异常。</summary>
        public int Value
        {
            get => TryRead(out int value) ? value : throw new MemoryProtectionException(FieldId);
            set { if (!TryWrite(value)) throw new MemoryProtectionException(FieldId); }
        }
        /// <summary>以可信加载值创建数值，默认值为 0。</summary>
        public ProtectedInt(string fieldId, int value = 0) => m_State = new ProtectedIntegerState(fieldId, value, true);
        /// <summary>校验并读取，失败时输出 0 且返回 false。</summary>
        public bool TryRead(out int value)
        {
            bool success = m_State.TryRead("read", out long candidate);
            value = success ? (int)candidate : 0;
            return success;
        }
        /// <summary>先检查旧状态再写入新值，异常字段保持隔离。</summary>
        public bool TryWrite(int value) => m_State.TryWrite(value);
        /// <summary>释放字段状态。</summary>
        public void Dispose() => m_State.Dispose();
    }
}
