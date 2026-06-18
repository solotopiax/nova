/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketScope.TcpPbMessage.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   TCP-Protobuf消息
 ***************************************************************/

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
        /* TCP Protobuf 消息格式（外壳 8 字节）：
         *
         * +---------+-----------+------------+
         * |   4字节  |   4字节   |   N字节    |
         * |  协议ID  |  序列号   |  Pb字节流  |
         * +---------+-----------+------------+
         */

        /// <summary>
        /// TCP Protobuf 协议网络消息，消息体为 Protobuf 编码后的字节流。
        /// </summary>
        public sealed class TcpPbMessage : NetMessageTcpBase
        {
            /// <summary>
            /// Protobuf 编码后的消息字节流。
            /// </summary>
            public byte[] Bytes;

            /// <summary>
            /// 构造 TcpPbMessage 实例。
            /// </summary>
            public TcpPbMessage() : base()
            {
            }

            /// <summary>
            /// 初始化发送消息（序列号由通道层自动分配）。
            /// </summary>
            /// <param name="channel">所属通信通道，用于分配序列号。</param>
            /// <param name="commandID">协议 ID。</param>
            /// <param name="bytes">Protobuf 编码后的字节流。</param>
            public void Initialize(NetChannelBase channel, int commandID, byte[] bytes)
            {
                Initialize(commandID);
                SequenceID = channel.GetSequenceID(commandID);
                Bytes = bytes;
            }

            /// <summary>
            /// 获取消息体字节长度。
            /// </summary>
            /// <returns>消息体字节数。</returns>
            public override int GetBodyLength()
            {
                return Bytes != null ? Bytes.Length : 0;
            }

            /// <summary>
            /// 复位消息对象（清空字节流引用，供对象池复用）。
            /// </summary>
            public override void Reset()
            {
                base.Reset();
                Bytes = null;
            }
        }
    }
}
