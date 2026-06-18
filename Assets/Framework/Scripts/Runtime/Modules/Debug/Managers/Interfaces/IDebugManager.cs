/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IDebugManager.cs
 * author:    taoye
 * created:   2026/5/9
 * descrip:   调试 Manager 对 Component 暴露的契约接口。
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 调试 Manager 接口，对 DebugComponent 暴露 Initialize / Shutdown 契约。
    /// </summary>
    public interface IDebugManager
    {
        /// <summary>
        /// 初始化 DebugManager。
        /// </summary>
        /// <param name="config">调试模块配置。</param>
        void Initialize(DebugManagerConfig config);

        /// <summary>
        /// 关闭 DebugManager，释放资源。
        /// </summary>
        void Shutdown();
    }
}
