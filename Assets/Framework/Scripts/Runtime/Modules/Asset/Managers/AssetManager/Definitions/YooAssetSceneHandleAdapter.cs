/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  YooAssetSceneHandleAdapter.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   YooAsset.SceneHandle 到 ISceneHandle 的内部适配器，复用 ReferencePool 减少 GC
 ***************************************************************/

using Cysharp.Threading.Tasks;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset.SceneHandle 到 ISceneHandle 的内部适配器。
    /// 通过 ReferencePool 复用，调用 UnloadAsync 后自动归池。
    /// </summary>
    internal sealed class YooAssetSceneHandleAdapter : ISceneHandle, IReference
    {
        /// <summary>
        /// 被包装的 YooAsset 原生场景句柄。
        /// </summary>
        private YooAsset.SceneHandle m_Inner;

        /// <summary>
        /// 句柄是否仍然有效（m_Inner 不为 null 且原生句柄有效）。
        /// </summary>
        public bool IsValid => m_Inner != null && m_Inner.IsValid;

        /// <summary>
        /// 异步加载是否已完成。
        /// </summary>
        public bool IsDone => m_Inner != null && m_Inner.IsDone;

        /// <summary>
        /// 绑定原生句柄，由 AssetManager 内部在 Get 后立即调用。
        /// </summary>
        /// <param name="inner">YooAsset 原生场景句柄。</param>
        internal void Bind(YooAsset.SceneHandle inner)
        {
            m_Inner = inner;
        }

        /// <summary>
        /// 异步卸载场景并释放句柄（引用计数 -1），之后将适配器归还 ReferencePool。
        /// </summary>
        /// <returns>等待卸载完成的 UniTask。</returns>
        public async UniTask UnloadAsync()
        {
            if (m_Inner != null)
            {
                YooAsset.UnloadSceneOperation op = m_Inner.UnloadSceneAsync();
                await UniTask.WaitUntil(() => op.IsDone);
            }

            ReferencePool.Put(this);
        }

        /// <summary>
        /// 清理内部状态，由 ReferencePool 在归池或重建时调用。
        /// </summary>
        void IReference.Clear()
        {
            m_Inner = null;
        }
    }
}
