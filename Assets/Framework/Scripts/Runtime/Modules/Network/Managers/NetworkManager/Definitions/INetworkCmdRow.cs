/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  INetworkCmdRow.cs
 * author:    taoye
 * created:   2026/4/26
 * descrip:   网络指令数据行接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 网络指令数据行接口。
    /// Luban 生成的 NetCmd bean 类须实现此接口，框架侧通过接口直接访问字段，彻底消除反射和 JArray 直接解析。
    /// </summary>
    public interface INetworkCmdRow
    {
        /// <summary>
        /// 指令名称（主键）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 网络方式（"HTTP_GET" / "HTTP_POST" / "HTTP_URL" / "WS"）。
        /// </summary>
        string Way { get; }

        /// <summary>
        /// Host 唯一标识，对应域名表的 Key。
        /// </summary>
        string HostKey { get; }

        /// <summary>
        /// 接口路径，与 HostKey URL 拼接成完整 URL。
        /// </summary>
        string Path { get; }
    }
}
