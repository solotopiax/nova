/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IRawFileHandle.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   原始文件句柄中性接口，不耦合任何第三方资源框架
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 原始文件句柄中性接口（框架层抽象，不耦合任何第三方资源框架）。
    /// <para>
    /// 适用于以 RawFile 模式打包的二进制资源（DLL 字节、数据文件等）。
    /// 使用完毕后必须调用 Release 以归还引用计数。
    /// </para>
    /// </summary>
    public interface IRawFileHandle
    {
        /// <summary>
        /// 句柄是否仍然有效（未释放）。
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 异步加载是否已完成。
        /// </summary>
        bool IsDone { get; }

        /// <summary>
        /// 尽力解析的底层资源包文件路径；同步加载、未完成、Web 或内存文件系统下可能为 null。
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// 读取原始文件的全部字节副本（IsDone 为 false 时返回 null）。
        /// </summary>
        /// <returns>文件字节数组，或 null（未就绪 / 文件不存在）。</returns>
        byte[] GetBytes();

        /// <summary>
        /// 释放句柄（引用计数 -1）。
        /// </summary>
        void Release();
    }
}
