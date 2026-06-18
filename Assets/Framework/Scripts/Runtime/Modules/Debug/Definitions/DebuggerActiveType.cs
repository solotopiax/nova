/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebuggerActiveType.cs
 * author:    taoye
 * created:   2026/5/9
 * descrip:   Debugger 激活类型枚举。
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Debugger 激活类型，决定 RuntimeDebugger 在何种构建环境下初始化。
    /// </summary>
    public enum DebuggerActiveType : byte
    {
        /// <summary>
        /// 始终激活。
        /// </summary>
        AlwaysEnable = 0,

        /// <summary>
        /// 仅在 Development 构建下激活。
        /// </summary>
        OnlyEnableWhenDevelopment = 1,

        /// <summary>
        /// 仅在编辑器下激活。
        /// </summary>
        OnlyEnableInEditor = 2,

        /// <summary>
        /// 始终禁用。
        /// </summary>
        AlwaysDisable = 3,
    }
}
