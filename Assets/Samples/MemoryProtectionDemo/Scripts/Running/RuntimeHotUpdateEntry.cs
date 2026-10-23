/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  RuntimeHotUpdateEntry.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   MemoryProtectionDemo 运行时增量热更新 DLL 的独立业务入口
 ***************************************************************/

namespace NovaFramework.Memory.Protection.Samples.Running
{
    /// <summary>
    /// 运行时增量业务入口；由主业务程序集在 DLL 加载完成后通过反射调用。
    /// </summary>
    public static class RuntimeHotUpdateEntry
    {
        /// <summary>
        /// 激活本次增量内容并返回可展示结果。
        /// </summary>
        /// <returns>用于 MemoryProtectionDemo 反馈区确认入口已执行的文本。</returns>
        public static string Activate()
        {
            return "NovaFramework.Memory.Protection.Samples.Running.RuntimeHotUpdateEntry -> activated";
        }
    }
}
