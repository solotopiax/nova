/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  YooAssetRawFileHandleAdapter.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   YooAsset.RawFileHandle 到 IRawFileHandle 的内部适配器，复用 ReferencePool 减少 GC
 ***************************************************************/

using System.IO;
using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset.RawFileHandle 到 IRawFileHandle 的内部适配器。
    /// 通过 ReferencePool 复用，调用 Release 后自动归池。
    /// </summary>
    internal sealed class YooAssetRawFileHandleAdapter : IRawFileHandle, IReference
    {
        /// <summary>
        /// 被包装的 YooAsset 原生原始文件句柄。
        /// </summary>
        private RawFileHandle m_Inner;

        /// <summary>
        /// 句柄是否仍然有效（m_Inner 不为 null 且原生句柄有效）。
        /// </summary>
        public bool IsValid => m_Inner != null && m_Inner.IsValid;

        /// <summary>
        /// 异步加载是否已完成。
        /// </summary>
        public bool IsDone => m_Inner != null && m_Inner.IsDone;

        /// <summary>
        /// 原始文件在本地磁盘的绝对路径（IsDone 为 false 时为 null 或空字符串）。
        /// </summary>
        public string FilePath => m_Inner != null && m_Inner.IsDone ? m_Inner.GetRawFilePath() : null;

        /// <summary>
        /// 读取文件全部字节（IsDone 为 false 时返回 null）。
        /// 每次调用均执行磁盘 IO，建议调用方缓存结果。
        /// </summary>
        /// <returns>文件字节数组，或 null（未就绪 / 文件不存在）。</returns>
        public byte[] GetBytes()
        {
            if (m_Inner == null || !m_Inner.IsDone)
            {
                return null;
            }

            string path = m_Inner.GetRawFilePath();
            return string.IsNullOrEmpty(path) ? null : File.ReadAllBytes(path);
        }

        /// <summary>
        /// 绑定原生句柄，由 AssetManager 内部在 Get 后立即调用。
        /// </summary>
        /// <param name="inner">YooAsset 原生原始文件句柄。</param>
        internal void Bind(RawFileHandle inner)
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
