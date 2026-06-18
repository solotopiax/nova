/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketScope.TcpMessage.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   TCP明文消息
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
        /* TCP 明文消息格式（外壳 8 字节）：
         *
         * +---------+-----------+------------+
         * |   4字节  |   4字节   |   N字节    |
         * |  协议ID  |  序列号   |   消息体   |
         * +---------+-----------+------------+
         *
         * 消息体结构：
         * +------------+--------------+ ... +------------+------------+
         * |  消息1长度  |   消息1内容  |     |  消息X长度  |  消息X内容 |
         * +------------+--------------+ ... +------------+------------+
         */

        /// <summary>
        /// TCP 明文协议网络消息，消息体为多条 UTF-8 字符串的二进制打包。
        /// </summary>
        public sealed class TcpMessage : NetMessageTcpBase
        {
            /// <summary>
            /// 消息字符串列表（发送时编码，接收时解码后填充）。
            /// </summary>
            public List<string> Messages;

            /// <summary>
            /// 消息体字节流（发送侧在 Initialize 时预计算，避免重复转换）。
            /// </summary>
            private List<byte> m_BodyBytes;

            /// <summary>
            /// 构造 TcpMessage 实例。
            /// </summary>
            public TcpMessage() : base()
            {
                Messages = new List<string>();
                m_BodyBytes = new List<byte>();
            }

            /// <summary>
            /// 初始化发送消息（序列号由通道层自动分配，消息体同步预计算）。
            /// </summary>
            /// <param name="channel">所属通信通道，用于分配序列号。</param>
            /// <param name="commandID">协议 ID。</param>
            /// <param name="messages">消息字符串列表。</param>
            public void Initialize(NetChannelBase channel, int commandID, List<string> messages)
            {
                Initialize(commandID);
                SequenceID = channel.GetSequenceID(commandID);

                m_BodyBytes.Clear();
                Messages.AddRange(messages);

                for (int i = 0; i < Messages.Count;)
                {
                    if (string.IsNullOrEmpty(Messages[i]))
                    {
                        Messages.RemoveAt(i);
                        continue;
                    }

                    byte[] msgBytes = Encoding.UTF8.GetBytes(Messages[i]);
                    m_BodyBytes.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(msgBytes.Length)));
                    m_BodyBytes.AddRange(msgBytes);
                    i++;
                }
            }

            /// <summary>
            /// 获取消息体字节长度。
            /// </summary>
            /// <returns>消息体字节数。</returns>
            public override int GetBodyLength()
            {
                return m_BodyBytes.Count;
            }

            /// <summary>
            /// 复位消息对象（清空消息列表，供对象池复用）。
            /// </summary>
            public override void Reset()
            {
                base.Reset();
                if (Messages == null)
                {
                    Messages = new List<string>();
                }

                Messages.Clear();

                if (m_BodyBytes == null)
                {
                    m_BodyBytes = new List<byte>();
                }

                m_BodyBytes.Clear();
            }

            /// <summary>
            /// 获取消息体字节数组。
            /// </summary>
            /// <returns>消息体字节数组。</returns>
            public byte[] GetBodyBytes()
            {
                return m_BodyBytes.ToArray();
            }
        }
    }
}
