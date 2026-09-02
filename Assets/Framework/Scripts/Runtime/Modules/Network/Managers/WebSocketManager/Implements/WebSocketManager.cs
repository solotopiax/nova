/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketManager.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   WebSocket管理器
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// WebSocket 管理器，负责长连接通道的生命周期管理、消息收发与断线重连。
    /// 实现 IWebSocketManagerBridge 供 NetChannelBase 调用，避免通道直接引用管理器具体类型。
    /// </summary>
    internal sealed partial class WebSocketManager : WebSocketManagerBase, WebSocketScope.IWebSocketManagerBridge
    {
        /// <summary>
        /// 初始化 WebSocketManager 的新实例。
        /// </summary>
        public WebSocketManager()
        {
            m_NetChannels = new List<WebSocketScope.NetChannelBase>();
            m_ConnectServerCoroutines = new Dictionary<WebSocketScope.NetChannelBase, Coroutine>();
            m_AutoAuthenticateCoroutines = new Dictionary<WebSocketScope.NetChannelBase, Coroutine>();
            m_AutoHeartBeatCoroutines = new Dictionary<WebSocketScope.NetChannelBase, Coroutine>();
            m_AutoReconnectCoroutines = new Dictionary<WebSocketScope.NetChannelBase, Coroutine>();
        }

        /// <summary>
        /// 初始化。
        /// </summary>
        /// <param name="config">配置信息。</param>
        public override void Initialize(WebSocketManagerConfig config)
        {
            m_CoroutineRunner = config.CoroutineRunner;
            if (m_CoroutineRunner == null)
            {
                Log.Fatal(LogTag.WebSocket, "ICoroutineRunner 无效。");
                return;
            }

            m_SpecialMessageCreator = config.SpecialMessageCreator;
            m_ConnectTimeout = config.ConnectTimeout;
            m_AuthenticateTimeout = config.AuthenticateTimeout;
            m_HeartBeatTimeInterval = config.HeartBeatTimeInterval;
            m_HeartBeatTimeout = config.HeartBeatTimeout;
            m_AutoReconnectMaxCounter = config.AutoReconnectMaxCounter;
            m_AutoReconnectTimeInterval = config.AutoReconnectTimeInterval;
            m_EnableAutoReconnect = config.EnableAutoReconnect;

#if !UNITY_EDITOR && UNITY_WEBGL
            WebSocketScope.WebGL.Initialize();
#endif
        }

        /// <summary>
        /// 连接服务器（复用已有通道或新建通道）。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <param name="autoReconnect">是否自动重连。</param>
        public override void ConnectServer(WebSocketScope.NetChannelType channelType, string serverAddress, bool autoReconnect = true)
        {
            WebSocketScope.NetChannelBase channel = FindFreeNetChannel(channelType, serverAddress);
            if (channel == null)
            {
                channel = channelType == WebSocketScope.NetChannelType.Tcp
                    ? (WebSocketScope.NetChannelBase)new WebSocketScope.TcpChannel()
                    : new WebSocketScope.TcpPbChannel();

                channel.Init(this, m_CoroutineRunner, autoReconnect, serverAddress);
                channel.ReceiveMessageEvent += (ch, msg) => OnReceiveMessage?.Invoke(ch, msg);
                channel.SendMessageEvent += (ch, msg) => OnSendMessage?.Invoke(ch, msg);
                channel.DisconnectServerEvent += (ch) =>
                {
                    int channelIdx = FindNetChannelIndex(ch);
                    if (channelIdx >= 0)
                    {
                        OnDisconnect?.Invoke(channelIdx, ch.ServerAddress);
                    }
                };

                m_NetChannels.Add(channel);
            }

            m_ConnectServerCoroutines[channel] = m_CoroutineRunner.StartCoroutine(ConnectServerCoroutine(channel));
        }

        /// <summary>
        /// 手动触发重连。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        public override void ReconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            WebSocketScope.NetChannelBase channel = FindNetChannel(channelType, serverAddress);
            if (channel == null)
            {
                Log.Warning(LogTag.WebSocket, "ReconnectServer 未找到通道：{0} {1}。", channelType, serverAddress);
                return;
            }

            StopChannelCoroutines(channel);
            m_ConnectServerCoroutines[channel] = m_CoroutineRunner.StartCoroutine(ConnectServerCoroutine(channel));
        }

        /// <summary>
        /// 主动断开连接，终止自动重连。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        public override void DisconnectServer(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            WebSocketScope.NetChannelBase channel = FindNetChannel(channelType, serverAddress);
            if (channel == null) return;

            StopChannelCoroutines(channel);

