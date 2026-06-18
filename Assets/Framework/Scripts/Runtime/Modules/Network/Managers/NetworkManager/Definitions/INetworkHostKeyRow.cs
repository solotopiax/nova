/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  INetworkHostKeyRow.cs
 * author:    taoye
 * created:   2026/4/26
 * descrip:   域名数据行接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 域名数据行接口。
    /// Luban 生成的 HostKey bean 类须实现此接口，框架侧通过接口直接访问字段。
    /// </summary>
    public interface INetworkHostKeyRow
    {
        /// <summary>
        /// 域名标识名称（主键）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 域名 URL 值。
        /// </summary>
        string Value { get; }
    }
}
