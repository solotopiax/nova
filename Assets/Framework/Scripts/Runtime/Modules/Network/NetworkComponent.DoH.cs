/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponent.DoH.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network组件 —— DoH接口
 ***************************************************************/

using System.Net;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 组件 —— DoH DNS 解析接口。
    /// </summary>
    public sealed partial class NetworkComponent : FrameworkComponent
    {
        /// <summary>
        /// 遍历当前已加载的 NetCmd 中所有 HTTP 类型 URL，异步收集各域名 IP。
        /// 通常在 LoadNetCmds 完成后调用一次。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async UniTask CollectAllIPAddresses()
        {
            await m_DoHManager.CollectAllIPAddresses(m_NetworkManager.GetAllNetCmdUrls());
        }

        /// <summary>
        /// 对指定 URL 执行 DoH DNS 查询。
        /// </summary>
        /// <param name="url">目标 URL。</param>
        /// <returns>异步任务。</returns>
        public async UniTask DNSQuery(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new System.ArgumentException("Value cannot be null or empty.", nameof(url));
            }

            await m_DoHManager.DNSQuery(url);
        }

        /// <summary>
        /// 从 URL 中提取主机名。
        /// </summary>
        /// <param name="url">完整 URL 字符串。</param>
        /// <returns>主机名字符串。</returns>
        public string GetHostName(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new System.ArgumentException("Value cannot be null or empty.", nameof(url));
            }

            return m_DoHManager.GetHostName(url);
        }

        /// <summary>
        /// 通过主机名获取已收集的 IP 地址数组。
        /// </summary>
        /// <param name="hostName">目标主机名。</param>
        /// <returns>IP 地址数组，未收集时返回 null。</returns>
        public IPAddress[] GetIPAddresses(string hostName)
        {
            if (string.IsNullOrEmpty(hostName))
            {
                throw new System.ArgumentException("Value cannot be null or empty.", nameof(hostName));
            }

            return m_DoHManager.GetIPAddresses(hostName);
        }

        /// <summary>
        /// 清空所有已收集的 IP 地址与 DNS 缓存。
        /// </summary>
        public void ClearDoH()
        {
            m_DoHManager.Clear();
        }
    }
}
