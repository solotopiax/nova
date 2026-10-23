/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProtectedLong.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   稳定的长整数防护入口和普通数值 JSON 契约
 ***************************************************************/

using System;
using Newtonsoft.Json;

namespace NovaFramework.Runtime
{
    /// <summary>长期敏感 long 的引用存储；未安装插件时保持同一 API 和数值 JSON 格式。</summary>
    [JsonConverter(typeof(ProtectedIntegerJsonConverter))]
    public sealed class ProtectedLong : IDisposable
    {
        private readonly ProtectedIntegerState m_State;
        /// <summary>业务逻辑字段标识，不包含玩家 ID 或内存地址。</summary>
        public string FieldId => m_State.FieldId;
        /// <summary>检测异常后被隔离；业务应停止此字段的结算并从可信源恢复。</summary>
        public bool IsQuarantined => m_State.Faulted;
        /// <summary>当前字段是否实际使用防护后端。</summary>
        public bool IsProtected => m_State.Storage != null && !m_State.Disposed && !m_State.Faulted;
        /// <summary>简便读写入口；异常时抛脱敏异常，需避免让其成为 Unity 未处理异常。</summary>
        public long Value
        {
            get => TryRead(out long value) ? value : throw new MemoryProtectionException(FieldId);
            set { if (!TryWrite(value)) throw new MemoryProtectionException(FieldId); }
        }

        /// <summary>从可信加载结果创建受保护字段；内存密钥由插件生成。</summary>
        public ProtectedLong(string fieldId, long value = 0) => m_State = new ProtectedIntegerState(fieldId, value, false);
        /// <summary>读取并校验；失败时 value 为 0，此时不得继续业务结算或保存。</summary>
        public bool TryRead(out long value) => m_State.TryRead("read", out value);
        /// <summary>先校验现有状态，再写入新值；被隔离字段不会接受覆盖写。</summary>
        public bool TryWrite(long value) => m_State.TryWrite(value);
        /// <summary>释放本字段的运行时内存状态。</summary>
        public void Dispose() => m_State.Dispose();
    }

    /// <summary>简便属性入口的脱敏异常；推荐业务边界使用 TryRead/TryWrite 处理失败。</summary>
    public sealed class MemoryProtectionException : InvalidOperationException
    {
        /// <summary>构造只含逻辑字段名的异常，不携带任何受保护数值或原始插件异常。</summary>
        public MemoryProtectionException(string fieldId) : base("受保护字段不可用：" + fieldId) { }
    }
}
