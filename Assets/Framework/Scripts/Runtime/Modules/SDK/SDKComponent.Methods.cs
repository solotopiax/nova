/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKComponent.Methods.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   SDK 组件 —— 私有方法
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    public sealed partial class SDKComponent : FrameworkComponent
    {
        /// <summary>
        /// 确保 Manager 已缓存跨模块依赖。
        /// Start 与 InitializeTask 都会进入此方法，避免 InitializeTask 早于 Start 被访问时空跑 InitializeAsync。PluginEntries 不参与运行时启用或排序。
        /// </summary>
        private void ConfigureManagerIfNeeded()
        {
            if (m_SDKManager == null || m_IsManagerConfigured)
            {
                return;
            }

            m_SDKManager.Initialize(new SDKManagerConfig { PluginEntries = m_PluginEntries });
            m_IsManagerConfigured = true;
        }

        /// <summary>
        /// 获取或创建 InitializeAsync 惰性任务。
        /// 首次调用时向 Manager 发起 InitializeAsync（使用组件销毁令牌），并通过 AsyncLazy 共享完成结果。
        /// 后续调用返回同一共享任务，允许初始化中的并发等待与完成后的重复等待，且不重复执行初始化。
        /// Manager 为 null（Awake 创建失败）时返回已完成的任务。
        /// </summary>
        /// <returns>InitializeAsync 对应的 UniTask。</returns>
        private UniTask GetOrCreateInitializeTask()
        {
            if (m_SDKManager == null)
            {
                return UniTask.CompletedTask;
            }

            ConfigureManagerIfNeeded();

            if (m_InitializeTaskCache != null)
            {
                return m_InitializeTaskCache.Task;
            }

            var ct = this.GetCancellationTokenOnDestroy();
            m_InitializeTaskCache = m_SDKManager.InitializeAsync(ct).ToAsyncLazy();
            return m_InitializeTaskCache.Task;
        }
    }
}
