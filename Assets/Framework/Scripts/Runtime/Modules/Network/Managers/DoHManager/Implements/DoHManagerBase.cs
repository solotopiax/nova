/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DoHManagerBase.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DoH管理器基类
 ***************************************************************/

using System.Collections.Generic;
using System.Net;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DoH 管理器基类。
    /// </summary>
    internal abstract class DoHManagerBase : FrameworkManager, IDoHManager
    {
        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        /// <remarks>值越小优先级越高，越先 Update、越后 Shutdown。</remarks>
        public override int Priority => 11;

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public abstract void Initialize(DoHManagerConfig config);

        /// <summary>
        /// 当前运行时是否实际启用 DoH。
        /// </summary>
        public abstract bool IsEnabled { get; }

        /// <summary>
        /// 在当前进程内禁用 DoH，后续请求改用系统 DNS。
        /// </summary>
        public abstract void DisableForRuntime();

        /// <summary>
        /// 收集所有 HostKey URL 对应的 IP 地址。
        /// </summary>
        /// <param name="urls">HostKey URL 集合。</param>
        /// <returns>异步任务。</returns>
        public abstract UniTask CollectAllIPAddresses(IEnumerable<string> urls);

        /// <summary>
        /// 对指定 URL 执行 DoH DNS 查询。
        /// </summary>
        /// <param name="url">目标 URL。</param>
        /// <returns>异步任务。</returns>
        public abstract UniTask DNSQuery(string url);

        /// <summary>
        /// 根据 DoH 缓存与即时查询结果构造请求候选 URL。
        /// </summary>
        /// <param name="originalUrl">原始请求 URL。</param>
        /// <param name="canUseIpCandidate">是否允许生成 IP 直连候选。</param>
        /// <returns>按 IP 候选、原始 URL 顺序排列的候选列表。</returns>
        public abstract UniTask<IReadOnlyList<string>> BuildRequestUrlCandidatesAsync(string originalUrl, bool canUseIpCandidate);

        /// <summary>
        /// 从 URL 中提取主机名。
        /// </summary>
        /// <param name="url">完整 URL 字符串。</param>
        /// <returns>主机名字符串。</returns>
        public abstract string GetHostName(string url);

        /// <summary>
        /// 通过主机名获取已收集的 IP 地址数组。
        /// </summary>
        /// <param name="hostName">目标主机名。</param>
        /// <returns>IP 地址数组，未收集时返回 null。</returns>
        public abstract IPAddress[] GetIPAddresses(string hostName);

        /// <summary>
        /// 清空所有已收集的 IP 地址与 DNS 缓存。
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// 原始业务域名对应的最终 IP 地址，<原始主机名, IPAddress 列表>。
        /// </summary>
        public abstract IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses { get; }

        /// <summary>
        /// 按原始业务域名保存的 DoH 解析诊断树。
        /// </summary>
        public abstract IReadOnlyDictionary<string, DoHResolutionNode> ResolutionRoots { get; }

        /// <summary>
        /// 所有已收集的 IP 地址，<原始 URL, 替换 IP 后的 URL 列表>。
        /// </summary>
        public abstract IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses { get; }

        /// <summary>
        /// 最近一次 DNSQuery 返回的 DNS 应答集合。
        /// </summary>
        public abstract DNSAnswer[] DNSAnswers { get; }

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
