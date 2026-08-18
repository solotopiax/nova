/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  YooAssetRawFileHandleAdapter.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   YooAsset AssetHandle/RawFileObject 到 IRawFileHandle 的内部适配器
 ***************************************************************/

using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset AssetHandle 与 RawFileObject 到 IRawFileHandle 的内部适配器。
    /// 通过 ReferencePool 复用，调用 Release 后自动归池。
    /// </summary>
    internal sealed class YooAssetRawFileHandleAdapter : IRawFileHandle, IReference
    {
        /// <summary>
        /// 持有 RawFileObject 生命周期的 YooAsset 资源句柄。
        /// </summary>
        private AssetHandle m_Inner;

        /// <summary>
        /// 由资源句柄加载得到的原始文件字节对象。
        /// </summary>
        private RawFileObject m_RawFile;

        /// <summary>
        /// EnsureBundleFileAsync 返回的底层资源包文件路径。
        /// </summary>
        private string m_FilePath;

        /// <summary>
        /// 标记当前租约是否已经释放，避免重复归池和重复减少资源引用计数。
        /// </summary>
        private bool m_IsReleased = true;

        /// <summary>
        /// 句柄是否仍然有效（m_Inner 不为 null 且原生句柄有效）。
        /// </summary>
        public bool IsValid => m_Inner != null && m_Inner.IsValid;

        /// <summary>
        /// 异步加载是否已完成。
        /// </summary>
        public bool IsDone => m_Inner != null && m_Inner.IsDone;

        /// <summary>
        /// 尽力解析的底层资源包文件路径；同步加载、Web 或内存文件系统下可能为 null。
        /// </summary>
        public string FilePath => !m_IsReleased && IsValid && IsDone ? m_FilePath : null;

        /// <summary>
        /// 从 RawFileObject 读取全部字节副本（未完成、句柄无效或已释放时返回 null）。
        /// </summary>
        /// <returns>文件字节数组，或 null（未就绪 / 文件不存在）。</returns>
        public byte[] GetBytes()
        {
            return !m_IsReleased && IsValid && IsDone ? m_RawFile?.GetBytes() : null;
        }

        /// <summary>
        /// 绑定 YooAsset 资源句柄、原始文件对象与已确保就绪的底层包文件路径。
        /// </summary>
        /// <param name="inner">持有原始文件对象生命周期的资源句柄。</param>
        /// <param name="rawFile">资源句柄加载得到的原始文件对象。</param>
        /// <param name="filePath">EnsureBundleFileAsync 返回的底层资源包文件路径。</param>
        internal void Bind(AssetHandle inner, RawFileObject rawFile, string filePath)
        {
            m_Inner = inner;
            m_RawFile = rawFile;
            m_FilePath = filePath;
            m_IsReleased = false;
        }

        /// <summary>
        /// 释放句柄（引用计数 -1），并将适配器归还 ReferencePool。
        /// </summary>
        public void Release()
        {
            if (m_IsReleased)
                return;

            m_IsReleased = true;
            AssetHandle inner = m_Inner;
            m_Inner = null;
            m_RawFile = null;
            m_FilePath = null;
            try
            {
                inner?.Release();
            }
            finally
            {
                ReferencePool.Put(this);
            }
        }

        /// <summary>
        /// 清理内部状态，由 ReferencePool 在归池或重建时调用。
        /// </summary>
        void IReference.Clear()
        {
            m_Inner = null;
            m_RawFile = null;
            m_FilePath = null;
            m_IsReleased = true;
        }
    }
}
