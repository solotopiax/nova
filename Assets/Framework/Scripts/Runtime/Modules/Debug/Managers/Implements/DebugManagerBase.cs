/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugManagerBase.cs
 * author:    taoye
 * created:   2026/5/9
 * descrip:   调试 Manager 抽象基类。
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 调试 Manager 抽象基类，继承 FrameworkManager 并声明 IDebugManager 契约。
    /// </summary>
    internal abstract class DebugManagerBase : FrameworkManager, IDebugManager
    {
        /// <summary>
        /// Manager 优先级，数值越小越先 Update。
        /// </summary>
        public override int Priority => 0;

        /// <summary>
        /// 初始化 DebugManager。
        /// </summary>
        /// <param name="config">调试模块配置。</param>
        public abstract void Initialize(DebugManagerConfig config);

        /// <summary>
        /// 每帧更新。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭 DebugManager，释放资源。
        /// </summary>
        public abstract override void Shutdown();
    }
}
