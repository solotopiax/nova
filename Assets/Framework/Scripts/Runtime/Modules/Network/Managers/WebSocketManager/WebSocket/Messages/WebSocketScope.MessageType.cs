/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketScope.MessageType.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   消息类型(cmdID)
 ***************************************************************/

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
        /// <summary>
        /// 内置协议 ID 枚举，用于识别心跳与认证消息。
        /// </summary>
        public enum MessageType
        {
            /// <summary>
            /// 无效。
            /// </summary>
            None = 0,

            /// <summary>
            /// TCP 明文认证协议 ID。
            /// </summary>
            Authenticate = 5002,

            /// <summary>
            /// TCP 明文心跳协议 ID。
            /// </summary>
            HeartBeat = 5003,

            /// <summary>
            /// TCP Protobuf 认证协议 ID。
            /// </summary>
            AuthenticatePb = 6002,

            /// <summary>
            /// TCP Protobuf 心跳协议 ID。
            /// </summary>
            HeartBeatPb = 6003
        }
    }
}
