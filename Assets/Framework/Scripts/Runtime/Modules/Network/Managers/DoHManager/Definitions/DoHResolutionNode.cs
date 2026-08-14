/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DoHResolutionNode.cs
 * author:    taoye
 * created:   2026/7/23
 * descrip:   DoH解析诊断节点
 ***************************************************************/

using System.Collections.Generic;
using System.Net;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DoH 根查询的来源。
    /// </summary>
    public enum DoHResolutionSource
    {
        /// <summary>
        /// HostKey 数据加载完成后的启动预热。
        /// </summary>
        HostKeyPrewarm,

        /// <summary>
        /// 调用方手动调用 DNSQuery 时显式触发。
        /// </summary>
        RuntimeDiscovered
    }

    /// <summary>
    /// 单个域名的 DoH 解析诊断节点；CNAME 目标通过 Children 表达层级关系。
    /// </summary>
    public sealed class DoHResolutionNode
    {
        /// <summary>
        /// 初始化 DoH 解析诊断节点。
        /// </summary>
        /// <param name="hostName">节点域名。</param>
        /// <param name="source">根查询来源。</param>
        internal DoHResolutionNode(string hostName, DoHResolutionSource source)
        {
            HostName = hostName;
            Source = source;
            Addresses = new List<IPAddress>();
            Children = new List<DoHResolutionNode>();
        }

        /// <summary>
        /// 当前节点的域名。
        /// </summary>
        public string HostName { get; }

        /// <summary>
        /// 当前域名直接解析到的 IP 地址。
        /// </summary>
        public List<IPAddress> Addresses { get; }

        /// <summary>
        /// 当前域名指向的 CNAME 子节点。
        /// </summary>
        public List<DoHResolutionNode> Children { get; }

        /// <summary>
        /// 根查询来源。HostKey 预热优先于手动 DNS 查询。
        /// </summary>
        public DoHResolutionSource Source { get; internal set; }

        /// <summary>
        /// 当前节点或其 CNAME 子树是否获得了 IP。
        /// </summary>
        public bool IsResolved
        {
            get
            {
                if (Addresses.Count > 0)
                {
                    return true;
                }

                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i].IsResolved)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 未获得 IP 时的诊断说明；成功时为空。
        /// </summary>
        public string FailureReason { get; internal set; }
    }
}
