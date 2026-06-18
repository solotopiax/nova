/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketScope.NetMessageTcpBase.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   TCP协议消息基类
 ***************************************************************/

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
        /// <summary>
        /// TCP 协议网络消息基类，携带协议 ID 与序列号。
        /// </summary>
        public class NetMessageTcpBase : NetMessageBase
        {
            /// <summary>
            /// 协议 ID（通信协议的唯一标识）。
            /// </summary>
            public int CommandID;

            /// <summary>
            /// 序列号（该协议 ID 下的通信顺序编号，从 1 开始自增）。
            /// </summary>
            public int SequenceID;

            /// <summary>
            /// 构造 TCP 消息基类实例。
            /// </summary>
            public NetMessageTcpBase()
            {
            }

            /// <summary>
            /// 初始化消息（仅设置协议 ID，序列号由通道层填充）。
            /// </summary>
            /// <param name="commandID">协议 ID。</param>
            public void Initialize(int commandID)
            {
                Reset();
                CommandID = commandID;
            }

            /// <summary>
            /// 获取消息体字节长度（基类默认 0）。
            /// </summary>
            /// <returns>0。</returns>
            public override int GetBodyLength()
            {
                return 0;
            }

            /// <summary>
            /// 复位协议 ID 与序列号。
            /// </summary>
            public override void Reset()
            {
                CommandID  = 0;
                SequenceID = 0;
            }
        }
    }
}
