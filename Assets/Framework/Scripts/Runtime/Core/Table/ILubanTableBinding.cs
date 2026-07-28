/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ILubanTableBinding.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   Luban 生成 Tables 的运行时加载 Binding
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 连接 Luban 生成代码与 Nova 资源加载器；表清单和解码逻辑由具体 Binding 决定。
    /// </summary>
    public interface ILubanTableBinding
    {
        /// <summary>
        /// 获取当前 Tables 构造过程需要的全部 output_data_file。
        /// </summary>
        IReadOnlyList<string> DataFiles { get; }

        /// <summary>
        /// 使用原始字节加载器构造 Luban 生成的 Tables 容器。
        /// </summary>
        /// <param name="loader">按 output_data_file 返回独立字节的加载器。</param>
        /// <returns>Luban 生成的 Tables 容器。</returns>
        ILubanTables Create(Func<string, byte[]> loader);
    }
}
