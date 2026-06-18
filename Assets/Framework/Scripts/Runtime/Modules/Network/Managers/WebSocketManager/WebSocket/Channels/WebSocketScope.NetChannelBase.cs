/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketScope.NetChannelBase.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   通信通道基类
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
        /// <summary>
        /// WebSocketManager 向通道暴露的轻量接口，避免通道持有 Manager 具体类型。
        /// </summary>
        public interface IWebSocketManagerBridge
        {
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
            /// 从对象池创建消息对象。
            /// </summary>
            /// <param name="channelType">通道类型。</param>
            /// <returns>复用或新建的消息对象。</returns>
            NetMessageBase CreateMessage(NetChannelType channelType);

            /// <summary>
            /// 将消息对象归还对象池。
            /// </summary>
            /// <param name="message">待回收的消息对象。</param>
            void RecycleMessage(NetMessageBase message);

            /// <summary>
            /// 将委托延迟调度到主线程执行（子线程安全）。
            /// </summary>
            /// <param name="action">待主线程执行的委托。</param>
            void LazyToQueueOnMainThread(Action action);
        }

        /// <summary>
        /// 通信通道抽象基类，封装 WebSocket 连接生命周期、消息发送/接收缓冲与重连支持。
        /// </summary>
        public abstract class NetChannelBase
        {
            /// <summary>
            /// 服务器地址。
            /// </summary>
            protected string m_ServerAddress;
            public string ServerAddress => m_ServerAddress;

            /// <summary>
            /// 是否开启自动重连机制。
            /// </summary>
            protected bool m_AutoReconnect = true;
            public bool AutoReconnect => m_AutoReconnect;

            /// <summary>
            /// 连接是否已被主动断开（用于阻止自动重连）。
            /// </summary>
            protected bool m_IsDisconnectedSubjectively;
            public bool IsDisconnectedSubjectively => m_IsDisconnectedSubjectively;

            /// <summary>
            /// 是否需要立即发送身份认证消息。
            /// </summary>
            public bool NeedAuthenticateRightNow;

            /// <summary>
            /// 是否正在等待身份认证响应。
            /// </summary>
            public bool WaitingAuthenticate = true;

            /// <summary>
            /// 最后一次身份认证消息发送时间。
            /// </summary>
            public DateTime AuthenticateSendTime = DateTime.Now;

            /// <summary>
            /// 是否正在等待心跳响应。
            /// </summary>
            public bool WaitingHeartBeat;

            /// <summary>
            /// 最后一次心跳消息发送时间。
            /// </summary>
            public DateTime HeartBeatSendTime = DateTime.Now;

            /// <summary>
            /// 每协议 ID 独立维护的序列号，<协议 ID, 当前最大序列号>。
            /// </summary>
            private Dictionary<int, int> m_SequenceIDs;

            /// <summary>
            /// 接收路径复用缓冲区，避免每次 ReceiveAsync 时分配 byte[]。
            /// 仅在接收线程中使用，无并发竞争。
            /// </summary>
            protected byte[] m_ReceiveBuffer = new byte[8192];

            /// <summary>
            /// 接收路径复用 MemoryStream，用于拼接分片帧数据。
            /// 使用前需 SetLength(0) + Position = 0 复位。仅在接收线程中使用。
            /// </summary>
            protected MemoryStream m_ReceiveStream = new MemoryStream();

            /// <summary>
            /// 接收路径复用的 4 字节缓冲区，用于解析消息体内子消息的长度前缀。
            /// 仅在接收线程中使用，无并发竞争。
            /// </summary>
            protected byte[] m_LengthBuffer = new byte[4];

            /// <summary>
            /// WebSocketManager 接口桥，用于访问超时配置与对象池。
            /// </summary>
            protected IWebSocketManagerBridge m_Bridge;

            /// <summary>
            /// 协程运行器（WebGL 平台用于替代多线程）。
            /// </summary>
            protected ICoroutineRunner m_CoroutineRunner;

            /// <summary>
            /// 在线发送数据缓冲（已连接时等待发出）。
            /// </summary>
            private List<byte[]> m_SendDataBuffer = new List<byte[]>();

            /// <summary>
            /// 在线发送消息对象缓冲（与 m_SendDataBuffer 一一对应）。
            /// </summary>
            private List<NetMessageBase> m_SendNetMessageBaseBuffer = new List<NetMessageBase>();

            /// <summary>
            /// 离线发送数据缓冲（断线期间缓存，重连后批量补发）。
            /// </summary>
            private List<byte[]> m_SendDataOfflineBuffer = new List<byte[]>();

            /// <summary>
            /// 离线发送消息对象缓冲（与 m_SendDataOfflineBuffer 一一对应）。
            /// </summary>
            private List<NetMessageBase> m_SendNetMessageBaseOfflineBuffer = new List<NetMessageBase>();

            /// <summary>
            /// 发送缓冲区线程安全锁，保护在线缓冲的并发读写。
            /// </summary>
            private readonly object m_SendBufferLock = new object();

            /// <summary>
            /// 离线发送缓冲区线程安全锁，保护离线缓冲的并发读写。
            /// </summary>
            private readonly object m_SendOfflineBufferLock = new object();

            /// <summary>
            /// 是否需要保持连接（TcpChannel / TcpPbChannel 为 true）。
            /// </summary>
            public virtual bool IsNeedConnect => true;

#if !UNITY_EDITOR && UNITY_WEBGL

            /// <summary>
            /// WebGL 原生层是否已发生错误（影响 IsConnected 判断）。
            /// </summary>
            private bool m_IsWebSocketErrorOnWebGL;

            /// <summary>
            /// 发送消息协程句柄（WebGL 专用）。
            /// </summary>
            private Coroutine m_SendCoroutine;

            /// <summary>
            /// 二进制帧格式（WebGL 专用，固定为 arraybuffer）。
            /// </summary>
            private string m_BinaryType = "arraybuffer";

            /// <summary>
            /// 当前 WebSocket JS 实例 ID（WebGL 专用）。
            /// </summary>
            public int WebSocketInstanceID { get; private set; }

            /// <summary>
            /// 是否已连接（WebGL：通过 JS 层查询 state）。
            /// </summary>
            public bool IsConnected
            {
                get
                {
                    try { return WebSocketScope.WebGL.WebSocketGetState(WebSocketInstanceID) == (int)WebSocketState.Open && !m_IsWebSocketErrorOnWebGL; }
                    catch { return false; }
                }
            }

#else

            /// <summary>
            /// 发送线程（非 WebGL）。
            /// </summary>
            private Thread m_SendThread;

            /// <summary>
            /// 接收线程（非 WebGL）。
            /// </summary>
            private Thread m_ReceiveThread;

            /// <summary>
            /// 是否正在运行线程（非 WebGL）。
            /// </summary>
            private bool m_IsEnableThread;

            /// <summary>
            /// 原生 ClientWebSocket 实例（非 WebGL）。
            /// </summary>
            public ClientWebSocket WebSocket { get; private set; }

            /// <summary>
            /// 是否已连接（非 WebGL：通过 WebSocket.State 判断）。
            /// </summary>
            public bool IsConnected
            {
                get
                {
                    try { return WebSocket != null && WebSocket.State == System.Net.WebSockets.WebSocketState.Open; }
                    catch { return false; }
                }
            }

#endif

            /// <summary>
            /// 是否身份认证成功（已连接 + 不需要认证 + 认证完毕）。
            /// </summary>
            public bool IsAuthenticatedSuccess => IsNeedConnect && IsConnected && !NeedAuthenticateRightNow && !WaitingAuthenticate;

            /// <summary>
            /// 消息发送成功事件。
            /// </summary>
            public event Action<NetChannelBase, NetMessageBase> SendMessageEvent;

            /// <summary>
            /// 消息接收成功事件。
            /// </summary>
            public event Action<NetChannelBase, NetMessageBase> ReceiveMessageEvent;

            /// <summary>
            /// 与服务器断开连接事件。
            /// </summary>
            public event Action<NetChannelBase> DisconnectServerEvent;

            /// <summary>
            /// 初始化通道，启动发送/接收线程（或协程）。
            /// </summary>
            /// <param name="bridge">WebSocketManager 桥接接口。</param>
            /// <param name="coroutineRunner">协程运行器（WebGL 平台使用）。</param>
            /// <param name="autoReconnect">是否自动重连。</param>
            /// <param name="serverAddress">服务器地址。</param>
            public virtual void Init(IWebSocketManagerBridge bridge, ICoroutineRunner coroutineRunner, bool autoReconnect, string serverAddress)
            {
                m_Bridge          = bridge;
                m_CoroutineRunner = coroutineRunner;
                m_AutoReconnect   = autoReconnect;
                m_ServerAddress   = serverAddress;

                NeedAuthenticateRightNow = false;
                WaitingAuthenticate      = true;
                AuthenticateSendTime     = DateTime.Now;
                WaitingHeartBeat         = false;
                HeartBeatSendTime        = DateTime.Now;
                m_SequenceIDs            = new Dictionary<int, int>();

#if !UNITY_EDITOR && UNITY_WEBGL
                if (IsNeedConnect)
                {
                    m_SendCoroutine = m_CoroutineRunner.StartCoroutine(SendMessageNeedConnect());
                    WebSocketScope.WebGL.OnMessageWebSocket  += OnWebGLMessageReceived;
                    WebSocketScope.WebGL.OnErrorWebSocket    += OnWebGLError;
                }
#else
                m_IsEnableThread = true;
                if (IsNeedConnect)
                {
                    m_SendThread = new Thread(SendMessageNeedConnect);
                    m_SendThread.Start();

                    m_ReceiveThread = new Thread(ReceiveMessageNeedConnect);
                    m_ReceiveThread.Start();
                }
#endif
            }

            /// <summary>
            /// 终结通道，停止所有线程/协程并关闭 Socket，清空发送缓冲。
            /// </summary>
            /// <returns>异步任务。</returns>
            public virtual async UniTask OnTerminate()
            {
#if !UNITY_EDITOR && UNITY_WEBGL
                if (m_SendCoroutine != null)
                {
                    m_CoroutineRunner.StopCoroutine(m_SendCoroutine);
                }

                WebSocketScope.WebGL.OnMessageWebSocket -= OnWebGLMessageReceived;
                WebSocketScope.WebGL.OnErrorWebSocket   -= OnWebGLError;
                m_CoroutineRunner.StartCoroutine(CloseSocket(false));
#else
                m_IsEnableThread = false;
                if (m_SendThread != null && m_SendThread.IsAlive)
                {
                    m_SendThread.Join(3000);
                    m_SendThread = null;
                }

                if (m_ReceiveThread != null && m_ReceiveThread.IsAlive)
                {
                    m_ReceiveThread.Join(3000);
                    m_ReceiveThread = null;
                }

                await CloseSocket(true);
#endif
                lock (m_SendBufferLock)
                {
                    m_SendDataBuffer.Clear();
                    m_SendNetMessageBaseBuffer.Clear();
                }

                lock (m_SendOfflineBufferLock)
                {
                    m_SendDataOfflineBuffer.Clear();
                    m_SendNetMessageBaseOfflineBuffer.Clear();
                }

                m_ReceiveStream.SetLength(0);
                m_ReceiveStream.Position = 0;

                Log.Debug(LogTag.WebSocket, "{0} 已断开。", this);
            }

            /// <summary>
            /// 获取并自增指定协议 ID 的序列号。
            /// </summary>
            /// <param name="commandID">协议 ID。</param>
            /// <returns>自增后的序列号。</returns>
            public int GetSequenceID(int commandID)
            {
                if (commandID < 0)
                {
                    Log.Warning(LogTag.WebSocket, "GetSequenceID 中 commandID 无效。");
                    return 0;
                }

                if (!m_SequenceIDs.ContainsKey(commandID))
                {
                    m_SequenceIDs.Add(commandID, 0);
                }

                return ++m_SequenceIDs[commandID];
            }

            /// <summary>
            /// 将消息封装为字节数组（由子类实现协议格式）。
            /// </summary>
            /// <param name="message">待封装的消息对象。</param>
            /// <returns>封装后的字节数组。</returns>
            public abstract byte[] PackageMessageToBytes(NetMessageBase message);

            /// <summary>
            /// 向在线发送缓冲注入消息（已连接时等待发出）。
            /// </summary>
            /// <param name="bytes">封装后的消息字节数组。</param>
            /// <param name="message">消息对象（用于回调与回收）。</param>
            public void InjectBytesToBuffer(byte[] bytes, NetMessageBase message)
            {
                if (bytes == null || message == null)
                {
                    return;
                }

                lock (m_SendBufferLock)
                {
                    m_SendDataBuffer.Add(bytes);
                    m_SendNetMessageBaseBuffer.Add(message);
                }
            }

            /// <summary>
            /// 向离线发送缓冲注入消息（断线期间缓存，重连后批量补发）。
            /// </summary>
            /// <param name="bytes">封装后的消息字节数组。</param>
            /// <param name="message">消息对象。</param>
            public void InjectBytesToOfflineBuffer(byte[] bytes, NetMessageBase message)
            {
                if (bytes == null || message == null)
                {
                    return;
                }

                lock (m_SendOfflineBufferLock)
                {
                    m_SendDataOfflineBuffer.Add(bytes);
                    m_SendNetMessageBaseOfflineBuffer.Add(message);
                }
            }

            /// <summary>
            /// 将离线缓冲批量移入在线缓冲（重连成功后补发离线消息）。
            /// </summary>
            public void InjectOfflineBufferIntoBuffer()
            {
                List<byte[]> dataSnapshot;
                List<NetMessageBase> messageSnapshot;

                lock (m_SendOfflineBufferLock)
                {
                    if (m_SendDataOfflineBuffer.Count == 0)
                    {
                        return;
                    }

                    if (m_SendDataOfflineBuffer.Count != m_SendNetMessageBaseOfflineBuffer.Count)
                    {
                        Log.Error(LogTag.WebSocket, "离线缓冲数据不一致（Data:{0}, Message:{1}），清空缓冲。", m_SendDataOfflineBuffer.Count, m_SendNetMessageBaseOfflineBuffer.Count);
                        m_SendDataOfflineBuffer.Clear();
                        m_SendNetMessageBaseOfflineBuffer.Clear();
                        return;
                    }

                    dataSnapshot = new List<byte[]>(m_SendDataOfflineBuffer);
                    messageSnapshot = new List<NetMessageBase>(m_SendNetMessageBaseOfflineBuffer);
                    m_SendDataOfflineBuffer.Clear();
                    m_SendNetMessageBaseOfflineBuffer.Clear();
                }

                lock (m_SendBufferLock)
                {
                    m_SendDataBuffer.AddRange(dataSnapshot);
                    m_SendNetMessageBaseBuffer.AddRange(messageSnapshot);
                }
            }

            /// <summary>
            /// 判断消息是否为心跳消息（由子类根据协议 ID 判断）。
            /// </summary>
            /// <param name="message">待判断的消息对象。</param>
            /// <returns>是心跳消息返回 true。</returns>
            public abstract bool IsHeartBeatMessage(NetMessageBase message);

            /// <summary>
            /// 判断消息是否为身份认证消息（由子类根据协议 ID 判断）。
            /// </summary>
            /// <param name="message">待判断的消息对象。</param>
            /// <returns>是认证消息返回 true。</returns>
            public abstract bool IsAuthenticateMessage(NetMessageBase message);

            /// <summary>
            /// 判断身份认证响应是否成功（由子类解析消息内容判断）。
            /// </summary>
            /// <param name="message">身份认证响应消息。</param>
            /// <returns>认证成功返回 true。</returns>
            public abstract bool IsAuthenticateMessageSuccess(NetMessageBase message);

            /// <summary>
            /// 检查身份认证是否已超时。
            /// </summary>
            /// <returns>超时返回 true。</returns>
            public bool CheckAuthenticateTimeout()
            {
                return (DateTime.Now - AuthenticateSendTime).TotalSeconds >= m_Bridge.AuthenticateTimeout;
            }

            /// <summary>
            /// 检查心跳是否已超时。
            /// </summary>
            /// <returns>超时返回 true。</returns>
            public bool CheckHeatBeatTimeout()
            {
                return (DateTime.Now - HeartBeatSendTime).TotalSeconds >= m_Bridge.HeartBeatTimeout;
            }

            /// <summary>
            /// 获取通信通道类型。
            /// </summary>
            /// <returns>通道类型枚举值。</returns>
            public abstract NetChannelType GetNetChannelType();

            /// <summary>
            /// 比较通道类型与服务器地址是否一致。
            /// </summary>
            /// <param name="channelType">通道类型。</param>
            /// <param name="channel">另一个通道实例。</param>
            /// <returns>一致返回 true。</returns>
            public bool Equals(NetChannelType channelType, NetChannelBase channel)
            {
                return Equals(channelType, channel.ServerAddress);
            }

            /// <summary>
            /// 比较通道类型与服务器地址是否一致。
            /// </summary>
            /// <param name="channelType">通道类型。</param>
            /// <param name="serverAddress">服务器地址。</param>
            /// <returns>一致返回 true。</returns>
            public abstract bool Equals(NetChannelType channelType, string serverAddress);

            /// <summary>
            /// 返回通道描述字符串。
            /// </summary>
            /// <returns>格式为"通道[类型：地址]"的字符串。</returns>
            public override string ToString()
            {
                return $"通道[{GetNetChannelType()}：{m_ServerAddress}]";
            }

#if !UNITY_EDITOR && UNITY_WEBGL

            /// <summary>
            /// 创建 WebSocket 连接（WebGL 协程版）。
            /// </summary>
            /// <returns>协程迭代器。</returns>
            public IEnumerator CreateSocket()
            {
                bool gotResult = false;

                void OnOpen(int instanceId)
                {
                    if (instanceId != WebSocketInstanceID) return;
                    m_IsWebSocketErrorOnWebGL = false;
                    m_IsDisconnectedSubjectively = false;
                    NeedAuthenticateRightNow = IsConnected;
                    gotResult = true;
                }

                WebSocketScope.WebGL.OnOpenWebSocket -= OnOpen;
                WebSocketScope.WebGL.OnOpenWebSocket += OnOpen;

                if (WebSocketInstanceID == 0)
                {
                    WebSocketInstanceID = WebSocketScope.WebGL.WebSocketAllocate(m_ServerAddress, m_BinaryType);
                    int code = WebSocketScope.WebGL.WebSocketConnect(WebSocketInstanceID);
                    if (code < 0)
                    {
                        gotResult = true;
                    }
                }
                else
                {
                    gotResult = true;
                }

                while (!gotResult)
                {
                    yield return null;
                }

                WebSocketScope.WebGL.OnOpenWebSocket -= OnOpen;
            }

            /// <summary>
            /// 关闭 WebSocket 连接（WebGL 协程版）。
            /// </summary>
            /// <param name="subjectively">true 表示主动关闭（阻止自动重连）。</param>
            /// <param name="callback">关闭完成回调。</param>
            /// <returns>协程迭代器。</returns>
            public IEnumerator CloseSocket(bool subjectively, Action callback = null)
            {
                bool gotResult = false;

                void OnClose(int instanceId, int closeCode, string reason)
                {
                    if (instanceId != WebSocketInstanceID) return;
                    DisconnectServerEvent?.Invoke(this);
                    WebSocketInstanceID = 0;
                    gotResult = true;
                    callback?.Invoke();
                }

                WebSocketScope.WebGL.OnCloseWebSocket -= OnClose;
                WebSocketScope.WebGL.OnCloseWebSocket += OnClose;

                if (WebSocketInstanceID != 0)
                {
                    NeedAuthenticateRightNow = false;
                    m_IsDisconnectedSubjectively = subjectively;
                    if (IsNeedConnect && IsConnected)
                    {
                        int code = WebSocketScope.WebGL.WebSocketClose(WebSocketInstanceID, (int)WebSocketCloseStatus.NormalClosure, "Normal Closure");
                        if (code < 0)
                        {
                            gotResult = true;
                        }
                    }
                    else
                    {
                        gotResult = true;
                    }
                }
                else
                {
                    gotResult = true;
                }

                while (!gotResult)
                {
                    yield return null;
                }

                WebSocketScope.WebGL.OnCloseWebSocket -= OnClose;
            }

            /// <summary>
            /// 解析接收到的 WebSocket 消息字节（WebGL 子类实现）。
            /// </summary>
            /// <param name="recvBytes">接收到的原始字节数组。</param>
            /// <returns>解析出的消息对象，解析失败返回 null。</returns>
            protected abstract NetMessageBase ReceiveMessage(byte[] recvBytes);

            /// <summary>
            /// 协程：发送在线缓冲中的消息（WebGL）。
            /// </summary>
            private IEnumerator SendMessageNeedConnect()
            {
                yield return null;
                while (true)
                {
                    byte[] sendData = null;
                    NetMessageBase netMessage = null;

                    lock (m_SendBufferLock)
                    {
                        if (IsConnected && m_SendDataBuffer.Count > 0)
                        {
                            sendData = m_SendDataBuffer[0];
                            netMessage = m_SendNetMessageBaseBuffer[0];
                            m_SendDataBuffer.RemoveAt(0);
                            m_SendNetMessageBaseBuffer.RemoveAt(0);
                        }
                    }

                    if (sendData != null)
                    {
                        WebSocketScope.WebGL.WebSocketSend(WebSocketInstanceID, sendData, sendData.Length);
                        SendMessageEvent?.Invoke(this, netMessage);
                        m_Bridge.RecycleMessage(netMessage);
                    }

                    yield return null;
                }
            }

            /// <summary>
            /// 处理 WebGL 接收到的消息字节。
            /// </summary>
            /// <param name="instanceId">WebSocket 实例 ID。</param>
            /// <param name="recvBytes">接收到的字节数组。</param>
            private void OnWebGLMessageReceived(int instanceId, byte[] recvBytes)
            {
                if (instanceId != WebSocketInstanceID || !IsConnected) return;

                NetMessageBase message = ReceiveMessage(recvBytes);
                if (message == null) return;

                ReceiveMessageEvent?.Invoke(this, message);

                if (IsHeartBeatMessage(message))
                {
                    WaitingHeartBeat = false;
                }

                if (IsAuthenticateMessage(message))
                {
                    if (IsAuthenticateMessageSuccess(message))
                    {
                        WaitingAuthenticate = false;
                        Log.Debug(LogTag.NetworkWebSocket, "{0} 身份认证成功。", this);
                    }
                    else
                    {
                        m_CoroutineRunner.StartCoroutine(CloseSocket(true));
                        Log.Debug(LogTag.NetworkWebSocket, "{0} 身份认证失败，断开连接。", this);
                    }
                }

                m_Bridge.RecycleMessage(message);
            }

            /// <summary>
            /// 处理 WebGL 原生层 WebSocket 错误事件。
            /// </summary>
            /// <param name="instanceId">WebSocket 实例 ID。</param>
            /// <param name="errorMsg">错误信息。</param>
            private void OnWebGLError(int instanceId, string errorMsg)
            {
                if (instanceId != WebSocketInstanceID) return;
                Log.Error(LogTag.NetworkWebSocket, "{0} WebGL 原生层异常：{1}，等待断线重连。", this, errorMsg);
                NeedAuthenticateRightNow = false;
                m_IsDisconnectedSubjectively = false;
                m_IsWebSocketErrorOnWebGL = true;
                WebSocketInstanceID = 0;
                DisconnectServerEvent?.Invoke(this);
            }

#else

            /// <summary>
            /// 创建 WebSocket 连接（非 WebGL 异步版）。
            /// </summary>
            /// <returns>异步任务。</returns>
            public async UniTask CreateSocket()
            {
                if (WebSocket == null)
                {
                    WebSocket = new ClientWebSocket();
                    Task connectTask = WebSocket.ConnectAsync(new Uri(m_ServerAddress), CancellationToken.None);
                    Task timeoutTask = Task.Delay(Mathf.CeilToInt(m_Bridge.ConnectTimeout * 1000));
                    await Task.WhenAny(connectTask, timeoutTask);
                    m_IsDisconnectedSubjectively = false;
                    NeedAuthenticateRightNow = IsConnected;
                }
            }

            /// <summary>
            /// 关闭 WebSocket 连接（非 WebGL 异步版）。
            /// </summary>
            /// <param name="subjectively">true 表示主动关闭。</param>
            /// <returns>异步任务。</returns>
            public async UniTask CloseSocket(bool subjectively)
            {
                if (WebSocket == null) return;

                NeedAuthenticateRightNow = false;
                m_IsDisconnectedSubjectively = subjectively;
                if (IsNeedConnect && IsConnected)
                {
                    await WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Normal Closure", CancellationToken.None);
                    DisconnectServerEvent?.Invoke(this);
                }

                WebSocket.Dispose();
                WebSocket = null;
            }

            /// <summary>
            /// 异步接收消息（非 WebGL，子类实现解析逻辑）。
            /// </summary>
            /// <param name="client">当前 WebSocket 客户端实例。</param>
            /// <returns>解析出的消息对象，解析失败返回 null。</returns>
            protected abstract UniTask<NetMessageBase> ReceiveMessage(ClientWebSocket client);

            /// <summary>
            /// 线程入口：发送（非 WebGL）。
            /// </summary>
            private void SendMessageNeedConnect()
            {
                SendMessageNeedConnectAsync().Forget();
            }

            /// <summary>
            /// 异步发送在线缓冲中的消息（非 WebGL）。
            /// </summary>
            private async UniTask SendMessageNeedConnectAsync()
            {
                while (m_IsEnableThread)
                {
                    byte[] sendData = null;
                    NetMessageBase netMessage = null;

                    lock (m_SendBufferLock)
                    {
                        if (IsConnected && m_SendDataBuffer.Count > 0)
                        {
                            sendData = m_SendDataBuffer[0];
                            netMessage = m_SendNetMessageBaseBuffer[0];
                            m_SendDataBuffer.RemoveAt(0);
                            m_SendNetMessageBaseBuffer.RemoveAt(0);
                        }
                    }

                    if (sendData != null)
                    {
                        bool isValid = sendData != null;
                        if (isValid)
                        {
                            await WebSocket.SendAsync(new ArraySegment<byte>(sendData), WebSocketMessageType.Binary, true, CancellationToken.None);
                        }

                        NetMessageBase msg = netMessage;
                        bool valid = isValid;
                        m_Bridge.LazyToQueueOnMainThread(() =>
                        {
                            if (valid)
                            {
                                SendMessageEvent?.Invoke(this, msg);
                            }

                            m_Bridge.RecycleMessage(msg);
                        });
                    }
                    else
                    {
                        await UniTask.Delay(1);
                    }
                }
            }

            /// <summary>
            /// 线程入口：接收（非 WebGL）。
            /// </summary>
            private void ReceiveMessageNeedConnect()
            {
                ReceiveMessageNeedConnectAsync().Forget();
            }

            /// <summary>
            /// 异步接收并处理消息（非 WebGL）。
            /// </summary>
            private async UniTask ReceiveMessageNeedConnectAsync()
            {
                while (m_IsEnableThread)
                {
                    if (IsConnected)
                    {
                        NetMessageBase message = await ReceiveMessage(WebSocket);
                        if (message == null) continue;

                        m_Bridge.LazyToQueueOnMainThread(async () =>
                        {
                            ReceiveMessageEvent?.Invoke(this, message);

                            if (IsHeartBeatMessage(message))
                            {
                                WaitingHeartBeat = false;
                            }

                            if (IsAuthenticateMessage(message))
                            {
                                if (IsAuthenticateMessageSuccess(message))
                                {
                                    WaitingAuthenticate = false;
                                    Log.Debug(LogTag.WebSocket, "{0} 身份认证成功。", this);
                                }
                                else
                                {
                                    await CloseSocket(true);
                                    Log.Debug(LogTag.WebSocket, "{0} 身份认证失败，断开连接。", this);
                                }
                            }

                            m_Bridge.RecycleMessage(message);
                        });
                    }
                    else
                    {
                        await UniTask.Delay(1);
                    }
                }
            }

#endif
        }
    }
}
