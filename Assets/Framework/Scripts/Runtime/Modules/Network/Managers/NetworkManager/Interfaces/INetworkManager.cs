/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  INetworkManager.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network管理器接口
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network管理器接口，负责 NetCmd URL 路由、网络状态检测与服务器时间获取。
    /// </summary>
    public interface INetworkManager
    {
        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        void Initialize(NetworkManagerConfig config);

        /// <summary>
        /// 异步加载域名表与指令表的 Luban 数据并构建运行时缓存。
        /// </summary>
        /// <returns>加载并解析成功返回 true。</returns>
        UniTask<bool> LoadNetCmdsAsync();

        /// <summary>
        /// 同步加载域名表与指令表的 Luban 数据并构建运行时缓存。
        /// </summary>
        /// <returns>加载并解析成功返回 true。</returns>
        bool LoadNetCmdsSync();

        /// <summary>
        /// 根据 NetCmd 名称获取对应完整 URL（HostKey URL + Path）。
        /// </summary>
        /// <param name="tbName">Luban 表类型名称，用于构造复合缓存键以避免跨表同名冲突。</param>
        /// <param name="dtName">NetCmd 数据类型名称。</param>
        /// <returns>目标 URL 字符串，名称不存在时返回 null。</returns>
        string GetNetCmdUrl(string tbName, string dtName);

        /// <summary>
        /// 根据 NetCmd 名称获取对应完整 URL（HostKey URL + Path），泛型版本提供编译期类型约束。
        /// </summary>
        /// <typeparam name="T">Luban 表类型（编译期约束），Name 用于构造复合缓存键。</typeparam>
        /// <param name="dtName">NetCmd 数据类型名称。</param>
        /// <returns>目标 URL 字符串，名称不存在时返回 null。</returns>
        string GetNetCmdUrl<T>(string dtName) where T : class, ITable;

        /// <summary>
        /// 根据指令行数据解析完整 URL（HostKey URL + Path）。
        /// </summary>
        /// <param name="cmdRow">网络指令行数据。</param>
        /// <returns>完整 URL 字符串，解析失败时返回 null。</returns>
        string ResolveNetCmdUrl(INetworkCmdRow cmdRow);

        /// <summary>
        /// 根据指令名称（INetworkCmdRow.Name）解析对应指令行数据。
        /// </summary>
        /// <param name="cmdName">指令名称，对应 INetworkCmdRow.Name。</param>
        /// <returns>匹配的指令行数据，未找到时返回 null。</returns>
        INetworkCmdRow ResolveNetCmdRow(string cmdName);

        /// <summary>
        /// 获取当前已加载的所有 HTTP 类型 NetCmd 的完整 URL 集合，供 DoH 收集 IP 使用。
        /// </summary>
        /// <returns>去重后的完整 URL 枚举。</returns>
        IEnumerable<string> GetAllNetCmdUrls();

        /// <summary>
        /// 获取指定类型的 Luban 表实例。
        /// </summary>
        /// <typeparam name="T">Luban 表类型。</typeparam>
        /// <returns>表实例，不存在时返回 null。</returns>
        T GetNetCmd<T>() where T : class, ITable;

        /// <summary>
        /// 根据表类型名获取指定的 Luban 表实例。
        /// </summary>
        /// <param name="tbName">Luban 表类型名。</param>
        /// <returns>表实例，不存在时返回 null。</returns>
        ITable GetNetCmd(string tbName);

        /// <summary>
        /// 检查当前设备网络激活状态。
        /// </summary>
        /// <returns>网络可用时返回 true，否则返回 false。</returns>
        bool CheckNetworkActive();

        /// <summary>
        /// 对字符串进行 URL 编码（UTF-8 百分号编码）。
        /// </summary>
        /// <param name="str">待编码的字符串。</param>
        /// <returns>编码后的字符串。</returns>
        string UrlEncode(string str);

        /// <summary>
        /// 异步查询外网 IP 地址。
        /// </summary>
        /// <returns>外网 IP 地址字符串。</returns>
        UniTask<string> QueryPublicIPAddressAsync();

        /// <summary>
        /// 查询本机内网 IP 地址。
        /// </summary>
        /// <returns>内网 IP 地址字符串，无可用地址时返回 null。</returns>
        string QueryLocalIPAddress();

        /// <summary>
        /// 注入服务器时间获取委托，由业务层提供具体的网络请求实现。
        /// </summary>
        /// <param name="fetcher">异步获取服务器 UTC0 时间戳（毫秒）的委托。</param>
        void SetServerTimeFetcher(Func<UniTask<long>> fetcher);

        /// <summary>
        /// 异步调用已注入的委托获取服务器 UTC0 时间戳，结果写入 ServerTime。
        /// </summary>
        /// <returns>表示异步操作的 UniTask。</returns>
        UniTask FetchServerTimeAsync();

        /// <summary>
        /// 服务器时间戳（UTC0，毫秒）。
        /// </summary>
        long ServerTime { get; }
    }
}
