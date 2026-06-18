/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  WebSocketScope.NetChannelType.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   通道类型定义
 ***************************************************************/

namespace NovaFramework.Runtime
{
    public partial class WebSocketScope
    {
        /// <summary>
        /// 通信通道类型枚举。
        /// </summary>
        public enum NetChannelType : byte
        {
            /// <summary>
            /// 无效。
            /// </summary>
            None = 0,

            /// <summary>
            /// TCP 明文通道。
            /// </summary>
            Tcp,

            /// <summary>
            /// TCP Protobuf 通道。
            /// </summary>
            TcpPb
        }
    }
}
