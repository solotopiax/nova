/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  YooAssetHandleAdapter.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   YooAsset.AssetHandle 到 IAssetHandle 的内部适配器，复用 ReferencePool 减少 GC
 ***************************************************************/

using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset.AssetHandle 到 IAssetHandle 的内部适配器。
    /// 通过 ReferencePool 复用，调用 Release 后自动归池。
    /// </summary>
    /// <typeparam name="T">资源类型。</typeparam>
    internal sealed class YooAssetHandleAdapter<T> : IAssetHandle<T>, IReference where T : UnityEngine.Object
    {
        /// <summary>
        /// 被包装的 YooAsset 原生句柄。
        /// </summary>
        private AssetHandle m_Inner;

        /// <summary>
        /// 句柄是否仍然有效（m_Inner 不为 null 且原生句柄有效）。
        /// </summary>
        public bool IsValid => m_Inner != null && m_Inner.IsValid;

        /// <summary>
        /// 异步加载是否已完成。
        /// </summary>
        public bool IsDone => m_Inner != null && m_Inner.IsDone;

        /// <summary>
        /// 已加载的资源对象（非强类型，IsDone 为 false 时为 null）。
        /// </summary>
        public UnityEngine.Object AssetObject => m_Inner?.AssetObject;

        /// <summary>
        /// 已加载的强类型资源对象（IsDone 为 false 时为 null）。
        /// </summary>
        public T Asset => m_Inner?.GetAssetObject<T>();

        /// <summary>
        /// 绑定原生句柄，由 AssetManager 内部在 Get 后立即调用。
        /// </summary>
        /// <param name="inner">YooAsset 原生句柄。</param>
        internal void Bind(AssetHandle inner)
        {
            m_Inner = inner;
        }

        /// <summary>
        /// 释放句柄（引用计数 -1），并将适配器归还 ReferencePool。
        /// </summary>
        public void Release()
        {
            m_Inner?.Release();
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
