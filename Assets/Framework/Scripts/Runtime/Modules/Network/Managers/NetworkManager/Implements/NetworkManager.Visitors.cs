/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkManager.Visitors.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network管理器 —— 属性与字段
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 管理器。
    /// </summary>
    internal sealed partial class NetworkManager : NetworkManagerBase
    {
        /// <summary>
        /// HostKeyTables 类短名称，反射构造时使用。
        /// </summary>
        private const string c_HostKeyTablesClassName = "HostKeyTables";

        /// <summary>
        /// NetworkTables 类短名称，反射构造时使用。
        /// </summary>
        private const string c_NetworkTablesClassName = "NetworkTables";

        /// <summary>
        /// 资源管理器，在 Initialize 中获取并缓存，供 DataReceiver 委托使用。
        /// </summary>
        private IAssetManager m_AssetManager;

        /// <summary>
        /// HTTP 管理器，在 Initialize 中获取并缓存，供 QueryPublicIPAddressAsync 使用。
        /// </summary>
        private IHttpManager m_HttpManager;

        /// <summary>
        /// 服务器时间获取委托，由业务层通过 SetServerTimeFetcher 注入，返回 UTC0 毫秒时间戳。
        /// </summary>
        private Func<UniTask<long>> m_ServerTimeFetcher;

        /// <summary>
        /// 域名表单元设置列表。
        /// </summary>
        private List<HostKeyUnitSetting> m_HostKeyUnitSettings;

        /// <summary>
        /// 指令表单元设置列表。
        /// </summary>
        private List<NetCmdUnitSetting> m_NetCmdUnitSettings;

        /// <summary>
        /// Luban 表实例存储，<类型名, ITable 实例>。
        /// </summary>
        private Dictionary<string, ITable> m_NetworkDatas;

        /// <summary>
        /// 域名运行时缓存，<HostKey 名称, Host URL>，从 Luban JSON 中提取。
        /// </summary>
        private Dictionary<string, string> m_HostKeyCache;

        /// <summary>
        /// 指令运行时缓存，<"表类型名.Cmd名称", CmdCacheEntry>，复合键避免跨表同名冲突。
        /// </summary>
        private Dictionary<string, CmdCacheEntry> m_CmdCache;

        /// <summary>
        /// 指令行索引，<INetworkCmdRow.Name, INetworkCmdRow>，供 ResolveNetCmdRow 按名称单键查找；同名后写覆盖前写。
        /// </summary>
        private Dictionary<string, INetworkCmdRow> m_CmdRowIndex;

        /// <summary>
        /// 服务器时间戳（UTC0，毫秒），由 FetchServerTimeAsync 写入。
        /// </summary>
        private long m_ServerTime;
        public override long ServerTime => m_ServerTime;

        /// <summary>
        /// 指令缓存条目，存储从 Luban JSON 中提取的指令运行时数据。
        /// </summary>
        private struct CmdCacheEntry
        {
            /// <summary>
            /// 网络方式（"HTTP_GET" / "HTTP_POST" / "HTTP_URL" / "WS"）。
            /// </summary>
            public string Way;

            /// <summary>
            /// Host 唯一标识，对应域名表的 Key。
            /// </summary>
            public string HostKey;

            /// <summary>
            /// 接口路径，与 HostKey URL 拼接成完整 URL。
            /// </summary>
            public string Path;
        }
    }
}
