/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IWebSocketManager.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   WebSocket管理器接口
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// WebSocket 管理器接口，负责长连接通道的生命周期管理、消息收发与断线重连。
    /// </summary>
    public interface IWebSocketManager
    {
        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        void Initialize(WebSocketManagerConfig config);

        /// <summary>
        /// 连接服务器（创建或复用同类型同地址通道）。
        /// </summary>
        /// <param name="channelType">通道类型（Tcp / TcpPb）。</param>
        /// <param name="serverAddress">服务器 WebSocket 地址。</param>
        /// <param name="autoReconnect">是否开启断线自动重连。</param>
        void ConnectServer(WebSocketScope.NetChannelType channelType, string serverAddress, bool autoReconnect = true);

        /// <summary>
        /// 手动触发重连（仅对已存在的通道生效）。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        void ReconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress);

        /// <summary>
        /// 主动断开连接（终止自动重连）。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        void DisconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress);

        /// <summary>
        /// 模拟非主观异常断开（保留自动重连机制，用于测试重连流程）。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        void TestDisconnectServerAbnormally(WebSocketScope.NetChannelType channelType, string serverAddress);

        /// <summary>
        /// 查询指定通道是否已建立连接。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <returns>已连接返回 true。</returns>
        bool IsConnected(WebSocketScope.NetChannelType channelType, string serverAddress);

        /// <summary>
        /// 查询指定通道是否身份认证成功。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <returns>认证成功返回 true。</returns>
        bool IsAuthenticatedSuccess(WebSocketScope.NetChannelType channelType, string serverAddress);

        /// <summary>
        /// 向指定通道发送消息。
        /// 已连接且认证成功 → 立即发；断线但可重连 → 写入离线缓冲；其他情况 → 返回 false。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <param name="message">消息对象（由 CreateMessage 创建）。</param>
        /// <returns>成功注入缓冲返回 true，否则返回 false。</returns>
        bool SendMessage(WebSocketScope.NetChannelType channelType, string serverAddress, WebSocketScope.NetMessageBase message);

        /// <summary>
        /// 从对象池获取（或创建）消息对象。
        /// </summary>
        /// <param name="channelType">通道类型，决定创建 TcpMessage 或 TcpPbMessage。</param>
        /// <returns>消息对象。</returns>
        WebSocketScope.NetMessageBase CreateMessage(WebSocketScope.NetChannelType channelType);

        /// <summary>
        /// 将消息对象归还对象池。
        /// </summary>
        /// <param name="message">待回收的消息对象。</param>
        void RecycleMessage(WebSocketScope.NetMessageBase message);

        /// <summary>
        /// 开始连接事件，<通道索引, 服务器地址>。
        /// </summary>
        event Action<int, string> OnBeginConnect;

        /// <summary>
        /// 连接成功事件，<通道索引, 服务器地址>。
        /// </summary>
        event Action<int, string> OnConnectSuccess;

        /// <summary>
        /// 连接失败事件，<通道索引, 服务器地址>。
        /// </summary>
        event Action<int, string> OnConnectFail;

        /// <summary>
        /// 断开连接事件，<通道索引, 服务器地址>。
        /// </summary>
        event Action<int, string> OnDisconnect;

        /// <summary>
        /// 重连失败事件（已达重连上限），<通道索引, 服务器地址>。
        /// </summary>
        event Action<int, string> OnReconnectFailed;

        /// <summary>
        /// 认证成功事件，<通道索引, 服务器地址>。
        /// </summary>
        event Action<int, string> OnAuthenticateSuccess;

        /// <summary>
        /// 认证失败事件，<通道索引, 服务器地址>。
        /// </summary>
        event Action<int, string> OnAuthenticateFail;

        /// <summary>
        /// 收到消息事件，<通道实例, 消息对象>。
        /// </summary>
        event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnReceiveMessage;

        /// <summary>
        /// 发送消息成功事件，<通道实例, 消息对象>。
        /// </summary>
        event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnSendMessage;

        /// <summary>
        /// 连接超时时间（秒）。
        /// </summary>
        float ConnectTimeout { get; }

        /// <summary>
        /// 身份认证超时时间（秒）。
        /// </summary>
        float AuthenticateTimeout { get; }

        /// <summary>
        /// 心跳超时时间（秒）。
        /// </summary>
        float HeartBeatTimeout { get; }

        /// <summary>
        /// 所有通信通道实例（只读），供编辑器运行时状态面板使用。
        /// </summary>
        IReadOnlyList<WebSocketScope.NetChannelBase> NetChannels { get; }
    }
}
