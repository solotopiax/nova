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
        /// 获取或创建 InitializeAsync 惰性任务。
        /// 首次调用时向 Manager 发起 InitializeAsync（使用组件销毁令牌），后续调用返回同一 UniTask。
        /// Manager 为 null（Awake 创建失败）时返回已完成的任务。
        /// </summary>
        /// <returns>InitializeAsync 对应的 UniTask。</returns>
        private UniTask GetOrCreateInitializeTask()
        {
            if (m_SDKManager == null)
            {
                return UniTask.CompletedTask;
            }

            if (m_InitializeTaskCache.HasValue)
            {
                return m_InitializeTaskCache.Value;
            }

            var ct = this.GetCancellationTokenOnDestroy();
            m_InitializeTaskCache = m_SDKManager.InitializeAsync(ct);
            return m_InitializeTaskCache.Value;
        }
    }
}
