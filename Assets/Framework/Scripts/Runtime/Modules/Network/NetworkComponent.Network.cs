/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponent.Network.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network组件 —— NetworkManager接口
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 组件 —— NetCmd 路由、网络状态与服务器时间接口。
    /// </summary>
    public sealed partial class NetworkComponent : FrameworkComponent
    {
        /// <summary>
        /// 获取指定类型的 Luban 表实例。
        /// </summary>
        /// <typeparam name="T">Luban 表类型。</typeparam>
        /// <returns>表实例，不存在时返回 null。</returns>
        public T GetNetCmd<T>() where T : class, ITable
        {
            return m_NetworkManager.GetNetCmd<T>();
        }

        /// <summary>
        /// 根据表类型名获取指定的 Luban 表实例。
        /// </summary>
        /// <param name="tbName">Luban 表类型名。</param>
        /// <returns>表实例，不存在时返回 null。</returns>
        public ITable GetNetCmd(string tbName)
        {
            return m_NetworkManager.GetNetCmd(tbName);
        }

        /// <summary>
        /// 根据复合键（tbName + dtName）获取对应完整 URL（HostKey URL + Path）。
        /// </summary>
        /// <param name="tbName">Luban 表类型名称，用于构造复合缓存键以避免跨表同名冲突。</param>
        /// <param name="dtName">NetCmd 数据类型名称。</param>
        /// <returns>目标 URL 字符串，名称不存在时返回 null。</returns>
        public string GetNetCmdUrl(string tbName, string dtName)
        {
            return m_NetworkManager.GetNetCmdUrl(tbName, dtName);
        }

        /// <summary>
        /// 根据 NetCmd 名称获取对应完整 URL（HostKey URL + Path），泛型版本提供编译期类型约束。
        /// </summary>
        /// <typeparam name="T">Luban 表类型（编译期约束），Name 用于构造复合缓存键。</typeparam>
        /// <param name="dtName">NetCmd 数据类型名称。</param>
        /// <returns>目标 URL 字符串，名称不存在时返回 null。</returns>
        public string GetNetCmdUrl<T>(string dtName) where T : class, ITable
        {
            return m_NetworkManager.GetNetCmdUrl<T>(dtName);
        }

        /// <summary>
        /// 根据指令行数据解析完整 URL（HostKey URL + Path）。
        /// </summary>
        /// <param name="cmdRow">网络指令行数据。</param>
        /// <returns>完整 URL 字符串，解析失败时返回 null。</returns>
        public string ResolveNetCmdUrl(INetworkCmdRow cmdRow)
        {
            return m_NetworkManager.ResolveNetCmdUrl(cmdRow);
        }

        /// <summary>
        /// 根据指令名称（INetworkCmdRow.Name）解析对应指令行数据。
        /// </summary>
        /// <param name="cmdName">指令名称。</param>
        /// <returns>匹配的指令行数据，未找到时返回 null。</returns>
        public INetworkCmdRow ResolveNetCmdRow(string cmdName)
        {
            return m_NetworkManager.ResolveNetCmdRow(cmdName);
        }

        /// <summary>
        /// 检查当前设备网络激活状态。
        /// </summary>
        /// <returns>网络可用时返回 true，否则返回 false。</returns>
        public bool CheckNetworkActive()
        {
            return m_NetworkManager.CheckNetworkActive();
        }

        /// <summary>
        /// 对字符串进行 URL 编码（UTF-8 百分号编码）。
        /// </summary>
        /// <param name="str">待编码的字符串。</param>
        /// <returns>编码后的字符串。</returns>
        public string UrlEncode(string str)
        {
            if (str == null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            return m_NetworkManager.UrlEncode(str);
        }

        /// <summary>
        /// 查询本机内网 IP 地址。
        /// </summary>
        /// <returns>内网 IP 地址字符串，无可用地址时返回 null。</returns>
        public string QueryLocalIPAddress()
        {
            return m_NetworkManager.QueryLocalIPAddress();
        }

        /// <summary>
        /// 注入服务器时间获取委托，由业务层提供具体的网络请求实现。
        /// </summary>
        /// <param name="fetcher">异步获取服务器 UTC0 时间戳（毫秒）的委托。</param>
        public void SetServerTimeFetcher(Func<UniTask<long>> fetcher)
        {
            if (fetcher == null)
            {
                throw new ArgumentNullException(nameof(fetcher));
            }

            m_NetworkManager.SetServerTimeFetcher(fetcher);
        }

        /// <summary>
        /// 异步调用已注入的委托获取服务器 UTC0 时间戳，结果写入 ServerTime。
        /// </summary>
        /// <returns>表示异步操作的 UniTask。</returns>
        public UniTask FetchServerTimeAsync()
        {
            return m_NetworkManager.FetchServerTimeAsync();
        }
    }
}
