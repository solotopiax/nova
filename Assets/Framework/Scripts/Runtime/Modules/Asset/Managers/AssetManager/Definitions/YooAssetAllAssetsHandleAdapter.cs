/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  YooAssetAllAssetsHandleAdapter.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   YooAsset.AllAssetsHandle 到 IAllAssetsHandle 的内部适配器，复用 ReferencePool 减少 GC
 ***************************************************************/

using System.Collections.Generic;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset.AllAssetsHandle 到 IAllAssetsHandle 的内部适配器。
    /// 通过 ReferencePool 复用，调用 Release 后自动归池。
    /// <para>
    /// 整批资源共用同一个引用计数句柄，整批同生共死。
    /// 禁止对单个资源执行局部 Release，必须等整批使用完毕后调用 Release。
    /// </para>
    /// </summary>
    /// <typeparam name="T">资源类型。</typeparam>
    internal sealed class YooAssetAllAssetsHandleAdapter<T> : IAllAssetsHandle<T>, IReference where T : UnityEngine.Object
    {
        /// <summary>
        /// 被包装的 YooAsset 原生全资源句柄。
        /// </summary>
        private AllAssetsHandle m_Inner;

        /// <summary>
        /// 缓存的强类型资源数组。
        /// </summary>
        private T[] m_Assets;

        /// <summary>
        /// 句柄是否仍然有效（m_Inner 不为 null 且原生句柄有效）。
        /// </summary>
        public bool IsValid => m_Inner != null && m_Inner.IsValid;

        /// <summary>
        /// 异步加载是否已完成。
        /// </summary>
        public bool IsDone => m_Inner != null && m_Inner.IsDone;

        /// <summary>
        /// 已加载的强类型资源数组（IsDone 为 false 时为 null）。
        /// </summary>
        public T[] Assets
        {
            get
            {
                if (m_Inner == null || !m_Inner.IsDone)
                {
                    return null;
                }

                if (m_Assets != null)
                {
                    return m_Assets;
                }

                IReadOnlyList<UnityEngine.Object> src = m_Inner.AllAssetObjects;
                int count = 0;
                for (int i = 0; i < src.Count; i++)
                {
                    if (src[i] is T)
                    {
                        count++;
                    }
                }

                m_Assets = new T[count];
                int idx = 0;
                for (int i = 0; i < src.Count; i++)
                {
                    if (src[i] is T item)
                    {
                        m_Assets[idx++] = item;
                    }
                }

                return m_Assets;
            }
        }

        /// <summary>
        /// 绑定原生句柄，由 AssetManager 内部在 Get 后立即调用。
        /// </summary>
        /// <param name="inner">YooAsset 原生全资源句柄。</param>
        internal void Bind(AllAssetsHandle inner)
        {
            m_Inner = inner;
            m_Assets = null;
        }

        /// <summary>
        /// 释放整批句柄（引用计数 -1），并将适配器归还 ReferencePool。
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
            m_Assets = null;
        }
    }
}
