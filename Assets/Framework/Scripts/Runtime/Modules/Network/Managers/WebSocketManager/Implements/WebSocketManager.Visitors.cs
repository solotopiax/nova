/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketManager.Visitors.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   WebSocket管理器 —— 属性与字段
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// WebSocket 管理器。
    /// </summary>
    internal sealed partial class WebSocketManager : WebSocketManagerBase, WebSocketScope.IWebSocketManagerBridge
    {
        /// <summary>
        /// 所有通信通道实例列表。
        /// </summary>
        private List<WebSocketScope.NetChannelBase> m_NetChannels;

        /// <summary>
        /// 所有通信通道实例（只读视图）。
        /// </summary>
        public override IReadOnlyList<WebSocketScope.NetChannelBase> NetChannels => m_NetChannels;

        /// <summary>
        /// <通道实例, 建立连接协程句柄>。
        /// </summary>
        private Dictionary<WebSocketScope.NetChannelBase, Coroutine> m_ConnectServerCoroutines;

        /// <summary>
        /// <通道实例, 身份认证协程句柄>。
        /// </summary>
        private Dictionary<WebSocketScope.NetChannelBase, Coroutine> m_AutoAuthenticateCoroutines;

        /// <summary>
        /// <通道实例, 心跳协程句柄>。
        /// </summary>
        private Dictionary<WebSocketScope.NetChannelBase, Coroutine> m_AutoHeartBeatCoroutines;

        /// <summary>
        /// <通道实例, 断线重连协程句柄>。
        /// </summary>
        private Dictionary<WebSocketScope.NetChannelBase, Coroutine> m_AutoReconnectCoroutines;

        /// <summary>
        /// 协程运行器，由初始化配置注入。
        /// </summary>
        private ICoroutineRunner m_CoroutineRunner;

        /// <summary>
        /// 特殊消息（心跳/认证）创建委托，由初始化配置注入。
        /// </summary>
        private Func<WebSocketScope.NetChannelType, string, WebSocketScope.NetMessageBase> m_SpecialMessageCreator;

        /// <summary>
        /// 连接超时时间（秒）。
        /// </summary>
        private float m_ConnectTimeout = 10f;
        public override float ConnectTimeout => m_ConnectTimeout;

        /// <summary>
        /// 身份认证超时时间（秒）。
        /// </summary>
        private float m_AuthenticateTimeout = 10f;
        public override float AuthenticateTimeout => m_AuthenticateTimeout;

        /// <summary>
        /// 心跳发送间隔（秒）。
        /// </summary>
        private float m_HeartBeatTimeInterval = 20f;

        /// <summary>
        /// 心跳响应超时时间（秒）。
        /// </summary>
        private float m_HeartBeatTimeout = 10f;
        public override float HeartBeatTimeout => m_HeartBeatTimeout;

        /// <summary>
        /// 自动重连最大次数。
        /// </summary>
        private int m_AutoReconnectMaxCounter = 5;

        /// <summary>
        /// 自动重连间隔时间（秒）。
        /// </summary>
        private float m_AutoReconnectTimeInterval = 3f;

        /// <summary>
        /// 是否启用自动重连。
        /// </summary>
        private bool m_EnableAutoReconnect = true;

        /// <summary>
        /// 开始连接事件，<通道索引, 服务器地址>。
        /// </summary>
        public override event Action<int, string> OnBeginConnect;

        /// <summary>
        /// 连接成功事件，<通道索引, 服务器地址>。
        /// </summary>
        public override event Action<int, string> OnConnectSuccess;

        /// <summary>
        /// 连接失败事件，<通道索引, 服务器地址>。
        /// </summary>
        public override event Action<int, string> OnConnectFail;

        /// <summary>
        /// 断开连接事件，<通道索引, 服务器地址>。
        /// </summary>
        public override event Action<int, string> OnDisconnect;

        /// <summary>
        /// 重连失败事件（已达上限），<通道索引, 服务器地址>。
        /// </summary>
        public override event Action<int, string> OnReconnectFailed;

        /// <summary>
        /// 认证成功事件，<通道索引, 服务器地址>。
        /// </summary>
        public override event Action<int, string> OnAuthenticateSuccess;

        /// <summary>
        /// 认证失败事件，<通道索引, 服务器地址>。
        /// </summary>
        public override event Action<int, string> OnAuthenticateFail;

        /// <summary>
        /// 收到消息事件，<通道实例, 消息对象>。
        /// </summary>
        public override event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnReceiveMessage;

        /// <summary>
        /// 发送消息成功事件，<通道实例, 消息对象>。
        /// </summary>
        public override event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnSendMessage;
    }
}
