/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketScope.TcpPbChannel.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   TCP-Protobuf通道
 ***************************************************************/

using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
        /// <summary>
        /// TCP Protobuf 通道，消息体为 Protobuf 编码的字节流。
        /// 认证响应的 success 字段解析由外部注入的 AuthenticateResponseValidator 委托完成。
        /// </summary>
        public sealed class TcpPbChannel : NetChannelBase
        {
            /// <summary>
            /// 认证响应校验委托，用于判断 TcpPbMessage 中的 success 字段。
            /// 参数为 TcpPbMessage.Bytes，返回 true 表示认证成功。
            /// 若为 null，则默认视为认证成功（无认证保护）。
            /// </summary>
            public Func<byte[], bool> AuthenticateResponseValidator;

            /// <summary>
            /// 是否需要保持连接（TCP Protobuf 通道始终需要）。
            /// </summary>
            public override bool IsNeedConnect => true;

            /// <summary>
            /// 将消息封装为二进制帧：[协议ID 4B][序列号 4B][Pb字节流 NB]。
            /// 注意：此方法可能在主线程或发送线程中调用，totalByte 大小随消息而异，
            /// 使用成员级缓冲区存在线程安全风险和容量不可预测问题，因此保持局部分配。
            /// </summary>
            /// <param name="message">TcpPbMessage 消息对象。</param>
            /// <returns>封装后的字节数组。</returns>
            public override byte[] PackageMessageToBytes(NetMessageBase message)
            {
                TcpPbMessage networkInfo = (TcpPbMessage)message;
                byte[] totalByte = new byte[NetMessageBase.c_OuterShellLength + networkInfo.GetBodyLength()];
                byte[] commandIDByte = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(networkInfo.CommandID));
                byte[] sequenceIDByte = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(networkInfo.SequenceID));
                commandIDByte.CopyTo(totalByte, 0);
                sequenceIDByte.CopyTo(totalByte, 4);
                if (networkInfo.Bytes != null)
                {
                    networkInfo.Bytes.CopyTo(totalByte, 8);
                }

                return totalByte;
            }

            /// <summary>
            /// 判断是否为心跳消息（协议 ID == HeartBeatPb）。
            /// </summary>
            /// <param name="message">消息对象。</param>
            /// <returns>是心跳消息返回 true。</returns>
            public override bool IsHeartBeatMessage(NetMessageBase message)
            {
                TcpPbMessage networkInfo = message as TcpPbMessage;
                return networkInfo != null && networkInfo.CommandID == (int)MessageType.HeartBeatPb;
            }

            /// <summary>
            /// 判断是否为身份认证消息（协议 ID == AuthenticatePb）。
            /// </summary>
            /// <param name="message">消息对象。</param>
            /// <returns>是认证消息返回 true。</returns>
            public override bool IsAuthenticateMessage(NetMessageBase message)
            {
                TcpPbMessage networkInfo = message as TcpPbMessage;
                return networkInfo != null && networkInfo.CommandID == (int)MessageType.AuthenticatePb;
            }

            /// <summary>
            /// 判断 Protobuf 认证响应是否成功。
            /// 成功与否由外部注入的 AuthenticateResponseValidator 委托决定；
            /// 若未注入则默认返回 true（不校验）。
            /// </summary>
            /// <param name="message">身份认证响应消息。</param>
            /// <returns>认证成功返回 true。</returns>
            public override bool IsAuthenticateMessageSuccess(NetMessageBase message)
            {
                TcpPbMessage networkInfo = message as TcpPbMessage;
                if (networkInfo == null || networkInfo.CommandID != (int)MessageType.AuthenticatePb)
                {
                    return false;
                }

                if (AuthenticateResponseValidator == null)
                {
                    return true;
                }

                return AuthenticateResponseValidator.Invoke(networkInfo.Bytes);
            }

            /// <summary>
            /// 获取通道类型。
            /// </summary>
            /// <returns>NetChannelType.TcpPb。</returns>
            public override NetChannelType GetNetChannelType()
            {
                return NetChannelType.TcpPb;
            }

            /// <summary>
            /// 判断通道类型与地址是否匹配。
            /// </summary>
            /// <param name="channelType">通道类型。</param>
            /// <param name="serverAddress">服务器地址。</param>
            /// <returns>均匹配返回 true。</returns>
            public override bool Equals(NetChannelType channelType, string serverAddress)
            {
                return channelType == NetChannelType.TcpPb && m_ServerAddress == serverAddress;
            }

#if !UNITY_EDITOR && UNITY_WEBGL

            /// <summary>
            /// 解析 WebGL 接收到的字节为 TcpPbMessage 对象。
            /// </summary>
            /// <param name="recvBytes">原始字节数组。</param>
            /// <returns>解析出的 TcpPbMessage，解析失败返回 null。</returns>
            protected override NetMessageBase ReceiveMessage(byte[] recvBytes)
            {
                try
                {
                    TcpPbMessage info = (TcpPbMessage)m_Bridge.CreateMessage(NetChannelType.TcpPb);
                    info.CommandID  = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(recvBytes, 0));
                    info.SequenceID = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(recvBytes, 4));
                    info.Bytes = new byte[recvBytes.Length - NetMessageBase.c_OuterShellLength];
                    Array.Copy(recvBytes, NetMessageBase.c_OuterShellLength, info.Bytes, 0, info.Bytes.Length);
                    return info;
                }
                catch (WebSocketException)
                {
                    // 正常断开，静默处理。
                    return null;
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.WebSocket, "TcpPbChannel.ReceiveMessage(WebGL) 异常：{0}。", e.Message);
                    return null;
                }
            }

#else

            /// <summary>
            /// 异步接收并解析 TcpPbMessage（非 WebGL）。
            /// </summary>
            /// <param name="client">ClientWebSocket 实例。</param>
            /// <returns>解析出的 TcpPbMessage，解析失败返回 null。</returns>
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

                    TcpPbMessage info = (TcpPbMessage)m_Bridge.CreateMessage(NetChannelType.TcpPb);
                    info.CommandID  = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(recvBytes, 0));
                    info.SequenceID = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(recvBytes, 4));
                    info.Bytes = new byte[recvBytes.Length - NetMessageBase.c_OuterShellLength];
                    Array.Copy(recvBytes, NetMessageBase.c_OuterShellLength, info.Bytes, 0, info.Bytes.Length);
                    return info;
                }
                catch (WebSocketException)
                {
                    // 正常断开，静默处理。
                    return null;
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.WebSocket, "TcpPbChannel.ReceiveMessage 异常：{0}。", e.Message);
                    return null;
                }
            }

#endif
        }
    }
}