#if !UNITY_EDITOR && UNITY_WEBGL
            m_CoroutineRunner.StartCoroutine(channel.CloseSocket(true));
#else
            channel.CloseSocket(true).Forget();
#endif
        }

        /// <summary>
        /// 模拟非主观异常断开（保留重连机制，用于测试）。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        public override void TestDisconnectServerAbnormally(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            WebSocketScope.NetChannelBase channel = FindNetChannel(channelType, serverAddress);
            if (channel == null) return;

#if !UNITY_EDITOR && UNITY_WEBGL
            m_CoroutineRunner.StartCoroutine(channel.CloseSocket(false));
#else
            channel.CloseSocket(false).Forget();
#endif
        }

        /// <summary>
        /// 查询连接状态。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <returns>已连接返回 true。</returns>
        public override bool IsConnected(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            WebSocketScope.NetChannelBase channel = FindNetChannel(channelType, serverAddress);
            return channel != null && channel.IsConnected;
        }

        /// <summary>
        /// 查询认证状态。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <returns>认证成功返回 true。</returns>
        public override bool IsAuthenticatedSuccess(WebSocketScope.NetChannelType channelType, string serverAddress)
        {
            WebSocketScope.NetChannelBase channel = FindNetChannel(channelType, serverAddress);
            return channel != null && channel.IsAuthenticatedSuccess;
        }

        /// <summary>
        /// 向指定通道发送消息。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <param name="serverAddress">服务器地址。</param>
        /// <param name="message">消息对象。</param>
        /// <returns>成功注入返回 true。</returns>
        public override bool SendMessage(WebSocketScope.NetChannelType channelType, string serverAddress, WebSocketScope.NetMessageBase message)
        {
            WebSocketScope.NetChannelBase channel = FindNetChannel(channelType, serverAddress);
            if (channel == null)
            {
                Log.Error(LogTag.WebSocket, "SendMessage 未找到通道：{0} {1}。", channelType, serverAddress);
                return false;
            }
            byte[] bytes = channel.PackageMessageToBytes(message);

            if (!channel.IsNeedConnect)
            {
                channel.InjectBytesToBuffer(bytes, message);
                return true;
            }

            if (channel.IsAuthenticatedSuccess)
            {
                channel.InjectBytesToBuffer(bytes, message);
                return true;
            }

            if (!channel.IsDisconnectedSubjectively)
            {
                channel.InjectBytesToOfflineBuffer(bytes, message);
                return true;
            }

            Log.Error(LogTag.WebSocket, "{0} 发送失败：通道已主动断开。", channel);
            return false;
        }

        /// <summary>
        /// 从框架 ReferencePool 获取消息对象。
        /// </summary>
        /// <param name="channelType">通道类型。</param>
        /// <returns>消息对象。</returns>
        public override WebSocketScope.NetMessageBase CreateMessage(WebSocketScope.NetChannelType channelType)
        {
            return channelType == WebSocketScope.NetChannelType.Tcp
                ? (WebSocketScope.NetMessageBase)ReferencePool.Get<WebSocketScope.TcpMessage>()
                : ReferencePool.Get<WebSocketScope.TcpPbMessage>();
        }

        /// <summary>
        /// 将消息对象归还框架 ReferencePool。
        /// </summary>
        /// <param name="message">消息对象。</param>
        public override void RecycleMessage(WebSocketScope.NetMessageBase message)
        {
            if (message == null) return;
            ReferencePool.Put(message);
        }

        /// <summary>
        /// 管理器轮询（每帧分发跨线程消息）。
        /// </summary>
        public override void Update()
        {
            MutexDataUpdate();
        }

        /// <summary>
        /// 关闭并清理所有通道。发起异步终结请求，不在主线程同步等待。
        /// </summary>
        public override void Shutdown()
        {
            OnTerminateAsync().Forget();
        }

        /// <summary>
        /// 异步终结所有通道，停止所有协程，每个通道最多等待 3 秒。
        /// </summary>
        private async UniTask OnTerminateAsync()
        {
            foreach (WebSocketScope.NetChannelBase channel in m_NetChannels)
            {
                StopChannelCoroutines(channel);
#if !UNITY_EDITOR && UNITY_WEBGL
                m_CoroutineRunner.StartCoroutine(channel.CloseSocket(false));
#else
                try
                {
                    UniTask closeTask = channel.OnTerminate();
                    await UniTask.WhenAny(closeTask, UniTask.Delay(3000));
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.WebSocket, "OnTerminateAsync 关闭通道超时或异常：{0}。", e.Message);
                }
#endif
            }

            m_NetChannels.Clear();
        }
    }
}
