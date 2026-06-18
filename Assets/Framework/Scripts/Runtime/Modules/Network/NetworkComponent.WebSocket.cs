/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponent.WebSocket.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network组件 —— WebSocket接口
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Network 组件 —— WebSocket 长连接接口。
    /// </summary>
    public sealed partial class NetworkComponent : FrameworkComponent
    {
        /// <summary>开始连接事件，<通道索引, 服务器地址>。</summary>
        public event Action<int, string> OnWebSocketBeginConnect
        {
            add    => m_WebSocketManager.OnBeginConnect    += value;
            remove => m_WebSocketManager.OnBeginConnect    -= value;
        }

        /// <summary>连接成功事件，<通道索引, 服务器地址>。</summary>
        public event Action<int, string> OnWebSocketConnectSuccess
        {
            add    => m_WebSocketManager.OnConnectSuccess  += value;
            remove => m_WebSocketManager.OnConnectSuccess  -= value;
        }

        /// <summary>连接失败事件，<通道索引, 服务器地址>。</summary>
        public event Action<int, string> OnWebSocketConnectFail
        {
            add    => m_WebSocketManager.OnConnectFail     += value;
            remove => m_WebSocketManager.OnConnectFail     -= value;
        }

        /// <summary>断开连接事件，<通道索引, 服务器地址>。</summary>
        public event Action<int, string> OnWebSocketDisconnect
        {
            add    => m_WebSocketManager.OnDisconnect      += value;
            remove => m_WebSocketManager.OnDisconnect      -= value;
        }

        /// <summary>重连失败（已达上限）事件，<通道索引, 服务器地址>。</summary>
        public event Action<int, string> OnWebSocketReconnectFailed
        {
            add    => m_WebSocketManager.OnReconnectFailed += value;
            remove => m_WebSocketManager.OnReconnectFailed -= value;
        }

        /// <summary>认证成功事件，<通道索引, 服务器地址>。</summary>
        public event Action<int, string> OnWebSocketAuthenticateSuccess
        {
            add    => m_WebSocketManager.OnAuthenticateSuccess += value;
            remove => m_WebSocketManager.OnAuthenticateSuccess -= value;
        }

        /// <summary>认证失败事件，<通道索引, 服务器地址>。</summary>
        public event Action<int, string> OnWebSocketAuthenticateFail
        {
            add    => m_WebSocketManager.OnAuthenticateFail += value;
            remove => m_WebSocketManager.OnAuthenticateFail -= value;
        }

        /// <summary>收到消息事件，<通道实例, 消息对象>。</summary>
        public event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnWebSocketReceiveMessage
        {
            add    => m_WebSocketManager.OnReceiveMessage  += value;
            remove => m_WebSocketManager.OnReceiveMessage  -= value;
        }

        /// <summary>发送消息成功事件，<通道实例, 消息对象>。</summary>
        public event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnWebSocketSendMessage
        {
            add    => m_WebSocketManager.OnSendMessage     += value;
            remove => m_WebSocketManager.OnSendMessage     -= value;
        }

        /// <summary>
        /// 连接指定 WebSocket 通道。
        /// </summary>
        /// <param name="channelType">通道类型（Tcp / TcpPb）。</param>
        /// <param name="serverAddress">服务器 WebSocket 地址。</param>
        /// <param name="autoReconnect">是否开启断线自动重连。</param>
        public void ConnectServer(WebSocketScope.NetChannelType channelType, string serverAddress, bool autoReconnect = true)
        {
            if (string.IsNullOrEmpty(serverAddress))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(serverAddress));
            }

            m_WebSocketManager.ConnectServer(channelType, serverAddress, autoReconnect);
        }

        /// <summary>
        /// 手动触发重连（仅对已存在的通道生效）。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        public void ReconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            if (string.IsNullOrEmpty(serverAddress))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(serverAddress));
            }

            m_WebSocketManager.ReconnectServer(channelType, serverAddress);
        }

        /// <summary>
        /// 主动断开指定通道连接（终止自动重连）。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        public void DisconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            if (string.IsNullOrEmpty(serverAddress))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(serverAddress));
            }

            m_WebSocketManager.DisconnectServer(channelType, serverAddress);
        }

        /// <summary>
        /// 模拟非主观异常断开（保留自动重连机制，用于测试重连流程）。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        public void TestDisconnectServerAbnormally(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            if (string.IsNullOrEmpty(serverAddress))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(serverAddress));
            }

            m_WebSocketManager.TestDisconnectServerAbnormally(channelType, serverAddress);
        }

        /// <summary>
        /// 查询指定通道是否已建立连接。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <returns>已连接返回 true。</returns>
        public bool IsWebSocketConnected(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            if (string.IsNullOrEmpty(serverAddress))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(serverAddress));
            }

            return m_WebSocketManager.IsConnected(channelType, serverAddress);
        }

        /// <summary>
        /// 查询指定通道是否身份认证成功。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <returns>认证成功返回 true。</returns>
        public bool IsWebSocketAuthenticatedSuccess(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            if (string.IsNullOrEmpty(serverAddress))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(serverAddress));
            }

            return m_WebSocketManager.IsAuthenticatedSuccess(channelType, serverAddress);
        }

        /// <summary>
        /// 从对象池获取（或创建）消息对象。
        /// </summary>
        /// <param name="channelType">通道类型，决定创建 TcpMessage 或 TcpPbMessage。</param>
        /// <returns>消息对象。</returns>
        public WebSocketScope.NetMessageBase CreateMessage(WebSocketScope.NetChannelType channelType)
        {
            return m_WebSocketManager.CreateMessage(channelType);
        }

        /// <summary>
        /// 将消息对象归还对象池。
        /// </summary>
        /// <param name="message">待回收的消息对象。</param>
        public void RecycleMessage(WebSocketScope.NetMessageBase message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            m_WebSocketManager.RecycleMessage(message);
        }

        /// <summary>
        /// 向指定通道发送消息。
        /// 已连接且认证成功 → 立即发；断线但可重连 → 写入离线缓冲；其他情况 → 返回 false。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <param name="message">消息对象（由 CreateMessage 创建）。</param>
        /// <returns>成功注入发送缓冲返回 true，否则返回 false。</returns>
        public bool SendMessage(WebSocketScope.NetChannelType channelType, string serverAddress, WebSocketScope.NetMessageBase message)
        {
            if (string.IsNullOrEmpty(serverAddress))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(serverAddress));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return m_WebSocketManager.SendMessage(channelType, serverAddress, message);
        }
    }
}
