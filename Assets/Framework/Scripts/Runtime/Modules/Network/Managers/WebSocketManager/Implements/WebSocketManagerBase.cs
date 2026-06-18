/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketManagerBase.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   WebSocket管理器基类
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// WebSocket 管理器基类。
    /// </summary>
    internal abstract class WebSocketManagerBase : FrameworkManager, IWebSocketManager
    {
        /// <summary>
        /// 管理器优先级（值越小越先 Update、越后 Shutdown）。
        /// </summary>
        /// <remarks>值越小优先级越高，越先 Update、越后 Shutdown。</remarks>
        public override int Priority => 9;

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public abstract void Initialize(WebSocketManagerConfig config);

        /// <summary>
        /// 连接服务器。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <param name="autoReconnect">是否自动重连。</param>
        public abstract void ConnectServer(WebSocketScope.NetChannelType channelType, string serverAddress, bool autoReconnect = true);

        /// <summary>
        /// 手动重连。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        public abstract void ReconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress);

        /// <summary>
        /// 主动断开连接。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        public abstract void DisconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress);

        /// <summary>
        /// 模拟异常断开。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        public abstract void TestDisconnectServerAbnormally(WebSocketScope.NetChannelType channelType, string serverAddress);

        /// <summary>
        /// 查询连接状态。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <returns>已连接返回 true。</returns>
        public abstract bool IsConnected(WebSocketScope.NetChannelType channelType, string serverAddress);

        /// <summary>
        /// 查询认证状态。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <returns>认证成功返回 true。</returns>
        public abstract bool IsAuthenticatedSuccess(WebSocketScope.NetChannelType channelType, string serverAddress);

        /// <summary>
        /// 发送消息。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <param name="message">消息对象。</param>
        /// <returns>成功注入缓冲返回 true。</returns>
        public abstract bool SendMessage(WebSocketScope.NetChannelType channelType, string serverAddress, WebSocketScope.NetMessageBase message);

        /// <summary>
        /// 创建消息对象。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <returns>消息对象。</returns>
        public abstract WebSocketScope.NetMessageBase CreateMessage(WebSocketScope.NetChannelType channelType);

        /// <summary>
        /// 回收消息对象。
        /// </summary>
        /// <param name="message">消息对象。</param>
        public abstract void RecycleMessage(WebSocketScope.NetMessageBase message);

        /// <summary>开始连接事件。</summary>
        public abstract event Action<int, string> OnBeginConnect;

        /// <summary>连接成功事件。</summary>
        public abstract event Action<int, string> OnConnectSuccess;

        /// <summary>连接失败事件。</summary>
        public abstract event Action<int, string> OnConnectFail;

        /// <summary>断开连接事件。</summary>
        public abstract event Action<int, string> OnDisconnect;

        /// <summary>重连失败事件。</summary>
        public abstract event Action<int, string> OnReconnectFailed;

        /// <summary>认证成功事件。</summary>
        public abstract event Action<int, string> OnAuthenticateSuccess;

        /// <summary>认证失败事件。</summary>
        public abstract event Action<int, string> OnAuthenticateFail;

        /// <summary>收到消息事件。</summary>
        public abstract event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnReceiveMessage;

        /// <summary>发送消息成功事件。</summary>
        public abstract event Action<WebSocketScope.NetChannelBase, WebSocketScope.NetMessageBase> OnSendMessage;

        /// <summary>连接超时时间（秒）。</summary>
        public abstract float ConnectTimeout { get; }

        /// <summary>身份认证超时时间（秒）。</summary>
        public abstract float AuthenticateTimeout { get; }

        /// <summary>心跳超时时间（秒）。</summary>
        public abstract float HeartBeatTimeout { get; }

        /// <summary>所有通信通道实例（只读）。</summary>
        public abstract IReadOnlyList<WebSocketScope.NetChannelBase> NetChannels { get; }

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
