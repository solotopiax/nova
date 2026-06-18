/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponent.Visitors.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network组件 —— 属性与字段
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 组件。
    /// </summary>
    public sealed partial class NetworkComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前 Network 管理器类型名称（Inspector 下拉选择）。
        /// </summary>
        [Tooltip("Network 管理器实现类全名")]
        [SerializeField]
        private string m_CurNetworkManagerTypeName = "NovaFramework.Runtime.NetworkManager";
        public string CurNetworkManagerTypeName => m_CurNetworkManagerTypeName;

        /// <summary>
        /// 当前 HTTP 管理器类型名称（Inspector 下拉选择）。
        /// </summary>
        [Tooltip("HTTP 管理器实现类全名")]
        [SerializeField]
        private string m_CurHttpManagerTypeName = "NovaFramework.Runtime.HttpManager";
        public string CurHttpManagerTypeName => m_CurHttpManagerTypeName;

        /// <summary>
        /// 当前 DoH 管理器类型名称（Inspector 下拉选择）。
        /// </summary>
        [Tooltip("DoH 管理器实现类全名")]
        [SerializeField]
        private string m_CurDoHManagerTypeName = "NovaFramework.Runtime.DoHManager";
        public string CurDoHManagerTypeName => m_CurDoHManagerTypeName;

        /// <summary>
        /// 当前 WebSocket 管理器类型名称（Inspector 下拉选择）。
        /// </summary>
        [Tooltip("WebSocket 管理器实现类全名")]
        [SerializeField]
        private string m_CurWebSocketManagerTypeName = "NovaFramework.Runtime.WebSocketManager";
        public string CurWebSocketManagerTypeName => m_CurWebSocketManagerTypeName;

        /// <summary>
        /// 网络设置，包含域名表（HostKey）和指令表（NetCmd）两套独立 Luban 构建单元。
        /// </summary>
        [Tooltip("网络设置（HostKey 和 NetCmd 构建单元配置）")]
        [SerializeField]
        private NetworkSettings m_Settings;
        public NetworkSettings NetworkSettings => m_Settings;

        /// <summary>
        /// HTTP 管理器参数配置。
        /// </summary>
        [Tooltip("HTTP 管理器参数配置（超时、重试等）")]
        [SerializeField]
        private HttpSettings m_HttpSettings;

        /// <summary>
        /// DoH 管理器参数配置。
        /// </summary>
        [Tooltip("DoH (DNS over HTTPS) 管理器参数配置")]
        [SerializeField]
        private DoHSettings m_DoHSettings;

        /// <summary>
        /// WebSocket 管理器参数配置。
        /// </summary>
        [Tooltip("WebSocket 管理器参数配置（连接、心跳等）")]
        [SerializeField]
        private WebSocketSettings m_WebSocketSettings;

#if UNITY_EDITOR
        /// <summary>
        /// Protobuf 编辑器设置。
        /// </summary>
        [Tooltip("Protobuf 编辑器设置（Proto 文件管理与编译配置）")]
        [SerializeField]
        private ProtoSettings m_ProtoSettings;
        /// <summary>
        /// 编辑器侧获取 Protobuf 编辑器设置（仅 UNITY_EDITOR 可用）。
        /// </summary>
        public ProtoSettings ProtoSettings => m_ProtoSettings;
#endif

        /// <summary>
        /// 异步加载 NetCmd 数据任务完成源，防止重复加载的并发入口。
        /// </summary>
        private UniTaskCompletionSource<bool> m_LoadTcs;

        /// <summary>
        /// NetCmd 数据是否已加载完成。
        /// </summary>
        public bool IsLoadOver { get; private set; }

        /// <summary>
        /// Network 管理器实例。
        /// </summary>
        private INetworkManager m_NetworkManager;
        public INetworkManager NetworkManager => m_NetworkManager;

        /// <summary>
        /// HTTP 管理器实例。
        /// </summary>
        private IHttpManager m_HttpManager;
        public IHttpManager HttpManager => m_HttpManager;

        /// <summary>
        /// DoH 管理器实例。
        /// </summary>
        private IDoHManager m_DoHManager;
        public IDoHManager DoHManager => m_DoHManager;

        /// <summary>
        /// WebSocket 管理器实例。
        /// </summary>
        private IWebSocketManager m_WebSocketManager;
        public IWebSocketManager WebSocketManager => m_WebSocketManager;

        /// <summary>
        /// 服务器时间戳（UTC0，毫秒），由 FetchServerTimeAsync 写入。
        /// </summary>
        public long ServerTime => m_NetworkManager.ServerTime;

        /// <summary>
        /// 所有已收集的 IP 地址，<原始 URL, 替换 IP 后的 URL 列表>。
        /// </summary>
        public IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses => m_DoHManager.AllCollectedIPAddresses;

        /// <summary>
        /// 所有域名对应的 IP 地址，<主机名, IPAddress 列表>。
        /// </summary>
        public IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses => m_DoHManager.AllDomainIPAddresses;

        /// <summary>
        /// 所有 WebSocket 通信通道实例（只读），供运行时状态监控使用。
        /// </summary>
        public IReadOnlyList<WebSocketScope.NetChannelBase> WebSocketNetChannels => m_WebSocketManager.NetChannels;

        /// <summary>
        /// Kit Service 实例字典，<Service 类型, Service 实例>。
        /// </summary>
        private readonly Dictionary<Type, object> m_KitInstances = new Dictionary<Type, object>();
    }
}
