/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkManagerBase.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network管理器基类
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 管理器基类。
    /// </summary>
    internal abstract class NetworkManagerBase : FrameworkManager, INetworkManager
    {
        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        /// <remarks>值越小优先级越高，越先 Update、越后 Shutdown。</remarks>
        public override int Priority => 10;

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public abstract void Initialize(NetworkManagerConfig config);

        /// <summary>
        /// 异步加载域名表与指令表的 Luban 数据并构建运行时缓存。
        /// </summary>
        /// <returns>加载并解析成功返回 true。</returns>
        public abstract UniTask<bool> LoadNetCmdsAsync();

        /// <summary>
        /// 同步加载域名表与指令表的 Luban 数据并构建运行时缓存。
        /// </summary>
        /// <returns>加载并解析成功返回 true。</returns>
        public abstract bool LoadNetCmdsSync();

        /// <summary>
        /// 根据复合键（tbName + dtName）获取完整 URL。
        /// </summary>
        /// <param name="tbName">Luban 表类型名称，用于构造复合缓存键以避免跨表同名冲突。</param>
        /// <param name="dtName">NetCmd 数据类型名称。</param>
        /// <returns>目标 URL，不存在时返回 null。</returns>
        public abstract string GetNetCmdUrl(string tbName, string dtName);

        /// <summary>
        /// 根据 NetCmd 名称获取完整 URL，泛型版本提供编译期类型约束。
        /// </summary>
        /// <typeparam name="T">Luban 表类型（编译期约束），Name 用于构造复合缓存键。</typeparam>
        /// <param name="dtName">NetCmd 数据类型名称。</param>
        /// <returns>目标 URL，不存在时返回 null。</returns>
        public abstract string GetNetCmdUrl<T>(string dtName) where T : class, ITable;

        /// <summary>
        /// 根据指令行数据解析完整 URL（HostKey URL + Path）。
        /// </summary>
        /// <param name="cmdRow">网络指令行数据。</param>
        /// <returns>完整 URL 字符串，解析失败时返回 null。</returns>
        public abstract string ResolveNetCmdUrl(INetworkCmdRow cmdRow);

        /// <summary>
        /// 根据指令名称（INetworkCmdRow.Name）解析对应指令行数据。
        /// </summary>
        /// <param name="cmdName">指令名称，对应 INetworkCmdRow.Name。</param>
        /// <returns>匹配的指令行数据，未找到时返回 null。</returns>
        public abstract INetworkCmdRow ResolveNetCmdRow(string cmdName);

        /// <summary>
        /// 获取所有 HTTP 类型 NetCmd 的完整 URL 集合。
        /// </summary>
        /// <returns>去重后的完整 URL 枚举。</returns>
        public abstract IEnumerable<string> GetAllNetCmdUrls();

        /// <summary>
        /// 获取指定类型的 Luban 表实例。
        /// </summary>
        /// <typeparam name="T">Luban 表类型。</typeparam>
        /// <returns>表实例，不存在时返回 null。</returns>
        public abstract T GetNetCmd<T>() where T : class, ITable;

        /// <summary>
        /// 根据表类型名获取指定的 Luban 表实例。
        /// </summary>
        /// <param name="tbName">Luban 表类型名。</param>
        /// <returns>表实例，不存在时返回 null。</returns>
        public abstract ITable GetNetCmd(string tbName);

        /// <summary>
        /// 检查网络激活状态。
        /// </summary>
        /// <returns>网络可用返回 true。</returns>
        public abstract bool CheckNetworkActive();

        /// <summary>
        /// URL 编码。
        /// </summary>
        /// <param name="str">待编码字符串。</param>
        /// <returns>编码后的字符串。</returns>
        public abstract string UrlEncode(string str);

        /// <summary>
        /// 异步查询外网 IP 地址。
        /// </summary>
        /// <returns>外网 IP 地址字符串。</returns>
        public abstract UniTask<string> QueryPublicIPAddressAsync();

        /// <summary>
        /// 查询本机内网 IP 地址。
        /// </summary>
        /// <returns>内网 IP 地址字符串。</returns>
        public abstract string QueryLocalIPAddress();

        /// <summary>
        /// 注入服务器时间获取委托，由业务层提供具体的网络请求实现。
        /// </summary>
        /// <param name="fetcher">异步获取服务器 UTC0 时间戳（毫秒）的委托。</param>
        public abstract void SetServerTimeFetcher(Func<UniTask<long>> fetcher);

        /// <summary>
        /// 异步调用已注入的委托获取服务器 UTC0 时间戳，结果写入 ServerTime。
        /// </summary>
        /// <returns>表示异步操作的 UniTask。</returns>
        public abstract UniTask FetchServerTimeAsync();

        /// <summary>
        /// 服务器时间戳（UTC0，毫秒）。
        /// </summary>
        public abstract long ServerTime { get; }

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public abstract override void Update();

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public abstract override void Shutdown();
    }
}
