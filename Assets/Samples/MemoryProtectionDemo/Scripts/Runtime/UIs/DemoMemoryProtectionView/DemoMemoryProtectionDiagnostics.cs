/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoMemoryProtectionDiagnostics.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   编辑器专用演示诊断桥，Player 没有诊断实现
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Memory.Protection.Samples.Runtime
{
    /// <summary>仅由 MemoryProtectionDemo 的 Editor 程序集提供，不是业务接入内存防护的 API。</summary>
    public interface IDemoMemoryProtectionDiagnostics
    {
        /// <summary>仅将传入演示金币的下一次后端读取改为校验失败，不访问原厂密钥或数值。</summary>
        bool InjectValidationFailure(ProtectedLong gold);
        /// <summary>仅轮换传入的两个演示字段，不改变全局自动轮换时间或伪造批次埋点。</summary>
        bool RotateFields(ProtectedLong gold, ProtectedInt diamonds);
    }

    /// <summary>Runtime 只使用可选诊断桥；正式 Player 中为空，相关演示按钮灰显。</summary>
    public static class DemoMemoryProtectionDiagnostics
    {
        /// <summary>由 Sample Editor 代码注册的诊断实现，与可选商业插件无编译依赖。</summary>
        public static IDemoMemoryProtectionDiagnostics Editor { get; set; }
    }
}
