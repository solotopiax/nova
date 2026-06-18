/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketScope.TcpChannel.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   TCP明文通道
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
        /// <summary>
        /// TCP 明文通道，消息体为 JSON 字符串列表。
        /// </summary>
        public sealed class TcpChannel : NetChannelBase
        {
            /// <summary>
            /// 是否需要保持连接（TCP 通道始终需要）。
            /// </summary>
            public override bool IsNeedConnect => true;

            /// <summary>
            /// 将消息封装为二进制帧：[协议ID 4B][序列号 4B][消息体 NB]。
            /// 注意：此方法可能在主线程或发送线程中调用，totalByte 大小随消息而异，
            /// 使用成员级缓冲区存在线程安全风险和容量不可预测问题，因此保持局部分配。
            /// </summary>
            /// <param name="message">TcpMessage 消息对象。</param>
            /// <returns>封装后的字节数组。</returns>
            public override byte[] PackageMessageToBytes(NetMessageBase message)
            {
                TcpMessage networkInfo = (TcpMessage)message;
                byte[] totalByte = new byte[NetMessageBase.c_OuterShellLength + networkInfo.GetBodyLength()];
                byte[] commandIDByte = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(networkInfo.CommandID));
                byte[] sequenceIDByte = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(networkInfo.SequenceID));
                byte[] bodyByte = networkInfo.GetBodyBytes();
                commandIDByte.CopyTo(totalByte, 0);
                sequenceIDByte.CopyTo(totalByte, 4);
                bodyByte.CopyTo(totalByte, 8);
                return totalByte;
            }

            /// <summary>
            /// 判断是否为心跳消息（协议 ID == HeartBeat）。
            /// </summary>
            /// <param name="message">消息对象。</param>
            /// <returns>是心跳消息返回 true。</returns>
            public override bool IsHeartBeatMessage(NetMessageBase message)
            {
                TcpMessage networkInfo = message as TcpMessage;
                return networkInfo != null && networkInfo.CommandID == (int)MessageType.HeartBeat;
            }

            /// <summary>
            /// 判断是否为身份认证消息（协议 ID == Authenticate）。
            /// </summary>
            /// <param name="message">消息对象。</param>
            /// <returns>是认证消息返回 true。</returns>
            public override bool IsAuthenticateMessage(NetMessageBase message)
            {
                TcpMessage networkInfo = message as TcpMessage;
                return networkInfo != null && networkInfo.CommandID == (int)MessageType.Authenticate;
            }

            /// <summary>
            /// 判断身份认证是否成功（解析 JSON 中的 success 字段）。
            /// </summary>
            /// <param name="message">身份认证响应消息。</param>
            /// <returns>认证成功返回 true。</returns>
            public override bool IsAuthenticateMessageSuccess(NetMessageBase message)
            {
                TcpMessage networkInfo = message as TcpMessage;
                if (networkInfo == null || networkInfo.CommandID != (int)MessageType.Authenticate)
                {
                    return false;
                }

                foreach (string msg in networkInfo.Messages)
                {
                    JObject jObject = JObject.Parse(msg);
                    if (jObject.ContainsKey("success"))
                    {
                        return bool.Parse(jObject["success"].ToString());
                    }
                }

                return false;
            }

            /// <summary>
            /// 获取通道类型。
            /// </summary>
            /// <returns>NetChannelType.Tcp。</returns>
            public override NetChannelType GetNetChannelType()
            {
                return NetChannelType.Tcp;
            }

            /// <summary>
            /// 判断通道类型与地址是否匹配。
            /// </summary>
            /// <param name="channelType">通道类型。</param>
            /// <param name="serverAddress">服务器地址。</param>
            /// <returns>均匹配返回 true。</returns>
            public override bool Equals(NetChannelType channelType, string serverAddress)
            {
                return channelType == NetChannelType.Tcp && m_ServerAddress == serverAddress;
            }

#if !UNITY_EDITOR && UNITY_WEBGL

            /// <summary>
            /// 解析 WebGL 接收到的字节为 TcpMessage 对象。
            /// </summary>
            /// <param name="recvBytes">原始字节数组。</param>
            /// <returns>解析出的 TcpMessage，解析失败返回 null。</returns>
            protected override NetMessageBase ReceiveMessage(byte[] recvBytes)
            {
                try
                {
                    TcpMessage info = (TcpMessage)m_Bridge.CreateMessage(NetChannelType.Tcp);
                    info.CommandID  = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(recvBytes, 0));
                    info.SequenceID = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(recvBytes, 4));
                    info.Messages.Clear();

                    int bodyLength = recvBytes.Length - NetMessageBase.c_OuterShellLength;
                    int offset = NetMessageBase.c_OuterShellLength;

                    for (int i = 0; i < bodyLength;)
                    {
                        Buffer.BlockCopy(recvBytes, offset + i, m_LengthBuffer, 0, 4);
                        i += 4;
                        int len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(m_LengthBuffer, 0));
                        info.Messages.Add(Encoding.UTF8.GetString(recvBytes, offset + i, len));
                        i += len;
                    }

                    return info;
                }
                catch (WebSocketException)
                {
                    // 正常断开，静默处理。
                    return null;
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.WebSocket, "TcpChannel.ReceiveMessage(WebGL) 异常：{0}。", e.Message);
                    return null;
                }
            }

#else

            /// <summary>
            /// 异步接收并解析 TcpMessage（非 WebGL）。
            /// </summary>
            /// <param name="client">ClientWebSocket 实例。</param>
            /// <returns>解析出的 TcpMessage，解析失败返回 null。</returns>
            protected override async UniTask<NetMessageBase> ReceiveMessage(ClientWebSocket client)
            {
                try
                {
                    ArraySegment<byte> segment = new ArraySegment<byte>(m_ReceiveBuffer);
                    m_ReceiveStream.SetLength(0);
                    m_ReceiveStream.Position = 0;
                    byte[] recvBytes;
                    while (true)
                    {
                        WebSocketReceiveResult result = await client.ReceiveAsync(segment, CancellationToken.None);
                        m_ReceiveStream.Write(segment.Array, 0, result.Count);
                        if (result.EndOfMessage)
                        {
                            recvBytes = m_ReceiveStream.ToArray();
                            break;
                        }
                    }

                    TcpMessage info = (TcpMessage)m_Bridge.CreateMessage(NetChannelType.Tcp);
                    info.CommandID  = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(recvBytes, 0));
                    info.SequenceID = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(recvBytes, 4));
                    info.Messages.Clear();

                    int bodyLength = recvBytes.Length - NetMessageBase.c_OuterShellLength;
                    int offset = NetMessageBase.c_OuterShellLength;

                    for (int i = 0; i < bodyLength;)
                    {
                        Buffer.BlockCopy(recvBytes, offset + i, m_LengthBuffer, 0, 4);
                        i += 4;
                        int len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(m_LengthBuffer, 0));
                        info.Messages.Add(Encoding.UTF8.GetString(recvBytes, offset + i, len));
                        i += len;
                    }

                    return info;
                }
                catch (WebSocketException)
                {
                    // 正常断开，静默处理。
                    return null;
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.WebSocket, "TcpChannel.ReceiveMessage 异常：{0}。", e.Message);
                    return null;
                }
            }

#endif
        }
    }
}
